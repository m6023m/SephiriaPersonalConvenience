using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaDicePreview
{
    public static class OtherDicePredictor
    {
        public static T Field<T>(object source,string name) { return (T)AccessTools.Field(source.GetType(),name).GetValue(source); }
        public static MiracleMetadata[] MiracleNext(MiracleSelector2 source, MiracleController actor)
        {
            if(!NetworkServer.active || !source || !actor)return new MiracleMetadata[0];
            var staging=new GameObject("MiraclePreviewSimulation");staging.SetActive(false);
            try {
                var copy=UnityEngine.Object.Instantiate(source,staging.transform);
                var counts=new Dictionary<NetworkIdentity,int>(Field<Dictionary<NetworkIdentity,int>>(source,"rerolledCount"));
                int count;counts.TryGetValue(actor.netIdentity,out count);counts[actor.netIdentity]=count+1;
                var requested=Field<Dictionary<NetworkIdentity,List<Miracle>>>(source,"requestedMiracles").ToDictionary(p=>p.Key,p=>new List<Miracle>(p.Value));
                AccessTools.Field(typeof(MiracleSelector2),"rerolledCount").SetValue(copy,counts);
                AccessTools.Field(typeof(MiracleSelector2),"requestedMiracles").SetValue(copy,requested);
                AccessTools.Field(typeof(MiracleSelector2),"currentMiracles").SetValue(copy,new Dictionary<NetworkIdentity,List<Miracle>>());
                return (MiracleMetadata[])AccessTools.Method(typeof(MiracleSelector2),"GenerateMiracles").Invoke(copy,new object[]{actor.netIdentity,actor.UnitAvatar.RandomID,actor.UnitAvatar});
            } finally { UnityEngine.Object.Destroy(staging); }
        }
        public static EnhancementMetadata[] WeaponNext(Anvil source,PlayerAvatar actor)
        {
            var controller=actor?actor.GetComponent<WeaponControllerSimple>():null;
            if(!source||!controller||!controller.currentWeapon)return new EnhancementMetadata[0];
            var weapon=WeaponDatabase.FindWeaponById(controller.currentWeapon.entityId);
            var available=WeaponDatabase.GetWeaponEnhancements(weapon.id);if(available==null)return new EnhancementMetadata[0];
            var staging=new GameObject("WeaponPreviewSimulation");staging.SetActive(false);
            try {
                var copy=UnityEngine.Object.Instantiate(source,staging.transform);
                copy.candidates=new List<EnhancementMetadata>(source.candidates);
                var result=new List<EnhancementMetadata>();
                var random=new System.Random(unchecked(source.RandomID+actor.RandomID+source.localRerollSeedOffset+10000));
                int count=Mathf.Min(source.enhanceSlotCount+actor.GetCustomStatUnsafe("EXTRAWEAPONCHOICES"),available.Count);
                for(int i=0;i<count;i++)result.Add((EnhancementMetadata)AccessTools.Method(typeof(Anvil),"GetRandomEnhancement").Invoke(copy,new object[]{random,weapon,result.Select(e=>e.enhanced).ToArray()}));
                return result.ToArray();
            } finally {UnityEngine.Object.Destroy(staging);}
        }
    }
    [HarmonyPatch(typeof(UI_MiraclePanel),"OnOpened")]
    internal static class MiraclePreviewPatch { private static void Postfix(UI_MiraclePanel __instance){if(!__instance.GetComponent<OtherDicePreview>())__instance.gameObject.AddComponent<OtherDicePreview>();} }
    [HarmonyPatch(typeof(UI_WeaponEnhancementPanel),"OnOpened")]
    internal static class WeaponPreviewPatch { private static void Postfix(UI_WeaponEnhancementPanel __instance){if(!__instance.GetComponent<OtherDicePreview>())__instance.gameObject.AddComponent<OtherDicePreview>();} }
    public sealed class OtherDicePreview : MonoBehaviour
    {
        public GameObject PreviewRoot;
        public MiracleMetadata[] Miracles=new MiracleMetadata[0];
        public EnhancementMetadata[] Weapons=new EnhancementMetadata[0];
        private UI_MiraclePanel miracle;
        private UI_WeaponEnhancementPanel weapon;
        private string key;
        private float next;
        private TMP_Text text;
        private readonly List<UI_WeaponEnhancementButton> weaponCards=new List<UI_WeaponEnhancementButton>();
        private bool failed;
        private void Awake(){miracle=GetComponent<UI_MiraclePanel>();weapon=GetComponent<UI_WeaponEnhancementPanel>();}
        private void OnDisable(){key=null;failed=false;if(PreviewRoot)PreviewRoot.SetActive(false);}
        private void LateUpdate()
        {
            bool show=PersonalConveniencePlugin.Instance.ShowPreview.Value && (miracle?miracle.IsOpened && NetworkServer.active && miracle.rerollButton.activeInHierarchy:weapon && weapon.IsOpened && weapon.rerollButtonGroup.gameObject.activeInHierarchy);
            if(PreviewRoot)PreviewRoot.SetActive(show && !failed);if(!show||Time.unscaledTime<next)return;next=Time.unscaledTime+.15f;
            var player=miracle?OtherDicePredictor.Field<PlayerAvatar>(miracle,"playerAvatar"):OtherDicePredictor.Field<PlayerAvatar>(weapon,"player");
            if(!player)return;
            string state=LocalizationManager.Instance.CurrentLanguage+":"+String.Join(";",player.Inventory.inventoryMatrix.Values.Select(i=>i.InstanceID+","+i.EntityID+","+i.Quantity+","+i.XIdx+","+i.YIdx).ToArray());
            string[] names;
            try {
                if(miracle){
                    var source=OtherDicePredictor.Field<MiracleSelector2>(miracle,"miracleSelector");var actor=OtherDicePredictor.Field<MiracleController>(miracle,"miracleController");if(!source||!actor)return;
                    int count;OtherDicePredictor.Field<Dictionary<NetworkIdentity,int>>(source,"rerolledCount").TryGetValue(actor.netIdentity,out count);
                    state+=":"+source.GetInstanceID()+":"+count+":"+player.GetCustomStatUnsafe("EXTRAMIRACLECHOICES");if(key==state)return;
                    Miracles=OtherDicePredictor.MiracleNext(source,actor);names=Miracles.Select(m=>MiracleDatabase.FindMiracle(m.id).Name).ToArray();
                }else{
                    var source=OtherDicePredictor.Field<Anvil>(weapon,"anvil");if(!source)return;
                    state+=":"+source.GetInstanceID()+":"+source.localRerollSeedOffset+":"+player.GetComponent<WeaponControllerSimple>().currentWeapon.entityId+":"+player.GetCustomStatUnsafe("EXTRAWEAPONCHOICES");if(key==state)return;
                    Weapons=OtherDicePredictor.WeaponNext(source,player);names=Weapons.Select(w=>w.enhanced.Name).ToArray();
                }
                key=state;failed=false;Draw(names);
            }catch(Exception e){key=state;failed=true;if(PreviewRoot)PreviewRoot.SetActive(false);Debug.LogError("Dice preview unavailable: "+e);}
        }
        private void Draw(string[] names)
        {
            if(!PreviewRoot){
                PreviewRoot=new GameObject("NextDicePreview",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));PreviewRoot.transform.SetParent(transform,false);
                var rect=(RectTransform)PreviewRoot.transform;rect.anchorMin=new Vector2(miracle ? .1f : .04f,0);rect.anchorMax=new Vector2(miracle ? .9f : .82f,0);rect.offsetMin=new Vector2(0,miracle?55:8);rect.offsetMax=new Vector2(0,miracle?87:92);
                var bg=PreviewRoot.GetComponent<Image>();bg.color=miracle?new Color(.08f,.07f,.12f,.94f):Color.clear;bg.raycastTarget=false;
                var go=new GameObject("Results",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(PreviewRoot.transform,false);
                text=go.GetComponent<TextMeshProUGUI>();var template=miracle?miracle.rerollButtonText:weapon.rerollGuideText;text.font=template.font;text.fontSharedMaterial=template.fontSharedMaterial;text.fontSize=11;text.enableAutoSizing=true;text.fontSizeMin=8;text.fontSizeMax=11;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
                rect=(RectTransform)text.transform;rect.anchorMin=miracle?Vector2.zero:new Vector2(0,.76f);rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(6,3);rect.offsetMax=new Vector2(-6,-3);
            }
            if(miracle)text.text="<color=#FFDB94>"+(PreviewOptions.Korean?"다음 주사위 결과 · 미리보기":"Next dice roll · Preview")+"</color>\n"+String.Join("   /   ",names);
            else DrawWeaponCards();
            PreviewRoot.SetActive(true);
        }
        private void DrawWeaponCards()
        {
            foreach(var card in weaponCards)if(card){card.gameObject.SetActive(false);Destroy(card.gameObject);}weaponCards.Clear();
            text.text="<color=#FFDB94>"+(PreviewOptions.Korean?"다음 주사위 결과 · 미리보기":"Next dice roll · Preview")+"</color>";
            var player=OtherDicePredictor.Field<PlayerAvatar>(weapon,"player");
            var current=player?player.GetComponent<WeaponControllerSimple>().currentWeapon:null;
            if(!current||Weapons.Length==0)return;
            var rootRect=(RectTransform)PreviewRoot.transform;
            rootRect.anchorMin=new Vector2(.82f,.16f);rootRect.anchorMax=new Vector2(.995f,.9f);rootRect.offsetMin=rootRect.offsetMax=Vector2.zero;
            var titleRect=(RectTransform)text.transform;
            titleRect.anchorMin=new Vector2(0,.92f);titleRect.anchorMax=Vector2.one;titleRect.offsetMin=new Vector2(2,1);titleRect.offsetMax=new Vector2(-2,-1);
            Canvas.ForceUpdateCanvases();
            float rootWidth=Mathf.Max(1,rootRect.rect.width);
            for(int i=0;i<Weapons.Length;i++)
            {
                var prefab=current is WeaponSimple_Crossbow?(UI_WeaponEnhancementButton)weapon.buttonPrefab_Crossbow:weapon.buttonPrefab;
                var card=Instantiate(prefab,PreviewRoot.transform);weaponCards.Add(card);card.name="WeaponPreviewCard"+i;
                card.SetWeaponMethod(weapon,current,Weapons[i]);card.enabled=false;if(card.button)card.button.interactable=false;
                var group=card.GetComponent<CanvasGroup>()??card.gameObject.AddComponent<CanvasGroup>();group.alpha=.92f;group.interactable=false;group.blocksRaycasts=false;
                card.effectText.gameObject.SetActive(false);card.nameText.enableAutoSizing=false;card.nameText.fontSize=30;
                var rect=card.rectTransform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.8f-i*(.72f/Mathf.Max(1,Weapons.Length-1)));rect.anchoredPosition=Vector2.zero;
                float scale=Mathf.Min(.34f,(rootWidth-4)/Mathf.Max(1,rect.rect.width));
                rect.localScale=Vector3.one*Mathf.Max(.2f,scale);
            }
        }
    }
}



