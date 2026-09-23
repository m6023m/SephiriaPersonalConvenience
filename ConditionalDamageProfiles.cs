using System;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class ConditionalDamageProfiles
    {
        private static readonly System.Reflection.FieldInfo BeforeAttack=AccessTools.Field(typeof(UnitAvatar),"OnAttackUnitBeforeOperation");
        private static int PreviewIndex(Charm_Basic charm){return charm.LevelToIdx(RewardDpsPrediction.PreviewLevel);}
        internal static float[] CaptureCriticalChance(UnitAvatar owner,System.Reflection.FieldInfo field,out float direct)
        {
            direct=0;
            if(!owner||owner.IsDead||field==null)return null;
            var handlers=field.GetValue(owner) as Delegate;
            float[] values=null;
            foreach(var handler in handlers==null?new Delegate[0]:handlers.GetInvocationList())
            {
                var basic=handler.Target as Charm_IncreaseCriticalChance_NormalAttack;
                if(basic){direct+=basic.criticalBonusPercentByLevel.SafeRandomAccess(basic.CurrentLevelToIdx());continue;}
                var egg=handler.Target as Charm_FrozenEgg;var horn=handler.Target as Charm_KirinHorn;
                if(!egg&&!horn)continue;
                if(values==null)values=new float[Enum.GetValues(typeof(EDamageElementalType)).Length];
                float bonus=egg?egg.criticalChanceByLevel.SafeRandomAccess(egg.CurrentLevelToIdx()):horn.addCriticalByLevel.SafeRandomAccess(horn.CurrentLevelToIdx());
                var element=egg?EDamageElementalType.Ice:EDamageElementalType.Lightning;
                for(int i=0;i<values.Length;i++)if(DamageInstance.IsSameElementalType((EDamageElementalType)i,element))values[i]+=bonus;
            }
            var preview=RewardDpsPrediction.PreviewCharm;
            var basicPreview=preview as Charm_IncreaseCriticalChance_NormalAttack;
            if(basicPreview)direct+=basicPreview.criticalBonusPercentByLevel.SafeRandomAccess(PreviewIndex(basicPreview));
            var eggPreview=preview as Charm_FrozenEgg;var hornPreview=preview as Charm_KirinHorn;
            if(eggPreview||hornPreview)
            {
                if(values==null)values=new float[Enum.GetValues(typeof(EDamageElementalType)).Length];
                float bonus=eggPreview?eggPreview.criticalChanceByLevel.SafeRandomAccess(PreviewIndex(eggPreview)):hornPreview.addCriticalByLevel.SafeRandomAccess(PreviewIndex(hornPreview));
                var element=eggPreview?EDamageElementalType.Ice:EDamageElementalType.Lightning;
                for(int i=0;i<values.Length;i++)if(DamageInstance.IsSameElementalType((EDamageElementalType)i,element))values[i]+=bonus;
            }
            return values;
        }
        internal static DamageTooltip.Snapshot.RawStep[] CaptureFrostbiteSteps(UnitAvatar owner,System.Reflection.FieldInfo field)
        {
            if(!owner||owner.IsDead||field==null)return null;
            var callbacks=field.GetValue(owner) as Delegate;
            var steps=new System.Collections.Generic.List<DamageTooltip.Snapshot.RawStep>();
            foreach(var callback in callbacks==null?new Delegate[0]:callbacks.GetInvocationList())
            {
                var glove=callback.Target as Charm_WarmGlove;
                if(!glove||!glove.IsEffectEnabled)continue;
                var elements=new bool[Enum.GetValues(typeof(EDamageElementalType)).Length];
                for(int i=0;i<elements.Length;i++)elements[i]=DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice);
                steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=4,Percent=glove.damageBonusByLevel.SafeRandomAccess(glove.CurrentLevelToIdx()),Elements=elements});
            }
            var preview=RewardDpsPrediction.PreviewCharm as Charm_WarmGlove;
            if(preview)
            {
                var elements=new bool[Enum.GetValues(typeof(EDamageElementalType)).Length];
                for(int i=0;i<elements.Length;i++)elements[i]=DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice);
                steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=4,Percent=preview.damageBonusByLevel.SafeRandomAccess(PreviewIndex(preview)),Elements=elements});
            }
            return steps.ToArray();
        }
        internal static float[] CaptureFrostbite(UnitAvatar owner,System.Reflection.FieldInfo field)
        {
            if(!owner||owner.IsDead||field==null)return null;
            var callbacks=field.GetValue(owner) as Delegate;
            float[] values=null;
            foreach(var callback in callbacks==null?new Delegate[0]:callbacks.GetInvocationList())
            {
                var glove=callback.Target as Charm_WarmGlove;
                if(!glove||!glove.IsEffectEnabled)continue;
                if(values==null)
                {
                    values=new float[Enum.GetValues(typeof(EDamageElementalType)).Length];
                    for(int i=0;i<values.Length;i++)values[i]=1;
                }
                float factor=1+glove.damageBonusByLevel.SafeRandomAccess(glove.CurrentLevelToIdx())/100f;
                for(int i=0;i<values.Length;i++)
                    if(DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice))values[i]*=factor;
            }
            var preview=RewardDpsPrediction.PreviewCharm as Charm_WarmGlove;
            if(preview)
            {
                if(values==null){values=new float[Enum.GetValues(typeof(EDamageElementalType)).Length];for(int i=0;i<values.Length;i++)values[i]=1;}
                float factor=1+preview.damageBonusByLevel.SafeRandomAccess(PreviewIndex(preview))/100f;
                for(int i=0;i<values.Length;i++)if(DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice))values[i]*=factor;
            }
            return values;
        }
        internal static float FrostbiteFactor(float[] values,EDamageElementalType element)
        {
            int index=(int)element;
            return values!=null&&index>=0&&index<values.Length?values[index]:1;
        }
        internal static void Capture(PlayerAvatar player,DamageTooltip.Snapshot snapshot)
        {
            float directChance;
            snapshot.ElementCriticalChance=CaptureCriticalChance(player,BeforeAttack,out directChance);
            snapshot.WeaponCriticalChance+=directChance;
            snapshot.Frostbite=CaptureFrostbite(player,BeforeAttack);
            var handlers=BeforeAttack==null?null:BeforeAttack.GetValue(player) as Delegate;
            if(player.IsDead)return;
            var steps=new System.Collections.Generic.List<DamageTooltip.Snapshot.RawStep>();
            // Inspect registered effects only; inventory presence alone is not activation.
            foreach(var handler in handlers==null?new Delegate[0]:handlers.GetInvocationList())
            {
                var bat=handler.Target as Charm_PointedBat;
                if(bat)
                {
                    steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=0,Percent=bat.damageDecreaseRatio,Chance=bat.chance});
                    float factor=1-bat.damageDecreaseRatio/100f;
                    if(bat.chance>=100)snapshot.AlwaysDamageFactor*=factor;
                    else if(bat.chance>0)
                    {
                        snapshot.RandomDamageMinimum*=Math.Min(1,factor);
                        snapshot.RandomDamageMaximum*=Math.Max(1,factor);
                    }
                    continue;
                }
                var elemental=handler.Target as Charm_Burn;
                if(elemental)
                {
                    if(snapshot.ElementCritical==null)snapshot.ElementCritical=new int[Enum.GetValues(typeof(EDamageElementalType)).Length];
                    int amount=elemental.addCriticalDamageByLevel.SafeRandomAccess(elemental.CurrentLevelToIdx());
                    for(int i=0;i<snapshot.ElementCritical.Length;i++)
                        if(DamageInstance.IsSameElementalType((EDamageElementalType)i,elemental.targetElementalType))snapshot.ElementCritical[i]+=amount;
                    continue;
                }
                var debuffs=handler.Target as Charm_DebuffDamage;
                if(debuffs)
                {
                    steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=3,Percent=debuffs.additionalDamage.SafeRandomAccess(debuffs.CurrentLevelToIdx())});
                    snapshot.OneDebuff*=1+debuffs.additionalDamage.SafeRandomAccess(debuffs.CurrentLevelToIdx())/100f;
                    continue;
                }
                var close=handler.Target as Charm_TooCloseDamage;
                if(close)
                {
                    steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=1,Percent=close.additionalDamagePercentByLevel.SafeRandomAccess(close.CurrentLevelToIdx())});
                    snapshot.Close*=1+close.additionalDamagePercentByLevel.SafeRandomAccess(close.CurrentLevelToIdx())/100f;
                    continue;
                }
                var first=handler.Target as Charm_FirstAttackBonusDamage;
                if(first)
                {
                    steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=2,Percent=first.damageBonusByLevel.SafeRandomAccess(first.CurrentLevelToIdx())});
                    snapshot.First*=1+first.damageBonusByLevel.SafeRandomAccess(first.CurrentLevelToIdx())/100f;
                    continue;
                }
                var glove=handler.Target as Charm_WarmGlove;
                if(glove&&glove.IsEffectEnabled)
                {
                    var elements=new bool[Enum.GetValues(typeof(EDamageElementalType)).Length];
                    for(int i=0;i<elements.Length;i++)elements[i]=DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice);
                    steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=4,Percent=glove.damageBonusByLevel.SafeRandomAccess(glove.CurrentLevelToIdx()),Elements=elements});
                }
                var burn=handler.Target as Charm_BurnTargetDamageBonus;
                if(burn&&burn.IsEffectEnabled)snapshot.Burn+=burn.damageBonusByLevel.SafeRandomAccess(burn.CurrentLevelToIdx())/100f;
            }
            ApplyPreview(snapshot,steps,RewardDpsPrediction.PreviewCharm);
            snapshot.RawSteps=steps.ToArray();
        }
        private static void ApplyPreview(DamageTooltip.Snapshot snapshot,System.Collections.Generic.List<DamageTooltip.Snapshot.RawStep> steps,Charm_Basic preview)
        {
            if(!preview)return;
            var bat=preview as Charm_PointedBat;
            if(bat)
            {
                steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=0,Percent=bat.damageDecreaseRatio,Chance=bat.chance});
                float factor=1-bat.damageDecreaseRatio/100f;
                if(bat.chance>=100)snapshot.AlwaysDamageFactor*=factor;
                else if(bat.chance>0){snapshot.RandomDamageMinimum*=Math.Min(1,factor);snapshot.RandomDamageMaximum*=Math.Max(1,factor);}
            }
            var elemental=preview as Charm_Burn;
            if(elemental)
            {
                if(snapshot.ElementCritical==null)snapshot.ElementCritical=new int[Enum.GetValues(typeof(EDamageElementalType)).Length];
                int amount=elemental.addCriticalDamageByLevel.SafeRandomAccess(PreviewIndex(elemental));
                for(int i=0;i<snapshot.ElementCritical.Length;i++)if(DamageInstance.IsSameElementalType((EDamageElementalType)i,elemental.targetElementalType))snapshot.ElementCritical[i]+=amount;
            }
            var debuffs=preview as Charm_DebuffDamage;
            if(debuffs){float amount=debuffs.additionalDamage.SafeRandomAccess(PreviewIndex(debuffs));steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=3,Percent=amount});snapshot.OneDebuff*=1+amount/100f;}
            var close=preview as Charm_TooCloseDamage;
            if(close){float amount=close.additionalDamagePercentByLevel.SafeRandomAccess(PreviewIndex(close));steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=1,Percent=amount});snapshot.Close*=1+amount/100f;}
            var first=preview as Charm_FirstAttackBonusDamage;
            if(first){float amount=first.damageBonusByLevel.SafeRandomAccess(PreviewIndex(first));steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=2,Percent=amount});snapshot.First*=1+amount/100f;}
            var burn=preview as Charm_BurnTargetDamageBonus;
            if(burn)snapshot.Burn+=burn.damageBonusByLevel.SafeRandomAccess(PreviewIndex(burn))/100f;
        }
    }
    [HarmonyPatch]
    internal static class TooltipCharmEffectChangedPatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_Basic),"EnableEffect");
            yield return AccessTools.Method(typeof(Charm_Basic),"DisableEffect");
        }
        private static void Prefix(Charm_Basic __instance,out int __state)
        {
            __state=__instance.IsEffectEnabled?__instance.CurrentLevelToIdx()+1:-1;
        }
        private static void Postfix(Charm_Basic __instance,int __state)
        {
            int current=__instance.IsEffectEnabled?__instance.CurrentLevelToIdx()+1:-1;
            if(current!=__state&&DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);
        }
    }
}
