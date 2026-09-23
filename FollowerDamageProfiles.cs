using System;
using System.Text;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class FollowerDamageProfiles
    {
        private static readonly FieldInfo SummonedUnit=AccessTools.Field(typeof(Charm_SummonUnit),"currentUnit");
        private static readonly FieldInfo Following=AccessTools.Field(typeof(Charm_LeadNPC),"following");
        private static readonly FieldInfo LeadBonus=AccessTools.Field(typeof(Charm_LeadNPC),"added");
        private static readonly FieldInfo BallistaUnit=AccessTools.Field(typeof(Charm_MiniBallista),"ballista");
        private static readonly MethodInfo Related=AccessTools.Method(typeof(WeaponSimple),"GetRelatedStatMultiplier");
        private static readonly FieldInfo OwnBeforeAttack=AccessTools.Field(typeof(UnitAvatar),"OnAttackUnitBeforeOperation");
        private static readonly FieldInfo LeaderBeforeAttack=AccessTools.Field(typeof(UnitAvatar),"OnAttackUnitBeforeOperation_AsLeader");
        // Plain values only; evaluated on the same worker as other damage rows.
        public sealed class Hit
        {
            public float BaseAttack, Element, ElementRatio, AttackFactor=1, DefenseBonus;
            public int GoldBonus,LeaderGoldBonus,FollowerBonus,NegotiationBonus,DebuffBonus,PoisonDebuffBonus;
            public float AfterFactors;
            public float[] SpawnFactors;
            public int JellyfishBonus, All, DashBonus, Critical, Flat, NonDirectCritical;
            public int Elite,ArmorIgnore;
            public float CriticalChance,WeaponCriticalChance;
            public float[] OwnElementChance,LeaderElementChance;
            internal double Chance()
            {
                double chance=CriticalChance+(Direct?WeaponCriticalChance:0);
                int element=(int)EffectiveElement;
                if(OwnElementChance!=null&&element>=0&&element<OwnElementChance.Length)chance+=OwnElementChance[element];
                if(LeaderElementChance!=null&&element>=0&&element<LeaderElementChance.Length)chance+=LeaderElementChance[element];
                return chance;
            }
            public bool Direct=true;
            public bool QuantizeSpawn=true;
            public bool ExecutionEnabled;
            public EDamageElementalType DamageElement=EDamageElementalType.Physical;
            public bool ForcedChaos;
            private EDamageElementalType EffectiveElement {get{return ForcedChaos?EDamageElementalType.Chaos:DamageElement;}}
            internal EDamageElementalType DisplayElement {get{return EffectiveElement;}}
            public int[] ElementCritical;
            public float[] OwnFrostbite,LeaderFrostbite;
            public DamageTooltip.Snapshot.RawStep[] OwnRawSteps,LeaderRawSteps;
            public float FrostbiteFactor {get{return ConditionalDamageProfiles.FrostbiteFactor(OwnFrostbite,EffectiveElement)*ConditionalDamageProfiles.FrostbiteFactor(LeaderFrostbite,EffectiveElement);}}
            public bool ElementalEffect;
            internal Hit Scaled(float factor)
            {
                var copy=(Hit)MemberwiseClone();copy.AttackFactor*=factor;copy.AfterFactors*=factor;return copy;
            }
            public DamageTooltip.Pair Evaluate(bool elite=false,float rawFactor=1,int extraCritical=0,float criticalMultiplier=1,bool canCritical=true,float projectileDamagePercent=0,int conditions=0,int? targetDefense=null,int ignoreDefense=0,int? flatOverride=null,int randomMode=0)
            {
                float attack=BaseAttack+Element*ElementRatio;
                if(SpawnFactors!=null)foreach(float f in SpawnFactors)attack*=f;
                if(QuantizeSpawn)
                {
                    int integerAttack=(int)attack;
                    integerAttack+=(int)(integerAttack*JellyfishBonus/100f);
                    attack=integerAttack;
                }
                float raw=attack*AttackFactor+AfterFactors;
                raw+=raw*projectileDamagePercent/100f;
                raw*=rawFactor;
                raw=DamageTooltip.Snapshot.ApplyRawSteps(raw,OwnRawSteps,EffectiveElement,conditions,0,randomMode);
                raw=DamageTooltip.Snapshot.ApplyRawSteps(raw,LeaderRawSteps,EffectiveElement,conditions,0,randomMode);
                float normal=raw;
                if(Direct)normal+=raw*(DashBonus/100f);
                float all=raw*(All/100f),follower=raw*FollowerBonus/100f,negotiation=raw*(NegotiationBonus/100f),eliteBonus=elite?raw*Elite/100f:0;
                normal+=all+follower+negotiation+eliteBonus;
                if(ElementalEffect)
                {
                    normal+=normal*DebuffBonus/100f;
                    normal+=normal*PoisonDebuffBonus/100f;
                }
                normal+=normal*(GoldBonus/100f);
                normal+=normal*(LeaderGoldBonus/100f);
                int criticalValue=Direct?Critical:NonDirectCritical;
                criticalValue+=extraCritical;
                if(ElementCritical!=null&&(int)EffectiveElement>=0&&(int)EffectiveElement<ElementCritical.Length)criticalValue+=ElementCritical[(int)EffectiveElement];
                if(criticalMultiplier!=1)criticalValue=(int)Math.Round(criticalValue*criticalMultiplier,MidpointRounding.ToEven);
                float critical=canCritical?criticalValue/100f:0;
                int flat=flatOverride??Flat;
                return new DamageTooltip.Pair{Normal=DamageTooltip.FinishOutgoing(normal,DefenseBonus,flat,targetDefense,ignoreDefense),Critical=DamageTooltip.FinishOutgoing(normal+normal*critical,DefenseBonus,flat,targetDefense,ignoreDefense),Execution=DamageTooltip.FinishOutgoing(normal+normal*critical*2f,DefenseBonus,flat,targetDefense,ignoreDefense)};
            }
            public string Signature()
            {
                var b=new StringBuilder();
                foreach(float v in new[]{BaseAttack,Element,ElementRatio,AttackFactor,DefenseBonus})b.Append('|').Append(v.ToString("R"));
                b.Append('|').Append(GoldBonus).Append('|').Append(LeaderGoldBonus).Append('|').Append(FollowerBonus).Append('|').Append(NegotiationBonus);
                b.Append('|').Append(JellyfishBonus).Append('|').Append(All).Append('|').Append(DashBonus).Append('|').Append(Critical).Append('|').Append(Flat);
                b.Append('|').Append(Direct).Append('|').Append(NonDirectCritical);
                b.Append('|').Append(QuantizeSpawn);
                b.Append('|').Append(ExecutionEnabled);
                b.Append('|').Append(Elite).Append('|').Append(ArmorIgnore);
                b.Append('|').Append(CriticalChance.ToString("R")).Append('|').Append(WeaponCriticalChance.ToString("R"));
                foreach(var values in new[]{OwnElementChance,LeaderElementChance})
                {b.Append("|element-chance:");if(values!=null)foreach(float value in values)b.Append(value.ToString("R")).Append(',');}
                b.Append('|').Append((int)DamageElement).Append("|element-critical:");
                b.Append(ForcedChaos).Append('|');
                if(ElementCritical!=null)foreach(int bonus in ElementCritical)b.Append(bonus).Append(',');
                b.Append("|own-frostbite:");if(OwnFrostbite!=null)foreach(float f in OwnFrostbite)b.Append(f.ToString("R")).Append(',');
                b.Append("|leader-frostbite:");if(LeaderFrostbite!=null)foreach(float f in LeaderFrostbite)b.Append(f.ToString("R")).Append(',');
                foreach(var steps in new[]{OwnRawSteps,LeaderRawSteps})
                {
                    b.Append("|raw-steps:").Append(steps==null?0:steps.Length);
                    if(steps==null)continue;
                    foreach(var step in steps)
                    {
                        b.Append('|').Append(step.Kind).Append('|').Append(step.Percent.ToString("R")).Append('|').Append(step.Chance.ToString("R"));
                        b.Append('|').Append(step.Elements==null?0:step.Elements.Length);
                        if(step.Elements!=null)foreach(bool element in step.Elements)b.Append(element?'1':'0');
                    }
                }
                b.Append('|').Append(ElementalEffect).Append('|').Append(DebuffBonus).Append('|').Append(PoisonDebuffBonus);
                b.Append('|').Append(AfterFactors.ToString("R"));
                b.Append('|').Append(SpawnFactors==null?0:SpawnFactors.Length);
                if(SpawnFactors!=null)foreach(float f in SpawnFactors)b.Append('|').Append(f.ToString("R"));
                return b.ToString();
            }
        }
        private static int Gold(UnitAvatar avatar)
        {
            if(avatar.GetCustomStatUnsafe("GOLDHAND")<=0)return 0;
            int step=KeywordDatabase.GetConstValue("GOLDHANDLEAF");
            int bonus=step>0?avatar.Money/step:0;
            if(avatar.GetCustomStatUnsafe("GOLDHANDUNLIMIT")<=0)bonus=Math.Min(bonus,KeywordDatabase.GetConstValue("GOLDHANDMAX"));
            return bonus;
        }
        private static Hit Capture(UnitAvatar unit,PlayerAvatar leader,float factor)
        {
            int ownCritical=50+unit.GetCustomStat(ECustomStat.CriticalDamageBonus)+unit.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE");
            int amp=unit.GetCustomStatUnsafe("WEAPONCRITICALDAMAGEAMPLIFY");
            if(amp>0)ownCritical+=(int)(ownCritical*amp/100f);
            int contribution=leader.GetCustomStatUnsafe("FOLLOWERCRITICALCONTRIBUTE");
            int leaderCritical=contribution>0?(int)(leader.GetCustomStat(ECustomStat.CriticalDamageBonus)*contribution*.01f):0;
            ownCritical+=leaderCritical;
            float ownDirect,leaderDirect;
            var ownChance=ConditionalDamageProfiles.CaptureCriticalChance(unit,OwnBeforeAttack,out ownDirect);
            var leaderChance=ConditionalDamageProfiles.CaptureCriticalChance(leader,LeaderBeforeAttack,out leaderDirect);
            return new Hit{AttackFactor=factor,
                OwnElementChance=ownChance,LeaderElementChance=leaderChance,
                CriticalChance=unit.GetCustomStat(ECustomStat.Critical)/100f+leader.GetCustomStatUnsafe("FOLLOWERCRITICAL")/100f+
                    (contribution>0?leader.GetCustomStat(ECustomStat.Critical)*contribution*.0001f:0),
                WeaponCriticalChance=unit.GetCustomStatUnsafe("WEAPONCRITICAL")/100f+ownDirect+leaderDirect,
                ForcedChaos=unit.isForcedChaosDamage,
                ElementCritical=CaptureElementCritical(unit,leader),
                OwnFrostbite=ConditionalDamageProfiles.CaptureFrostbite(unit,OwnBeforeAttack),
                LeaderFrostbite=ConditionalDamageProfiles.CaptureFrostbite(leader,LeaderBeforeAttack),
                OwnRawSteps=ConditionalDamageProfiles.CaptureFrostbiteSteps(unit,OwnBeforeAttack),
                LeaderRawSteps=ConditionalDamageProfiles.CaptureFrostbiteSteps(leader,LeaderBeforeAttack),
                DebuffBonus=unit.GetCustomStatUnsafe("DEBUFFDAMAGE"),PoisonDebuffBonus=unit.GetCustomStatUnsafe("POISONDEBUFFDAMAGEBONUS"),
                Elite=unit.GetCustomStatUnsafe("ELITEDAMAGE"),ArmorIgnore=unit.GetCustomStatUnsafe("IGNOREDEFENSE")+leader.GetCustomStatUnsafe("IGNOREDEFENSE"),
                ExecutionEnabled=(contribution>0?leader.GetCustomStat(ECustomStat.EXECUTION):unit.GetCustomStat(ECustomStat.EXECUTION))>0,
                All=unit.GetCustomStat(ECustomStat.AllDamageBonus)+leader.GetCustomStat(ECustomStat.AllDamageBonus),
                FollowerBonus=leader.GetCustomStatUnsafe("FOLLOWERDAMAGE"),NegotiationBonus=leader.GetCustomStat(ECustomStat.ADVANCED_NEGOTIATION)>0?leader.GetCustomStat(ECustomStat.Negotiation):0,
                DashBonus=unit.GetCustomStatUnsafe("WEAPONDAMAGEBONUSBYDASHCOUNT")*unit.GetCustomStatUnsafe("DASHCOUNT"),
                Critical=ownCritical,NonDirectCritical=50+unit.GetCustomStat(ECustomStat.CriticalDamageBonus)+leaderCritical,Flat=unit.GetCustomStatUnsafe("TRUEDAMAGE")+leader.GetCustomStatUnsafe("TRUEDAMAGE"),GoldBonus=Gold(unit),LeaderGoldBonus=Gold(leader),
                DefenseBonus=unit.GetCustomStatUnsafe("DEFENSETOATTACK")>0?UnitAvatar.GetDamageReduction(1,unit.GetCustomStat(ECustomStat.DamageReduction)):0};
        }
        private static int[] CaptureElementCritical(UnitAvatar unit,PlayerAvatar leader)
        {
            int[] values=null;
            // Only leader-specific subscribers are inherited. Copying the leader's
            // ordinary attack list here would apply effects the native follower never receives.
            AddElementCritical(unit,OwnBeforeAttack,ref values);
            AddElementCritical(leader,LeaderBeforeAttack,ref values);
            return values;
        }
        private static void AddElementCritical(UnitAvatar owner,FieldInfo field,ref int[] values)
        {
            if(!owner||owner.IsDead||field==null)return;
            var callbacks=field.GetValue(owner) as Delegate;
            if(callbacks==null)return;
            foreach(var callback in callbacks.GetInvocationList())
            {
                var effect=callback.Target as Charm_Burn;if(!effect)continue;
                if(values==null)values=new int[Enum.GetValues(typeof(EDamageElementalType)).Length];
                int bonus=effect.addCriticalDamageByLevel.SafeRandomAccess(effect.CurrentLevelToIdx());
                for(int i=0;i<values.Length;i++)if(DamageInstance.IsSameElementalType((EDamageElementalType)i,effect.targetElementalType))values[i]+=bonus;
            }
        }
        internal static bool TrySummon(ActiveSkill_Summon skill,int level,float power,int cost,PlayerAvatar p,out string text)
        {
            text=null;
            var unit=skill.unitPrefab?skill.unitPrefab.GetComponent<Unit_Soldier>():null;
            // Derived soldier classes can replace the attack bonus and are audited separately.
            if(!unit||unit.GetType()!=typeof(Unit_Soldier)||!unit.fireData)return false;
            var hit=Capture(unit,p,1);
            hit.BaseAttack=skill.defaultDamage+(skill.additionalDamageByLevel.Length>0?skill.additionalDamageByLevel.SafeRandomAccess(level):0);
            hit.Element=p.GetCustomStat(skill.relatedDamage);hit.ElementRatio=skill.damagePercent*.01f;
            hit.SpawnFactors=new[]{1+p.GetCustomStat(ECustomStat.MagicDamageBonus)*.01f,1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")*.01f,cost>0?1+cost/10f*p.GetCustomStatUnsafe("MAGICMP")*.01f:1,power};
            var attacks=new StringBuilder();AppendFire(attacks,p,hit,unit.fireData,"소환 동료 기본 공격");
            text="소환 동료 기본 공격 1타 · 일반 / 치명타\n"+attacks+"\n소환 공격력의 소수점 절삭·동료 피해·교섭·치명타 전달 반영\n소환 "+skill.summonCount+"마리 · 지속 "+skill.summonTimer.time.ToString("0.#")+"초\n시전 비용 MP "+cost+" 기준 · 적 방어 적용 전";
            return true;
        }
        internal static string WeaponSummon(NewWeaponFireData_Summon fire,DamageTooltip.Hit input,PlayerAvatar p)
        {
            var unit=fire.summonPrefab?fire.summonPrefab.GetComponent<UnitAvatar>():null;
            if(!unit)return "소환 대상 데이터 없음";
            var hit=Capture(unit,p,1);
            // Weapon fire has already applied damageMultiplier/finalDamageMultiplier.
            // Native summon stores that result as an integer attack stat. It never
            // invokes the weapon's onCreateAttack projectile callback.
            hit.BaseAttack=input.Raw+input.ResourceAmount*input.ResourcePerUnit;
            hit.SpawnFactors=input.Factors;
            string attacks;
            if(!TryAttacks(unit,hit,p,out attacks))return "소환 대상 전용 공격 계산 필요: "+unit.unitID;
            UnitAvatar first;int count=fire.CountSummonedUnits(p,out first);
            var text=new StringBuilder("새로 소환할 동료의 공격별 피해 · 일반 / 치명타\n");
            text.AppendLine(attacks);
            text.Append("현재 같은 종류 ").Append(count).Append("마리 · 유지 한도 ").Append(fire.summonLimit).AppendLine("마리");
            if(count>=fire.summonLimit&&first)text.AppendLine("이 소환을 직접 실행하면 가장 먼저 발견한 동료를 교체");
            if(fire.isTemporaryUnit)text.Append("소환 ").Append(fire.temporaryUnitLifeTime.ToString("0.###")).AppendLine("초 후 체력 감소 시작");
            text.AppendLine("소환 시 공격력 소수점 절삭 · 동료 피해·교섭 보정 반영 · 소환 행동 자체의 직접 타격 없음");
            return text.ToString().TrimEnd();
        }
        internal static bool CaptureWeaponSummon(NewWeaponFireData_Summon fire,DamageTooltip.Hit input,PlayerAvatar player,
            out UnitAvatar unit,out Hit hit)
        {
            unit=fire&&fire.summonPrefab?fire.summonPrefab.GetComponent<UnitAvatar>():null;hit=null;
            if(!unit)return false;
            hit=Capture(unit,player,1);
            // Native stores the resolved fire damage as the follower's integer
            // attack. Keep the source formula as spawn factors so quantization
            // occurs in the worker before follower-only bonuses.
            hit.BaseAttack=input.Raw+input.ResourceAmount*input.ResourcePerUnit;
            hit.SpawnFactors=input.Factors;hit.AfterFactors=input.AfterFactors;
            return true;
        }
        internal static bool TryCharm(Charm_SummonUnit source,Charm_SummonUnit live,int level,PlayerAvatar p,out string text)
        {
            text=null;
            UnitAvatar unit;Hit hit;
            if(!CaptureCharm(source,live,level,p,out unit,out hit))return false;
            string attacks;if(!TryAttacks(unit,hit,p,out attacks))return false;
            bool summoned=live&&SummonedUnit.GetValue(live) as UnitAI_NewBasic;
            text="소환 동료 공격별 1타 · 일반 / 치명타\n"+attacks+"\n"+(summoned?"소환된 동료의 현재 능력치":"소환 전 기본 상태")+" 기준 · 적 방어 적용 전\n소환 공격력의 소수점 절삭·동료 피해·교섭·치명타 전달 반영";
            if(source.isJellyfish&&p.GetCustomStatUnsafe("JELLYFISHDOUBLEATTACK")>0)text+="\n연속 공격 활성: 각 공격이 실제 적중한 경우에만 합산";
            return true;
        }
        internal static bool CaptureCharm(Charm_SummonUnit source,Charm_SummonUnit live,int level,PlayerAvatar p,out UnitAvatar unit,out Hit hit)
        {
            var summoned=live?SummonedUnit.GetValue(live) as UnitAI_NewBasic:null;
            unit=summoned?summoned.Avatar:source.unitPrefab?source.unitPrefab.GetComponent<UnitAvatar>():null;
            hit=null;if(!unit)return false;
            hit=Capture(unit,p,1);
            hit.BaseAttack=source.damageByLevel.SafeRandomAccess(source.LevelToIdx(level));
            if(!summoned&&live)hit.ForcedChaos=ReadChaoticMode(live);
            hit.SpawnFactors=new[]{1+(live && live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f};
            if(source.isJellyfish)hit.JellyfishBonus=p.GetCustomStatUnsafe("JELLYFISHBASICDAMAGE");
            return true;
        }
        internal static bool TryBallista(Charm_MiniBallista source,Charm_MiniBallista live,int level,PlayerAvatar p,out string text)
        {
            text=null;
            Unit_MiniBallista unit;Hit hit;int count;bool actual;
            if(!CaptureBallista(source,live,level,p,out unit,out hit,out count,out actual))return false;
            text="소형 발리스타 · 번개 탄환 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Follower=hit},unit.bulletPrefab,"탄환")+
                "\n동시 발사 "+count+"발 · 같은 적에게 실제 적중한 탄환만 합산\n동료 피해·교섭·치명타 전달 반영 · 적 방어 적용 전";
            int cloud=source.addCloudPercentByLevel.SafeRandomAccess(source.LevelToIdx(level));
            text+="\n적중 시 먹구름 추가 확률 "+cloud+"% · 먹구름 콤보 활성 필요";
            if(p.GetCustomStatUnsafe("ENHANCEDMINIBALLISTA")>0)text+="\n강화 활성: 유도·지형 관통·가드 성공 시 추가 발사";
            if(!actual)text+="\n표시 레벨로 소환할 때의 발사 수 기준";
            return true;
        }
        internal static bool CaptureBallista(Charm_MiniBallista source,Charm_MiniBallista live,int level,PlayerAvatar p,
            out Unit_MiniBallista unit,out Hit hit,out int count,out bool actual)
        {
            var summoned=live?BallistaUnit.GetValue(live) as Unit_MiniBallista:null;
            unit=summoned?summoned:source.unitPrefab?source.unitPrefab.GetComponent<Unit_MiniBallista>():null;
            hit=null;count=0;actual=false;if(!unit||!unit.bulletPrefab)return false;
            hit=Capture(unit,p,1);
            if(!summoned&&live)hit.ForcedChaos=ReadChaoticMode(live);
            // Summon and level-up use different native indices. For the actual
            // level, read the spawned count instead of replacing it silently.
            actual=summoned&&live.DisplayedLevel==level;
            count=actual?summoned.bulletCount:source.bulletCountByLevel.SafeRandomAccess(source.LevelToIdx(level));
            hit.BaseAttack=source.ballistaDamage;
            hit.DamageElement=EDamageElementalType.Lightning;
            hit.SpawnFactors=new[]{1+(live&&live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f};
            return true;
        }
        internal static bool TryLead(Charm_LeadNPC source,Charm_LeadNPC live,int level,PlayerAvatar p,out string text)
        {
            text=null;
            UnitAvatar unit;Hit hit;bool following;
            if(!CaptureLead(source,live,level,p,out unit,out hit,out following))return false;
            string attacks;if(!TryAttacks(unit,hit,p,out attacks))return false;
            text="동행 동료 공격별 1타 · 일반 / 치명타\n"+attacks+"\n"+(following?"현재 동료의 능력치·장비 효과 반영":"동행 전 기본 상태 · 소환 시 무작위 장비에 따라 달라질 수 있음")+"\n동행 증표 피해 증가·동료 피해·교섭·치명타 전달 반영 · 적 방어 적용 전";
            return true;
        }
        internal static bool CaptureLead(Charm_LeadNPC source,Charm_LeadNPC live,int level,PlayerAvatar p,out UnitAvatar unit,out Hit hit,out bool following)
        {
            var actual=live?Following.GetValue(live) as UnitAvatar:null;
            following=actual;unit=actual?actual:source.npcSocialID&&source.npcSocialID.avatarPrefab?source.npcSocialID.avatarPrefab.GetComponent<UnitAvatar>():null;
            hit=null;if(!unit)return false;
            hit=Capture(unit,p,1);hit.BaseAttack=unit.attack;
            if(!actual&&live)hit.ForcedChaos=ReadChaoticMode(live);
            // The live avatar already contains its currently applied contract bonus.
            hit.All+=source.levelBonusByLevel.SafeRandomAccess(source.LevelToIdx(level))-(actual?(int)LeadBonus.GetValue(live):0);
            return true;
        }
        internal static DpsProjectiles.Result CaptureFollowerFire(Hit source,NewWeaponFireData fire,PlayerAvatar player,float factor=1)
        {
            if(!fire)return new DpsProjectiles.Result{Unavailable="동료 공격 데이터 없음"};
            var prepared=source.Scaled(factor*fire.damageMultiplier*fire.CalculateFinalDamageMultiplier(0));
            prepared.DamageElement=fire.damageElementalType;
            return DpsProjectiles.Weapon(new DamageTooltip.WeaponAttackSample{Fire=fire,
                Hits=new System.Collections.Generic.List<DamageTooltip.Hit>{new DamageTooltip.Hit{Follower=prepared}}},player,false);
        }
        internal static DpsProjectiles.Result CaptureFollowerWeaponFire(UnitAvatar unit,Hit source,WeaponSimple weapon,NewWeaponFireData fire,int kind,PlayerAvatar player)
        {
            if(!unit||!weapon||!fire)return new DpsProjectiles.Result{Unavailable="동료 장착 무기 공격 데이터 없음"};
            object[] args={unit,fire.damageElementalType,fire.relatedStatFormula,EDamageElementalType.Normal};
            var hit=source.Scaled(1);hit.QuantizeSpawn=false;
            hit.BaseAttack=(float)Related.Invoke(weapon,args);hit.Element=0;hit.ElementRatio=0;
            hit.DamageElement=fire.useElementalTypeFromRelatedStatFormula?(EDamageElementalType)args[3]:fire.damageElementalType;
            int mp=WeaponBuffPreview.UsedMp(weapon,kind,unit);
            hit.SpawnFactors=new[]{1+unit.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                1+unit.GetCustomStat(kind==0?ECustomStat.BasicAttackDamageBonus:kind==1?ECustomStat.DashAttackDamageBonus:ECustomStat.SpecialAttackDamageBonus)/100f,
                mp>0?1+unit.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f:1,
                1+unit.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f};
            return CaptureFollowerFire(hit,fire,player,1);
        }
        internal static bool ReadChaoticMode(Charm_Basic charm)
        {
            var field=AccessTools.Field(charm.GetType(),"chaoticMode");
            return field!=null&&(bool)field.GetValue(charm);
        }
        private static bool TryAttacks(UnitAvatar unit,Hit hit,PlayerAvatar p,out string text)
        {
            var b=new StringBuilder();text=null;
            if(unit.GetType()==typeof(Unit_WeaponEquipped)||unit.GetType()==typeof(Unit_Villager))
            {
                var controller=unit.GetComponent<WeaponControllerSimple>();
                var weapon=controller?controller.currentWeapon:null;
                if(!weapon){text="현재 장착 무기 없음 · 장착 무기 결정 후 공격 피해 계산";return true;}
                AppendWeaponAttacks(b,p,unit,hit,weapon,weapon.basicComboAttacks,0,"동료 평타");
                AppendWeaponAttacks(b,p,unit,hit,weapon,weapon.dashAttacks,1,"동료 돌진");
                AppendWeaponAttacks(b,p,unit,hit,weapon,weapon.specialAttacks,2,"동료 특수");
            }
            else if(unit.GetType()==typeof(Unit_Soldier)||unit.GetType()==typeof(Unit_CarrotRabbitSoldier))AppendFire(b,p,hit,((Unit_Soldier)unit).fireData,"기본 공격");
            else if(unit.GetType()==typeof(Unit_Amethyst))
            {
                hit=hit.Scaled(1);hit.DamageElement=EDamageElementalType.Normal;
                b.AppendLine(ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Follower=hit},((Unit_Amethyst)unit).laserPrefab,"수정 레이저 1회"));
                b.AppendLine("조준 후 0.2초 뒤 대상이 남아 있을 때 발사 · 발사 시 소환수 자신에게 기본 피해 5");
            }
            else if(unit.GetType()==typeof(Unit_Grenadier))AppendFire(b,p,hit,((Unit_Grenadier)unit).fireData,"기본 탄환");
            else if(unit.GetType()==typeof(Unit_DuckSmith))
            {
                var smith=(Unit_DuckSmith)unit;
                AppendFire(b,p,hit,smith.attackFireData,"내려치기");AppendFire(b,p,hit,smith.spinAttackFireData,"회전 공격");
            }
            else if(unit.GetType()==typeof(Unit_DuckMerchant))
            {
                var merchant=(Unit_DuckMerchant)unit;
                for(int i=0;i<merchant.bulletPrefab.Length;i++)
                    b.AppendLine(ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Follower=hit},merchant.bulletPrefab[i],"투척 종류 "+(i+1)));
            }
            else if(unit.GetType()==typeof(Unit_MoleChieftain))
            {
                var mole=(Unit_MoleChieftain)unit;
                AppendFire(b,p,hit,mole.basicAttackFireData,"첫 휘두르기");
                AppendFire(b,p,hit.Scaled(mole.isCompanionBased?1:.6f),mole.secondBasicAttackFireData,"두 번째 휘두르기");
                AppendFire(b,p,hit.Scaled(mole.isCompanionBased?1:.5f),mole.throwDynamiteFireData,"다이너마이트 1개");
                AppendFire(b,p,hit.Scaled(mole.isCompanionBased?1:.75f),mole.drillAttackFireData,"드릴 1회");
                AppendFire(b,p,hit,mole.windmillFireData,"회전 공격 1회");
                if(mole.handStompPrefab)b.AppendLine(ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Follower=hit},mole.handStompPrefab,"내려찍기 파동"));
                if(mole.drumStage)AppendFire(b,p,hit.Scaled(mole.isCompanionBased?1:.6f),mole.drumStage.stoneFireData,"북 무대 낙석");
                b.AppendLine("연속 공격·분산 투사체는 실제 적중 횟수만 합산");
            }
            else return false;
            text=b.ToString().TrimEnd();return b.Length>0;
        }
        private static void AppendWeaponAttacks(StringBuilder text,PlayerAvatar p,UnitAvatar unit,Hit source,WeaponSimple weapon,NewWeaponFireData[] attacks,int kind,string label)
        {
            attacks=WeaponBuffPreview.Attacks(weapon,attacks,kind);
            int last=WeaponBuffPreview.FinalCombo(weapon);
            int mp=WeaponBuffPreview.UsedMp(weapon,kind,unit);
            for(int i=0;i<attacks.Length&&(kind!=0||i<=last);i++)
            {
                var fire=attacks[i];if(!fire)continue;
                object[] args={unit,fire.damageElementalType,fire.relatedStatFormula,EDamageElementalType.Normal};
                var hit=source.Scaled(1);hit.QuantizeSpawn=false;
                hit.BaseAttack=(float)Related.Invoke(weapon,args);hit.Element=0;hit.ElementRatio=0;
                hit.DamageElement=fire.useElementalTypeFromRelatedStatFormula?(EDamageElementalType)args[3]:fire.damageElementalType;
                hit.SpawnFactors=new[]{1+unit.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                    1+unit.GetCustomStat(kind==0?ECustomStat.BasicAttackDamageBonus:kind==1?ECustomStat.DashAttackDamageBonus:ECustomStat.SpecialAttackDamageBonus)/100f,
                    mp>0?1+unit.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f:1,
                    1+unit.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,
                    1+(kind==0?weapon.GetAdditionalBasicAttackDamagePercent(i):kind==2?weapon.GetAdditionalSpecialAttackDamagePercent(i):0)/100f,
                    kind==0&&(i==last||fire.forceFinalCombo)?1+unit.GetCustomStatUnsafe("LASTBASICATTACKDAMAGE")/100f:1};
                hit.AttackFactor*=fire.CalculateFinalDamageMultiplier(mp);
                AppendFire(text,p,hit,fire,label+" "+(i+1),true);
                AppendWeaponElements(text,p,unit,source,weapon,fire,kind);
            }
            text.AppendLine("동료 현재 장비·능력치 기준 · 무기 고유 추가타는 별도 계산 대상");
        }
        private static void AppendWeaponElements(StringBuilder text,PlayerAvatar p,UnitAvatar unit,Hit source,WeaponSimple weapon,NewWeaponFireData fire,int kind)
        {
            bool eclipse=false;
            if(WeaponBuffPreview.EclipseBuff(weapon))foreach(var addon in weapon.addons)if(addon is WeaponAddonKatana_FlameSword_Eclipse)eclipse=true;
            foreach(var addon in weapon.addons)
            {
                var extra=addon as WeaponAddonCommon_AdditionalElementalDamage;if(!extra)continue;
                int percent=extra.additionalDamagePercent+unit.GetCustomStatUnsafe("ADDITIONALELEMENTALDAMAGEBONUS");
                if(eclipse)percent+=KeywordDatabase.GetConstValue("katanaEclipseElementalDamageBonusPercent");
                var hit=source.Scaled(1);hit.QuantizeSpawn=false;hit.Direct=false;
                hit.BaseAttack=unit.GetCustomStat(extra.statId);hit.Element=0;hit.ElementRatio=0;
                hit.DamageElement=extra.elementalType;
                hit.SpawnFactors=new[]{percent/100f,fire.damageMultiplier,
                    1+unit.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                    1+unit.GetCustomStat(kind==0?ECustomStat.BasicAttackDamageBonus:kind==1?ECustomStat.DashAttackDamageBonus:ECustomStat.SpecialAttackDamageBonus)/100f,
                    1+unit.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,
                    eclipse&&kind==0?1+unit.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f:1};
                if(eclipse&&kind==0)hit.NonDirectCritical+=unit.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE");
                string element=extra.elementalType==EDamageElementalType.Fire?"화염":extra.elementalType==EDamageElementalType.Ice?"냉기":extra.elementalType==EDamageElementalType.Lightning?"번개":"속성";
                text.Append("  ").Append(element).Append(" 추가타 1회: ").AppendLine(DamageTooltip.Hits(p,new DamageTooltip.Hit{Follower=hit}));
            }
        }
        private static void AppendFire(StringBuilder b,PlayerAvatar p,Hit source,NewWeaponFireData fire,string label,bool resolvedElement=false)
        {
            if(!fire)return;
            var hit=new DamageTooltip.Hit{Follower=source.Scaled(fire.damageMultiplier*fire.CalculateFinalDamageMultiplier(0))};
            if(!resolvedElement)hit.Follower.DamageElement=fire.damageElementalType;
            if(fire.GetType()==typeof(NewWeaponFireData_Bullet)||fire.GetType()==typeof(NewWeaponFireData_BulletSpread)||fire.GetType()==typeof(NewWeaponFireData_BulletBurst))
            {
                string description;ProjectileDamageProfiles.DescribeWeaponFire(p,hit,fire,out description);
                b.Append(label).Append(": ").AppendLine(description);
            }
            else if(fire.GetType()==typeof(NewWeaponFireData_MeleeAttack))
            {
                ProjectileDamageProfiles.PrepareMelee(hit,fire);
                b.Append(label).Append(": ").AppendLine(ProjectileDamageProfiles.DescribeMelee(p,hit,fire));
            }
            else b.Append(label).AppendLine(": 전용 발사 방식 계산 필요");
        }
    }
    [HarmonyPatch]
    internal static class TooltipCompanionChaosChangedPatch
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_SummonUnit),"SetChaoticMode");
            yield return AccessTools.Method(typeof(Charm_MiniBallista),"SetChaoticMode");
            yield return AccessTools.Method(typeof(Charm_LeadNPC),"SetChaoticMode");
        }
        private static void Prefix(Charm_Basic __instance,out bool __state){__state=FollowerDamageProfiles.ReadChaoticMode(__instance);}
        private static void Postfix(Charm_Basic __instance,bool __state)
        {
            if(__state!=FollowerDamageProfiles.ReadChaoticMode(__instance)&&DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);
        }
    }
    [HarmonyPatch(typeof(Charm_SummonUnit),"UpdateUnitDamageServer")]
    internal static class TooltipSummonDamageChangedPatch
    {
        private static void Postfix(){if(DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);}
    }
}
