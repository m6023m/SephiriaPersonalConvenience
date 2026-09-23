using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaDicePreview
{
    public sealed class PreviewOptions : MonoBehaviour
    {
        public UI_HorizontalSelectionBox Box, ComboBox, RetryBox, UpdateBox, DamageBox, RewardDpsBox;
        public int TabIndex;
        private UI_TabButton tabButton;
        private UI_TabContent content;
        private readonly List<Action> refreshers = new List<Action>();
        public static bool Korean { get { return LocalizationManager.Instance.CurrentLanguage == "ko-KR"; } }
        public void Ensure(UI_OptionsPanel panel)
        {
            if (!content)
            {
                var template = panel.tab.tabContents[0].GetComponentsInChildren<UI_OptionBox_Common_Integer>(true).First(t => t.enabled && t.box && t.valueText);
                var staging = new GameObject("PersonalOptionsStaging"); staging.SetActive(false);
                content=Instantiate(panel.tab.tabContents[0],staging.transform);content.gameObject.SetActive(false);content.name="PersonalConvenienceSettings";
                var scroll=content.GetComponentInChildren<ScrollRect>(true);var items=scroll.content;
                foreach(var activator in items.GetComponents<UI_FontBoxActivator>()){activator.enabled=false;Destroy(activator);}
                foreach(Transform child in items){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                var layout=items.GetComponent<VerticalLayoutGroup>();layout.childForceExpandHeight=false;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandWidth=true;
                content.parent=panel;
                RetryBox=Row(template,items,PersonalConveniencePlugin.Instance.ShowRoomRetry,"이번 방 재시도 버튼","Retry current room button","RoomRetryOption");
                Box=Row(template,items,PersonalConveniencePlugin.Instance.ShowPreview,"주사위 결과 미리보기","Next dice roll preview","DicePreviewOption");
                ComboBox=Row(template,items,PersonalConveniencePlugin.Instance.ShowComboHighlight,"과일 꼬치 콤보 강조","Fruit skewer combo outline","FruitComboOption");
                RetryBox.forceNavDown=Box;Box.forceNavUp=RetryBox;Box.forceNavDown=ComboBox;ComboBox.forceNavUp=Box;
                UpdateBox=UpdateRow(template,items);
                DamageBox=Row(template,items,PersonalConveniencePlugin.Instance.ShowDamageDetails,"공격 피해 상세보기","Attack damage details","DamageDetailsOption");
                RewardDpsBox=Row(template,items,PersonalConveniencePlugin.Instance.ShowRewardDpsPrediction,"보상 DPS 증가율","Reward DPS increase","RewardDpsOption");
                DamageBox.transform.SetSiblingIndex(UpdateBox.transform.GetSiblingIndex());
                RewardDpsBox.transform.SetSiblingIndex(UpdateBox.transform.GetSiblingIndex());
                ComboBox.forceNavDown=DamageBox;DamageBox.forceNavUp=ComboBox;DamageBox.forceNavDown=RewardDpsBox;RewardDpsBox.forceNavUp=DamageBox;RewardDpsBox.forceNavDown=UpdateBox;UpdateBox.forceNavUp=RewardDpsBox;
                content.selectionOnOpened=RetryBox.gameObject;
                var hint=Instantiate(template.valueText.text,items);hint.name="ConvenienceGuide";
                foreach(Transform child in hint.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                foreach(var localized in hint.GetComponents<UI_LocalizationStringText>())localized.enabled=false;
                var hintLayout=hint.gameObject.AddComponent<LayoutElement>();hintLayout.preferredHeight=98;
                hint.margin=new Vector4(6,0,6,0);hint.alignment=TextAlignmentOptions.TopLeft;hint.fontSize=11;hint.enableAutoSizing=false;hint.textWrappingMode=TextWrappingModes.Normal;hint.raycastTarget=false;
                refreshers.Add(delegate{hint.text=Korean?"\n이번 방 재시도: 일시정지 메뉴에서 현재 방을 다시 시작합니다.\n\n주사위 미리보기: 보상·기적·무기 강화의 다음 1회 결과를 표시합니다.\n\n콤보 강조: 과일 꼬치에서 늘린 콤보의 아이템을\n상점·보상 선택지·미리보기에서 민트색 테두리로 표시합니다.":"\nRoom retry: restart this room from the pause menu.\n\nDice preview: show the next reward, miracle or weapon reroll.\n\nCombo outline: mint borders mark the combos favored by your fruit skewer in shops, rewards and previews.";});
                TabIndex=panel.tab.tabButtons.Length;
                tabButton=Instantiate(panel.tab.tabButtons[0],staging.transform);tabButton.name="PersonalConvenienceTab";
                foreach(var localized in tabButton.GetComponentsInChildren<UI_LocalizationStringText>(true))localized.enabled=false;
                var pointer=tabButton as UI_TabPointClickableButton;if(pointer)pointer.tab=panel.tab;
                var click=tabButton.GetComponent<Button>();if(click){click.onClick=new Button.ButtonClickedEvent();click.onClick.AddListener(delegate{panel.SelectTab(TabIndex);});}
                content.transform.SetParent(panel.tab.tabContents[0].transform.parent,false);
                tabButton.transform.SetParent(panel.tab.tabButtons[0].transform.parent,false);
                var previous=panel.tab.tabButtons;
                panel.tab.tabButtons=previous.Concat(new[]{tabButton}).ToArray();
                panel.tab.tabContents=panel.tab.tabContents.Concat(new[]{content}).ToArray();
                ResizeTabs(panel.tab,previous);
                foreach(var page in panel.tab.tabContents) TabFrame.Page(page);
                foreach(var button in panel.tab.tabButtons) button.gameObject.AddComponent<TabFrame>();
                tabButton.transform.parent.SetAsLastSibling();
                tabButton.SetTabButtonSprite(false);tabButton.gameObject.SetActive(true);
                scroll.verticalNormalizedPosition=1;Destroy(staging);
            }
            foreach(var text in tabButton.GetComponentsInChildren<TMP_Text>(true))text.text=Korean?"확장":"Extras";
            foreach(var refresh in refreshers)refresh();
        }
        private void ResizeTabs(UI_Tab tab,UI_TabButton[] previous)
        {
            var first=(RectTransform)previous[0].transform;var last=(RectTransform)previous[previous.Length-1].transform;
            float left=first.anchoredPosition.x-first.rect.width*first.pivot.x;
            float right=last.anchoredPosition.x+last.rect.width*(1-last.pivot.x);
            var layout=first.parent.GetComponent<HorizontalLayoutGroup>();
            if(layout){layout.childControlWidth=true;layout.childForceExpandWidth=false;}
            float width = layout ? (previous.Sum(b => ((RectTransform)b.transform).rect.width) - layout.spacing) / tab.tabButtons.Length : (right-left)/tab.tabButtons.Length;
            foreach(var button in tab.tabButtons){
                var rect=(RectTransform)button.transform;
                if(layout){var size=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=width;size.flexibleWidth=0;}
                else {int i=Array.IndexOf(tab.tabButtons,button);rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width-3);rect.anchoredPosition=new Vector2(left+i*width+rect.pivot.x*(width-3),rect.anchoredPosition.y);}
                foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.enableAutoSizing=true;text.fontSizeMin=9;text.fontSizeMax=12;text.textWrappingMode=TextWrappingModes.NoWrap;}
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)first.parent);
        }
        private UI_HorizontalSelectionBox UpdateRow(UI_OptionBox_Common_Integer template, Transform parent)
        {
            var row=Instantiate(template.gameObject,parent);row.SetActive(false);row.name="ConvenienceUpdateOption";
            var original=row.GetComponent<UI_OptionBox_Common_Integer>();original.enabled=false;
            var box=row.GetComponent<UI_HorizontalSelectionBox>();var value=original.valueText.text;TMP_Text label=null;
            foreach(var localized in row.GetComponentsInChildren<UI_LocalizationStringText>(true)){localized.enabled=false;if(localized.text!=value)label=localized.text;}
            Destroy(original);box.ValueChangedCallback=new UnityEngine.Events.UnityEvent<int>();box.numberOfElements=2;box.overflowType=UI_HorizontalSelectionBox.OverflowType.Repeat;
            box.forceNavUp=null;box.forceNavDown=null;
            var le=row.GetComponent<LayoutElement>()??row.AddComponent<LayoutElement>();le.preferredHeight=20;le.minHeight=20;
            Action refresh=delegate{box.ChangeValueWithoutNotify(0);if(label)label.text=Korean?"편의 모드 업데이트":"Convenience mod update";value.text=(Korean?"확인":"Check")+" ("+PersonalConveniencePlugin.Version+")";};
            refreshers.Add(refresh);box.OnValueChanged+=delegate(int i){if(UpdateUI.Instance)UpdateUI.Instance.Check(true);refresh();};row.SetActive(true);refresh();return box;
        }
        private UI_HorizontalSelectionBox Row(UI_OptionBox_Common_Integer template,Transform parent,ConfigEntry<bool> setting,string ko,string en,string name)
        {
            var row=Instantiate(template.gameObject,parent);row.SetActive(false);row.name=name;
            var original=row.GetComponent<UI_OptionBox_Common_Integer>();original.enabled=false;
            var box=row.GetComponent<UI_HorizontalSelectionBox>();var value=original.valueText.text;TMP_Text label=null;
            foreach(var localized in row.GetComponentsInChildren<UI_LocalizationStringText>(true)){localized.enabled=false;if(localized.text!=value)label=localized.text;}
            Destroy(original);box.ValueChangedCallback=new UnityEngine.Events.UnityEvent<int>();box.numberOfElements=2;box.overflowType=UI_HorizontalSelectionBox.OverflowType.Repeat;
            box.forceNavUp=null;box.forceNavDown=null;
            var le=row.GetComponent<LayoutElement>()??row.AddComponent<LayoutElement>();le.preferredHeight=20;le.minHeight=20;
            Action refresh=delegate{box.ChangeValueWithoutNotify(setting.Value?1:0);if(label)label.text=Korean?ko:en;value.text=setting.Value?(Korean?"켜기":"On"):(Korean?"끄기":"Off");};
            refreshers.Add(refresh);box.OnValueChanged+=delegate(int i){setting.Value=i==1;refresh();};row.SetActive(true);refresh();return box;
        }
    }
}






namespace SephiriaDicePreview
{
    // Native pages bake the five-tab notch into their background sprite. Use a shared
    // rectangular frame so the added sixth tab and all original tabs line up correctly.
    public sealed class TabFrame : MonoBehaviour
    {
        private UI_TabButton button;
        private Image fill;
        private Image[] edges;
        private bool last;
        private static readonly Color Body = new Color32(47,47,69,255);
        private static readonly Color Active = new Color32(222,222,239,255);
        public static void Page(UI_TabContent page)
        {
            var original=page.GetComponent<Image>();if(original)original.enabled=false;
            var body=ImageRect(page.transform,"OptionsFrame",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-20),Body);
            body.raycastTarget=true;body.transform.SetAsFirstSibling();
            Border(body.transform,Active,2,true);
        }
        private void Awake()
        {
            button=GetComponent<UI_TabButton>();button.butttonImage.color=Color.clear;
            fill=ImageRect(transform,"TabFrame",Vector2.zero,Vector2.one,new Vector2(0,-2),Vector2.zero,Color.black);
            fill.transform.SetAsFirstSibling();edges=Border(fill.transform,Active,2,false);Refresh();
        }
        private void LateUpdate(){if(last!=button.IsActivated)Refresh();}
        private void Refresh(){last=button.IsActivated;fill.color=last?Body:Color.black;foreach(var edge in edges)edge.color=last?Active:(Color)new Color32(55,55,55,255);}
        private static Image[] Border(Transform parent,Color color,float width,bool bottom)
        {
            var list=new List<Image>();
            list.Add(ImageRect(parent,"Top",new Vector2(0,1),Vector2.one,new Vector2(width,-width),new Vector2(-width,0),color));
            list.Add(ImageRect(parent,"Left",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(width,-width),color));
            list.Add(ImageRect(parent,"Right",new Vector2(1,0),Vector2.one,new Vector2(-width,0),new Vector2(0,-width),color));
            if(bottom)list.Add(ImageRect(parent,"Bottom",Vector2.zero,new Vector2(1,0),new Vector2(width,0),new Vector2(-width,width),color));
            return list.ToArray();
        }
        private static Image ImageRect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(LayoutElement));go.transform.SetParent(parent,false);go.GetComponent<LayoutElement>().ignoreLayout=true;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;var r=image.rectTransform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return image;
        }
    }
}



