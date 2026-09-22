using System;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class ConditionalDamageProfiles
    {
        private static readonly System.Reflection.FieldInfo BeforeAttack=AccessTools.Field(typeof(UnitAvatar),"OnAttackUnitBeforeOperation");
        internal static DamageTooltip.Snapshot.RawStep[] CaptureFrostbiteSteps(UnitAvatar owner,System.Reflection.FieldInfo field)
        {
            if(!owner||owner.IsDead||field==null)return null;
            var callbacks=field.GetValue(owner) as Delegate;
            if(callbacks==null)return null;
            var steps=new System.Collections.Generic.List<DamageTooltip.Snapshot.RawStep>();
            foreach(var callback in callbacks.GetInvocationList())
            {
                var glove=callback.Target as Charm_WarmGlove;
                if(!glove||!glove.IsEffectEnabled)continue;
                var elements=new bool[Enum.GetValues(typeof(EDamageElementalType)).Length];
                for(int i=0;i<elements.Length;i++)elements[i]=DamageInstance.IsSameElementalType((EDamageElementalType)i,EDamageElementalType.Ice);
                steps.Add(new DamageTooltip.Snapshot.RawStep{Kind=4,Percent=glove.damageBonusByLevel.SafeRandomAccess(glove.CurrentLevelToIdx()),Elements=elements});
            }
            return steps.ToArray();
        }
        internal static float[] CaptureFrostbite(UnitAvatar owner,System.Reflection.FieldInfo field)
        {
            if(!owner||owner.IsDead||field==null)return null;
            var callbacks=field.GetValue(owner) as Delegate;
            if(callbacks==null)return null;
            float[] values=null;
            foreach(var callback in callbacks.GetInvocationList())
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
            return values;
        }
        internal static float FrostbiteFactor(float[] values,EDamageElementalType element)
        {
            int index=(int)element;
            return values!=null&&index>=0&&index<values.Length?values[index]:1;
        }
        internal static void Capture(PlayerAvatar player,DamageTooltip.Snapshot snapshot)
        {
            snapshot.Frostbite=CaptureFrostbite(player,BeforeAttack);
            var handlers=BeforeAttack==null?null:BeforeAttack.GetValue(player) as Delegate;
            if(handlers==null||player.IsDead)return;
            var steps=new System.Collections.Generic.List<DamageTooltip.Snapshot.RawStep>();
            // Inspect registered effects only; inventory presence alone is not activation.
            foreach(var handler in handlers.GetInvocationList())
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
            snapshot.RawSteps=steps.ToArray();
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
