using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SephiriaDicePreview
{
    [HarmonyPatch]
    internal static class TooltipBuffLifecycleChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // A capped AddStack can renew duration without changing any custom stat.
            // Recovery/marker buffs can also change the preview without a stat event.
            foreach(string name in new[]{"Initialize","AddStack","Amplify","RequestEnd","Destroy","ApplyStatus","RemoveStatus"})
                yield return AccessTools.Method(typeof(CharacterBuff),name);
        }
        private static void Postfix(CharacterBuff __instance)
        {
            if(__instance.Target)DamageTooltip.Invalidate(__instance.Target);
        }
    }

    [HarmonyPatch]
    internal static class TooltipGreatSwordTransformationChangedPatch
    {
        private struct State
        {
            internal bool Transformed;
            internal float Timer;
        }
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_GreatSword),"NetworkisTransformed");
            yield return AccessTools.Method(typeof(WeaponSimple_GreatSword),"DeserializeSyncVars");
            // Also catch a timer reset while the transformed flag stays true.
            yield return AccessTools.Method(typeof(WeaponSimple_GreatSword),"SubAttackButtonUp");
        }
        private static void Prefix(WeaponSimple_GreatSword __instance,out State __state)
        {__state=new State{Transformed=__instance.isTransformed,Timer=__instance.transformResetTimer.GetTimer()};}
        private static void Postfix(WeaponSimple_GreatSword __instance,State __state)
        {
            if((__state.Transformed!=__instance.isTransformed||__state.Timer!=__instance.transformResetTimer.GetTimer())&&__instance.Networkowner)
                DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch(typeof(AutoMagicCaster),"Initialize")]
    internal static class TooltipAutoMagicLinkChangedPatch
    {
        private static void Postfix(AutoMagicCaster __instance)
        {
            if(__instance.owner)DamageTooltip.Invalidate(__instance.owner);
        }
    }
    // Event-driven invalidation: no inventory traversal or damage calculation in Update.
    [HarmonyPatch]
    internal static class TooltipDarkCloudAmountChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(ComboEffect_DarkCloud),"NetworkdarkCloud");
            yield return AccessTools.Method(typeof(ComboEffect_DarkCloud),"DeserializeSyncVars");
        }
        private static void Prefix(ComboEffect_DarkCloud __instance,out int __state){__state=__instance.darkCloud;}
        private static void Postfix(ComboEffect_DarkCloud __instance,int __state)
        {
            if(__state!=__instance.darkCloud)DamageTooltip.Invalidate(__instance.Networkavatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipGrowthProgressChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_GrowthStatusInstance),"AddGrowthQuestProgress");
            yield return AccessTools.Method(typeof(Charm_GrowthStatusInstance),"LoadItemOnServer");
        }
        private static void Prefix(int ___questCounter,out int __state){__state=___questCounter;}
        private static void Postfix(Charm_GrowthStatusInstance __instance,int ___questCounter,int __state)
        {
            if(__state!=___questCounter)DamageTooltip.Invalidate(__instance.NetworkAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipCrossbowContinueChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WeaponSimple_Crossbow),"Update");
            yield return AccessTools.Method(typeof(WeaponSimple_Crossbow),"OnSpecialAttackCreated");
        }
        private static void Prefix(WeaponSimple_Crossbow __instance,out int __state){__state=__instance.continueBonusCount;}
        private static void Postfix(WeaponSimple_Crossbow __instance,int __state)
        {
            if(__state!=__instance.continueBonusCount&&__instance.Networkowner)DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipStaffStackChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_QuartterStaff),"NetworklastDashCount");
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_QuartterStaff),"NetworkcanCrystalExplosion");
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_QuartterStaff),"NetworkisNormalAttackRollingTurnedOn");
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_QuartterStaff),"NetworknextBasicAttackIsSummon");
            yield return AccessTools.Method(typeof(WeaponSimple_QuartterStaff),"DeserializeSyncVars");
        }
        private static int State(WeaponSimple_QuartterStaff weapon){return ((int)weapon.lastDashCount<<3)|(weapon.canCrystalExplosion?1:0)|(weapon.isNormalAttackRollingTurnedOn?2:0)|(weapon.nextBasicAttackIsSummon?4:0);}
        private static void Prefix(WeaponSimple_QuartterStaff __instance,out int __state){__state=State(__instance);}
        private static void Postfix(WeaponSimple_QuartterStaff __instance,int __state)
        {
            if(__state!=State(__instance)&&__instance.Networkowner)DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipCloudSlashChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"NetworkresidualLightningStack","NetworkisCloudSlashAttack","NetworkcloudSlashStage","NetworkcloudSlashEx","NetworkcloudSlashUsedStacks","NetworkisEnhancedDashAttackActive","NetworkenhancedDashAttackElementalType"})
                yield return AccessTools.PropertySetter(typeof(WeaponSimple_Katana),name);
            yield return AccessTools.Method(typeof(WeaponSimple_Katana),"DeserializeSyncVars");
        }
        private struct State
        {
            internal int Residual,Used,Stage;
            internal bool Active,Enhanced,DashReady;
            internal EDamageElementalType DashElement;
        }
        private static void Prefix(WeaponSimple_Katana __instance,out State __state)
        {__state=new State{Residual=__instance.residualLightningStack,Used=__instance.cloudSlashUsedStacks,Stage=__instance.cloudSlashStage,Active=__instance.isCloudSlashAttack,Enhanced=__instance.cloudSlashEx,DashReady=__instance.isEnhancedDashAttackActive,DashElement=__instance.enhancedDashAttackElementalType};}
        private static void Postfix(WeaponSimple_Katana __instance,State __state)
        {
            if((__state.Residual!=__instance.residualLightningStack||__state.Used!=__instance.cloudSlashUsedStacks||__state.Stage!=__instance.cloudSlashStage||__state.Active!=__instance.isCloudSlashAttack||__state.Enhanced!=__instance.cloudSlashEx||__state.DashReady!=__instance.isEnhancedDashAttackActive||__state.DashElement!=__instance.enhancedDashAttackElementalType)&&__instance.Networkowner)
                DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipShieldSweepChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_SwordAndShield),"NetworkchargedSweep_New_Stack");
            yield return AccessTools.Method(typeof(WeaponSimple_SwordAndShield),"UserCode_RpcSetGuardSweep__Boolean");
            yield return AccessTools.Method(typeof(WeaponSimple_SwordAndShield),"DeserializeSyncVars");
        }
        private static void Prefix(WeaponSimple_SwordAndShield __instance,bool ____guardSweepEnabled,out long __state)
        {__state=((long)__instance.chargedSweep_New_Stack<<1)|(____guardSweepEnabled?1L:0L);}
        private static void Postfix(WeaponSimple_SwordAndShield __instance,bool ____guardSweepEnabled,long __state)
        {
            long state=((long)__instance.chargedSweep_New_Stack<<1)|(____guardSweepEnabled?1L:0L);
            if(__state!=state&&__instance.Networkowner)DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipKatanaBasicStateChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_Katana),"NetworkisEclipseBuffActivated");
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_Katana),"NetworkisEclipseAttackActivated");
            yield return AccessTools.Method(typeof(WeaponSimple_Katana),"OnServerReceivedMessage");
            yield return AccessTools.Method(typeof(WeaponSimple_Katana),"BasicAttackCreated");
            yield return AccessTools.Method(typeof(WeaponSimple_Katana),"DeserializeSyncVars");
        }
        private static int State(WeaponSimple_Katana weapon,bool stuck)
        {return (stuck?1:0)|(weapon.isEclipseBuffActivated?2:0)|(weapon.isEclipseAttackActivated?4:0);}
        private static void Prefix(WeaponSimple_Katana __instance,bool ___isBladeStuck,out int __state)
        {__state=State(__instance,___isBladeStuck);}
        private static void Postfix(WeaponSimple_Katana __instance,bool ___isBladeStuck,int __state)
        {
            if(__state!=State(__instance,___isBladeStuck)&&__instance.Networkowner)DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipDashChargeChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharacterDash),"Update");
            yield return AccessTools.Method(typeof(CharacterDash),"StartDash");
            yield return AccessTools.Method(typeof(CharacterDash),"RestoreDashCount");
            yield return AccessTools.Method(typeof(CharacterDash),"UserCode_TargetRestoreDashCount__NetworkConnectionToClient__Int32");
        }
        private static void Prefix(CharacterDash __instance,out int __state){__state=__instance.currentDashCount;}
        private static void Postfix(CharacterDash __instance,int __state)
        {
            if(__state!=__instance.currentDashCount)DamageTooltip.Invalidate(__instance.UnitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipTempestStackChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WeaponAddonCommon_Tempest),"Update");
            yield return AccessTools.Method(typeof(WeaponAddonCommon_Tempest),"HandleGuard");
            yield return AccessTools.Method(typeof(WeaponAddonCommon_Tempest),"UseAttackCost");
        }
        private static void Prefix(int ___tempestStack,out int __state){__state=___tempestStack;}
        private static void Postfix(WeaponAddonCommon_Tempest __instance,int ___tempestStack,int __state)
        {
            // Only compare two integers in the native timer callback. No reflection,
            // snapshot or damage evaluation is performed each frame.
            if(__state!=___tempestStack&&__instance.parent&&__instance.parent.Networkowner)
                DamageTooltip.Invalidate(__instance.parent.Networkowner.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipWaterBagStateChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_WaterBag),"OnUpdate");
            yield return AccessTools.Method(typeof(Charm_WaterBag),"OnMpChangedServerside");
            yield return AccessTools.Method(typeof(Charm_WaterBag),"LoadItemOnServer");
            yield return AccessTools.Method(typeof(Charm_WaterBag),"OnDisconnected");
        }
        private static void Prefix(Charm_WaterBag __instance,int ___currentStack,out long __state)
        {__state=((long)___currentStack<<1)|(__instance.coolTime.Check()?1L:0L);}
        private static void Postfix(Charm_WaterBag __instance,int ___currentStack,long __state)
        {
            // Compare readiness, not elapsed time: a running cooldown must not cause
            // a new snapshot every frame. Full-buff MP-use projection needs this edge.
            long state=((long)___currentStack<<1)|(__instance.coolTime.Check()?1L:0L);
            if(__state!=state)DamageTooltip.Invalidate(__instance.NetworkAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipMpLossCounterChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_GainBuffOnMPLoss),"OnMpUsed");
            yield return AccessTools.Method(typeof(Charm_GainBuffOnMPLoss),"OnUpdate");
            yield return AccessTools.Method(typeof(Charm_GainBuffOnMPLoss),"OnDisconnected");
        }
        private static void Prefix(int ___lossMpCounter,out int __state){__state=___lossMpCounter;}
        private static void Postfix(Charm_GainBuffOnMPLoss __instance,int ___lossMpCounter,int __state)
        {
            if(__state!=___lossMpCounter)DamageTooltip.Invalidate(__instance.NetworkAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipMagicAmmoChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(Charm_Magic),"NetworkcurrentAmmo");
            yield return AccessTools.Method(typeof(Charm_Magic),"DeserializeSyncVars");
        }
        private static void Prefix(Charm_Magic __instance,out int __state){__state=__instance.currentAmmo;}
        private static void Postfix(Charm_Magic __instance,int __state)
        {
            if(__state!=__instance.currentAmmo)DamageTooltip.Invalidate(__instance.NetworkAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipMagicCostChangedPatch
    {
        private struct State
        {
            internal int Cost,Recovery;
            internal float Cycle;
        }
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(Charm_Magic),"NetworkadditionalCost");
            yield return AccessTools.PropertySetter(typeof(Charm_Magic),"NetworkadditionalcooldownRecoverySpeedSynced");
            yield return AccessTools.PropertySetter(typeof(Charm_Magic),"NetworkcooldownTimeInThisCycle");
            yield return AccessTools.Method(typeof(Charm_Magic),"DeserializeSyncVars");
        }
        private static void Prefix(Charm_Magic __instance,out State __state)
        {__state=new State{Cost=__instance.AdditionalCost,Recovery=__instance.AdditionalcooldownRecoverySpeed,Cycle=__instance.cooldownTimeInThisCycle};}
        private static void Postfix(Charm_Magic __instance,State __state)
        {
            if(__state.Cost!=__instance.AdditionalCost||__state.Recovery!=__instance.AdditionalcooldownRecoverySpeed||__state.Cycle!=__instance.cooldownTimeInThisCycle)
                DamageTooltip.Invalidate(__instance.NetworkAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipLastMagicChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SkillController),"OnStartFireMagic");
            yield return AccessTools.Method(typeof(SkillController),"ClearLastUsedMagic");
        }
        private static void Prefix(SkillController __instance,out Charm_Magic __state){__state=__instance.GetLastUsedMagic();}
        private static void Postfix(SkillController __instance,Charm_Magic __state)
        {
            if(__state!=__instance.GetLastUsedMagic())DamageTooltip.Invalidate(__instance.GetComponent<UnitAvatar>());
        }
    }

    [HarmonyPatch(typeof(SkillController),"set_IsInGlobalCooldown")]
    internal static class TooltipGlobalCooldownChangedPatch
    {
        private static void Prefix(SkillController __instance,out bool __state){__state=__instance.IsInGlobalCooldown;}
        private static void Postfix(SkillController __instance,bool __state)
        {
            if(__state!=__instance.IsInGlobalCooldown)DamageTooltip.Invalidate(__instance.UnitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipWeaponMpTriggerChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WeaponControllerSimple),"SetMpConsumedAttackTriggerFromAnimationValue");
            yield return AccessTools.Method(typeof(WeaponControllerSimple),"ResetMpConsumedAttackTriggerFromAnimation");
        }
        private static void Prefix(WeaponControllerSimple __instance,out int __state){__state=__instance.mpConsumedAttackTriggerFromAnimationValue;}
        private static void Postfix(WeaponControllerSimple __instance,int __state)
        {
            if(__state!=__instance.mpConsumedAttackTriggerFromAnimationValue)DamageTooltip.Invalidate(__instance.unitAvatar);
        }
    }

    [HarmonyPatch]
    internal static class TooltipCompressedAmmoChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertySetter(typeof(WeaponSimple_Crossbow),"NetworkhasCompressedAmmo");
            yield return AccessTools.Method(typeof(WeaponSimple_Crossbow),"DeserializeSyncVars");
        }
        private static void Prefix(WeaponSimple_Crossbow __instance,out bool __state){__state=__instance.hasCompressedAmmo;}
        private static void Postfix(WeaponSimple_Crossbow __instance,bool __state)
        {
            if(__state!=__instance.hasCompressedAmmo&&__instance.owner)DamageTooltip.Invalidate(__instance.Networkowner.unitAvatar);
        }
    }
}
