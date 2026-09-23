using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class WeaponAdditionalDamageProfiles
    {
        private static readonly FieldInfo BoneEngaged=AccessTools.Field(typeof(WeaponAddonGreatsword_BoneBlood),"isEngaged");
        private static readonly FieldInfo BoneBasicBonus=AccessTools.Field(typeof(WeaponAddonGreatsword_BoneBlood),"basicAttackBonus");
        private static readonly FieldInfo BladeStuck=AccessTools.Field(typeof(WeaponSimple_Katana),"isBladeStuck");
        private static readonly FieldInfo GuardSweep=AccessTools.Field(typeof(WeaponSimple_SwordAndShield),"_guardSweepEnabled");
        private static readonly FieldInfo DashCreated=AccessTools.Field(typeof(WeaponSimple),"onDashAttackCreated");
        private static readonly FieldInfo EnhancedDashReady=AccessTools.Field(typeof(Charm_KatanaEnhancedDashAttackActivator),"isEnhancedAttackActive");
        internal static float EnhancedKatanaDashPercent(WeaponSimple weapon)
        {
            var katana=weapon as WeaponSimple_Katana;
            if(!katana)return 0;
            var callbacks=DashCreated.GetValue(weapon) as Delegate;
            if(callbacks==null)return 0;
            float percent=0;
            var consumed=new HashSet<int>();
            foreach(var callback in callbacks.GetInvocationList())
            {
                var scroll=callback.Target as Charm_KatanaEnhancedDashAttackActivator;
                if(scroll&&scroll.katana==katana&&(bool)EnhancedDashReady.GetValue(scroll)&&consumed.Add(scroll.GetInstanceID()))
                    percent+=scroll.damagePercentByLevel.SafeRandomAccess(scroll.CurrentLevelToIdx());
            }
            return percent;
        }
        internal static float FinalComboArtifactPercent(PlayerAvatar p)
        {
            float percent=0;
            if(p.Inventory)foreach(var entry in p.Inventory.charms)
            {
                var crown=entry.Value as Charm_IncreaseLastAttackDamage_SwordAndShield;
                if(crown&&crown.IsEffectEnabled)percent+=crown.damagePercentByLevel[crown.CurrentLevelToIdx()];
            }
            return percent;
        }
        internal static float FinalComboCriticalChance(PlayerAvatar p)
        {
            float chance=0;
            if(p.Inventory)foreach(var entry in p.Inventory.charms)
            {
                var crown=entry.Value as Charm_FinalComboCritical;
                if(crown&&crown.IsEffectEnabled)chance+=crown.criticalBonusPercentByLevel.SafeRandomAccess(crown.CurrentLevelToIdx());
            }
            return chance;
        }
        internal static void Append(StringBuilder text,WeaponSimple weapon,PlayerAvatar p)
        {
            foreach(var addon in weapon.addons)
            {
                var needle=addon as WeaponAddonGreatsword_FireNeedleBullet;
                if(needle)
                {
                    var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.PhysicalDamage),Factors=new[]{needle.needleBulletDamageRatio,1+p.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f}};
                    int critical=50+p.GetCustomStat(ECustomStat.CriticalDamageBonus)+p.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE");
                    int amp=p.GetCustomStatUnsafe("WEAPONCRITICALDAMAGEAMPLIFY");if(amp>0)critical+=(int)(critical*amp/100f);
                    int count=needle.needleBulletCount+Math.Max(0,(int)((critical-50)/20f));
                    text.AppendLine("치명타·처형 적중 후 가시 탄환 · 일반 / 치명타");
                    text.AppendLine(ProjectileDamageProfiles.Describe(p,hit,needle.needleBulletPrefab,"가시 1발"));
                    text.Append("기본 무기 치명타 피해율 기준 발사 ").Append(count).AppendLine("발 · 발동 공격의 추가 치명타 피해율에 따라 증가");
                    text.AppendLine("무기의 직접 타격에서 발동 · 각 탄환의 실제 적중 횟수만 합산");
                }
                var ring=addon as WeaponAddonCommon_BurnRing;
                if(ring)
                {
                    var hit=new DamageTooltip.Hit{Raw=p.GetCustomStatUnsafe(ring.relatedStatUnsafe),Element=ring.elementalType,Factors=new[]{ring.damagePercent/100f,1+p.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,1+p.GetCustomStatUnsafe("BURNDAMAGE")/100f}};
                    text.Append("화염 고리 범위 내 대상 1명·1틱: ").AppendLine(DamageTooltip.Hits(p,hit));
                    float speed=1+p.GetCustomStatUnsafe("BURNSPEED")/100f;
                    if(speed>0)text.Append("판정 간격 ").Append((ring.burnRingTickTimer.time/speed).ToString("0.###")).Append("초 · 지속 ").Append(ring.burnRingDuration.ToString("0.###")).AppendLine("초");
                    else text.AppendLine("현재 화상 속도로는 고리 판정 시간이 진행되지 않음");
                    text.Append(KeywordDatabase.Convert("<tag="+ring.debuffKeywordName+">")).AppendLine(" 부여 시 발동·지속시간 갱신 · 평타 피해 증가는 적용하지 않음");
                }
                var c4=addon as WeaponAddonCommon_C4Bomb;
                if(c4)
                {
                    int cap=KeywordDatabase.GetConstValue("crossbowC4BombMaxCountPerTarget");
                    text.Append("C4 부착: 평타·돌진이 살아 있는 적에게 적중할 때 · ").AppendLine(cap>0?"대상당 최대 "+cap+"개":"부착 수 제한 없음");
                }
            }
        }
        internal static int ShieldChargeStacks(WeaponSimple_SwordAndShield shield)
        {
            if(!shield.chargedSweep_New)return 0;
            var snapshot=DamageTooltip.CurrentCapture;
            // Conditional damage belongs to the full-buff view. Capture values
            // without changing the live weapon's stack or charging timer.
            return snapshot==null?Math.Max(0,shield.chargedSweep_New_Stack):
                snapshot.FullConditions?Math.Max(0,KeywordDatabase.GetConstValue("chargedSweepMaxStack")):0;
        }
        internal static int? ShieldAnimationOverride(WeaponSimple_SwordAndShield shield)
        {
            if(!shield.overrideSweepAddon)return null;
            int? selected=null;
            foreach(string parameter in (shield.changedSweepParameter??"").Split(','))
            {
                string[] pair=parameter.Split('=');int value;
                if(pair.Length==2&&pair[0]=="ANIMATION"&&int.TryParse(pair[1],out value))selected=value;
            }
            return selected;
        }
        internal static bool ShieldGuardReady(WeaponSimple_SwordAndShield shield)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            return snapshot==null?(bool)GuardSweep.GetValue(shield):snapshot.FullConditions&&shield.guardSweep;
        }
        private static bool ShieldGuardAtFire(WeaponSimple_SwordAndShield shield)
        {
            if(!ShieldGuardReady(shield))return false;
            // The native input selector can consume the guard buff before the
            // projectile selector/created callback runs. Preview that state
            // transition numerically rather than changing the live weapon.
            if(ShieldAnimationOverride(shield).HasValue)return true;
            var player=DamageTooltip.Player;
            if((player&&player.GetCustomStatUnsafe("DARKCLOUDSWEEP")>0)||shield.isFlameEaterHaetaeEnabled)return true;
            if(shield.shieldThrowing)return false;
            return shield.guardSweep_New;
        }
        internal static NewWeaponFireData ShieldSpecialAttack(WeaponSimple_SwordAndShield shield,int index)
        {
            // Same priority as the native selector, using local data only.
            var addon=shield.overrideSweepAddon;
            if(index==0&&addon)return addon.GetFireData("SWEEP",0);
            bool guard=ShieldGuardAtFire(shield);
            if(ShieldChargeStacks(shield)>0)
                return guard?shield.chargedSweepFireData_New_GuardEnhanced:shield.chargedSweepFireData_New;
            if(shield.guardSweep_New&&guard)return shield.guardSweepFireData_New;
            int state=shield.currentAllElementalState;
            if(shield.isAllElementalEnabled&&shield.allElementalSets!=null&&state>=0&&state<shield.allElementalSets.Length)
            {
                var set=shield.allElementalSets[state];
                var attack=set==null||set.specialAttacks==null||set.specialAttacks.Length==0?null:set.specialAttacks.SafeRandomAccess(index);
                if(attack)return attack;
            }
            return shield.specialAttacks==null||shield.specialAttacks.Length==0?null:shield.specialAttacks.SafeRandomAccess(index);
        }
        internal static void ApplyCreatedAttack(WeaponSimple weapon,NewWeaponFireData attack,int kind,PlayerAvatar p,DamageTooltip.Hit hit)
        {
            float percent=0;
            if(kind==0&&attack is NewWeaponFireData_MeleeAttack)
            {
                foreach(var addon in weapon.addons)
                {
                    var bone=addon as WeaponAddonGreatsword_BoneBlood;
                    if(bone)percent+=WeaponBuffPreview.BoneBonus(bone,(bool)BoneEngaged.GetValue(bone)?(int)BoneBasicBonus.GetValue(bone):0);
                    // Native callbacks add into the same projectile percentage. Apply
                    // their sum once, including MP left after a full-buff preview.
                    if(addon is WeaponAddonGreatsword_UseMPBasicAttack&&WeaponBuffPreview.UsedMp(weapon,kind,p)>0)
                        percent+=KeywordDatabase.GetConstValue("GreatSwordTwinDamageBonus");
                }
            }
            var katana=weapon as WeaponSimple_Katana;
            if(kind==0&&katana)
            {
                int defense=p.GetCustomStat(ECustomStat.DamageReduction);
                if(p.GetCustomStatUnsafe("DEFENSEKATANA")>0)
                    percent+=DefenseSteps(defense,"defenseKatanaDamageBonusStatSplitUnit","defenseKatanaDamageBonusPercent");
                if(katana.useSheathHardening&&(bool)BladeStuck.GetValue(katana))
                    percent+=DefenseSteps(defense,"katanaSheathHardeningAttackStateDamageBonusStatSplitUnit","katanaSheathHardeningAttackStateDamageBonus");
                if(WeaponBuffPreview.Eclipse(weapon))
                {
                    percent+=p.GetCustomStatUnsafe("FLAMESWORDDAMAGE");
                    hit.ExtraCritical+=p.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE");
                    hit.ExtraCriticalChance+=p.GetCustomStatUnsafe("FLAMESWORDCRITICAL");
                }
            }
            var shield=weapon as WeaponSimple_SwordAndShield;
            var great=weapon as WeaponSimple_GreatSword;
            if(kind==2&&great&&great.rapidWhirlwind)
                hit.ExtraCriticalChance+=KeywordDatabase.GetConstValue("greatswordRapidWhirlwindCriticalChancePercent");
            if(kind==2&&katana&&katana.isCloudSlashAttack)
                percent+=katana.cloudSlashDamagePercentPerStack*katana.cloudSlashUsedStacks;
            if(kind==2&&shield)
            {
                // Read the captured local reference. Prefab previews have no spawned
                // network identity, and damage calculation must not resolve one.
                if(shield.overrideSweepAddon)percent+=SweepStatBonus(shield.changedSweepParameter,p);
                if(shield.guardSweep_New&&ShieldGuardAtFire(shield))percent+=KeywordDatabase.GetConstValue("guardSweepDamageBonusPercent");
                percent+=KeywordDatabase.GetConstValue("chargedSweepDamageBonusPercent")*ShieldChargeStacks(shield);
            }
            var special=attack as NewWeaponFireData_SpecialProjectile;
            var crossbow=weapon as WeaponSimple_Crossbow;
            if(crossbow)
            {
                if(kind==0&&p.GetCustomStatUnsafe("GRENADEATTACK")>0)percent+=p.GetCustomStatUnsafe("ATTACKSPEED");
                if(kind==2)
                {
                    if(crossbow.continueBonus)
                    {
                        var snapshot=DamageTooltip.CurrentCapture;
                        int count=snapshot==null?crossbow.continueBonusCount:snapshot.FullConditions?5:0;
                        if(count>0&&snapshot!=null&&snapshot.Dps!=null)
                        {
                            // A single-shot sustained rotation must beat the
                            // native inactivity timeout to keep its streak.
                            var timing=DpsTiming.Special(crossbow,p);
                            if(timing.Unavailable!=null||timing.Seconds>=crossbow.continueBonusResetTimer.time)count=0;
                        }
                        percent+=count*10;
                    }
                    if(crossbow.useMiniDrone)percent+=Math.Max(0,p.MaxMp-50)*crossbow.miniDroneAddDamagePercentPerMP;
                }
            }
            var staff=weapon as WeaponSimple_QuartterStaff;
            if(staff)
            {
                if(kind==0&&staff.canCrystalExplosion)percent+=p.GetCustomStat(ECustomStat.SpecialAttackDamageBonus);
                if(kind==1&&staff.lastDashCount>0)
                {
                    // This modifies defaultDamageRatio, before fixed additionalDamage.
                    var factors=new List<float>(hit.Factors??new float[0]);
                    factors.Add(1+staff.lastDashCount*p.GetCustomStatUnsafe("SPEARDASHATTACKBONUSBYLASTDASH")/100f);
                    hit.Factors=factors.ToArray();
                }
            }
            if(special&&special.projectilePrefab&&special.projectilePrefab.GetComponent<SpecialProjectile_C4BombExplosion>())
            {
            float physical=p.GetCustomStat(ECustomStat.PhysicalDamage)*KeywordDatabase.GetConstValue("crossbowC4BombExplosionPhysicalPercent")/100f;
            int step=Math.Max(1,KeywordDatabase.GetConstValue("crossbowC4BombExplosionPerDefense"));
            int defenseSteps=p.GetCustomStat(ECustomStat.DamageReduction)/step;
            float defense=defenseSteps>0?1+defenseSteps*.01f:1;
            // C4's input damage scales only the physical term, not the fixed term.
            Scale(hit,physical*defense);
            hit.AfterFactors+=KeywordDatabase.GetConstValue("crossbowC4BombExplosionDefaultDamage")*defense;
            hit.Weapon=true;
            }
            ApplyConditional(weapon,p,hit);
            hit.ProjectileDamagePercent+=percent;
        }
        internal static void ApplyConditional(WeaponSimple weapon,PlayerAvatar p,DamageTooltip.Hit hit)
        {
            if(!weapon||weapon.addons==null||!p||hit==null)return;
            foreach(var addon in weapon.addons)
            {
                var conditional=addon as WeaponAddonCommon_ConditionalStat;
                if(!conditional)continue;
                int value=p.GetCustomStatUnsafe(conditional.sourceStatId);
                bool active=conditional.condition==WeaponAddonCommon_ConditionalStat.ECondition.LOWER_THAN_OR_EQUAL?value<=conditional.sourceStatValue:
                    conditional.condition==WeaponAddonCommon_ConditionalStat.ECondition.GREATER_THAN_OR_EQUAL?value>=conditional.sourceStatValue:
                    conditional.condition==WeaponAddonCommon_ConditionalStat.ECondition.EQUAL?value==conditional.sourceStatValue:value!=conditional.sourceStatValue;
                var snapshot=DamageTooltip.CurrentCapture;
                if(!active&&(snapshot==null||!snapshot.FullConditions))continue;
                var factors=new List<float>(hit.Factors??new float[0]);
                factors.Add(1+conditional.addDamagePercent/100f);hit.Factors=factors.ToArray();
            }
        }
        private static float DefenseSteps(int defense,string divisorKey,string bonusKey)
        {
            int divisor=KeywordDatabase.GetConstValue(divisorKey);
            // Native code divides integers before storing the result in a float.
            int steps=divisor!=0?defense/divisor:0;
            return steps>0?steps*(float)KeywordDatabase.GetConstValue(bonusKey):0;
        }
        private static int SweepStatBonus(string parameters,PlayerAvatar p)
        {
            int bonus=0;
            foreach(string parameter in (parameters??"").Split(','))
            {
                var pair=parameter.Split('=');
                if(pair.Length!=2||pair[0]!="DAMAGEBONUSBYSTAT")continue;
                var parts=pair[1].Split('/');if(parts.Length!=3)continue;
                string stat=parts[0].Trim().ToUpperInvariant();
                if(stat=="DEFENSE")stat=ECustomStat.DamageReduction.ToString().ToUpperInvariant();
                int divisor=ParameterValue(parts[1]),amount=ParameterValue(parts[2]);
                bonus=p.GetCustomStatUnsafe(stat)/Math.Max(1,divisor)*amount;
            }
            return bonus;
        }
        private static int ParameterValue(string raw)
        {
            string value=raw.Trim();
            return value.StartsWith("[")&&value.EndsWith("]")?KeywordDatabase.GetConstValue(value.Substring(1,value.Length-2)):int.Parse(value);
        }
        private static void Scale(DamageTooltip.Hit hit,float factor)
        {
            var factors=new List<float>(hit.Factors??new float[0]);factors.Add(factor);
            hit.Factors=factors.ToArray();hit.AfterFactors*=factor;
        }
    }
}
