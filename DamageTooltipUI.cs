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
    internal sealed class DamageItemHeader
    {
        internal string Name;
        internal Sprite Icon;
        internal Color NameColor=Color.white;
    }
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
        private Button dpsButton;
        private TMP_Text dpsLabel;
        private UI_PadGlyphImage dpsGlyph;
        private SynergyTooltipData comboData;
        private ItemEntity artifactEntity;
        private Charm_Basic artifactLive;
        private int artifactLevel,artifactInstanceId;
        private DamageItemHeader artifactHeader;
        public int Offset;
        public static bool Enabled { get { return PersonalConveniencePlugin.Instance && PersonalConveniencePlugin.Instance.ShowDamageDetails.Value; } }
        public void Bind(UI_BaseTooltip ui,TMP_Text template,int offset)
        {
            tooltip=ui;Offset=offset;comboData=null;artifactEntity=null;artifactLive=null;artifactLevel=artifactInstanceId=0;artifactHeader=null;
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
                dpsButton=Instantiate(button,template.transform.parent);dpsButton.name="DamageDpsButton";
                dpsButton.onClick=new Button.ButtonClickedEvent();dpsButton.onClick.AddListener(OpenDps);
                dpsLabel=dpsButton.GetComponentInChildren<TMP_Text>(true);dpsLabel.text="DPS 보기";
                dpsGlyph=dpsButton.GetComponentInChildren<UI_PadGlyphImage>(true);
                dpsButton.transform.SetAsLastSibling();
            }
            if(button)button.gameObject.SetActive(Enabled);
            if(dpsButton)dpsButton.gameObject.SetActive(Enabled);
            if(ui is UI_WeaponTooltip)((UI_WeaponTooltip)ui).debugInfoText.gameObject.SetActive(false);
        }
        public void BindCombo(UI_SynergyTooltip ui,SynergyTooltipData data)
        {
            Bind(ui,ui.effectText,0);comboData=data;
        }
        public void BindArtifact(UI_CharmTooltip ui,ITooltip data,TMP_Text template,int offset)
        {
            Bind(ui,template,offset);
            var own=data as NewItemOwnInstance;
            artifactLive=own!=null?own.Charm:null;
            var itemEntity=data as ItemEntity;
            var identified=data as IItemEntity;
            artifactEntity=own!=null?own.Entity:itemEntity??(identified!=null?ItemDatabase.FindItemById(identified.IEntityID):ui.currentEntity);
            artifactLevel=(artifactLive?artifactLive.DisplayedLevel:0)+offset;
            var item=data as IItemInstance;
            artifactInstanceId=item!=null?item.IUID:0;
            if(!artifactLive&&item!=null)
            {
                int enchant;if(int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(item.IUID,"Enchant"),out enchant))artifactLevel+=enchant;
            }
            var name=HarmonyLib.AccessTools.Field(typeof(UI_CharmTooltip),"nameText").GetValue(ui) as TMP_Text;
            var icon=HarmonyLib.AccessTools.Field(typeof(UI_CharmTooltip),"iconImage").GetValue(ui) as Image;
            if(artifactEntity)artifactHeader=new DamageItemHeader{Name=name?name.text:artifactEntity.Name,Icon=icon?icon.sprite:artifactEntity.Icon,NameColor=name?name.color:ItemDatabase.GetColorViaItemRarity(artifactEntity.rarity)};
        }
        private void Update()
        {
            if(!button)return;
            if(button.gameObject.activeSelf!=Enabled)button.gameObject.SetActive(Enabled);
            if(dpsButton&&dpsButton.gameObject.activeSelf!=Enabled)dpsButton.gameObject.SetActive(Enabled);
            if(!Enabled || !tooltip || !tooltip.IsOpened || tooltip.TooltipObject==null)return;
            label.text="상세보기";
            DamageShortcut.Refresh(openGlyph,"leftAlt","leftStickPress");
            if(dpsLabel)dpsLabel.text="DPS 보기";
            DamageShortcut.Refresh(dpsGlyph,"rightAlt","rightStickPress");
            if(UIManager.Instance.GetElement<UI_MessageBoxHolder>().HasOpenedBox)return;
            if((Gamepad.current!=null&&Gamepad.current.leftStickButton.wasPressedThisFrame)||(Keyboard.current!=null&&Keyboard.current.leftAltKey.wasPressedThisFrame))OpenDetails();
            if((Gamepad.current!=null&&Gamepad.current.rightStickButton.wasPressedThisFrame)||(Keyboard.current!=null&&Keyboard.current.rightAltKey.wasPressedThisFrame))OpenDps();
        }
        public void OpenDetails()
        {Open(false);}
        public void OpenDps()
        {Open(true);}
        private void Open(bool dps)
        {
            if(!Enabled||!tooltip||tooltip.TooltipObject==null||!DamageTooltip.Player)return;
            var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();if(holder.HasOpenedBox)return;
            // Bind values before the modal takes focus and the native tooltip closes.
            var player=DamageTooltip.Player;
            Func<string> collect;string id;DamageItemHeader header;
            var comboUI=tooltip as UI_SynergyTooltip;
            if(comboUI&&comboData!=null)
            {
                var data=comboData;
                var effect=data.comboEffectInstance;
                if(!effect&&data.itemCategory&&data.itemCategory.comboEffectPrefab)effect=data.itemCategory.comboEffectPrefab.GetComponent<ComboEffectBase>();
                if(!effect)return;
                var selected=effect;int count=data.comboCount;
                collect=delegate{return dps?DpsCapture.Combo(selected,count,player):ComboDamageProfiles.Detail(selected,count,player);};
                id="C"+effect.GetInstanceID()+":"+count;
                header=new DamageItemHeader{Name=comboUI.nameText.text,Icon=comboUI.iconImage.sprite,NameColor=comboUI.nameText.color};
            }
            else if(tooltip is UI_WeaponTooltip)
            {
                var weaponUI=(UI_WeaponTooltip)tooltip;
                var weapon=tooltip.TooltipObject as WeaponSimple;var entity=tooltip.TooltipObject as WeaponEntity;
                if(!weapon&&entity&&entity.mainWeaponPrefab)weapon=entity.mainWeaponPrefab.GetComponent<WeaponSimple>();
                if(!weapon)return;
                var selected=weapon;collect=delegate{return dps?DpsCapture.Weapon(selected,player):DamageTooltip.WeaponText(selected,player);};id="W"+weapon.GetInstanceID();
                header=new DamageItemHeader{Name=weaponUI.weaponNameText.text,Icon=weaponUI.weaponIconImage.sprite,NameColor=weaponUI.weaponNameText.color};
            }
            else
            {
                var entity=artifactEntity;var live=artifactLive;int level=artifactLevel;
                if(!entity)return;
                collect=delegate{return dps?DpsCapture.Artifact(entity,live,level,player):DamageTooltip.ArtifactText(entity,live,level,player);};id="A"+entity.id+":"+level+":"+artifactInstanceId+":"+(live?live.GetInstanceID():0);
                header=artifactHeader;
            }
            var box=(UI_MessageBox_Yes)holder.OpenYes("계산 중입니다",delegate{});
            (box.GetComponent<DamageDetailsDialog>()??box.gameObject.AddComponent<DamageDetailsDialog>()).Begin(box,(dps?"DPS:":"")+id,collect,player,dps,header);
        }
    }

    public sealed class DamageDetailsDialog : MonoBehaviour
    {
        private static readonly Dictionary<string,string> Previous=new Dictionary<string,string>();
        private static readonly Dictionary<string,string> PreviousSignature=new Dictionary<string,string>();
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
        private string requestedFloor;
        private bool requestedBattle;
        private bool dirty;
        private bool silentCapture,showCalculating;
        private string pendingSignature;
        private bool fullBuff,requestedFullBuff;
        private bool dpsMode;
        private WeaponSimple_Crossbow requestedCrossbow;
        private bool requestedIceBuffReady;
        private Button buffButton;
        private TMP_Text buffLabel;
        private Button itemHeader;
        private TMP_Text itemHeaderLabel;
        private Image itemHeaderIcon;
        private Vector2 baseBoxSize,baseTextOffsetMax;
        private Vector4 baseTextMargin;
        private bool layoutCaptured;
        private UI_PadGlyphImage buffGlyph,closeGlyph,previousGlyph,nextGlyph;
        private string ResultKey {get{return id+(fullBuff?":full-buff":":current");}}
        private float nextCapture;
        public bool IsCalculating {get{return captureQueued||pending!=null;}}
        public static DamageDetailsDialog Current;
        internal void Begin(UI_MessageBox_Yes ui,string source,Func<string> gather,PlayerAvatar avatar,bool dps=false,DamageItemHeader header=null)
        {
            dpsMode=dps;
            openedFrame=Time.frameCount;box=ui;id=source+":"+avatar.GetInstanceID();collect=gather;player=avatar;Current=this;
            pending=null;captureQueued=false;fullBuff=false;page=0;result="";error="";
            silentCapture=false;showCalculating=false;pendingSignature=null;
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
            if(!layoutCaptured)
            {
                layoutCaptured=true;baseBoxSize=box.rectTransform.sizeDelta;baseTextOffsetMax=box.text.rectTransform.offsetMax;baseTextMargin=box.text.margin;
            }
            box.rectTransform.sizeDelta=new Vector2(baseBoxSize.x,baseBoxSize.y+38);
            box.text.rectTransform.offsetMax=baseTextOffsetMax;
            box.text.margin=new Vector4(baseTextMargin.x,baseTextMargin.y+34,baseTextMargin.z,baseTextMargin.w);
            EnsureItemHeader(header);
            DamageShortcut.Place(buffButton,-40,144);DamageShortcut.Place(box.yesButton,76,72);
            Request(false);
        }
        private void EnsureItemHeader(DamageItemHeader info)
        {
            if(!itemHeader)
            {
                itemHeader=Instantiate(box.yesButton,box.transform);itemHeader.name="DamageViewedItemHeader";
                itemHeader.onClick=new Button.ButtonClickedEvent();itemHeader.navigation=new Navigation{mode=Navigation.Mode.None};
                foreach(var loc in itemHeader.GetComponentsInChildren<UI_LocalizationStringText>(true))loc.enabled=false;
                foreach(var glyph in itemHeader.GetComponentsInChildren<UI_PadGlyphImage>(true))Destroy(glyph.gameObject);
                foreach(var graphic in itemHeader.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                var rect=(RectTransform)itemHeader.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);
                rect.sizeDelta=new Vector2(-20,30);rect.anchoredPosition=new Vector2(0,-7);
                var layout=itemHeader.GetComponent<LayoutElement>()??itemHeader.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
                itemHeaderLabel=itemHeader.GetComponentInChildren<TMP_Text>(true);itemHeaderLabel.alignment=TextAlignmentOptions.MidlineLeft;
                itemHeaderLabel.enableAutoSizing=true;itemHeaderLabel.fontSizeMin=9;itemHeaderLabel.fontSizeMax=13;itemHeaderLabel.margin=new Vector4(38,0,5,0);
                var iconObject=new GameObject("ViewedItemIcon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                var iconRect=(RectTransform)iconObject.transform;iconRect.SetParent(itemHeader.transform,false);iconRect.anchorMin=iconRect.anchorMax=new Vector2(0,.5f);iconRect.pivot=new Vector2(.5f,.5f);iconRect.anchoredPosition=new Vector2(19,0);iconRect.sizeDelta=new Vector2(25,25);
                itemHeaderIcon=iconObject.GetComponent<Image>();itemHeaderIcon.preserveAspect=true;itemHeaderIcon.raycastTarget=false;
            }
            itemHeader.gameObject.SetActive(info!=null);
            if(info==null)return;
            itemHeaderLabel.text=info.Name??"";itemHeaderLabel.color=info.NameColor;itemHeaderIcon.sprite=info.Icon;itemHeaderIcon.color=info.Icon?Color.white:Color.clear;
            itemHeader.transform.SetAsFirstSibling();
        }
        public void ToggleFullBuff()
        {
            if(!box||!box.IsOpened||!DamageTooltipView.Enabled)return;
            fullBuff=!fullBuff;page=0;error="";dirty=true;
            string old;result=Previous.TryGetValue(ResultKey,out old)?old:"";
            if(pending==null)Request(false);else Draw();
        }
        private static string Signature(DamageTooltip.Snapshot s)
        {
            var b=new StringBuilder();b.Append(s.Template.Length).Append(':').Append(s.Template);b.Append('|').Append(s.All).Append('|').Append(s.DashBonus).Append('|').Append(s.DashCount).Append('|').Append(s.Critical).Append('|').Append(s.WeaponCritical).Append('|').Append(s.WeaponAmp).Append('|').Append(s.Flat).Append('|').Append(s.Gold.ToString("R")).Append('|').Append(s.Defense.ToString("R"));
            b.Append("|dps:").Append(s.Dps==null?"":s.Dps.Signature());
            b.Append("|full-conditions:").Append(s.FullConditions);
            b.Append("|crit-chance:").Append(s.CriticalChance.ToString("R")).Append(',').Append(s.WeaponCriticalChance.ToString("R")).Append(',').Append(s.MagicCriticalChance.ToString("R"));
            b.Append("|magic-critical-damage:").Append(s.MagicCriticalDamage);
            b.Append("|element-chance:");if(s.ElementCriticalChance!=null)foreach(float chance in s.ElementCriticalChance)b.Append(chance.ToString("R")).Append(',');
            foreach(var debuff in s.Debuffs)b.Append("|debuff-detail:").Append(debuff.Signature());
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
                    b.Append("|hit-chance:").Append(h.ExtraCriticalChance.ToString("R")).Append(':').Append(h.Magic);
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
        private void Request(bool silent)
        {
            if(pending!=null){dirty=true;return;}
            // Render the pending state before synchronous Unity snapshot reads.
            // Repeated requests before that frame share one capture.
            if(!captureQueued){captureQueued=true;captureAfterFrame=Time.frameCount+1;silentCapture=silent&&result.Length>0;showCalculating=!silentCapture;}
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
            requestedFloor=player.currentFloorGuid;
            requestedFullBuff=fullBuff;
            var controller=player.GetComponent<WeaponControllerSimple>();
            requestedCrossbow=fullBuff&&controller?controller.currentWeapon as WeaponSimple_Crossbow:null;
            requestedIceBuffReady=requestedCrossbow&&requestedCrossbow.iceBuffCoolDownTimer.Check();
            try
            {
                var snapshot=fullBuff?FullBuffPlan.Capture(player,collect):DamageTooltip.Capture(collect,player);
                pendingSignature=(fullBuff?"full:":"current:")+Signature(snapshot);
                string previousSignature;
                if(silentCapture&&PreviousSignature.TryGetValue(ResultKey,out previousSignature)&&previousSignature==pendingSignature)
                {
                    pendingSignature=null;silentCapture=false;showCalculating=false;Draw();return;
                }
                showCalculating=true;
                pending=CalculateAsync(snapshot,fullBuff);
                error="";
            }
            catch(Exception e) {error="피해 정보를 계산할 수 없습니다.";Debug.LogWarning("Damage details: "+e);pending=null;pendingSignature=null;silentCapture=false;showCalculating=false;}
            Draw();
        }
        private void Update()
        {
            if(!box||!box.IsOpened){captureQueued=false;return;}
            if(!DamageTooltipView.Enabled||!player){captureQueued=false;box.Close();return;}
            if(captureQueued&&pending==null&&Time.frameCount>=captureAfterFrame)CaptureRequested();
            if(player.MP!=requestedMp||player.Money!=requestedMoney||player.hp!=requestedHp||player.IsInBattle!=requestedBattle)dirty=true;
            if(dpsMode&&player.currentFloorGuid!=requestedFloor)dirty=true;
            if(fullBuff&&requestedCrossbow&&requestedCrossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.IceBuff&&requestedIceBuffReady!=requestedCrossbow.iceBuffCoolDownTimer.Check())dirty=true;
            if(pending!=null&&pending.IsCompleted)
            {
                var completed=pending;pending=null;
                if(completed.IsFaulted) {error="피해 정보를 계산할 수 없습니다.";Debug.LogWarning(completed.Exception);}
                else if(requestedRevision==DamageTooltip.Revision&&!dirty&&requestedFullBuff==fullBuff)
                {
                    result=completed.Result;
                    if(Previous.Count>=64){Previous.Clear();PreviousSignature.Clear();}
                    Previous[ResultKey]=result;PreviousSignature[ResultKey]=pendingSignature;
                }
                else dirty=true;
                pendingSignature=null;silentCapture=false;showCalculating=false;
                Draw();
            }
            if((dirty||requestedRevision!=DamageTooltip.Revision)&&pending==null&&!captureQueued&&Time.unscaledTime>=nextCapture)Request(true);
            RefreshShortcuts();
            if(Time.frameCount==openedFrame)return;
            var pad=Gamepad.current;var keys=Keyboard.current;
            if((pad!=null&&pad.leftShoulder.wasPressedThisFrame)||(keys!=null&&keys.rightCtrlKey.wasPressedThisFrame))ToggleFullBuff();
            int delta=0;
            if((pad!=null&&pad.dpad.right.wasPressedThisFrame)||(keys!=null&&keys.rightArrowKey.wasPressedThisFrame))delta=1;
            if((pad!=null&&pad.dpad.left.wasPressedThisFrame)||(keys!=null&&keys.leftArrowKey.wasPressedThisFrame))delta=-1;
            if(Mouse.current!=null) {float wheel=Mouse.current.scroll.ReadValue().y;if(wheel!=0)delta=wheel<0?1:-1;}
            if(delta!=0){page+=delta;Draw();}
            if((pad!=null&&(pad.buttonEast.wasPressedThisFrame||(dpsMode?pad.rightStickButton.wasPressedThisFrame:pad.leftStickButton.wasPressedThisFrame)))||(keys!=null&&keys.escapeKey.wasPressedThisFrame))box.Close();
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
            var text=new StringBuilder(dpsMode?(fullBuff?"<color=#81E6C4>풀 버프 · 예상 DPS</color>\n":"<color=#81E6C4>현재 상태 · 예상 DPS</color>\n"):fullBuff?"<color=#81E6C4>풀 버프 피해량</color>\n":"<color=#81E6C4>현재 상태 · 공격 피해 상세보기</color>\n");
            if(buffLabel)buffLabel.text=fullBuff?"현재 상태로 보기":dpsMode?"풀 버프 DPS":"풀 버프 피해량";
            RefreshShortcuts();
            for(int i=page*perPage;i<Math.Min(lines.Length,(page+1)*perPage);i++)text.AppendLine(lines[i]);
            text.AppendLine(showCalculating?"<color=#EDB14D>계산 중입니다</color>":"<color=#00000000>계산 중입니다</color>");
            if(error.Length>0)text.AppendLine(error);
            text.Append("\n<align=center>").Append(page+1).Append(" / ").Append(pages).Append("</align>");
            box.text.text=text.ToString();
        }
    }
}
