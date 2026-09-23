using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("1.0.8.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.8.0")]

namespace SephiriaDicePreview
{
    [BepInPlugin("local.sephiria.personal-convenience", "Sephiria Personal Convenience", "1.0.8")]
    public sealed class PersonalConveniencePlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.8";
        public static PersonalConveniencePlugin Instance;
        public ConfigEntry<bool> ShowPreview;
        public ConfigEntry<bool> ShowComboHighlight;
        public ConfigEntry<bool> ShowRoomRetry;
        public ConfigEntry<bool> ShowDamageDetails;
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
        private TMPro.TMP_Text heading;
        private bool failed;
        private void Awake() { panel = GetComponent<UI_SephiriteRewardPanel>(); }
        private void OnDisable() { if (PreviewRoot) PreviewRoot.SetActive(false); lastKey = null; failed = false; }
        private void LateUpdate()
        {
            if (!panel) return;
            bool show = panel.IsOpened && panel.rewardsGroupInteractable && panel.sephirite && panel.sephirite.isGenerated && !panel.sephirite.isAcquired
                && PersonalConveniencePlugin.Instance.ShowPreview.Value && NetworkServer.active;
            if (PreviewRoot) PreviewRoot.SetActive(show && !failed);
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
        private TMPro.TMP_Text Text(Transform parent, string name, string value, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TMPro.TextMeshProUGUI>(); text.font = panel.rerollGuideText.font;
            text.fontSharedMaterial = panel.rerollGuideText.fontSharedMaterial;
            text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size - 2; text.fontSizeMax = size;
            text.alignment = TMPro.TextAlignmentOptions.Center; text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.raycastTarget = false; text.text = value; return text;
        }
        private static void Rect(RectTransform r, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
        private void Draw()
        {
            if (!PreviewRoot)
            {
                PreviewRoot = new GameObject("NextDicePreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                PreviewRoot.transform.SetParent(panel.transform, false);
                Rect((RectTransform)PreviewRoot.transform, new Vector2(.025f, 1), new Vector2(.425f, 1), new Vector2(0,-82), new Vector2(0,-10));
                var bg = PreviewRoot.GetComponent<UnityEngine.UI.Image>(); bg.color = new Color(.08f,.07f,.12f,.94f); bg.raycastTarget = false;
                heading = Text(PreviewRoot.transform, "Heading", "", 11);
                Rect((RectTransform)heading.transform, new Vector2(0,1), Vector2.one, new Vector2(4,-17),new Vector2(-4,-2));
                heading.color = new Color(1,.86f,.58f);
            }
            heading.text = PreviewOptions.Korean ? "다음 주사위 결과 · 미리보기" : "Next dice roll · Preview";
            foreach (var card in cards) { card.SetActive(false); Destroy(card); } cards.Clear();
            int columns = Math.Min(6, Math.Max(1, Displayed.Length)); int rows = (Displayed.Length + columns - 1) / columns;
            for (int i=0;i<Displayed.Length;i++)
            {
                var item = ItemDatabase.FindItemById(Displayed[i].entityID); if (!item) continue;
                var card = new GameObject("PreviewItem" + i, typeof(RectTransform)); card.transform.SetParent(PreviewRoot.transform,false); cards.Add(card);
                SephiriaPersonalConvenience.ComboOutline.ForPreview(card, item.id);
                float left = (float)(i%columns)/columns, right = (float)(i%columns+1)/columns;
                float top = -(19 + (i/columns)*51f/Math.Max(1,rows)), bottom = top - 51f/Math.Max(1,rows);
                Rect((RectTransform)card.transform,new Vector2(left,1),new Vector2(right,1),new Vector2(2,bottom),new Vector2(-2,top));
                var iconGo = new GameObject("Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image)); iconGo.transform.SetParent(card.transform,false);
                var icon = iconGo.GetComponent<UnityEngine.UI.Image>(); icon.sprite=item.icon; icon.preserveAspect=true; icon.raycastTarget=false;
                                float iconSpace = 24f / Math.Max(1, rows);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f,1);
                icon.rectTransform.anchoredPosition = new Vector2(0,-iconSpace*.5f);
                icon.rectTransform.sizeDelta = new Vector2(icon.preferredWidth,icon.preferredHeight);
                float scale = Mathf.Min(1, iconSpace / Mathf.Max(1, Mathf.Max(icon.preferredWidth,icon.preferredHeight)));
                icon.rectTransform.localScale = Vector3.one * scale;
                if(item.type==EItemType.Charm && item.resourcePrefab) {
                    var charm=item.resourcePrefab.GetComponent<Charm_Basic>();
                    if(charm) for(int sub=0;sub<charm.GetSubIconCount();sub++) {
                        var subGo=new GameObject("Detail"+sub,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));subGo.transform.SetParent(icon.transform,false);
                        var subImage=subGo.GetComponent<UnityEngine.UI.Image>();subImage.sprite=charm.GetSubIconImage(default(ItemPosition),false,sub);subImage.raycastTarget=false;
                        subImage.rectTransform.sizeDelta=new Vector2(subImage.preferredWidth,subImage.preferredHeight);
                        subImage.rectTransform.anchoredPosition=charm.GetSubIconImageOffset(sub);
                    }
                }
                if(item.type==EItemType.StoneTablet) {
                    icon.material=OptionsBinding.Instance.Options.GetInt("ColorblindMode",0)==0?panel.rewardPrefab.tabletMaterial:panel.rewardPrefab.tabletMaterial_Colorblind;
                    var tablet=item.resourcePrefab.GetComponent<StoneTablet>();
                    if(tablet && DungeonManager.IsTabletRotatable(Displayed[i].instanceID,tablet.isRotatable)) icon.rectTransform.localRotation=Quaternion.Euler(0,0,panel.sephirite.rotation*90);
                }
                var name=Text(card.transform,"Name",item.Name,9); name.color=ItemDatabase.GetColorViaItemRarity(item.rarity);
                Rect((RectTransform)name.transform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-(iconSpace+1)));
            }
            PreviewRoot.SetActive(true);
        }
    }
}
