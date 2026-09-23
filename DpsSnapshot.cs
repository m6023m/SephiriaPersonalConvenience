using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SephiriaDicePreview
{
    // Built on the Unity thread once per invalidation; thereafter numbers/strings only.
    internal sealed class DpsSnapshot
    {
        internal sealed class Term
        {
            internal int Row,ProjectileArmorIgnore;
            internal double Count=1;
        }
        internal sealed class Cycle
        {
            internal string Name,Condition,Unavailable,ExternalRateLabel;
            internal double Seconds,Probability=1,Cooldown;
            internal double RechargeDashPeriod,RechargeDashReduction;
            internal double RechargeAttackPeriod,RechargeAttackReduction,RechargeSearch;
            internal double[] RechargeAttacks;
            internal bool RechargeAccumulates;
            internal double[] TriggerOffsets;
            internal int[] TriggerCounts;
            internal int BowAmmo;
            internal double BowFireInterval,BowReload,BowRepeatChance;
            internal int GroundRow=-1;
            internal double[] GroundStarts,GroundLengths;
            internal readonly List<Term> Terms=new List<Term>();
            internal readonly List<DpsProjectiles.DebuffContact> SpecialDebuffContacts=new List<DpsProjectiles.DebuffContact>();
            internal string SpecialDebuffProblem;
            internal readonly List<DpsProjectiles.DebuffContact> DirectDebuffContacts=new List<DpsProjectiles.DebuffContact>();
            internal string DirectDebuffProblem;
            internal double PlantingContacts;
            internal double LastPlantingContact;
            internal string PlantingContactProblem;
            internal DpsMagicNumbers.BoomerangCycle Boomerang;
        }
        internal int StageArmor,OwnerArmorIgnore;
        internal string StageName,BuffSummary;
        internal readonly List<Cycle> Cycles=new List<Cycle>();

        internal string Calculate(DamageTooltip.Snapshot damage)
        {
            var text=new StringBuilder();
            if(!string.IsNullOrEmpty(BuffSummary))text.AppendLine(BuffSummary);
            text.Append("스테이지 추가 방어 ").Append(StageArmor).AppendLine(" 적용");
            text.AppendLine("지속 공격 · 치명타 확률 반영 기대 DPS");
            var rechargeSchedules=new Dictionary<int,DpsNumbers.RechargeSchedule>();
            int cycleIndex=-1;
            foreach(var cycle in Cycles)
            {
                cycleIndex++;
                text.AppendLine("<color=#81E6C4>"+cycle.Name+"</color>");
                if(cycle.Unavailable!=null){text.AppendLine(cycle.Unavailable);continue;}
                if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0)throw new InvalidOperationException("Missing DPS cycle time: "+cycle.Name);
                double seconds=cycle.Seconds;
                double contactMultiplier=1;
                if(cycle.Boomerang!=null)cycle.Boomerang.Calculate(out seconds,out contactMultiplier);
                if(cycle.RechargeDashPeriod>0)seconds=DpsNumbers.DashReducedCooldown(seconds,cycle.RechargeDashPeriod,cycle.RechargeDashReduction);
                if(cycle.RechargeAttacks!=null)
                {
                    var schedule=cycle.RechargeAccumulates?
                        DpsNumbers.AdditiveRechargeSchedule(seconds-cycle.RechargeSearch,cycle.RechargeSearch,cycle.RechargeAttackPeriod,cycle.RechargeAttacks,cycle.RechargeAttackReduction):
                        DpsNumbers.AttackRechargeSchedule(seconds-cycle.RechargeSearch,cycle.RechargeSearch,cycle.RechargeAttackPeriod,cycle.RechargeAttacks,cycle.RechargeAttackReduction);
                    rechargeSchedules.Add(cycleIndex,schedule);seconds=schedule.MeanSeconds;
                }
                if(cycle.BowAmmo>0)
                {
                    double ordinary=DpsNumbers.ChargedBowSeconds(cycle.BowAmmo,cycle.BowFireInterval,cycle.BowReload,seconds,false);
                    double repeated=DpsNumbers.ChargedBowSeconds(cycle.BowAmmo,cycle.BowFireInterval,cycle.BowReload,seconds,true);
                    seconds=ordinary*(1-cycle.BowRepeatChance)+repeated*cycle.BowRepeatChance;
                }
                double rate=cycle.TriggerOffsets==null?1/seconds:DpsNumbers.ProcRate(cycle.TriggerOffsets,seconds,cycle.Cooldown,cycle.Probability,cycle.TriggerCounts);
                double normal=0,boss=0,normalFixed=0,bossFixed=0;EDamageElementalType? commonElement=null;bool mixedElement=false;
                var normalElements=new Dictionary<EDamageElementalType,double>();
                var bossElements=new Dictionary<EDamageElementalType,double>();
                foreach(var term in cycle.Terms)
                {
                    if(term.Row<0||term.Row>=damage.Rows.Count||!DpsNumbers.Finite(term.Count)||term.Count<0)throw new InvalidOperationException("Invalid DPS hit group");
                    foreach(var hit in damage.Rows[term.Row])
                    {
                        TrackElement(damage.GetDisplayElement(hit),ref commonElement,ref mixedElement);
                        int ignore=(hit.Follower!=null?hit.Follower.ArmorIgnore:OwnerArmorIgnore)+term.ProjectileArmorIgnore;
                        var ordinary=damage.Evaluate(hit,false,StageArmor,ignore);
                        var elite=damage.Evaluate(hit,true,StageArmor,ignore);
                        var ordinaryBase=damage.EvaluateWithoutFlat(hit,false,StageArmor,ignore);
                        var eliteBase=damage.EvaluateWithoutFlat(hit,true,StageArmor,ignore);
                        double count=term.Count*contactMultiplier;
                        double ordinaryValue=damage.Expected(hit,ordinary)*count,ordinaryBaseValue=damage.Expected(hit,ordinaryBase)*count;
                        double eliteValue=damage.Expected(hit,elite)*count,eliteBaseValue=damage.Expected(hit,eliteBase)*count;
                        normal+=ordinaryValue;boss+=eliteValue;
                        AddElement(normalElements,damage.GetDisplayElement(hit),ordinaryBaseValue);
                        AddElement(bossElements,damage.GetDisplayElement(hit),eliteBaseValue);
                        normalFixed+=ordinaryValue-ordinaryBaseValue;bossFixed+=eliteValue-eliteBaseValue;
                    }
                }
                if(cycle.GroundRow>=0)
                {
                    if(cycle.TriggerOffsets!=null)throw new InvalidOperationException("Chance-triggered ground requires its own coverage model");
                    double ticks=DpsNumbers.CoveredSeconds(cycle.Seconds,cycle.GroundStarts,cycle.GroundLengths)/.25;
                    foreach(var hit in damage.Rows[cycle.GroundRow])
                    {
                        TrackElement(damage.GetDisplayElement(hit),ref commonElement,ref mixedElement);
                        var ordinary=damage.Evaluate(hit,false,StageArmor,OwnerArmorIgnore);
                        var elite=damage.Evaluate(hit,true,StageArmor,OwnerArmorIgnore);
                        var ordinaryBase=damage.EvaluateWithoutFlat(hit,false,StageArmor,OwnerArmorIgnore);
                        var eliteBase=damage.EvaluateWithoutFlat(hit,true,StageArmor,OwnerArmorIgnore);
                        double ordinaryValue=damage.Expected(hit,ordinary)*ticks,ordinaryBaseValue=damage.Expected(hit,ordinaryBase)*ticks;
                        double eliteValue=damage.Expected(hit,elite)*ticks,eliteBaseValue=damage.Expected(hit,eliteBase)*ticks;
                        normal+=ordinaryValue;boss+=eliteValue;
                        AddElement(normalElements,damage.GetDisplayElement(hit),ordinaryBaseValue);
                        AddElement(bossElements,damage.GetDisplayElement(hit),eliteBaseValue);
                        normalFixed+=ordinaryValue-ordinaryBaseValue;bossFixed+=eliteValue-eliteBaseValue;
                    }
                }
                string suffix=cycle.ExternalRateLabel==null?"":" × "+cycle.ExternalRateLabel;
                string normalText=Show(normal*rate),bossText=Show(boss*rate);
                if(normalFixed!=0||bossFixed!=0||mixedElement)
                {
                    normalText=DamageTooltip.ColorHex(normalText,DamageTooltip.MixedColor(normalElements,normalFixed));
                    bossText=DamageTooltip.ColorHex(bossText,DamageTooltip.MixedColor(bossElements,bossFixed));
                }
                else if(commonElement.HasValue)
                {
                    normalText=DamageTooltip.ColorText(normalText,commonElement.Value);
                    bossText=DamageTooltip.ColorText(bossText,commonElement.Value);
                }
                text.Append("일반 몬스터: ").Append(normalText).AppendLine(suffix);
                text.Append("보스: ").Append(bossText).AppendLine(suffix);
                if(cycle.ExternalRateLabel!=null)text.AppendLine("표시 계수는 내림 · 계산에는 원래 소수값 적용");
                // Conditions are calculation metadata, not per-attack UI rows.
            }
            foreach(var debuff in damage.Debuffs)if(debuff.Visible(damage.FullConditions))
            {
                DpsNumbers.RechargeSchedule schedule;
                rechargeSchedules.TryGetValue(debuff.ApplicationCycleIndex,out schedule);
                text.AppendLine(debuff.Render(damage,true,schedule));
            }
            text.AppendLine("재장전·재사용 대기 포함");
            if(!damage.FullConditions)text.AppendLine("전타 적중·현재 강화 상태 유지·자원 충분 기준");
            text.Append("적 고유 방어·내성은 별도");
            return text.ToString();
        }
        internal double NormalTotal(DamageTooltip.Snapshot damage)
        {
            var rechargeSchedules=new Dictionary<int,DpsNumbers.RechargeSchedule>();
            double total=0;int cycleIndex=-1;
            foreach(var cycle in Cycles)
            {
                cycleIndex++;
                if(cycle.Unavailable!=null||!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0)continue;
                double seconds=cycle.Seconds,contactMultiplier=1;
                if(cycle.Boomerang!=null)cycle.Boomerang.Calculate(out seconds,out contactMultiplier);
                if(cycle.RechargeDashPeriod>0)seconds=DpsNumbers.DashReducedCooldown(seconds,cycle.RechargeDashPeriod,cycle.RechargeDashReduction);
                if(cycle.RechargeAttacks!=null)
                {
                    var schedule=cycle.RechargeAccumulates?
                        DpsNumbers.AdditiveRechargeSchedule(seconds-cycle.RechargeSearch,cycle.RechargeSearch,cycle.RechargeAttackPeriod,cycle.RechargeAttacks,cycle.RechargeAttackReduction):
                        DpsNumbers.AttackRechargeSchedule(seconds-cycle.RechargeSearch,cycle.RechargeSearch,cycle.RechargeAttackPeriod,cycle.RechargeAttacks,cycle.RechargeAttackReduction);
                    rechargeSchedules.Add(cycleIndex,schedule);seconds=schedule.MeanSeconds;
                }
                if(cycle.BowAmmo>0)
                {
                    double ordinary=DpsNumbers.ChargedBowSeconds(cycle.BowAmmo,cycle.BowFireInterval,cycle.BowReload,seconds,false);
                    double repeated=DpsNumbers.ChargedBowSeconds(cycle.BowAmmo,cycle.BowFireInterval,cycle.BowReload,seconds,true);
                    seconds=ordinary*(1-cycle.BowRepeatChance)+repeated*cycle.BowRepeatChance;
                }
                if(!DpsNumbers.Finite(seconds)||seconds<=0)continue;
                double rate=cycle.TriggerOffsets==null?1/seconds:DpsNumbers.ProcRate(cycle.TriggerOffsets,seconds,cycle.Cooldown,cycle.Probability,cycle.TriggerCounts);
                double sum=0;
                foreach(var term in cycle.Terms)
                {
                    if(term.Row<0||term.Row>=damage.Rows.Count||!DpsNumbers.Finite(term.Count)||term.Count<0)continue;
                    foreach(var hit in damage.Rows[term.Row])
                    {
                        int ignore=(hit.Follower!=null?hit.Follower.ArmorIgnore:OwnerArmorIgnore)+term.ProjectileArmorIgnore;
                        var pair=damage.Evaluate(hit,false,StageArmor,ignore);
                        sum+=damage.Expected(hit,pair)*term.Count*contactMultiplier;
                    }
                }
                if(cycle.GroundRow>=0&&cycle.GroundRow<damage.Rows.Count&&cycle.TriggerOffsets==null)
                {
                    double ticks=DpsNumbers.CoveredSeconds(cycle.Seconds,cycle.GroundStarts,cycle.GroundLengths)/.25;
                    foreach(var hit in damage.Rows[cycle.GroundRow])
                        sum+=damage.Expected(hit,damage.Evaluate(hit,false,StageArmor,OwnerArmorIgnore))*ticks;
                }
                total+=sum*rate;
            }
            foreach(var debuff in damage.Debuffs)
            {
                DpsNumbers.RechargeSchedule schedule;
                rechargeSchedules.TryGetValue(debuff.ApplicationCycleIndex,out schedule);
                total+=debuff.ExpectedDps(damage,false,schedule);
            }
            return DpsNumbers.Finite(total)&&total>0?total:0;
        }
        private static string Show(double value)
        {
            if(!DpsNumbers.Finite(value)||value<0)throw new InvalidOperationException("Invalid DPS result");
            return Math.Floor(value).ToString("0",CultureInfo.InvariantCulture);
        }
        private static void TrackElement(EDamageElementalType element,ref EDamageElementalType? common,ref bool mixed)
        {
            if(!common.HasValue)common=element;
            else if(common.Value!=element)mixed=true;
        }
        private static void AddElement(Dictionary<EDamageElementalType,double> values,EDamageElementalType element,double amount)
        {
            double current;values.TryGetValue(element,out current);values[element]=current+amount;
        }
        internal string Signature()
        {
            var text=new StringBuilder().Append(StageArmor).Append(':').Append(OwnerArmorIgnore).Append(':').Append(StageName).Append(':').Append(BuffSummary);
            foreach(var cycle in Cycles)
            {
                text.Append('|').Append(cycle.Name).Append(':').Append(cycle.Condition).Append(':').Append(cycle.Unavailable).Append(':').Append(cycle.ExternalRateLabel);
                text.Append(':').Append(cycle.Seconds.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(cycle.Probability.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(cycle.Cooldown.ToString("R",CultureInfo.InvariantCulture));
                text.Append(":dash-recharge:").Append(cycle.RechargeDashPeriod.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(cycle.RechargeDashReduction.ToString("R",CultureInfo.InvariantCulture));
                text.Append(":attack-recharge:").Append(cycle.RechargeAttackPeriod.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(cycle.RechargeAttackReduction.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(cycle.RechargeSearch.ToString("R",CultureInfo.InvariantCulture));
                text.Append(cycle.RechargeAccumulates?",additive":",pending");
                if(cycle.RechargeAttacks!=null)foreach(double offset in cycle.RechargeAttacks)text.Append(',').Append(offset.ToString("R",CultureInfo.InvariantCulture));
                if(cycle.TriggerOffsets!=null)foreach(double offset in cycle.TriggerOffsets)text.Append(',').Append(offset.ToString("R",CultureInfo.InvariantCulture));
                text.Append(";groups:");
                if(cycle.TriggerCounts!=null)foreach(int count in cycle.TriggerCounts)text.Append(',').Append(count);
                text.Append(";bow:").Append(cycle.BowAmmo).Append(',').Append(cycle.BowFireInterval.ToString("R",CultureInfo.InvariantCulture))
                    .Append(',').Append(cycle.BowReload.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(cycle.BowRepeatChance.ToString("R",CultureInfo.InvariantCulture));
                text.Append(";ground:").Append(cycle.GroundRow);
                if(cycle.Boomerang!=null)text.Append(";boomerang:").Append(cycle.Boomerang.Signature());
                if(cycle.GroundStarts!=null)foreach(double value in cycle.GroundStarts)text.Append(',').Append(value.ToString("R",CultureInfo.InvariantCulture));
                text.Append('/');
                if(cycle.GroundLengths!=null)foreach(double value in cycle.GroundLengths)text.Append(',').Append(value.ToString("R",CultureInfo.InvariantCulture));
                foreach(var term in cycle.Terms)text.Append(';').Append(term.Row).Append(',').Append(term.Count.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(term.ProjectileArmorIgnore);
            }
            return text.ToString();
        }
    }
}
