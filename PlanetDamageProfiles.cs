using System.Text;

namespace SephiriaDicePreview
{
    internal static class PlanetDamageProfiles
    {
        internal static bool TryDescribe(Charm_SummonGreenBat source,Charm_SummonGreenBat live,ItemEntity entity,int level,PlayerAvatar p,out string text)
        {
            text=null;
            var bat=source.greenbatPrefab?source.greenbatPrefab.GetComponent<GreenBat>():null;
            if(!bat)return false;
            float raw=source.damageByLevel.SafeRandomAccess(source.LevelToIdx(level));
            int ticks=0;float ignite=0;
            if(p.GetCustomStatUnsafe("PLANETIGNITEDAMAGE")>0)
            {
                ticks=entity&&(entity.rarity==EItemRarity.Legend||entity.rarity==EItemRarity.Eternal)?4:entity&&entity.rarity==EItemRarity.Rare?3:entity&&entity.rarity==EItemRarity.Uncommon?2:1;
                EDamageElementalType element;
                ignite=p.GetCustomStatUnsafe("PLASMAACTIVE")>0?CharacterDebuff_Plasma.CalculateTickDamage(p,out element):CharacterDebuff_Burn.CalculateTickDamage(p,out element);
            }
            float root=1+(live&&live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f;
            float planet=1+p.GetCustomStatUnsafe("PLANETDAMAGE")/100f;
            bool enhanced=live?live.IsEnhanced:source.IsEnhanced;
            var b=new StringBuilder("행성 탄환 1발 · 일반 / 치명타\n");
            b.Append("현재 상태: ").AppendLine(enhanced?"강화":"일반");
            b.AppendLine(ProjectileDamageProfiles.Describe(p,Hit(raw,ignite,ticks,root,planet,false,bat.elementalType),CapturePrefab(bat,false,ticks>0),"일반 상태"));
            b.AppendLine(ProjectileDamageProfiles.Describe(p,Hit(raw,ignite,ticks,root,planet,true,bat.elementalType),CapturePrefab(bat,true,ticks>0),"강화 상태"));
            if(ticks>0)b.Append("점화 연계: ").Append(p.GetCustomStatUnsafe("PLASMAACTIVE")>0?"플라즈마":"화상").Append(" ").Append(ticks).AppendLine("틱 상당의 피해를 기본 피해에 가산");
            var red=source as Charm_SummonRedPlanet;
            int count=red?red.countByLevel.SafeRandomAccess(source.LevelToIdx(level)):bat.fireCount;
            b.Append("발사 1회당 ").Append(count).AppendLine("발 · 각각 실제 적중한 횟수만 합산");
            b.Append("행성 피해 보너스·강화 1.5배 반영 · 적 방어 적용 전\n점화 궤적과 적중 후 상태이상 피해는 별도");
            if(live&&!live.IsEffectEnabled)b.Append("\n현재 발동 조건 미충족");
            text=b.ToString();return true;
        }
        internal static DamageTooltip.Hit CaptureHit(Charm_SummonGreenBat source,Charm_SummonGreenBat live,ItemEntity entity,int level,PlayerAvatar p,GreenBat bat)
        {
            float raw=source.damageByLevel.SafeRandomAccess(source.LevelToIdx(level));
            int ticks=0;float ignite=0;
            if(p.GetCustomStatUnsafe("PLANETIGNITEDAMAGE")>0)
            {
                ticks=entity&&(entity.rarity==EItemRarity.Legend||entity.rarity==EItemRarity.Eternal)?4:entity&&entity.rarity==EItemRarity.Rare?3:entity&&entity.rarity==EItemRarity.Uncommon?2:1;
                EDamageElementalType ignored;
                ignite=p.GetCustomStatUnsafe("PLASMAACTIVE")>0?CharacterDebuff_Plasma.CalculateTickDamage(p,out ignored):CharacterDebuff_Burn.CalculateTickDamage(p,out ignored);
            }
            return Hit(source.damageByLevel.SafeRandomAccess(source.LevelToIdx(level)),ignite,ticks,
                ArtifactDamageProfiles.RootBonus(live,p),1+p.GetCustomStatUnsafe("PLANETDAMAGE")/100f,
                live?live.IsEnhanced:source.IsEnhanced,bat.elementalType);
        }
        private static DamageTooltip.Hit Hit(float raw,float ignite,int ticks,float root,float planet,bool enhanced,EDamageElementalType element)
        {
            return new DamageTooltip.Hit{Raw=raw,Element=element,ResourceAmount=ticks,ResourcePerUnit=ignite,Factors=new[]{root,planet,enhanced?1.5f:1f}};
        }
        internal static UnityEngine.GameObject CapturePrefab(GreenBat bat,bool enhanced,bool ignite)
        {
            if(enhanced)return ignite&&bat.enhancedBulletWithIgniteTailPrafab?bat.enhancedBulletWithIgniteTailPrafab:bat.enhancedBulletPrafab;
            return ignite&&bat.bulletWithIgniteTailPrafab?bat.bulletWithIgniteTailPrafab:bat.bulletPrafab;
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(Charm_SummonGreenBat),"SetEnhancement")]
    internal static class TooltipPlanetEnhancementChangedPatch
    {
        private static void Prefix(Charm_SummonGreenBat __instance,out bool __state){__state=__instance.IsEnhanced;}
        private static void Postfix(Charm_SummonGreenBat __instance,bool __state)
        {
            if(__instance.IsEnhanced!=__state&&DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);
        }
    }
}
