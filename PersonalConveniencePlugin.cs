using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("1.0.9.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.9.0")]

namespace SephiriaDicePreview
{
    [BepInPlugin("local.sephiria.personal-convenience", "Sephiria Personal Convenience", "1.0.9")]
    public sealed class PersonalConveniencePlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.9";
        public static PersonalConveniencePlugin Instance;
        public ConfigEntry<bool> ShowPreview;
        public ConfigEntry<bool> ShowComboHighlight;
        public ConfigEntry<bool> ShowRoomRetry;
        public ConfigEntry<bool> ShowDamageDetails;
        public ConfigEntry<bool> ShowRewardDpsPrediction;
        public BepInEx.Logging.ManualLogSource Log { get { return Logger; } }
        private void Awake()
        {
            Instance = this;
            UpdateUI.CheckOnStartup = Config.Bind("Updates", "CheckOnStartup", true, "Check public GitHub releases for newer versions on startup.");
            gameObject.AddComponent<UpdateUI>();
            ShowPreview = Config.Bind("Display", "ShowNextRoll", true, "Show the next reward reroll in the game UI.");
            ShowComboHighlight = Config.Bind("Display", "HighlightFruitCombos", true, "Outline items matching the positive fruit-skewer combos selected for this run.");
            ShowRoomRetry = Config.Bind("Display", "ShowRoomRetry", true, "Show retry current room in the pause menu.");
            ShowDamageDetails = Config.Bind("Display", "ShowDamageDetails", true, "Show damage details on demand in equipment tooltips.");
            ShowRewardDpsPrediction = Config.Bind("Display", "ShowRewardDpsPrediction", true, "Show predicted DPS increases on artifact reward choices.");
            gameObject.AddComponent<SephiriaRoomRetry.RoomRetryPlugin>();
            gameObject.AddComponent<SephiriaRoomRetry.StageRetry>();
            new Harmony("local.sephiria.personal-convenience").PatchAll(typeof(PersonalConveniencePlugin).Assembly);
        }
    }

    public static class Predictor
    {
        internal static HashSet<int> PreviewIds;
        public static SephiriteRewardMetadata[] Next(Sephirite source, PlayerAvatar player)
        {
            if (!NetworkServer.active || !source || !player || !source.isGenerated || source.isAcquired)
                return new SephiriteRewardMetadata[0];
            if (PreviewIds != null) throw new InvalidOperationException("Nested reward prediction.");
            var staging = new GameObject("DicePreviewSimulation");
            staging.SetActive(false);
            try
            {
                // Inactive, unspawned clone: the real reward queue and repeat exclusion stay untouched.
                var copy = UnityEngine.Object.Instantiate(source, staging.transform);
                copy.rewards.IsWritable = delegate { return true; };
                copy.rewards.IsRecording = delegate { return false; };
                copy.rewards.OnDirty = null;
                copy.rewards.Clear();
                copy.appearedItems = new Queue<int>(source.appearedItems);
                copy.isGenerated = false;
                copy.isAcquired = false;
                copy.Initialize(unchecked(source.CurrentSeed + 10000));
                PreviewIds = new HashSet<int>((HashSet<int>)AccessTools.Field(typeof(DungeonManager), "issuedInstanceID").GetValue(DungeonManager.Instance));
                copy.GenerateItems(player.gameObject);
                return copy.rewards.ToArray();
            }
            finally
            {
                PreviewIds = null;
                UnityEngine.Object.Destroy(staging);
            }
        }
    }

    [HarmonyPatch(typeof(DungeonManager), "GenerateInstanceID")]
    internal static class PreviewInstanceIds
    {
        private static bool Prefix(System.Random rand, ref int __result)
        {
            if (Predictor.PreviewIds == null) return true;
            int id;
            do { id = rand.Next(); } while (id == -1 || !Predictor.PreviewIds.Add(id));
            __result = id;
            return false;
        }
    }
}

namespace SephiriaDicePreview
{
    [HarmonyPatch(typeof(UI_OptionsPanel), "OnOpened")]
    internal static class OptionsPatch
    {
        private static void Postfix(UI_OptionsPanel __instance)
        {
            var settings = __instance.GetComponent<PreviewOptions>() ?? __instance.gameObject.AddComponent<PreviewOptions>();
            settings.Ensure(__instance);
        }
    }
}
namespace SephiriaDicePreview
{
    [HarmonyPatch(typeof(UI_SephiriteRewardPanel), "OnOpened")]
    internal static class RewardPreviewPatch
    {
        private static void Postfix(UI_SephiriteRewardPanel __instance)
        {
            if (!__instance.GetComponent<RewardPreview>()) __instance.gameObject.AddComponent<RewardPreview>();
        }
    }
    public sealed class RewardPreview : MonoBehaviour
    {
        public GameObject PreviewRoot;
        public SephiriteRewardMetadata[] Displayed = new SephiriteRewardMetadata[0];
        private UI_SephiriteRewardPanel panel;
        private Sephirite lastSource;
        private string lastKey;
        private float nextCheck;
        private readonly List<GameObject> cards = new List<GameObject>();
        private bool failed;
        private void Awake() { panel = GetComponent<UI_SephiriteRewardPanel>(); }
        private void OnDisable() { if (PreviewRoot) PreviewRoot.SetActive(false); foreach(var card in cards)if(card)card.SetActive(false); lastKey = null; failed = false; }
        private void LateUpdate()
        {
            if (!panel) return;
            bool show = panel.IsOpened && panel.rewardsGroupInteractable && panel.sephirite && panel.sephirite.isGenerated && !panel.sephirite.isAcquired
                && PersonalConveniencePlugin.Instance.ShowPreview.Value && NetworkServer.active;
            if (PreviewRoot) PreviewRoot.SetActive(show && !failed);
            foreach(var card in cards)if(card)card.SetActive(show&&!failed);
            if (!show || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .15f;
            var player = (PlayerAvatar)AccessTools.Field(typeof(UI_SephiriteRewardPanel), "openedAvatar").GetValue(panel);
            if (!player) return;
            var source = panel.sephirite;
            var key = source.type + ":" + ((HashSet<int>)AccessTools.Field(typeof(DungeonManager), "issuedInstanceID").GetValue(DungeonManager.Instance)).Count + ":" + source.CurrentSeed + ":" + source.rotation + ":" + player.GetCustomStat(ECustomStat.Luck) + ":" + player.GetCustomStat(ECustomStat.TABLET) + ":" + player.GetCustomStatUnsafe("EXTRAITEMCHOICES") + ":" + LocalizationManager.Instance.CurrentLanguage + ":" + OptionsBinding.Instance.Options.GetInt("ColorblindMode", 0)
                + ":" + String.Join(";", player.Inventory.inventoryMatrix.Values.Select(i => i.InstanceID + "," + i.EntityID + "," + i.Quantity + "," + i.XIdx + "," + i.YIdx).ToArray());
            if (lastSource == source && lastKey == key) return;
            lastSource = source; lastKey = key; failed = false;
            try { Displayed = Predictor.Next(source, player); Draw(); }
            catch (Exception e) { failed = true; if (PreviewRoot) PreviewRoot.SetActive(false); Debug.LogError("Dice preview unavailable: " + e); }
        }
        private static void Rect(RectTransform r, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
        private void Draw()
        {
            if (!PreviewRoot)
            {
                PreviewRoot = new GameObject("NextDicePreview");
                PreviewRoot.transform.SetParent(panel.transform, false);
            }
            foreach (var card in cards) if(card) { card.SetActive(false); Destroy(card); } cards.Clear();
            var field=AccessTools.Field(typeof(UI_SephiriteRewardPanel),"rewardElements");
            var rewards=field==null?null:field.GetValue(panel) as List<UI_SephiriteRewardElement>;
            int count=Math.Min(Displayed.Length,rewards==null?0:rewards.Count);
            for (int i=0;i<count;i++)
            {
                var item = ItemDatabase.FindItemById(Displayed[i].entityID); if (!item) continue;
                var host=rewards[i];if(!host)continue;
                var card = new GameObject("NextDicePreviewItem" + i, typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image),typeof(CanvasGroup),typeof(UnityEngine.UI.RectMask2D));
                card.transform.SetParent(host.transform,false);card.transform.SetAsFirstSibling();cards.Add(card);
                var group=card.GetComponent<CanvasGroup>();group.alpha=.88f;group.interactable=false;group.blocksRaycasts=false;
                var bg=card.GetComponent<UnityEngine.UI.Image>();bg.raycastTarget=false;
                Charm_Magic magic=null;
                if(item.type==EItemType.Charm&&item.resourcePrefab) magic=item.resourcePrefab.GetComponent<Charm_Magic>();
                bool isMagic=magic&&magic.GetSubIconCount()>0;
                if(isMagic) { bg.sprite=null; bg.color=Color.clear; }
                else bg.sprite=item.rarity==EItemRarity.Uncommon?panel.rewardPrefab.uncummonBGSprite:item.rarity==EItemRarity.Rare?panel.rewardPrefab.rareBGSprite:item.rarity==EItemRarity.Legend?panel.rewardPrefab.legendBGSprite:panel.rewardPrefab.cummonBGSprite;
                float reveal=Mathf.Clamp(host.rectTransform.rect.width*.34f,20,42);
                Rect((RectTransform)card.transform,Vector2.zero,Vector2.one,new Vector2(-reveal,5),new Vector2(-reveal,5));
                SephiriaPersonalConvenience.ComboOutline.ForPreview(card, item.id);
                float iconSpace = Mathf.Clamp(Mathf.Min(reveal-6,host.rectTransform.rect.height-8),10,26);
                float iconX=Mathf.Clamp(reveal*.5f/Mathf.Max(1,host.rectTransform.rect.width),.1f,.4f);
                if(isMagic)
                {
                    var bookGo=new GameObject("MagicBookFrame",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));bookGo.transform.SetParent(card.transform,false);
                    var book=bookGo.GetComponent<UnityEngine.UI.Image>();book.sprite=item.icon;book.preserveAspect=true;book.raycastTarget=false;
                    book.rectTransform.anchorMin=book.rectTransform.anchorMax=new Vector2(iconX,.5f);
                    book.rectTransform.anchoredPosition=Vector2.zero;
                    book.rectTransform.sizeDelta=new Vector2(book.preferredWidth,book.preferredHeight);
                    float bookScale=Mathf.Min(1,iconSpace/Mathf.Max(1,Mathf.Max(book.preferredWidth,book.preferredHeight)));
                    book.rectTransform.localScale=Vector3.one*bookScale;
                }
                var iconGo = new GameObject("Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image)); iconGo.transform.SetParent(card.transform,false);
                var icon = iconGo.GetComponent<UnityEngine.UI.Image>();var previewIcon=item.icon;
                if(isMagic)
                {
                    var spellIcon=magic.GetSubIconImage(default(ItemPosition),false,0);
                    if(spellIcon)previewIcon=spellIcon;
                }
                icon.sprite=previewIcon; icon.preserveAspect=true; icon.raycastTarget=false;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(iconX,.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                if(isMagic)
                {
                    float spellSpace=Mathf.Max(6,iconSpace*.42f);
                    icon.rectTransform.sizeDelta=new Vector2(icon.preferredWidth,icon.preferredHeight);
                    float scale=Mathf.Min(1,spellSpace/Mathf.Max(1,Mathf.Max(icon.preferredWidth,icon.preferredHeight)));
                    icon.rectTransform.localScale=Vector3.one*scale;
                }
                else
                {
                    icon.rectTransform.sizeDelta = new Vector2(icon.preferredWidth,icon.preferredHeight);
                    float scale = Mathf.Min(1, iconSpace / Mathf.Max(1, Mathf.Max(icon.preferredWidth,icon.preferredHeight)));
                    icon.rectTransform.localScale = Vector3.one * scale;
                }
                if(item.type==EItemType.StoneTablet) {
                    icon.material=OptionsBinding.Instance.Options.GetInt("ColorblindMode",0)==0?panel.rewardPrefab.tabletMaterial:panel.rewardPrefab.tabletMaterial_Colorblind;
                    var tablet=item.resourcePrefab.GetComponent<StoneTablet>();
                    if(tablet && DungeonManager.IsTabletRotatable(Displayed[i].instanceID,tablet.isRotatable)) icon.rectTransform.localRotation=Quaternion.Euler(0,0,panel.sephirite.rotation*90);
                }
            }
            PreviewRoot.SetActive(true);
        }
    }
}
