using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaDicePreview
{
    internal static class DamageShortcut
    {
        internal static UI_PadGlyphImage Create(Transform parent,Vector2 position)
        {
            var go=new GameObject("DamageShortcut",typeof(RectTransform),typeof(Image));go.SetActive(false);
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;
            go.AddComponent<LayoutElement>().ignoreLayout=true;
            var image=go.GetComponent<Image>();image.raycastTarget=false;
            var glyph=go.AddComponent<UI_PadGlyphImage>();glyph.glyphImage=image;glyph.disableOnKeyboard=false;glyph.autoFitScale=1.25f;
            go.SetActive(true);return glyph;
        }
        internal static void Refresh(UI_PadGlyphImage glyph,string keyboard,string pad)
        {
            if(!glyph)return;
            bool mouse=ControlsChangeHandler.Current&&ControlsChangeHandler.Current.IsUsingKeyboardAndMouse;
            string key=mouse?keyboard:pad;
            if(glyph.iconName!=key||!glyph.glyphImage.sprite){glyph.iconName=key;glyph.UpdateGlyph();}
        }
        internal static void Place(Button button,float x,float width)
        {
            var layout=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
            var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,.5f);
            r.sizeDelta=new Vector2(width,22);r.anchoredPosition=new Vector2(x,20);
        }
    }
    public sealed class DamageTooltipView : MonoBehaviour
    {
        private UI_BaseTooltip tooltip;
        private Button button;
        private TMP_Text label;
        private UI_PadGlyphImage openGlyph;
        public int Offset;
        public static bool Enabled { get { return PersonalConveniencePlugin.Instance && PersonalConveniencePlugin.Instance.ShowDamageDetails.Value; } }
        public void Bind(UI_BaseTooltip ui,TMP_Text template,int offset)
        {
            tooltip=ui;Offset=offset;
            if(!button && Enabled)
            {
                var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
                button=Instantiate(holder.yesPrefab.yesButton,template.transform.parent);
                button.name="DamageDetailsButton";
                button.navigation=new Navigation {mode=Navigation.Mode.None};
                var native=button as UI_HorayButton;if(native)native.CanSelect+=delegate{return false;};
                button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(OpenDetails);
                foreach(var loc in button.GetComponentsInChildren<UI_LocalizationStringText>(true))loc.enabled=false;
                label=button.GetComponentInChildren<TMP_Text>(true);
                var size=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();size.ignoreLayout=false;size.minHeight=22;size.preferredHeight=22;size.preferredWidth=180;
                ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,180);
                ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,22);
                label.enableAutoSizing=true;label.fontSizeMax=10;label.fontSizeMin=8;
                button.transform.SetAsLastSibling();
                openGlyph=DamageShortcut.Create(button.transform,new Vector2(-65,0));
            }
            if(button)button.gameObject.SetActive(Enabled);
            if(ui is UI_WeaponTooltip)((UI_WeaponTooltip)ui).debugInfoText.gameObject.SetActive(false);
        }
        private void Update()
        {
            if(!button)return;
            if(button.gameObject.activeSelf!=Enabled)button.gameObject.SetActive(Enabled);
            if(!Enabled || !tooltip || !tooltip.IsOpened || tooltip.TooltipObject==null)return;
            label.text="상세보기";
            DamageShortcut.Refresh(openGlyph,"leftAlt","rightStickPress");
            if(UIManager.Instance.GetElement<UI_MessageBoxHolder>().HasOpenedBox)return;
            if((Gamepad.current!=null&&Gamepad.current.rightStickButton.wasPressedThisFrame)||(Keyboard.current!=null&&Keyboard.current.leftAltKey.wasPressedThisFrame))OpenDetails();
        }
        public void OpenDetails()
        {
            if(!Enabled||!tooltip||tooltip.TooltipObject==null||!DamageTooltip.Player)return;
            var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();if(holder.HasOpenedBox)return;
            // Bind values before the modal takes focus and the native tooltip closes.
            var player=DamageTooltip.Player;
            Func<string> collect;string id;
            var weaponUI=tooltip as UI_WeaponTooltip;
            if(weaponUI)
            {
                var weapon=tooltip.TooltipObject as WeaponSimple;var entity=tooltip.TooltipObject as WeaponEntity;
                if(!weapon&&entity&&entity.mainWeaponPrefab)weapon=entity.mainWeaponPrefab.GetComponent<WeaponSimple>();
                if(!weapon)return;
                var selected=weapon;collect=delegate{return DamageTooltip.WeaponText(selected,player);};id="W"+weapon.GetInstanceID();
            }
            else
            {
                var charmUI=(UI_CharmTooltip)tooltip;var entity=charmUI.currentEntity;
                var own=tooltip.TooltipObject as NewItemOwnInstance;var live=own!=null?own.Charm:null;
                int level=(live?live.DisplayedLevel:0)+Offset;
                var item=tooltip.TooltipObject as IItemInstance;
                if(!live&&item!=null) {int enchant;if(int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(item.IUID,"Enchant"),out enchant))level+=enchant;}
                collect=delegate{return DamageTooltip.ArtifactText(entity,live,level,player);};id="A"+entity.id+":"+level+":"+(live?live.GetInstanceID():0);
            }
            var box=(UI_MessageBox_Yes)holder.OpenYes("계산 중입니다",delegate{});
            (box.GetComponent<DamageDetailsDialog>()??box.gameObject.AddComponent<DamageDetailsDialog>()).Begin(box,id,collect,player);
        }
    }

    public sealed class DamageDetailsDialog : MonoBehaviour
    {
        private static readonly Dictionary<string,string> Previous=new Dictionary<string,string>();
        private static readonly Dictionary<string,Task<string>> Work=new Dictionary<string,Task<string>>();
        private UI_MessageBox_Yes box;
        private Func<string> collect;
        private PlayerAvatar player;
        private string id,result="",error="";
        private Task<string> pending;
        private bool captureQueued;
        private int captureAfterFrame;
        private int requestedRevision,page,openedFrame,requestedMp,requestedMoney;
        private float requestedHp;
        private bool requestedBattle;
        private bool dirty;
        private bool fullBuff,requestedFullBuff;
        private WeaponSimple_Crossbow requestedCrossbow;
        private bool requestedIceBuffReady;
        private Button buffButton;
        private TMP_Text buffLabel;
        private UI_PadGlyphImage buffGlyph,closeGlyph,previousGlyph,nextGlyph;
        private string ResultKey {get{return id+(fullBuff?":full-buff":":current");}}
        private float nextCapture;
        public bool IsCalculating {get{return captureQueued||pending!=null;}}
        public static DamageDetailsDialog Current;
        public void Begin(UI_MessageBox_Yes ui,string source,Func<string> gather,PlayerAvatar avatar)
        {
            openedFrame=Time.frameCount;box=ui;id=source+":"+avatar.GetInstanceID();collect=gather;player=avatar;Current=this;
            pending=null;captureQueued=false;fullBuff=false;page=0;result="";error="";
            string old;if(Previous.TryGetValue(ResultKey,out old))result=old;
            box.text.enableAutoSizing=false;box.text.fontSize=12;box.text.alignment=TextAlignmentOptions.TopLeft;
            foreach(var loc in box.yesButton.GetComponentsInChildren<UI_LocalizationStringText>(true))loc.enabled=false;
            box.yesButton.GetComponentInChildren<TMP_Text>(true).text="닫기";
            if(!buffButton)
            {
                buffButton=Instantiate(box.yesButton,box.yesButton.transform.parent);buffButton.name="FullBuffDamageButton";
                buffButton.onClick=new Button.ButtonClickedEvent();buffButton.onClick.AddListener(ToggleFullBuff);
                foreach(var loc in buffButton.GetComponentsInChildren<UI_LocalizationStringText>(true))loc.enabled=false;
                buffLabel=buffButton.GetComponentInChildren<TMP_Text>(true);buffLabel.enableAutoSizing=true;buffLabel.fontSizeMin=8;buffLabel.fontSizeMax=12;
                buffLabel.margin=new Vector4(22,0,0,0);
                box.yesButton.GetComponentInChildren<TMP_Text>(true).margin=new Vector4(16,0,0,0);
                buffGlyph=DamageShortcut.Create(buffButton.transform,new Vector2(-57,0));
                closeGlyph=DamageShortcut.Create(box.yesButton.transform,new Vector2(-25,0));
                previousGlyph=DamageShortcut.Create(box.yesButton.transform.parent,new Vector2(-30,0));
                nextGlyph=DamageShortcut.Create(box.yesButton.transform.parent,new Vector2(30,0));
                foreach(var glyph in new[]{previousGlyph,nextGlyph})
                {var g=(RectTransform)glyph.transform;g.anchorMin=g.anchorMax=new Vector2(.5f,0);g.anchoredPosition=new Vector2(g.anchoredPosition.x,52);}
                buffButton.navigation=new Navigation{mode=Navigation.Mode.None};
            }
            DamageShortcut.Place(buffButton,-40,144);DamageShortcut.Place(box.yesButton,76,72);
            Request();
        }
        public void ToggleFullBuff()
        {
            if(!box||!box.IsOpened||!DamageTooltipView.Enabled)return;
            fullBuff=!fullBuff;page=0;error="";dirty=true;
            string old;result=Previous.TryGetValue(ResultKey,out old)?old:"";
            if(pending==null)Request();else Draw();
        }
        private static string Signature(DamageTooltip.Snapshot s)
        {
            var b=new StringBuilder();b.Append(s.Template.Length).Append(':').Append(s.Template);b.Append('|').Append(s.All).Append('|').Append(s.DashBonus).Append('|').Append(s.DashCount).Append('|').Append(s.Critical).Append('|').Append(s.WeaponCritical).Append('|').Append(s.WeaponAmp).Append('|').Append(s.Flat).Append('|').Append(s.Gold.ToString("R")).Append('|').Append(s.Defense.ToString("R"));
            b.Append("|rows:").Append(s.Rows.Count);
            b.Append("|conditions:").Append(s.Close.ToString("R")).Append('|').Append(s.First.ToString("R")).Append('|').Append(s.Burn.ToString("R"));
            b.Append("|debuff:").Append(s.DebuffBonus).Append('|').Append(s.PoisonDebuffBonus);
            b.Append("|gold-defense:").Append(s.GoldBonus).Append('|').Append(s.DefenseBonus.ToString("R"));
            b.Append("|execution:").Append(s.ExecutionEnabled);
            b.Append("|one-debuff:").Append(s.OneDebuff.ToString("R"));
            b.Append("|frostbite:");if(s.Frostbite!=null)foreach(float factor in s.Frostbite)b.Append(factor.ToString("R")).Append(',');
            b.Append("|random-damage:").Append(s.AlwaysDamageFactor.ToString("R")).Append(',').Append(s.RandomDamageMinimum.ToString("R")).Append(',').Append(s.RandomDamageMaximum.ToString("R"));
            b.Append("|element-critical:");if(s.ElementCritical!=null)foreach(int bonus in s.ElementCritical)b.Append(bonus).Append(',');
            b.Append("|elite:").Append(s.Elite);
            b.Append("|raw-steps:").Append(s.RawSteps==null?0:s.RawSteps.Length);
            if(s.RawSteps!=null)foreach(var step in s.RawSteps)
            {
                b.Append('|').Append(step.Kind).Append('|').Append(step.Percent.ToString("R")).Append('|').Append(step.Chance.ToString("R"));
                b.Append('|').Append(step.Elements==null?0:step.Elements.Length);
                if(step.Elements!=null)foreach(bool element in step.Elements)b.Append(element?'1':'0');
            }
            foreach(var row in s.Rows)
            {
                b.Append("|hits:").Append(row.Length);
                foreach(var h in row)
                {
                    if(h.Follower!=null)
                    {
                        b.Append("|follower:").Append(h.Follower.Signature());
                        b.Append('|').Append(h.ExtraCritical).Append('|').Append(h.CriticalRateMultiplier.ToString("R"));
                        b.Append('|').Append(h.ProjectileDamagePercent.ToString("R")).Append('|').Append(h.CanCritical);
                        continue;
                    }
                    b.Append('|').Append(h.Raw.ToString("R")).Append('|').Append(h.ResourceAmount.ToString("R")).Append('|').Append(h.ResourcePerUnit.ToString("R")).Append('|').Append(h.Weapon).Append('|').Append(h.ExtraCritical);
                    b.Append('|').Append(h.AfterFactors.ToString("R"));
                    b.Append('|').Append(h.ElementalEffect);
                    b.Append('|').Append((int)h.Element);
                    b.Append('|').Append(h.CriticalRateMultiplier.ToString("R"));
                    b.Append('|').Append(h.ProjectileDamagePercent.ToString("R"));
                    b.Append('|').Append(h.CanCritical);
                    b.Append("|factors:").Append(h.Factors==null?0:h.Factors.Length);
                    if(h.Factors!=null)foreach(float f in h.Factors)b.Append('|').Append(f.ToString("R"));
                }
            }
            return b.ToString();
        }
        private void Request()
        {
            if(pending!=null){dirty=true;return;}
            // Render the pending state before synchronous Unity snapshot reads.
            // Repeated requests before that frame share one capture.
            if(!captureQueued){captureQueued=true;captureAfterFrame=Time.frameCount+1;}
            Draw();
        }
        private static Task<string> CalculateAsync(DamageTooltip.Snapshot snapshot,bool full)
        {
            return Task.Run(async delegate
            {
                // Everything below reads numeric snapshot data, never Unity objects.
                string key=(full?"full:":"current:")+Signature(snapshot);
                Task<string> shared;
                TaskCompletionSource<string> producer=null;
                lock(Work)
                {
                    if(!Work.TryGetValue(key,out shared))
                    {
                        if(Work.Count>=64)
                        {
                            var remove=new List<string>();
                            foreach(var pair in Work)if(pair.Value.IsCompleted)remove.Add(pair.Key);
                            foreach(var old in remove)Work.Remove(old);
                        }
                        producer=new TaskCompletionSource<string>();
                        shared=producer.Task;Work[key]=shared;
                    }
                    else System.Threading.Interlocked.Increment(ref DamageTooltip.CacheHits);
                }
                if(producer!=null)
                {
                    try{producer.SetResult(snapshot.Calculate());}
                    catch(Exception e)
                    {
                        lock(Work){Work.Remove(key);}
                        producer.SetException(e);
                    }
                }
                return await shared.ConfigureAwait(false);
            });
        }
        private void CaptureRequested()
        {
            captureQueued=false;
            requestedRevision=DamageTooltip.Revision;requestedMp=player.MP;requestedMoney=player.Money;requestedHp=player.hp;requestedBattle=player.IsInBattle;dirty=false;nextCapture=Time.unscaledTime+.2f;
            requestedFullBuff=fullBuff;
            var controller=player.GetComponent<WeaponControllerSimple>();
            requestedCrossbow=fullBuff&&controller?controller.currentWeapon as WeaponSimple_Crossbow:null;
            requestedIceBuffReady=requestedCrossbow&&requestedCrossbow.iceBuffCoolDownTimer.Check();
            try
            {
                var snapshot=fullBuff?FullBuffPlan.Capture(player,collect):DamageTooltip.Capture(collect,player);
                pending=CalculateAsync(snapshot,fullBuff);
                error="";
            }
            catch(Exception e) {error="피해 정보를 계산할 수 없습니다.";Debug.LogWarning("Damage details: "+e);pending=null;}
            Draw();
        }
        private void Update()
        {
            if(!box||!box.IsOpened){captureQueued=false;return;}
            if(!DamageTooltipView.Enabled||!player){captureQueued=false;box.Close();return;}
            if(captureQueued&&pending==null&&Time.frameCount>=captureAfterFrame)CaptureRequested();
            if(player.MP!=requestedMp||player.Money!=requestedMoney||player.hp!=requestedHp||player.IsInBattle!=requestedBattle)dirty=true;
            if(fullBuff&&requestedCrossbow&&requestedCrossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.IceBuff&&requestedIceBuffReady!=requestedCrossbow.iceBuffCoolDownTimer.Check())dirty=true;
            if(pending!=null&&pending.IsCompleted)
            {
                var completed=pending;pending=null;
                if(completed.IsFaulted) {error="피해 정보를 계산할 수 없습니다.";Debug.LogWarning(completed.Exception);}
                else if(requestedRevision==DamageTooltip.Revision&&!dirty&&requestedFullBuff==fullBuff){result=completed.Result;if(Previous.Count>=64)Previous.Clear();Previous[ResultKey]=result;}
                else dirty=true;
                Draw();
            }
            if((dirty||requestedRevision!=DamageTooltip.Revision)&&pending==null&&!captureQueued&&Time.unscaledTime>=nextCapture)Request();
            RefreshShortcuts();
            if(Time.frameCount==openedFrame)return;
            var pad=Gamepad.current;var keys=Keyboard.current;
            if((pad!=null&&pad.leftShoulder.wasPressedThisFrame)||(keys!=null&&keys.rightCtrlKey.wasPressedThisFrame))ToggleFullBuff();
            int delta=0;
            if((pad!=null&&pad.dpad.right.wasPressedThisFrame)||(keys!=null&&keys.rightArrowKey.wasPressedThisFrame))delta=1;
            if((pad!=null&&pad.dpad.left.wasPressedThisFrame)||(keys!=null&&keys.leftArrowKey.wasPressedThisFrame))delta=-1;
            if(Mouse.current!=null) {float wheel=Mouse.current.scroll.ReadValue().y;if(wheel!=0)delta=wheel<0?1:-1;}
            if(delta!=0){page+=delta;Draw();}
            if((pad!=null&&(pad.buttonEast.wasPressedThisFrame||pad.rightStickButton.wasPressedThisFrame))||(keys!=null&&keys.escapeKey.wasPressedThisFrame))box.Close();
        }
        private void RefreshShortcuts()
        {
            DamageShortcut.Refresh(buffGlyph,"rightCtrl","leftShoulder");DamageShortcut.Refresh(closeGlyph,"escape","buttonEast");
            DamageShortcut.Refresh(previousGlyph,"leftArrow","dpad/left");DamageShortcut.Refresh(nextGlyph,"rightArrow","dpad/right");
        }
        private void Draw()
        {
            var visible=new List<string>();foreach(string line in result.Trim().Split('\n'))if(!line.Contains("보스·미니보스"))visible.Add(line);
            var lines=visible.ToArray();const int perPage=10;int pages=Math.Max(1,(lines.Length+perPage-1)/perPage);page=Math.Max(0,Math.Min(page,pages-1));
            var text=new StringBuilder(fullBuff?"<color=#81E6C4>풀 버프 피해량</color>\n":"<color=#81E6C4>현재 상태 · 공격 피해 상세보기</color>\n");
            if(buffLabel)buffLabel.text=fullBuff?"현재 상태로 보기":"풀 버프 피해량";
            RefreshShortcuts();
            for(int i=page*perPage;i<Math.Min(lines.Length,(page+1)*perPage);i++)text.AppendLine(lines[i]);
            if(IsCalculating)text.AppendLine("<color=#EDB14D>계산 중입니다</color>");
            if(error.Length>0)text.AppendLine(error);
            text.Append("\n<align=center>").Append(page+1).Append(" / ").Append(pages).Append("</align>");
            box.text.text=text.ToString();
        }
    }
}
