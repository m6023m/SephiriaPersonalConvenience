using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal sealed class FullBuffPlan
    {
        private sealed class CalculationLimitException:Exception
        {
            internal CalculationLimitException(string message):base(message){}
        }
        private int advanceSteps;
        private sealed class PlannedBuff
        {
            internal CharacterBuff Definition;
            internal int Stack;
            internal float Amplified;
            internal Dictionary<string,int> Original;
            internal float Age,Duration;
            internal bool Expired;
            internal float RecoveryAge,RecoveryInterval;
            internal bool HealsHp,HealsMp;
            internal float HpPerTick;
            internal int MpPerTick;
            internal float DefaultDuration;
            internal bool IgnoreDurationBonus,RenewDurationOnStacked;
            internal int MaximumStack;
            internal float ReadDuration(int bonus)
            {
                float duration=DefaultDuration;
                if(!IgnoreDurationBonus)duration+=duration*(float)bonus/100f;
                return duration;
            }
            private int projectedStack;
            private float projectedAmplified;
            private Dictionary<string,int> projectedStats;
            private bool hasProjection,projectionSupported;
            private BuffStatRule rule;
            internal void CaptureDefinition()
            {
                DefaultDuration=Definition.defaultDuration;
                IgnoreDurationBonus=Definition.ignoreDurationBonus;
                RenewDurationOnStacked=Definition.renewDurationOnStacked;
                MaximumStack=Definition.MaxStackCount;
                projectionSupported=BuffStatProjection.TryCapture(Definition,out rule);
                hasProjection=false;
            }
            internal bool TryProject(out Dictionary<string,int> values)
            {
                // Time progression changes expiry/recovery, not the additive stat rule.
                // Keep this cache inside one capture so a later game-state change cannot
                // reuse old prefab fields or dynamically modified status entries.
                if(!hasProjection||projectedStack!=Stack||!projectedAmplified.Equals(Amplified))
                {
                    projectedStats=projectionSupported?rule.Evaluate(Stack,Amplified):null;
                    projectedStack=Stack;projectedAmplified=Amplified;hasProjection=true;
                }
                values=projectedStats;return projectionSupported;
            }
        }
        internal readonly Dictionary<string,int> StatChanges=new Dictionary<string,int>(StringComparer.Ordinal);
        internal readonly List<string> Sources=new List<string>();
        internal readonly List<string> Unresolved=new List<string>();
        internal readonly List<string> Notes=new List<string>();
        internal int RequiredMp;
        private int remainingMp;
        private readonly FullBuffMpRecovery mpRecovery=new FullBuffMpRecovery();
        private readonly FullBuffHpRecovery hpRecovery=new FullBuffHpRecovery();
        private readonly FullBuffSwordRecovery swordRecovery=new FullBuffSwordRecovery();
        private readonly FullBuffDashRecovery dashRecovery;
        private bool dashRecoverySupported=true;
        private readonly bool flameSwordEnabled,flameSwordComboEnabled;
        private readonly int flameSwordMaximum;
        private readonly bool initiallyDead,initiallyInBattle,dashInitiallyBlocked;
        private static readonly System.Reflection.FieldInfo SwordAutoAge=AccessTools.Field(typeof(Charm_FlameSwordAuto),"restoreTimer");
        private static readonly System.Reflection.FieldInfo SwordQueue=AccessTools.Field(typeof(ComboEffect_FlameSword),"rechargeQueue");
        private static readonly System.Reflection.FieldInfo HpRegenCounter=AccessTools.Field(typeof(UnitAvatar),"hpRegenCounter");
        private static readonly System.Reflection.FieldInfo HpRestoreAge=AccessTools.Field(typeof(UnitAvatar),"damageRestoreHpTickCounter");
        private static readonly System.Reflection.FieldInfo HpRestoreRate=AccessTools.Field(typeof(UnitAvatar),"damageRestoreHpRate");
        private static readonly System.Reflection.FieldInfo MpRegenCounter=AccessTools.Field(typeof(UnitAvatar),"mpRegenCounter");
        private static readonly System.Reflection.FieldInfo MpHealFraction=AccessTools.Field(typeof(UnitAvatar),"mpStack");
        private static readonly System.Reflection.FieldInfo MpJustUsed=AccessTools.Field(typeof(UnitAvatar),"mpJustUsed");
        private static readonly System.Reflection.FieldInfo MpDelayTimer=AccessTools.Field(typeof(UnitAvatar),"mpPeaceRegenTimer");
        internal WeaponSimple Weapon;
        internal bool Eclipse,Transformed;
        private float transformRemaining;
        private int? restoredHpCurse;
        private int restoredCursedMaxHp;
        private sealed class BoneState
        {
            internal int Blood,Damage,Reduction,Curse,CursedMaximum;
        }
        private readonly Dictionary<int,BoneState> boneStates=new Dictionary<int,BoneState>();
        private static object ReadBone(WeaponAddonGreatsword_BoneBlood addon,string field)
        {return AccessTools.Field(typeof(WeaponAddonGreatsword_BoneBlood),field).GetValue(addon);}
        private float eclipseExpiresAt=float.PositiveInfinity;
        private int eclipseBonus;
        private bool eclipseExpired;
        private static readonly System.Reflection.FieldInfo EclipseTimer=AccessTools.Field(typeof(WeaponSimple_Katana),"eclipseBuffTimer");
        private static readonly System.Reflection.FieldInfo EclipseSwords=AccessTools.Field(typeof(WeaponSimple_Katana),"consumedSwordCount");
        private float? projectedHp,projectedMaxHp;
        private bool healthMaximumLocked;
        private Charm_Magic lastBuffMagic;
        private sealed class MpTriggerBinding
        {
            internal FullBuffMpTrigger State;
            internal CharacterBuff Buff;
            internal float Amplified;
        }
        private readonly List<MpTriggerBinding> mpTriggers=new List<MpTriggerBinding>();
        private static readonly System.Reflection.FieldInfo MpLossCounter=AccessTools.Field(typeof(Charm_GainBuffOnMPLoss),"lossMpCounter");
        private static readonly System.Reflection.FieldInfo WaterStack=AccessTools.Field(typeof(Charm_WaterBag),"currentStack");
        private static readonly System.Reflection.FieldInfo MpUsed=AccessTools.Field(typeof(UnitAvatar),"OnMpUsedServerside");
        private readonly Dictionary<int,int> boneBonuses=new Dictionary<int,int>();
        private readonly List<FullBuffHealthTrigger> healthTriggers=new List<FullBuffHealthTrigger>();
        private static readonly System.Reflection.FieldInfo HpChanged=AccessTools.Field(typeof(UnitAvatar),"OnHpChangedServerside");
        private static readonly System.Reflection.FieldInfo HpDamageValue=AccessTools.Field(typeof(Charm_IncreaseAllDamageByHP),"enabledValue");
        private static readonly System.Reflection.FieldInfo CorkEnabled=AccessTools.Field(typeof(Charm_EnhancedPotionCork),"isEnabled");
        private readonly Dictionary<string,int> weaponChanges=new Dictionary<string,int>();
        private readonly Dictionary<string,PlannedBuff> buffs=new Dictionary<string,PlannedBuff>(StringComparer.Ordinal);
        private readonly PlayerAvatar player;
        private readonly int partySelfApplications;
        private readonly bool hasLegacyWeapon;
        private readonly DamageStatNumbers statNumbers;
        private readonly DamageResourceNumbers resources;
        private int ReadStat(string id){return statNumbers.Read(id,StatChanges);}
        private int ReadDelta(string id){int value;StatChanges.TryGetValue(id,out value);return value;}
        private int ReadMaximumMp(){return resources.MaximumMp(ReadDelta(DamageStatPreview.MaxMpDeltaKey),ReadStat("FINALMP"),ReadStat("INFINITYMP")>0);}
        private int ReadBaseMp(){return resources.BaseMp+ReadDelta(DamageStatPreview.MaxMpDeltaKey);}
        private int ReadReservedMp(){return resources.ReservedMp+ReadDelta(DamageStatPreview.ReservedMpDeltaKey);}
        private float ReadMaximumHp()
        {
            return healthMaximumLocked?projectedMaxHp.Value:resources.MaximumHp(ReadDelta(DamageStatPreview.MaxHpDeltaKey),ReadDelta(DamageStatPreview.FinalHpDeltaKey),restoredHpCurse,restoredCursedMaxHp);
        }
        private float ReadProjectedHp(){return projectedHp??resources.HpWithMaximum(ReadMaximumHp());}
        private float elapsed;
        private float nextMagicAt;
        private static readonly System.Reflection.FieldInfo BuffTimer=AccessTools.Field(typeof(CharacterBuff),"buffTimer");
        private static readonly System.Reflection.FieldInfo MagicCreated=AccessTools.Field(typeof(SkillController),"OnCreateMagicServerside");
        private static readonly System.Reflection.FieldInfo ForkStack=AccessTools.Field(typeof(Charm_TuningForks),"currentStack");
        private static readonly System.Reflection.FieldInfo ForkEnhanced=AccessTools.Field(typeof(Charm_TuningForks),"enhancedWeaponDamage");
        private readonly Dictionary<int,int> forkStacks=new Dictionary<int,int>();
        private readonly List<FullBuffTuningFork> magicTriggers=new List<FullBuffTuningFork>();
        private struct MultipleCastRule
        {
            internal int Threshold,Additional;
        }
        private readonly List<MultipleCastRule> multipleCastRules=new List<MultipleCastRule>();
        private static readonly System.Reflection.FieldInfo MultipleCast=AccessTools.Field(typeof(SkillController),"OnGetMultipleCastCount");
        private void CaptureMultipleCast(PlayerAvatar owner)
        {
            var controller=owner.GetComponent<SkillController>();
            var callbacks=controller?MultipleCast.GetValue(controller) as Delegate:null;
            if(callbacks==null)return;
            foreach(var callback in callbacks.GetInvocationList())
            {
                var charm=callback.Target as Charm_MPMultipleCast;
                if(!charm){Unresolved.Add("연속 시전 횟수의 추가 효과 계산 필요");continue;}
                if(!charm.NetworkAvatar)continue;
                multipleCastRules.Add(new MultipleCastRule{
                    Threshold=charm.multipleCastMPThresholdByLevel.SafeRandomAccess(charm.CurrentLevelToIdx()),
                    Additional=charm.multicast});
            }
        }
        private int ReadMagicCost(FullBuffMagicNumbers magic)
        {return magic.Cost(ReadStat("NOMAGICCOST"),ReadStat("PARTYBUFF"),ReadStat("MAGICCOSTREDUCE"));}
        private void CaptureMpTriggers(PlayerAvatar owner)
        {
            if(owner.IsDead)return;
            var callbacks=MpUsed.GetValue(owner) as Delegate;
            if(callbacks==null)return;
            var captured=new Dictionary<int,MpTriggerBinding>();
            foreach(var callback in callbacks.GetInvocationList())
            {
                var water=callback.Target as Charm_WaterBag;
                var loss=callback.Target as Charm_GainBuffOnMPLoss;
                if(!water&&(!loss||!owner.IsInBattle))continue;
                int id=water?water.GetInstanceID():loss.GetInstanceID();
                MpTriggerBinding binding;
                if(!captured.TryGetValue(id,out binding))
                {
                    binding=new MpTriggerBinding();
                    if(water)binding.State=new FullBuffMpTrigger((int)WaterStack.GetValue(water),
                        water.mpStackByLevel.SafeRandomAccess(water.CurrentLevelToIdx()),
                        Math.Max(0,water.coolTime.GetRemainingTime()),water.coolTime.time);
                    else
                    {
                        if(loss.requiredMP<=0){Unresolved.Add("MP 소모 버프의 발동 기준 확인 필요");continue;}
                        binding.State=new FullBuffMpTrigger((int)MpLossCounter.GetValue(loss),loss.requiredMP);
                        binding.Buff=loss.buffPrefab;binding.Amplified=loss.buffAmplify;
                    }
                    captured.Add(id,binding);
                }
                mpTriggers.Add(binding);
            }
        }
        private void CaptureMagicTriggers(PlayerAvatar owner)
        {
            var controller=owner.GetComponent<SkillController>();
            var callbacks=controller?MagicCreated.GetValue(controller) as Delegate:null;
            if(callbacks==null)return;
            var captured=new Dictionary<int,FullBuffTuningFork>();
            foreach(var callback in callbacks.GetInvocationList())
            {
                var fork=callback.Target as Charm_TuningForks;
                if(!fork)continue;
                int id=fork.GetInstanceID();FullBuffTuningFork trigger;
                if(!captured.TryGetValue(id,out trigger))
                {
                    trigger=new FullBuffTuningFork(id,(int)ForkStack.GetValue(fork),(bool)ForkEnhanced.GetValue(fork),fork.maxStack,
                        Math.Max(0,fork.cooldownTimer.GetRemainingTime()),fork.cooldownTimer.time);
                    captured.Add(id,trigger);forkStacks[id]=trigger.Stack;
                }
                magicTriggers.Add(trigger);
            }
        }
        private void ProjectMagicCreated()
        {
            foreach(var trigger in magicTriggers)
            {
                trigger.OnMagic(elapsed);
                forkStacks[trigger.Id]=trigger.Stack;
            }
        }
        internal FullBuffPlan(PlayerAvatar owner)
        {
            player=owner;
            var legacy=owner.GetComponent<WeaponController>();
            hasLegacyWeapon=legacy&&legacy.GetWeapon()!=null;
            initiallyDead=owner.IsDead;initiallyInBattle=owner.IsInBattle;
            foreach(var member in PlayerSpawner.MultiplayerList)
                if(member&&member.PlayerAvatar==owner&&(member.transform.position-owner.transform.position).magnitude<10f)
                    partySelfApplications++;
            statNumbers=new DamageStatNumbers(owner.customStats,owner.calculatedBonusStats,owner.customStatsAmp);
            resources=new DamageResourceNumbers(owner.hp,owner.maxHp,owner.finalMaxHp,owner.maxMp,owner.reservedMp,owner.isHPCursed,owner.cursedMaxHp,KeywordDatabase.GetConstValue("infinityMPMax"));
            CaptureHealthTriggers(owner);
            CaptureMagicTriggers(owner);
            CaptureMpTriggers(owner);
            CaptureMultipleCast(owner);
            remainingMp=owner.MP;
            nextMagicAt=FullBuffCastTiming.ReadRemainingGlobalCooldown(owner);
            var controller=owner.GetComponent<WeaponControllerSimple>();
            Weapon=controller?controller.currentWeapon:null;
            var dash=owner.CurrentDashModule;
            dashInitiallyBlocked=!owner.CanMove||owner.CanDash.IsFalse()||(dash&&dash.IsDashing);
            if(dash)dashRecovery=new FullBuffDashRecovery(dash.currentDashCount,dash.cooldownTimer.GetTimer(),
                dash.cooldownTimer.time,dash.cooldownTimer.resetOnTime,dash.cooldownTimer.activeOnce);
            var great=Weapon as WeaponSimple_GreatSword;
            if(great&&great.isTransformed)
            {
                Transformed=true;transformRemaining=great.transformResetTimer.GetRemainingTime();
                foreach(var addon in great.addons)
                {
                    var bone=addon as WeaponAddonGreatsword_BoneBlood;
                    if(!bone||!(bool)ReadBone(bone,"isEngaged"))continue;
                    boneStates[bone.GetInstanceID()]=new BoneState{
                        Blood=(int)ReadBone(bone,"bloodStack"),Damage=(int)ReadBone(bone,"appliedDamageBonus"),
                        Reduction=(int)ReadBone(bone,"appliedDamageReduction"),Curse=(sbyte)ReadBone(bone,"cachedHpCursed"),
                        CursedMaximum=(int)ReadBone(bone,"cachedCursedMaxHp")};
                    projectedHp=owner.hp;projectedMaxHp=owner.MaxHp;healthMaximumLocked=true;
                }
            }
            var katana=controller?controller.currentWeapon as WeaponSimple_Katana:null;
            if(katana&&katana.isEclipseBuffActivated)
            {
                Eclipse=true;
                eclipseExpiresAt=Math.Max(0,((Timer)EclipseTimer.GetValue(katana)).GetRemainingTime());
                eclipseBonus=(int)EclipseSwords.GetValue(katana)*KeywordDatabase.GetConstValue("katanaEclipseDamageBonusByConsumeSwordCount");
            }
            var flameSword=owner.Inventory?owner.Inventory.FindComboEffect("FLAMESWORD") as ComboEffect_FlameSword:null;
            if(flameSword)
            {
                swordRecovery.Count=flameSword.currentSword;
                flameSwordEnabled=flameSword.isFlameSwordEnabled;flameSwordComboEnabled=flameSword.isEnabled;
                flameSwordMaximum=flameSword.maxSword;
            }
            if(flameSword&&flameSword.isFlameSwordEnabled)
            {
                var pending=SwordQueue.GetValue(flameSword) as Queue<float>;
                if(pending!=null)swordRecovery.Pending.AddRange(pending);
                var seen=new HashSet<int>();
                foreach(var entry in owner.Inventory.charms)
                {
                    var automatic=entry.Value as Charm_FlameSwordAuto;
                    if(!automatic||!automatic.IsEffectEnabled||!seen.Add(automatic.GetInstanceID()))continue;
                    swordRecovery.Sources.Add(new FullBuffSwordRecovery.Automatic{Age=(float)SwordAutoAge.GetValue(automatic),Interval=automatic.restoreFlameSwordTimeByLevel.SafeRandomAccess(automatic.CurrentLevelToIdx())});
                }
            }
            hpRecovery.Counter=(float)HpRegenCounter.GetValue(owner);
            hpRecovery.RestoreAge=(float)HpRestoreAge.GetValue(owner);
            hpRecovery.RestoreRate=(float)HpRestoreRate.GetValue(owner);
            hpRecovery.RestoreRemaining=owner.DamageRestoreHpRemaining;
            mpRecovery.Counter=(float)MpRegenCounter.GetValue(owner);
            mpRecovery.HealFraction=(float)MpHealFraction.GetValue(owner);
            mpRecovery.JustUsed=(bool)MpJustUsed.GetValue(owner);
            var delay=(Timer)MpDelayTimer.GetValue(owner);
            mpRecovery.DelayAge=delay.GetTimer();mpRecovery.DelayDuration=delay.time;
            foreach(var buff in owner.Buffs)
            {
                if(!buff||buff.IsEndBuff)continue;
                var planned=new PlannedBuff{Definition=buff,Stack=buff.CurrentStack,Amplified=buff.Amplified,Age=((Timer)BuffTimer.GetValue(buff)).GetTimer(),Duration=buff.BuffDuration};
                CaptureBuffDefinition(planned);
                Dictionary<string,int> original;planned.TryProject(out original);planned.Original=original;
                buffs[buff.ID]=planned;
            }
        }
        private void CaptureBuffDefinition(PlannedBuff planned)
        {
            planned.CaptureDefinition();
            var heal=planned.Definition as CharacterBuff_Heal;
            var mp=planned.Definition as CharacterBuff_MPHeal;
            planned.HealsHp=heal;planned.HealsMp=mp;
            planned.HpPerTick=heal?heal.healAmount:0;
            planned.MpPerTick=mp?mp.mpHealAmount:0;
            Timer timer=heal?heal.healTickTimer:mp?mp.healTickTimer:null;
            planned.RecoveryInterval=0;planned.RecoveryAge=0;
            if(timer==null)return;
            if(timer.time<=0||!timer.resetOnTime||timer.activeOnce)
            {Unresolved.Add(planned.Definition.ID+": 특수 회복 타이머 계산 필요");return;}
            planned.RecoveryAge=timer.GetTimer();planned.RecoveryInterval=timer.time;
        }
        private void Advance(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))
                throw new CalculationLimitException("버프 준비 시간을 계산할 수 없습니다.");
            while(seconds>0)
            {
                if(++advanceSteps>4096)throw new CalculationLimitException("버프 준비 시간 계산 한도 초과");
                seconds-=AdvanceSegment(seconds);
            }
        }
        private float AdvanceSegment(float seconds)
        {
            // Split at buff expiry, so recovery uses the stats that apply in each interval.
            float segment=seconds;
            if(Eclipse)segment=Math.Min(segment,Math.Max(0,eclipseExpiresAt-elapsed));
            foreach(var entry in buffs)
                if(!entry.Value.Expired)
                {
                    segment=Math.Min(segment,Math.Max(0,entry.Value.Duration-entry.Value.Age));
                    if(!initiallyDead&&entry.Value.RecoveryInterval>0)segment=Math.Min(segment,Math.Max(0,entry.Value.RecoveryInterval-entry.Value.RecoveryAge));
                }
            Resolve();
            float transformDurationFactor=1;
            if(Transformed)
            {
                transformDurationFactor=1+ReadStat("BUFFDURATION")/100f;
                if(transformDurationFactor<=0||float.IsNaN(transformDurationFactor)||float.IsInfinity(transformDurationFactor))
                    throw new CalculationLimitException("대검 변신의 지속시간 보정을 계산할 수 없습니다.");
                segment=Math.Min(segment,Math.Max(0,transformRemaining)*transformDurationFactor);
            }
            if(!initiallyDead&&segment>0)
            {
                SyncProjectedHealth();
                float hp=ReadProjectedHp(),maximum=ReadMaximumHp();
                float original=hp;
                if(!hpRecovery.Advance(ref hp,maximum,segment,ReadStat("HPREGEN"),
                    ReadStat("HEALINGPENALTY"),ReadStat("HARDMODEHEALINGPENALTY"),ReadStat("HPDAMAGERESTORE")>0))
                    throw new CalculationLimitException("버프 준비 중 체력 회복 계산 한도 초과");
                if(hp!=original){projectedHp=hp;projectedMaxHp=maximum;}
                remainingMp=mpRecovery.Advance(remainingMp,segment,ReadMaximumMp(),ReadReservedMp(),ReadBaseMp(),
                    ReadStat("MPREGEN"),ReadStat("MPREGENMULTIPLE"),ReadStat("MPRESONANCE"),initiallyInBattle);
                if(mpRecovery.LimitExceeded)throw new CalculationLimitException("버프 준비 중 마나 회복 계산 한도 초과");
                if(flameSwordEnabled&&!swordRecovery.Advance(segment,flameSwordMaximum+ReadStat("FLAMESWORDMAX"),ReadStat("FLAMESWORDPICKBONUS")))
                    throw new CalculationLimitException("버프 준비 중 화염검 회복 시간 계산 필요");
            }
            // CharacterDash.Update restores charges independently of the HP/MP recovery
            // branch. Keep the copied timer even when current stats reduce the maximum.
            if(dashRecovery!=null&&!dashRecovery.Advance(segment,ReadStat("DASHRECOVERY")))dashRecoverySupported=false;
            elapsed+=segment;
            if(Transformed)
            {
                transformRemaining-=segment/transformDurationFactor;
                if(transformRemaining<=0)ExpireTransformation();
            }
            if(!initiallyDead)
            {
                SyncProjectedHealth();
                foreach(var entry in buffs)
                {
                    var buff=entry.Value;
                    if(buff.Expired||buff.RecoveryInterval<=0)continue;
                    buff.RecoveryAge+=segment;
                    if(buff.RecoveryAge<buff.RecoveryInterval)continue;
                    buff.RecoveryAge=0;
                    if(buff.HealsHp)
                    {
                        projectedMaxHp=ReadMaximumHp();
                        projectedHp=FullBuffHpRecovery.Heal(ReadProjectedHp(),projectedMaxHp.Value,buff.HpPerTick,ReadStat("HEALINGPENALTY"),ReadStat("HARDMODEHEALINGPENALTY"));
                    }
                    if(buff.HealsMp)remainingMp=mpRecovery.Heal(remainingMp,buff.MpPerTick,ReadMaximumMp(),ReadStat("MPREGENMULTIPLE"));
                }
            }
            if(Eclipse&&elapsed>=eclipseExpiresAt)
            {
                Eclipse=false;eclipseExpired=true;
                AddWeaponChange("FINALWEAPONDAMAGE",-eclipseBonus);
                Notes.Add("일식: 버프 준비 중 지속시간 만료");
            }
            foreach(var entry in buffs)
            {
                var buff=entry.Value;
                if(buff.Expired)continue;
                buff.Age+=segment;
                if(buff.Age<buff.Duration)continue;
                buff.Expired=true;buff.Stack=0;
                if(buff.Original==null)Unresolved.Add(entry.Key+": 사용 중 만료되는 버프의 능력치 계산 필요");
                Notes.Add(entry.Key+": 버프 사용 과정에서 지속시간 만료");
            }
            RefreshHealthBuffEffects();
            return segment;
        }
        internal void CollectWeapon(WeaponSimple weapon)
        {
            Weapon=weapon;if(!weapon)return;
            var type=weapon.GetType();
            bool audited=type==typeof(WeaponSimple_GreatSword)||type==typeof(WeaponSimple_Katana)||type==typeof(WeaponSimple_Crossbow)||type==typeof(WeaponSimple_SwordAndShield)||type==typeof(WeaponSimple_Dagger)||type==typeof(WeaponSimple_QuartterStaff)||type==typeof(WeaponSimple_Staff)||type==typeof(WeaponSimple_Golem)||type==typeof(WeaponSimple_Bow);
            var great=weapon as WeaponSimple_GreatSword;
            var greatCost=great?new FullBuffWeaponCost(great.sweepCost,great.sweepCostBonus,true):null;
            bool startedTransformation=false;
            if(great&&great.specialAttackToTransform)
            {
                audited=true;
                if(Transformed)
                {Sources.Add("대검 변신 (이미 활성)");}
                else if(great.owner)
                {
                    Resolve();bool affordable=remainingMp>=greatCost.Read(ReadStat("SPECIALATTACKCOSTREDUCTION"));
                    if(!affordable)Notes.Add("대검 변신 제외: 충전 시작에 필요한 MP 부족");
                    else if(great.moneyWhirlwind)Unresolved.Add("대검 변신과 금화 소비 조합 계산 필요");
                    else
                    {
                        float charge;string reason;
                        if(!FullBuffCastTiming.TryReadTransformCharge(player,great,out charge,out reason))Unresolved.Add(reason);
                        else
                        {
                            Advance(charge);
                            if(TrySpendWeaponMp(greatCost,"대검 변신"))
                            {Transformed=true;startedTransformation=true;transformRemaining=great.transformResetTimer.time;Sources.Add("대검 변신");Notes.Add("대검 변신 충전 "+charge.ToString("0.###")+"초 후 MP 소비");}
                        }
                    }
                }
            }
            var katana=weapon as WeaponSimple_Katana;
            if(katana&&katana.sheathActionType==WeaponSimple_Katana.ESheathActionType.Eclipse)
            {
                audited=true;
                if(katana.isEclipseBuffActivated&&!eclipseExpired){Eclipse=true;Sources.Add("일식 (이미 활성)");}
                else
                {
                    int swords=flameSwordComboEnabled?Math.Min(swordRecovery.Count,KeywordDatabase.GetConstValue("katanaConsumeSwordCountLimit")):0;
                    if(swords>0)
                    {
                        Eclipse=true;Sources.Add("일식 · 버프 준비 후 화염검 "+swords+"개 사용");
                        swordRecovery.Count-=swords;
                        eclipseBonus=swords*KeywordDatabase.GetConstValue("katanaEclipseDamageBonusByConsumeSwordCount");
                        AddWeaponChange("FINALWEAPONDAMAGE",eclipseBonus);
                        Resolve();
                        eclipseExpiresAt=elapsed+KeywordDatabase.GetConstValue("katanaEclipseBuffTime")*(1+ReadStat("BUFFDURATION")/100f);
                    }
                    else Notes.Add("일식 제외: 현재 사용 가능한 화염검 없음");
                }
            }
            var crossbow=weapon as WeaponSimple_Crossbow;
            if(crossbow&&crossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.IceBuff)
            {
                audited=true;
                if(crossbow.iceBuffCoolDownTimer.GetRemainingTime()>elapsed)Notes.Add("석궁 냉기 버프 제외: 재사용 대기 중 · 이미 적용된 효과는 유지");
                else if(crossbow.owner&&crossbow.iceBuffPrefab&&TrySpendWeaponMp(new FullBuffWeaponCost(crossbow.useMiniDrone?3:crossbow.specialAttackCost,0,false),"석궁 냉기 버프"))Apply(crossbow.iceBuffPrefab,"석궁 냉기 버프",1);
            }
            if(weapon is WeaponSimple_SwordAndShield)Notes.Add("가드 성공으로 얻는 버프는 현재 적용 중인 효과만 유지");
            var dagger=weapon as WeaponSimple_Dagger;
            if(dagger&&dagger.basicAttackFinal)
            {
                // HandleBeginDashAnimation applies this even when the attack hits no enemy.
                // Dash consumes a dash charge, not MP (CharacterDash.StartDash).
                Resolve();
                bool canApply=false;
                if(ReadStat("DASHDISABLE")>0)Notes.Add("단검 돌진 버프 제외: 돌진 사용 불가");
                else if(dashInitiallyBlocked)Unresolved.Add("단검 돌진 버프: 현재 이동·돌진 제한의 해제 시점 계산 필요");
                else if(!dashRecoverySupported)Unresolved.Add("단검 돌진 버프: 돌진 회복 타이머 계산 필요");
                else if(dashRecovery==null||!dashRecovery.TryUse(ReadStat("FIXEDDASH"),ReadStat("DASHCOUNT"),ReadStat("INFINITYDASH")))
                    Notes.Add("단검 돌진 버프 제외: 사용 가능한 돌진 횟수 없음 · 이미 적용된 효과는 유지");
                else canApply=true;
                if(canApply)Apply(dagger.basicAttackFinalBuffPrefab,"단검 돌진 공격 버프",1);
            }
            foreach(var addon in weapon.addons)
            {
                if(addon is WeaponAddonGreatsword_BoneBlood&&startedTransformation)
                    ProjectBoneBlood((WeaponAddonGreatsword_BoneBlood)addon);
                if(addon is WeaponAddonCommon_AttackBuff||addon is WeaponAddonCommon_GuardBuff||addon is WeaponAddon_PerfectGuardBuff||addon is WeaponAddonDagger_FreeParry)
                    Notes.Add("적중·방어 성공 조건의 무기 효과는 임의로 발동시키지 않음");
            }
            if(!audited)Unresolved.Add("이 무기의 사용 가능 버프 계산을 준비 중입니다.");
        }
        private bool TrySpendWeaponMp(FullBuffWeaponCost definition,string source)
        {
            Resolve();
            int available=remainingMp;
            float cost=definition.Read(ReadStat("SPECIALATTACKCOSTREDUCTION"));
            // Crossbow gates against the float cost, but UseMp receives its int cast.
            // INFINITYMP prevents subtraction; it does not bypass this eligibility gate.
            if(available<cost){Notes.Add(source+" 제외: 앞선 버프 사용 후 MP 부족");return false;}
            remainingMp=SpendProjectedMp((int)cost,available);
            return true;
        }
        private int SpendProjectedMp(int requested,int available,bool useMp=true)
        {
            if(!useMp)return available;
            mpRecovery.Used();
            // Native UseMp runs MP-use effects before checking INFINITYMP. A buff
            // gained here can prevent this payment, but cannot refund earlier payments.
            ProjectMpUse(requested);
            Resolve();
            if(ReadStat("INFINITYMP")>0)return available;
            int paid=Math.Min(available,requested);
            RequiredMp+=paid;
            return available-paid;
        }
        private void ProjectMpUse(int requested)
        {
            // UnitAvatar.UseMp invokes its event with the requested amount BEFORE
            // subtracting/clamping MP, including when INFINITYMP prevents subtraction.
            if(requested<0)return;
            // Preserve the native subscriber order without invoking gameplay callbacks.
            // A duplicate subscription is also processed twice, just as the native event is.
            foreach(var binding in mpTriggers)
            {
                int count=binding.State.OnUse(requested,elapsed);
                if(binding.State.Water)
                {
                    if(count!=0)
                    {
                        AddWeaponChange(DamageStatPreview.MaxMpDeltaKey,count);
                        Notes.Add("물주머니: MP 사용으로 최대 MP 중첩 +1 · 다음 발동까지 "+binding.State.Cooldown.ToString("0.###")+"초");
                    }
                }
                else if(binding.Buff)for(int i=0;i<count;i++)Apply(binding.Buff,"MP 소모 연계 버프",binding.Amplified);
            }
        }
        private void ProjectBoneBlood(WeaponAddonGreatsword_BoneBlood addon)
        {
            Resolve();
            float hp=projectedHp??ReadProjectedHp();
            int maximum=Mathf.FloorToInt(projectedMaxHp??ReadMaximumHp());
            int capped=Math.Min(maximum,addon.transformedMaxHp);
            int blood=Math.Max(0,Mathf.FloorToInt(hp)-capped);
            int excess=Math.Max(0,maximum-capped);
            int bonus=addon.maxHpPerBonusAmount>0?excess/addon.maxHpPerBonusAmount*addon.basicAttackBonusPerAmount:0;
            boneStates[addon.GetInstanceID()]=new BoneState{Blood=blood,Damage=blood*addon.damagePerBloodStack,
                Reduction=addon.receivedDamageReductionPercent,Curse=restoredHpCurse??resources.HpCurse,
                CursedMaximum=restoredHpCurse.HasValue?restoredCursedMaxHp:resources.CursedMaxHp};
            projectedHp=Math.Min(hp,capped);projectedMaxHp=capped;
            healthMaximumLocked=true;
            boneBonuses[addon.GetInstanceID()]=bonus;
            AddWeaponChange("ALLDAMAGEBONUS",blood*addon.damagePerBloodStack);
            AddWeaponChange("RECEIVEDDAMAGEREDUCTION",addon.receivedDamageReductionPercent);
            Notes.Add("재조립 후 HP "+projectedHp.Value.ToString("0.##")+" / "+capped+" · 혈액 "+blood+" · 근접 평타 +"+bonus+"%");
            // Engage calls SetHp only when the cap actually removes HP. Only then do native
            // OnHpChanged subscribers recompute their cached bonuses; this is not a damage hit.
            if(hp>capped)ProjectHealthTriggeredStats(projectedHp.Value,capped);
        }
        private void ExpireTransformation()
        {
            Transformed=false;
            foreach(var entry in boneStates)
            {
                var state=entry.Value;
                AddWeaponChange("ALLDAMAGEBONUS",-state.Damage);
                AddWeaponChange("RECEIVEDDAMAGEREDUCTION",-state.Reduction);
                boneBonuses[entry.Key]=0;
                restoredHpCurse=state.Curse;restoredCursedMaxHp=state.CursedMaximum;
                healthMaximumLocked=false;
                Resolve();
                float maximum=ReadRestoredMaximumHp();
                float hp=projectedHp??player.hp;
                projectedHp=Math.Min(maximum,hp+Math.Max(0,state.Blood));projectedMaxHp=maximum;
                if(state.Blood>0||hp>maximum)ProjectHealthTriggeredStats(projectedHp.Value,maximum);
            }
            boneStates.Clear();
            Notes.Add("대검 변신: 버프 준비 중 지속시간 만료");
        }
        private float ReadRestoredMaximumHp()
        {
            return resources.MaximumHp(ReadDelta(DamageStatPreview.MaxHpDeltaKey),ReadDelta(DamageStatPreview.FinalHpDeltaKey),restoredHpCurse,restoredCursedMaxHp);
        }
        private void AddWeaponChange(string id,int value)
        {
            int old;weaponChanges.TryGetValue(id,out old);weaponChanges[id]=old+value;
        }
        private void CaptureHealthTriggers(PlayerAvatar owner)
        {
            var callbacks=HpChanged.GetValue(owner) as Delegate;
            if(callbacks==null)return;
            var captured=new Dictionary<int,FullBuffHealthTrigger>();
            foreach(var callback in callbacks.GetInvocationList())
            {
                var damage=callback.Target as Charm_IncreaseAllDamageByHP;
                var cork=callback.Target as Charm_EnhancedPotionCork;
                if(!damage&&!cork)continue;
                int id=damage?damage.GetInstanceID():cork.GetInstanceID();
                FullBuffHealthTrigger trigger;
                if(!captured.TryGetValue(id,out trigger))
                {
                    trigger=damage?
                        new FullBuffHealthTrigger("ALLDAMAGEBONUS",damage.overHpPercent,damage.damagePercentByLevel.SafeRandomAccess(damage.CurrentLevelToIdx()),true,(int)HpDamageValue.GetValue(damage)):
                        new FullBuffHealthTrigger("HPPOTIONBONUS",cork.hpRatio,cork.potionBonus,false,(bool)CorkEnabled.GetValue(cork)?cork.potionBonus:0);
                    captured.Add(id,trigger);
                }
                // Preserve callback order. Duplicate registration shares the original
                // object's cached applied value rather than creating another bonus.
                healthTriggers.Add(trigger);
            }
        }
        private void ProjectHealthTriggeredStats(float hp,float maximum)
        {
            foreach(var trigger in healthTriggers)
            {
                int delta=trigger.Update(hp,maximum);
                if(delta!=0)AddWeaponChange(trigger.Stat,delta);
            }
        }
        private void SyncProjectedHealth(DamageStatPreview preview=null)
        {
            if(!projectedHp.HasValue)return;
            float maximum=ReadMaximumHp();
            if(maximum!=projectedMaxHp.Value)
            {
                float ratio=projectedMaxHp.Value!=0?Math.Min(1,projectedHp.Value/projectedMaxHp.Value):0;
                projectedHp=maximum*ratio;projectedMaxHp=maximum;
            }
            if(preview!=null)preview.SetHealth(projectedHp.Value,projectedMaxHp.Value);
        }
        private void RefreshHealthBuffEffects()
        {
            Resolve();
            if(!projectedHp.HasValue&&!StatChanges.ContainsKey(DamageStatPreview.MaxHpDeltaKey)&&!StatChanges.ContainsKey(DamageStatPreview.FinalHpDeltaKey))return;
            // AddMaxHp/AddMaxHpPercent notify HP subscribers even when preserving the
            // HP ratio. Update their cached contributions from the projected values.
            SyncProjectedHealth();
            ProjectHealthTriggeredStats(ReadProjectedHp(),ReadMaximumHp());
            Resolve();
        }
        internal static DamageTooltip.Snapshot Capture(PlayerAvatar p,Func<string> collect)
        {
            try{return CapturePlan(p,collect);}
            catch(CalculationLimitException e)
            {
                return new DamageTooltip.Snapshot{Template="풀 버프 피해량을 계산할 수 없습니다.\n"+e.Message};
            }
        }
        private static DamageTooltip.Snapshot CapturePlan(PlayerAvatar p,Func<string> collect)
        {
            var plan=new FullBuffPlan(p);plan.CollectMagic();
            var controller=p.GetComponent<WeaponControllerSimple>();
            plan.CollectWeapon(controller?controller.currentWeapon:null);plan.Resolve();
            if(plan.Unresolved.Count>0)return new DamageTooltip.Snapshot{Template="아직 계산을 지원하지 않는 효과가 있어 풀 버프 피해량을 표시할 수 없습니다.\n"+string.Join("\n",new HashSet<string>(plan.Unresolved))};
            using(var stats=new DamageStatPreview(p,plan.StatChanges))
            using(var weapon=new WeaponBuffPreview(plan.Weapon,plan.Eclipse,plan.Transformed,plan.boneBonuses))
            {
                stats.SetRemainingMp(plan.remainingMp);
                if(plan.lastBuffMagic)stats.SetLastMagic(plan.lastBuffMagic);
                stats.SetTuningForkStacks(plan.forkStacks);
                if(plan.projectedHp.HasValue)stats.SetHealth(plan.projectedHp.Value,plan.projectedMaxHp.Value);
                var snapshot=DamageTooltip.Capture(delegate
                {
                    DamageTooltip.CurrentCapture.FullConditions=true;
                    string description=collect();
                    foreach(var entry in plan.buffs)
                    {
                        var buff=entry.Value;
                        if(buff.Expired||buff.Stack<=0)continue;
                        if(DamageTooltip.CurrentCapture!=null&&DamageTooltip.CurrentCapture.Dps!=null)
                        {
                            MagicDamageProfiles.CaptureBuffDps(buff.Definition,buff.Amplified,p,buff.Duration-buff.Age);
                            continue;
                        }
                        string periodic=MagicDamageProfiles.DescribeBuffAttack(buff.Definition,buff.Amplified,p);
                        if(periodic!=null)description+="\n\n사용 후 유지되는 버프의 별도 피해\n"+periodic;
                    }
                    return description;
                },p);
                snapshot.FullConditions=true;
                snapshot.Template="풀 버프 예상 피해량\n"+(plan.Sources.Count>0?string.Join(" · ",plan.Sources.ToArray()):"사용할 별도 버프 없음")+"\n총 소모 MP "+plan.RequiredMp+" · 사용 후 MP "+p.MP+"\n\n"+snapshot.Template;
                if(snapshot.Dps!=null)snapshot.Dps.BuffSummary=(plan.Sources.Count>0?string.Join(" · ",plan.Sources.ToArray()):"사용할 별도 버프 없음")+"\n총 소모 MP "+plan.RequiredMp+" · 사용 후 MP "+p.MP;
                return snapshot;
            }
        }
        internal void CollectMagic()
        {
            if(!player.Inventory)return;
            var seen=new HashSet<int>();
            for(int x=0;x<player.Inventory.Width;x++)for(int y=0;y<player.Inventory.Height;y++)
            {
                var item=player.Inventory.FindItem(new ItemPosition(x,y));
                var charm=item!=null?item.Charm as Charm_Magic:null;
                if(!charm||!charm.IsEffectEnabled||!seen.Add(charm.GetInstanceID())||!charm.ContainedMagic||!charm.ContainedMagic.magicPrefab)continue;
                var skill=charm.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Buff>();
                if(!skill)continue;
                string name=charm.ContainedMagic.Name;
                var magicNumbers=new FullBuffMagicNumbers(charm.ContainedMagic.mpCostsByLevel.SafeRandomAccess(charm.CurrentLevelToIdx()),
                    charm.AdditionalCost,charm.currentAmmo,charm.IsEffectEnabled);
                var magicTiming=FullBuffCastTiming.Capture(player,charm);
                // CanCast only reads enabled state, ammo and cost. Never invoke FireCasting
                // here: a preview must not consume charges, MP or trigger gameplay callbacks.
                ECanUseSkillResult availability;
                int cost,casts;
                Resolve();
                cost=ReadMagicCost(magicNumbers);
                availability=magicNumbers.Availability(remainingMp,cost);
                casts=ReadMultipleCastCount();
                if(availability!=ECanUseSkillResult.Succeeded)
                {
                    string reason=availability==ECanUseSkillResult.Failed_NotYet?"재사용 대기 중":availability==ECanUseSkillResult.Failed_NotEnoughMana?"시전에 필요한 MP 부족":"현재 사용 불가";
                    Notes.Add(name+" 제외: "+reason);
                    continue;
                }
                // WeaponMaxDamage inherits OnStartMagic unchanged; its buff projection
                // still has to be supported independently. Unknown overrides need an audit.
                if(skill.GetType()!=typeof(ActiveSkill_Buff)&&skill.GetType()!=typeof(ActiveSkill_Buff_WeaponMaxDamage)){Unresolved.Add(name+": 전용 버프 동작 계산 필요");continue;}
                Advance(Math.Max(0,nextMagicAt-elapsed));
                float before,after,global;string timingReason;bool hasTiming;
                Resolve();
                hasTiming=magicTiming.TryRead(ReadStat("MAGICQUICKCAST"),ReadStat("ATTACKSPEED"),ReadStat("FIXEDATTACKSPEED"),
                    out before,out after,out global,out timingReason);
                if(!hasTiming){Unresolved.Add(name+": "+timingReason);continue;}
                nextMagicAt=elapsed+global;
                Advance(before);
                Resolve();
                availability=magicNumbers.Availability(remainingMp,ReadMagicCost(magicNumbers));
                casts=ReadMultipleCastCount();
                if(availability!=ECanUseSkillResult.Succeeded)
                {
                    Notes.Add(name+": 시전 동작 후 발사 시점의 사용 조건 미충족");
                    lastBuffMagic=charm;
                    Advance(after);
                    continue;
                }
                // Native MultipleCreateMagic checks affordability once, then each repeat
                // spends MP with a zero floor; it does not cancel later repeats at zero MP.
                for(int cast=0;cast<casts;cast++)
                {
                    bool useMp;
                    if(cast>0)
                    {
                        Advance(.15f);
                    }
                    Resolve();
                    cost=Math.Max(0,ReadMagicCost(magicNumbers));
                    useMp=ReadStat("NOMAGICCOST")<=0;
                    remainingMp=SpendProjectedMp(cost,remainingMp,useMp);
                    ApplyMagicBuff(skill,name);
                    // SkillController.OnSkillCreated runs after the buff magic starts.
                    ProjectMagicCreated();
                }
                Advance(Math.Max(0,after-(casts-1)*.15f));
                lastBuffMagic=charm;
                if(casts>1)Notes.Add(name+": 1회 사용으로 "+casts+"회 발동 · 발동 간격 0.15초");
            }
        }
        private int ReadMultipleCastCount()
        {
            // Mirror the native contributors without invoking arbitrary event subscribers.
            // The count is fixed before MultipleCreateMagic begins consuming MP.
            int count=1;
            int maximum=ReadMaximumMp();
            foreach(var rule in multipleCastRules)
                if(maximum>=rule.Threshold)count+=rule.Additional;
            return Math.Max(1,count);
        }
        private void ApplyMagicBuff(ActiveSkill_Buff skill,string source)
        {
            Resolve();bool party;
            party=ReadStat("PARTYBUFF")>0;
            if(!party){Apply(skill.buffPrefab,source,1);return;}
            // ActiveSkill_Buff's party branch visits the spawner list and has no
            // fallback self-cast. Match its strict distance check for this player.
            for(int i=0;i<partySelfApplications;i++)Apply(skill.buffPrefab,source,1);
            if(partySelfApplications==0)Notes.Add(source+": 파티 버프 대상 범위에 자신이 없어 자기 능력치에는 미적용");
        }
        internal void Apply(CharacterBuff prefab,string source,float amplified)
        {
            if(!prefab){Unresolved.Add(source+": 버프 데이터 없음");return;}
            if(prefab.GetType()==typeof(CharacterBuff_WeaponMaxDamageUp)&&hasLegacyWeapon)
                Unresolved.Add(source+": 구형 무기의 최대 공격력 변경 계산 필요");
            PlannedBuff planned;
            if(!buffs.TryGetValue(prefab.ID,out planned))
            {
                // Every live buff was captured before preparation began. A missing ID
                // is a new virtual instance, never a reason to query the live player.
                planned=new PlannedBuff{Definition=prefab,Stack=0,Amplified=1,Age=0,Duration=prefab.defaultDuration,
                    Original=new Dictionary<string,int>()};
                CaptureBuffDefinition(planned);
                buffs.Add(prefab.ID,planned);
            }
            if(planned.Original==null){Unresolved.Add(source+": 현재 버프 계산 필요");return;}
            Resolve();
            float duration=planned.ReadDuration(ReadStat("BUFFDURATION"));
            if(planned.Expired)
            {
                // The native instance has been removed: a new application uses the prefab.
                planned.Definition=prefab;planned.Age=0;planned.Stack=0;planned.Expired=false;
                CaptureBuffDefinition(planned);
                duration=planned.ReadDuration(ReadStat("BUFFDURATION"));
            }
            if(planned.Stack==0||planned.RenewDurationOnStacked)planned.Age=0;
            planned.Duration=duration;
            // Native ApplyBuff keeps the first definition for an ID, adds one stack, then sets amplification.
            if(planned.Stack==0)planned.Stack=1;
            else if(planned.Stack<planned.MaximumStack)planned.Stack++;
            planned.Amplified=amplified;
            if(!Sources.Contains(source))Sources.Add(source);
            RefreshHealthBuffEffects();
        }
        internal void Resolve()
        {
            StatChanges.Clear();
            foreach(var change in weaponChanges)Add(change.Key,change.Value);
            foreach(var entry in buffs)
            {
                var planned=entry.Value;Dictionary<string,int> projected;
                if(planned.Original==null)continue;
                if(planned.Expired)
                {
                    foreach(var old in planned.Original)Add(old.Key,-old.Value);
                    continue;
                }
                if(!planned.TryProject(out projected))
                {
                    string reason=entry.Key+": 버프 능력치 계산 필요";
                    if(!Unresolved.Contains(reason))Unresolved.Add(reason);
                    continue;
                }
                foreach(var old in planned.Original)Add(old.Key,-old.Value);
                foreach(var added in projected)Add(added.Key,added.Value);
            }
        }
        private void Add(string key,int value)
        {
            int old;StatChanges.TryGetValue(key,out old);StatChanges[key]=old+value;
        }
    }
}
