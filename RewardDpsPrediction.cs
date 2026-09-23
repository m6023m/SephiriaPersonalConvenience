using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaDicePreview
{
    internal static class RewardDpsPrediction
    {
        internal sealed class ComboChange
        {
            internal string Category;
            internal ComboEffectBase Effect;
            internal int Before,After;
        }
        internal sealed class ItemSource
        {
            internal string Name;
            internal WeaponSimple Weapon;
            internal ItemEntity Entity;
            internal Charm_Basic Live;
            internal int Level;
        }
        [ThreadStatic] internal static Charm_Basic PreviewCharm;
        [ThreadStatic] internal static int PreviewLevel;
        private sealed class Scope:IDisposable
        {
            private readonly Charm_Basic previous;
            private readonly int previousLevel;
            internal Scope(Charm_Basic charm,int level){previous=PreviewCharm;previousLevel=PreviewLevel;PreviewCharm=charm;PreviewLevel=level;}
            public void Dispose(){PreviewCharm=previous;PreviewLevel=previousLevel;}
        }
        internal static IDisposable Push(Charm_Basic charm,int level){return new Scope(charm,level);}

        internal static Dictionary<string,int> Changes(ItemEntity entity,int level,PlayerAvatar player,out List<ComboChange> combos)
        {
            var result=new Dictionary<string,int>(StringComparer.Ordinal);
            combos=new List<ComboChange>();
            if(!entity||!entity.resourcePrefab)return result;
            var source=entity.resourcePrefab.GetComponent<Charm_Basic>();
            var status=source as Charm_StatusInstance;
            if(status!=null)
            {
                int index=status.LevelToIdx(level);
                foreach(var group in status.stats)
                    AddStatus(result,StatusDatabase.CreateStatusEntity(group.statusID,group.valuesByLevel.SafeRandomAccess(index)));
            }
            if(entity.categories==null||!player.Inventory)return result;
            foreach(string categoryName in entity.categories.Distinct())
            {
                var category=ItemDatabase.FindItemCategory(categoryName);
                if(!category||!category.comboEffectPrefab)continue;
                var current=player.Inventory.FindComboEffect(categoryName);
                var effect=category.comboEffectPrefab.GetComponent<ComboEffectBase>();
                if(!effect)continue;
                int before=current?current.comboCount:0,after=before+1;
                combos.Add(new ComboChange{Category=categoryName,Effect=effect,Before=before,After=after});
                foreach(var stat in effect.addStatByCombo)
                    if(stat.comboCount>before&&stat.comboCount<=after)
                        foreach(string encoded in stat.status)AddStatus(result,StatusDatabase.CreateStatusEntity(encoded));
            }
            return result;
        }
        private static void Add(Dictionary<string,int> result,string key,int value)
        {
            if(string.IsNullOrEmpty(key)||value==0)return;
            key=key.ToUpperInvariant();int old;result.TryGetValue(key,out old);result[key]=old+value;
        }
        private static void AddStatus(Dictionary<string,int> result,StatusInstance status)
        {
            if(status==null)return;
            string[] names=null;
            if(status.GetType()==typeof(StatusInstance_MaxMP))names=new[]{DamageStatPreview.MaxMpDeltaKey};
            else if(status.GetType()==typeof(StatusInstance_MaxHP))names=new[]{DamageStatPreview.MaxHpDeltaKey};
            else if(status.GetType()==typeof(StatusInstance_FinalHP))names=new[]{DamageStatPreview.FinalHpDeltaKey};
            else if(status.GetType()==typeof(StatusInstance_Custom))
            {
                string name=(string)AccessTools.Field(typeof(StatusInstance_Custom),"customID").GetValue(status);
                names=string.IsNullOrEmpty(name)?null:new[]{name};
            }
            else if(status.GetType()==typeof(StatusInstance_CustomAmp))
            {
                string name=(string)AccessTools.Field(typeof(StatusInstance_CustomAmp),"customID").GetValue(status);
                names=string.IsNullOrEmpty(name)?null:new[]{DamageStatPreview.AmpPrefix+name};
            }
            else names=BuffStatusMappings.Resolve(status.GetType().Name);
            if(names!=null)foreach(string name in names)Add(result,name,status.Value);
        }

        internal static List<ItemSource> Equipped(PlayerAvatar player)
        {
            var result=new List<ItemSource>();
            var controller=player?player.GetComponent<WeaponControllerSimple>():null;
            if(controller&&controller.currentWeapon)
            {
                var weapon=controller.currentWeapon;var entity=WeaponDatabase.FindWeaponById(weapon.entityId);
                result.Add(new ItemSource{Name=entity?entity.Name:WeaponDatabase.GetWeaponTypeName(weapon.weaponType),Weapon=weapon});
            }
            if(player&&player.Inventory)
            {
                var seen=new HashSet<int>();
                foreach(var live in player.Inventory.charms.Values)
                {
                    if(!live||!live.IsEffectEnabled||!seen.Add(live.GetInstanceID())||live.Item==null)continue;
                    result.Add(new ItemSource{Name=live.Item.Name,Entity=live.Item.Entity,Live=live,Level=live.DisplayedLevel});
                }
            }
            return result;
        }
        internal static DamageTooltip.Snapshot Capture(ItemSource item,PlayerAvatar player,IDictionary<string,int> changes=null,Charm_Basic preview=null,int previewLevel=0)
        {
            using(var stats=changes==null?null:new DamageStatPreview(player,changes))
            using(Push(preview,previewLevel))
                return DamageTooltip.Capture(delegate
                {
                    return item.Weapon?DpsCapture.Weapon(item.Weapon,player):DpsCapture.Artifact(item.Entity,item.Live,item.Level,player);
                },player);
        }
        internal static DamageTooltip.Snapshot CaptureArtifact(ItemEntity entity,int level,PlayerAvatar player,IDictionary<string,int> changes)
        {
            var source=entity.resourcePrefab.GetComponent<Charm_Basic>();
            return Capture(new ItemSource{Name=entity.Name,Entity=entity,Level=level},player,changes,source,level);
        }
        internal static DamageTooltip.Snapshot CaptureCombo(ComboEffectBase effect,int count,PlayerAvatar player,IDictionary<string,int> changes=null,Charm_Basic preview=null,int level=0)
        {
            using(var stats=changes==null?null:new DamageStatPreview(player,changes))
            using(Push(preview,level))
                return DamageTooltip.Capture(delegate{return DpsCapture.Combo(effect,count,player);},player);
        }
        internal static double Total(DamageTooltip.Snapshot snapshot)
        {
            return snapshot!=null&&snapshot.Dps!=null?snapshot.Dps.NormalTotal(snapshot):0;
        }
    }

    [HarmonyPatch(typeof(UI_SephiriteRewardElement),"Initialize")]
    internal static class RewardDpsBadgePatch
    {
        private static void Postfix(UI_SephiriteRewardElement __instance)
        {
            try{RewardDpsBadge.Attach(__instance);}
            catch(Exception e){Debug.LogWarning("Reward DPS badge: "+e.Message);}
        }
    }

    public sealed class RewardDpsBadge:MonoBehaviour
    {
        private UI_SephiriteRewardElement reward;
        private GameObject badge,recommendation;
        private TMP_Text label;
        private static Sprite recommendationSprite;
        public bool Eligible {get;private set;}
        internal double LevelZeroPercent,MaxPercent;
        internal bool NewAttack;
        internal UI_SephiriteRewardElement Reward{get{return reward;}}
        internal static void Attach(UI_SephiriteRewardElement source)
        {
            var view=source.GetComponent<RewardDpsBadge>()??source.gameObject.AddComponent<RewardDpsBadge>();
            view.reward=source;view.Ensure();
            var panel=source.GetComponentInParent<UI_SephiriteRewardPanel>();
            if(panel)(panel.GetComponent<RewardDpsPanel>()??panel.gameObject.AddComponent<RewardDpsPanel>()).Register(view);
        }
        private void Ensure()
        {
            if(badge)return;
            badge=new GameObject("DpsIncreaseBadge",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            badge.transform.SetParent(transform,false);
            var rect=(RectTransform)badge.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(1,0);
            rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(0,.5f);
            rect.anchoredPosition=new Vector2(3,0);rect.sizeDelta=new Vector2(30,20);
            var bg=badge.GetComponent<Image>();bg.raycastTarget=false;
            if(reward.bgImage)
            {
                bg.sprite=reward.bgImage.sprite;bg.type=reward.bgImage.type;bg.material=reward.bgImage.material;
                bg.pixelsPerUnitMultiplier=reward.bgImage.pixelsPerUnitMultiplier;bg.color=Color.white;
            }
            else bg.color=new Color32(21,18,31,225);
            var textObject=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));textObject.transform.SetParent(badge.transform,false);
            label=textObject.GetComponent<TextMeshProUGUI>();
            var panel=reward.GetComponentInParent<UI_SephiriteRewardPanel>();
            if(panel&&panel.rerollGuideText){label.font=panel.rerollGuideText.font;label.fontSharedMaterial=panel.rerollGuideText.fontSharedMaterial;}
            label.fontSize=6.5f;label.enableAutoSizing=true;label.fontSizeMin=5;label.fontSizeMax=6.5f;label.alignment=TextAlignmentOptions.Center;
            label.color=new Color32(216,255,236,255);label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            var textRect=(RectTransform)textObject.transform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(2,1);textRect.offsetMax=new Vector2(-2,-1);
            recommendation=new GameObject("DpsRecommendation",typeof(RectTransform));
            recommendation.transform.SetParent(transform,false);
            var recommendRect=(RectTransform)recommendation.transform;recommendRect.anchorMin=recommendRect.anchorMax=Vector2.zero;
            recommendRect.pivot=Vector2.zero;recommendRect.anchoredPosition=Vector2.one;recommendRect.sizeDelta=new Vector2(6,6);
            var icon=recommendation.AddComponent<CanvasRenderer>();
            var image=recommendation.AddComponent<Image>();image.sprite=RecommendationSprite();image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;
            recommendation.SetActive(false);
        }
        private static Sprite RecommendationSprite()
        {
            if(recommendationSprite)return recommendationSprite;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SephiriaPersonalConvenience.Assets.thumbs-up.png"))
            {
                if(stream==null)return null;
                var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);
                var texture=new Texture2D(2,2,TextureFormat.ARGB32,false);texture.name="DpsRecommendationThumb";
                if(!texture.LoadImage(bytes)){Destroy(texture);return null;}
                texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
                recommendationSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),32);
                recommendationSprite.name="DpsRecommendationThumb";
                return recommendationSprite;
            }
        }
        internal void Loading()
        {
            Ensure();Eligible=true;badge.SetActive(true);recommendation.SetActive(false);label.text="…";
        }
        internal void Hide(){Ensure();Eligible=false;badge.SetActive(false);recommendation.SetActive(false);}
        internal void SetResult(double zero,double max,bool newAttack)
        {
            LevelZeroPercent=zero;MaxPercent=max;NewAttack=newAttack;Eligible=true;Ensure();badge.SetActive(true);
            label.text=newAttack?"신규\n신규":Percent(zero)+"\n"+Percent(max);
        }
        private static string Percent(double value)
        {
            int shown=(int)Math.Truncate(value);
            return (shown>=0?"+":"")+shown+"%";
        }
        internal void Recommend(bool value)
        {
            Ensure();recommendation.SetActive(value&&Eligible);
        }
    }

    public sealed class RewardDpsPanel:MonoBehaviour
    {
        private UI_SephiriteRewardPanel panel;
        private readonly List<RewardDpsBadge> badges=new List<RewardDpsBadge>();
        private Coroutine work;
        private int generation;
        private float refreshAt;
        private void Awake(){panel=GetComponent<UI_SephiriteRewardPanel>();}
        internal void Register(RewardDpsBadge badge)
        {
            if(!badges.Contains(badge))badges.Add(badge);
            refreshAt=Time.unscaledTime+.05f;
        }
        private void OnDisable(){generation++;if(work!=null)StopCoroutine(work);work=null;}
        private void LateUpdate()
        {
            if(!panel||!panel.IsOpened||Time.unscaledTime<refreshAt)return;
            refreshAt=float.PositiveInfinity;generation++;
            if(work!=null)StopCoroutine(work);
            work=StartCoroutine(Calculate(generation));
        }
        private IEnumerator Calculate(int token)
        {
            badges.RemoveAll(x=>!x);
            var active=badges.Where(x=>x&&x.gameObject.activeInHierarchy&&x.Reward!=null).ToList();
            if(PersonalConveniencePlugin.Instance==null||!PersonalConveniencePlugin.Instance.ShowRewardDpsPrediction.Value)
            {foreach(var badge in active)badge.Hide();work=null;yield break;}
            foreach(var badge in active)badge.Loading();
            yield return null;
            var player=(PlayerAvatar)AccessTools.Field(typeof(UI_SephiriteRewardPanel),"openedAvatar").GetValue(panel);
            if(!player)player=SephiriaPersonalConvenience.FruitCombos.LocalPlayer;
            if(!player)yield break;
            RewardDpsPrediction.ItemSource primary=null;double baseline=0;
            foreach(var item in RewardDpsPrediction.Equipped(player))
            {
                double value=0;
                try{value=RewardDpsPrediction.Total(RewardDpsPrediction.Capture(item,player));}
                catch(Exception e){Debug.LogWarning("Reward DPS baseline: "+e.Message);}
                if(value>baseline){baseline=value;primary=item;}
                yield return null;
            }
            foreach(var badge in active)
            {
                if(token!=generation)yield break;
                var entity=ItemDatabase.FindItemById(badge.Reward.reward.entityID);
                if(!entity||entity.type!=EItemType.Charm||!entity.resourcePrefab)
                {badge.Hide();continue;}
                var source=entity.resourcePrefab.GetComponent<Charm_Basic>();
                if(!source){badge.SetResult(0,0,baseline<=0);continue;}
                double zero=Predict(primary,baseline,entity,source,0,player);
                yield return null;
                double maximum=Predict(primary,baseline,entity,source,source.maxLevel,player);
                badge.SetResult(zero,maximum,baseline<=0);
                yield return null;
            }
            if(token!=generation)yield break;
            RewardDpsBadge best=null;
            foreach(var badge in active)
            {
                badge.Recommend(false);
                if(!badge.Eligible)continue;
                if(best==null||badge.LevelZeroPercent>best.LevelZeroPercent||
                    (badge.LevelZeroPercent==best.LevelZeroPercent&&badge.MaxPercent>best.MaxPercent))best=badge;
            }
            if(best)best.Recommend(true);
            work=null;
        }
        private static double Predict(RewardDpsPrediction.ItemSource primary,double baseline,ItemEntity entity,Charm_Basic source,int level,PlayerAvatar player)
        {
            List<RewardDpsPrediction.ComboChange> combos;
            var changes=RewardDpsPrediction.Changes(entity,level,player,out combos);
            double total=0,primaryAfter=0,candidateOwn=0,comboDelta=0,localBaseline=baseline;
            try
            {
                if(primary!=null)localBaseline=RewardDpsPrediction.Total(RewardDpsPrediction.Capture(primary,player));
                if(primary!=null)primaryAfter=RewardDpsPrediction.Total(RewardDpsPrediction.Capture(primary,player,changes,source,level));
                candidateOwn=RewardDpsPrediction.Total(RewardDpsPrediction.CaptureArtifact(entity,level,player,changes));
                total=primaryAfter+candidateOwn;
                foreach(var combo in combos)
                {
                    double before=combo.Before>0?RewardDpsPrediction.Total(RewardDpsPrediction.CaptureCombo(combo.Effect,combo.Before,player)):0;
                    double after=RewardDpsPrediction.Total(RewardDpsPrediction.CaptureCombo(combo.Effect,combo.After,player,changes,source,level));
                    comboDelta+=after-before;
                }
                total+=comboDelta;
            }
            catch(Exception e){Debug.LogWarning("Reward DPS prediction "+entity.name+": "+e.Message);return 0;}
            if(localBaseline<=0)return total>0?double.PositiveInfinity:0;
            double percent=(total-localBaseline)*100d/localBaseline;
            return percent;
        }
    }
}
