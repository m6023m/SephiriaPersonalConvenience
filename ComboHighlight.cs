using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using SephiriaDicePreview;

namespace SephiriaPersonalConvenience
{
    public static class FruitCombos
    {
        public static PlayerAvatar LocalPlayer
        {
            get { return PlayerSpawner.MultiplayerList.Where(p => p && p.isLocalPlayer).Select(p => p.PlayerAvatar).FirstOrDefault(); }
        }
        public static bool Matches(ItemEntity item, PlayerAvatar player)
        {
            if (!item || !player || item.categories == null || !player.spawner) return false;
            // During a run, use the skewer actually consumed at entry (including loaded runs).
            IEnumerable<GridInventory.ItemDropBonusData> bonuses = player.isInDungeon > 0
                ? (IEnumerable<GridInventory.ItemDropBonusData>)player.spawner.consumeFruitSkewerBonus
                : player.localDataStorage.fruitSkewerBonus;
            foreach (string category in item.categories)
            {
                int weight = 0;
                foreach (var bonus in bonuses) if (bonus.categoryName == category) weight += bonus.weight;
                if (weight > 0) return true;
            }
            return false;
        }
    }
    public sealed class ComboOutline : MonoBehaviour
    {
        private UI_SephiriteRewardElement reward;
        private UI_NewInventoryIcon shop;
        private int previewEntityId = -1;
        private GameObject border;
        private float nextCheck;
        public bool Highlighted { get { return border && border.activeInHierarchy; } }
        public static void ForReward(UI_SephiriteRewardElement source)
        {
            var outline = source.GetComponent<ComboOutline>() ?? source.gameObject.AddComponent<ComboOutline>();
            outline.reward = source;
            outline.Create(source.bgImage ? source.bgImage.rectTransform : source.rectTransform, 1.5f);
            outline.Refresh();
        }
        public static void ForShop(UI_NewInventoryIcon source)
        {
            if (!source.GetComponentInParent<UI_ShopPanel>()) return;
            var outline = source.GetComponent<ComboOutline>() ?? source.gameObject.AddComponent<ComboOutline>();
            outline.shop = source;
            outline.Create(source.bgImage ? source.bgImage.rectTransform : source.rectTransform, 1.5f);
            outline.Refresh();
        }
        public static void ForPreview(GameObject card, int entityId)
        {
            var outline = card.AddComponent<ComboOutline>(); outline.previewEntityId = entityId;
            outline.Create((RectTransform)card.transform, 1f); outline.Refresh();
        }
        private void Create(RectTransform target, float width)
        {
            if(border) return;
            border = new GameObject("FruitComboOutline", typeof(RectTransform), typeof(LayoutElement));
            border.transform.SetParent(target,false);border.GetComponent<LayoutElement>().ignoreLayout=true;
            var rect=(RectTransform)border.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(-1,-1);rect.offsetMax=new Vector2(1,1);
            Edge("Top",new Vector2(0,1),Vector2.one,new Vector2(0,-width),Vector2.zero);
            Edge("Bottom",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,width));
            Edge("Left",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(width,0));
            Edge("Right",new Vector2(1,0),Vector2.one,new Vector2(-width,0),Vector2.zero);
        }
        private void Edge(string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(border.transform,false);
            var image=go.GetComponent<Image>();image.color=new Color32(93,255,215,255);image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
        }
        private void OnEnable() { nextCheck=0; }
        private void LateUpdate() { if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.1f;Refresh(); }
        public void Refresh()
        {
            if(!border)return;
            int id=previewEntityId;
            if(reward)id=reward.reward.entityID;
            else if(shop)id=shop.Item!=null?shop.Item.EntityID:-1;
            bool visible=PersonalConveniencePlugin.Instance && PersonalConveniencePlugin.Instance.ShowComboHighlight.Value
                && id>=0 && FruitCombos.Matches(ItemDatabase.FindItemById(id),FruitCombos.LocalPlayer);
            border.SetActive(visible);
        }
    }
    [HarmonyPatch(typeof(UI_SephiriteRewardElement),"Initialize")]
    internal static class RewardOutlinePatch
    {
        private static void Postfix(UI_SephiriteRewardElement __instance) { ComboOutline.ForReward(__instance); }
    }
    [HarmonyPatch(typeof(UI_NewInventoryIcon),"SetItemReference")]
    internal static class ShopOutlinePatch
    {
        private static void Postfix(UI_NewInventoryIcon __instance) { ComboOutline.ForShop(__instance); }
    }
}
