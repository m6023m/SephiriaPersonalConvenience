using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SephiriaDicePreview
{
    // Capture only definitions and attacker stats. Never apply a debuff to a unit.
    internal sealed class DebuffDamagePreview
    {
        private static readonly System.Reflection.FieldInfo AddonEnabled=HarmonyLib.AccessTools.Field(typeof(WeaponAddon),"isEnabled");
        private static readonly System.Reflection.FieldInfo FrostDamageTimer=HarmonyLib.AccessTools.Field(typeof(CharacterDebuff_Frostbite),"frostDamageTimer");
        private sealed class SourceGroup
        {
            internal CharacterDebuff Prefab;
            internal int Applications=int.MaxValue;
            internal int MaximumApplications;
            internal readonly List<string> Triggers=new List<string>();
            internal readonly List<WeaponAddon> Sources=new List<WeaponAddon>();
        }
        internal string Name,Condition,Unavailable;
        internal int Row,Stacks;
        internal double Interval,Duration;
        internal bool OnExpiration;
        internal bool FullOnly,CurrentOnly;
        internal bool Visible(bool full){return full?!CurrentOnly:!FullOnly;}
        internal double ApplicationChance=-1;
        internal double ApplicationPeriod;
        internal int ApplicationCycleIndex=-1;
        internal double[] ApplicationOffsets;
        internal int AddedStacks,MaximumStacks;
        internal bool RenewOnApplication;
        internal bool ResetAtMaximum;
        internal bool OneShotAtMaximum;
        internal double StunDuration;
        internal string Signature()
        {
            string signature=Name+"|"+Condition+"|"+Unavailable+"|"+Row+"|"+Stacks+"|"+OnExpiration+"|"+FullOnly+"|"+CurrentOnly+"|"+Interval.ToString("R",CultureInfo.InvariantCulture)+"|"+Duration.ToString("R",CultureInfo.InvariantCulture)+"|"+ApplicationChance.ToString("R",CultureInfo.InvariantCulture);
            signature+="|applications:"+ApplicationPeriod.ToString("R",CultureInfo.InvariantCulture)+":"+AddedStacks+":"+MaximumStacks+":"+RenewOnApplication+":"+ResetAtMaximum+":"+ApplicationCycleIndex;
            if(ApplicationOffsets!=null)foreach(double offset in ApplicationOffsets)signature+=","+offset.ToString("R",CultureInfo.InvariantCulture);
            return signature;
        }
        internal string Render(DamageTooltip.Snapshot snapshot,bool dps,DpsNumbers.RechargeSchedule recharge=null)
        {
            var text=new StringBuilder("\n<color=#81E6C4>"+Name+"</color>\n");
            if(Unavailable!=null)return text.Append(Unavailable).ToString();
            if(dps&&ApplicationCycleIndex>=0&&recharge==null)return text.Append("연결된 공격의 발동 주기를 계산할 수 없음").ToString();
            double ticks=OnExpiration?1:Interval>0?Math.Max(0,Math.Floor(Duration/Interval+1e-7)):0;
            double applicationPeriod=ApplicationPeriod;double[] applicationOffsets=ApplicationOffsets;
            if(dps&&recharge!=null&&applicationOffsets!=null)
            {
                long size=(long)recharge.Offsets.Length*applicationOffsets.Length;
                if(size>1000000)throw new InvalidOperationException("Debuff application schedule too large");
                var combined=new double[(int)size];int at=0;
                foreach(double activation in recharge.Offsets)foreach(double offset in applicationOffsets)combined[at++]=activation+offset;
                applicationPeriod=recharge.Period;applicationOffsets=combined;
            }
            double[] rates=dps&&!OnExpiration&&ApplicationChance<0&&applicationPeriod>0&&Interval>0?DpsDebuffs.TickRates(applicationPeriod,applicationOffsets,Duration,Interval,AddedStacks,MaximumStacks,RenewOnApplication,ResetAtMaximum):null;
            double[] accumulated=dps&&OnExpiration&&applicationPeriod>0&&Duration>0?
                DpsDebuffs.AccumulatedRates(applicationPeriod,applicationOffsets,Duration,AddedStacks,MaximumStacks,RenewOnApplication,
                    (stack,elapsed)=>new[]{ExpectedScaled(snapshot,false,stack,elapsed/Duration),ExpectedScaled(snapshot,true,stack,elapsed/Duration)}):null;
            var element=snapshot.Rows[Row][0].Element;
            for(int target=0;target<(dps?2:1);target++)
            {
                bool boss=target==1;
                var pair=dps?snapshot.Evaluate(snapshot.Rows[Row][0],boss,snapshot.Dps.StageArmor,snapshot.Dps.OwnerArmorIgnore):snapshot.Evaluate(snapshot.Rows[Row][0]);
                string label=dps?(boss?"보스: ":"일반 몬스터: "):"";
                if(OneShotAtMaximum)
                {
                    if(dps)
                    {
                        if(applicationPeriod<=0||applicationOffsets==null)
                        {text.Append(label).AppendLine("빙결 발동 주기를 계산할 수 없음");continue;}
                        double rate=DpsDebuffs.ResetRate(applicationPeriod,applicationOffsets,Duration,AddedStacks,MaximumStacks,RenewOnApplication);
                        double expected=snapshot.Expected(snapshot.Rows[Row][0],pair);
                        text.Append(label).Append("기대 DPS: ").Append(DamageTooltip.ColorText(Show(expected*rate),element)).AppendLine();
                    }
                    else text.Append("발동 시 ").Append(DamageTooltip.ColorText(Show(pair.Normal)+" / "+Show(pair.Critical),element)).AppendLine();
                    continue;
                }
                if(dps)
                {
                    if(accumulated!=null)
                    {text.Append(label).Append("기대 DPS: ").Append(DamageTooltip.ColorText(Show(accumulated[target]),element)).AppendLine();continue;}
                    if(rates!=null)
                    {
                        double total=0;
                        for(int stack=1;stack<rates.Length;stack++)
                        {
                            if(rates[stack]==0||Stacks<=0)continue;
                            total+=ExpectedScaled(snapshot,boss,stack,1)*rates[stack];
                        }
                        text.Append(label).Append("기대 DPS: ").Append(DamageTooltip.ColorText(Show(total),element)).AppendLine();
                        continue;
                    }
                    double expected=snapshot.Expected(snapshot.Rows[Row][0],pair);
                    if(ApplicationChance>=0)
                    {
                        double frequency=applicationPeriod>0&&applicationOffsets!=null?applicationOffsets.Length/applicationPeriod:1;
                        text.Append(label).Append("기대 DPS: ").Append(DamageTooltip.ColorText(Show(expected*ApplicationChance*frequency),element));
                        text.AppendLine(applicationPeriod>0?"":" × 초당 디버프 부여 횟수");
                    }
                    else
                        text.Append(label).Append(OnExpiration?"만료 반복 기대 DPS: ":"유지 중 기대 DPS: ").Append(DamageTooltip.ColorText(Show(Interval>0?expected/Interval:0),element)).AppendLine();
                    continue;
                }
                if(ApplicationChance>=0)
                {
                    text.Append(label).Append("발동 성공 시 ").Append(DamageTooltip.ColorText(Show(pair.Normal)+" / "+Show(pair.Critical),element));
                    text.Append(" · 부여당 기대 피해 ").Append(DamageTooltip.ColorText(Show(pair.Normal*ApplicationChance)+" / "+Show(pair.Critical*ApplicationChance),element)).AppendLine();
                    if(dps)text.Append("DPS: ").Append(Show(pair.Normal*ApplicationChance)).Append(" / ").Append(Show(pair.Critical*ApplicationChance)).AppendLine(" × 초당 디버프 부여 횟수");
                    continue;
                }
                text.Append(label).Append(OnExpiration?"만료 시 ":"틱당 ").Append(DamageTooltip.ColorText(Show(pair.Normal)+" / "+Show(pair.Critical),element));
                text.Append(" · 총 ").Append(DamageTooltip.ColorText(Show(pair.Normal*ticks)+" / "+Show(pair.Critical*ticks),element));
                if(dps)text.Append(OnExpiration?" · 만료 반복 DPS ":" · 유지 DPS ").Append(Show(Interval>0?pair.Normal/Interval:0)).Append(" / ").Append(Show(Interval>0?pair.Critical/Interval:0));
                text.AppendLine();
            }
            if(OneShotAtMaximum)
            {
                text.Append(MaximumStacks).Append("중첩 도달 시 발동");
                if(StunDuration>0)text.Append(" · 기절 ").Append(StunDuration.ToString("0.###",CultureInfo.InvariantCulture)).Append("초");
                return text.AppendLine().ToString();
            }
            if(ApplicationChance>=0)return text.ToString();
            text.Append(Stacks).Append("중첩 · ").Append(Duration.ToString("0.###",CultureInfo.InvariantCulture)).Append("초 · ").Append(ticks.ToString("0",CultureInfo.InvariantCulture)).AppendLine(OnExpiration?"회 만료 피해":"틱");
            return text.ToString();
        }
        internal double ExpectedDps(DamageTooltip.Snapshot snapshot,bool boss,DpsNumbers.RechargeSchedule recharge=null)
        {
            if(Unavailable!=null||!Visible(snapshot.FullConditions))return 0;
            if(ApplicationCycleIndex>=0&&recharge==null)return 0;
            double applicationPeriod=ApplicationPeriod;double[] applicationOffsets=ApplicationOffsets;
            if(recharge!=null&&applicationOffsets!=null)
            {
                long size=(long)recharge.Offsets.Length*applicationOffsets.Length;
                if(size>1000000)return 0;
                var combined=new double[(int)size];int at=0;
                foreach(double activation in recharge.Offsets)foreach(double offset in applicationOffsets)combined[at++]=activation+offset;
                applicationPeriod=recharge.Period;applicationOffsets=combined;
            }
            double[] rates=!OnExpiration&&ApplicationChance<0&&applicationPeriod>0&&Interval>0?
                DpsDebuffs.TickRates(applicationPeriod,applicationOffsets,Duration,Interval,AddedStacks,MaximumStacks,RenewOnApplication,ResetAtMaximum):null;
            double[] accumulated=OnExpiration&&applicationPeriod>0&&Duration>0?
                DpsDebuffs.AccumulatedRates(applicationPeriod,applicationOffsets,Duration,AddedStacks,MaximumStacks,RenewOnApplication,
                    (stack,elapsed)=>new[]{ExpectedScaled(snapshot,false,stack,elapsed/Duration),ExpectedScaled(snapshot,true,stack,elapsed/Duration)}):null;
            if(OneShotAtMaximum)
            {
                if(applicationPeriod<=0||applicationOffsets==null)return 0;
                double rate=DpsDebuffs.ResetRate(applicationPeriod,applicationOffsets,Duration,AddedStacks,MaximumStacks,RenewOnApplication);
                var pair=snapshot.Evaluate(snapshot.Rows[Row][0],boss,snapshot.Dps.StageArmor,snapshot.Dps.OwnerArmorIgnore);
                return snapshot.Expected(snapshot.Rows[Row][0],pair)*rate;
            }
            if(accumulated!=null)return accumulated[boss?1:0];
            if(rates!=null)
            {
                double total=0;
                for(int stack=1;stack<rates.Length;stack++)if(rates[stack]!=0&&Stacks>0)
                    total+=ExpectedScaled(snapshot,boss,stack,1)*rates[stack];
                return total;
            }
            var damage=snapshot.Evaluate(snapshot.Rows[Row][0],boss,snapshot.Dps.StageArmor,snapshot.Dps.OwnerArmorIgnore);
            double expected=snapshot.Expected(snapshot.Rows[Row][0],damage);
            if(ApplicationChance>=0)
            {
                double frequency=applicationPeriod>0&&applicationOffsets!=null?applicationOffsets.Length/applicationPeriod:1;
                return expected*ApplicationChance*frequency;
            }
            return Interval>0?expected/Interval:0;
        }
        private static string Show(double value){return Math.Floor(value).ToString("0",CultureInfo.InvariantCulture);}
        private double ExpectedScaled(DamageTooltip.Snapshot snapshot,bool boss,int stack,double factor)
        {
            if(Stacks<=0)return 0;
            var original=snapshot.Rows[Row][0];
            var hit=new DamageTooltip.Hit{Raw=original.Raw*(float)(stack*factor/Stacks),Element=original.Element,ElementalEffect=original.ElementalEffect,CanCritical=original.CanCritical};
            return snapshot.Expected(hit,snapshot.Evaluate(hit,boss,snapshot.Dps.StageArmor,snapshot.Dps.OwnerArmorIgnore));
        }

        internal static void CaptureArtifact(CharacterDebuff prefab,PlayerAvatar player,string source,int applications,double period=0,double spacing=0,int cycleIndex=-1)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            if(snapshot==null||applications<=0)return;
            if(!prefab)
            {snapshot.Debuffs.Add(new DebuffDamagePreview{Name=source+" · 디버프",Unavailable="부가 디버프 데이터 없음"});return;}
            if((prefab.ID=="BURN"||prefab.ID=="ELECTRIC")&&player.GetCustomStatUnsafe("PLASMAACTIVE")>0)
                prefab=UnitDatabase.GetDebuff("PLASMA");
            if(!prefab)
            {snapshot.Debuffs.Add(new DebuffDamagePreview{Name=source+" · 디버프",Unavailable="변환된 디버프 데이터 없음"});return;}
            int first=snapshot.Debuffs.Count;
            Capture(prefab,player,"아티팩트 적중 시 부여 · 직접 피해와 별도",1,applications);
            double[] offsets=null;
            if(period>0&&SupportsCadence(prefab))
            {
                offsets=new double[applications];
                for(int application=0;application<applications;application++)offsets[application]=application*spacing;
            }
            for(int i=first;i<snapshot.Debuffs.Count;i++)
            {
                var row=snapshot.Debuffs[i];row.Name=source+" · "+row.Name;
                if(offsets!=null)
                {
                    AttachCadence(row,prefab,player,period,offsets);
                    row.ApplicationCycleIndex=cycleIndex;
                }
            }
        }

        internal static void CaptureWeapon(WeaponSimple weapon,PlayerAvatar player)
        {
            if(DamageTooltip.CurrentCapture==null||weapon.addons==null)return;
            var groups=new Dictionary<int,SourceGroup>();
            var controller=player.GetComponent<WeaponControllerSimple>();
            bool liveEquipped=controller&&controller.currentWeapon==weapon;
            foreach(var addon in weapon.addons)
            {
                if(!addon)continue;
                if(liveEquipped&&!(bool)AddonEnabled.GetValue(addon))continue;
                CharacterDebuff prefab=null;string trigger=null;
                int applications=1,maximumApplications=1;
                var general=addon as WeaponAddonCommon_DebuffAttack;
                var special=addon as WeaponAddonCommon_SpecialAttackDebuff;
                var repeated=addon as WeaponAddonCommon_DebuffAttack_OnlySpecialAttack;
                if(general){prefab=general.debuffPrefab;trigger="직접 공격 적중 · 부여 확률 "+general.debuffPercent.ToString("0.###")+"%";}
                else if(special){prefab=special.debuffPrefab;trigger="특수 공격 적중 · 부여 확률 "+special.debuffPercent.ToString("0.###")+"%";}
                else if(repeated)
                {
                    prefab=repeated.debuffPrefab;applications=(int)Math.Max(0,Math.Ceiling(repeated.debuffCount));
                    trigger="특수 공격 적중 · "+applications+"회 부여 · "+repeated.additionalDebuffProjectildSwingID+" 추가 "+repeated.additionalDebuffCount+"회";
                    maximumApplications=applications+Math.Max(0,repeated.additionalDebuffCount);
                    if(applications==0)applications=Math.Max(0,repeated.additionalDebuffCount);
                    if(repeated.debuffCount<=0)trigger+=" · 지정된 공격에서만 부여";
                }
                if(!prefab||applications==0)continue;
                if((prefab.ID=="BURN"||prefab.ID=="ELECTRIC")&&player.GetCustomStatUnsafe("PLASMAACTIVE")>0)prefab=UnitDatabase.GetDebuff("PLASMA");
                if(!prefab)continue;
                SourceGroup group;int key=prefab.GetInstanceID();
                if(!groups.TryGetValue(key,out group)){group=new SourceGroup{Prefab=prefab};groups.Add(key,group);}
                group.Applications=Math.Min(group.Applications,applications);
                group.MaximumApplications=Math.Max(group.MaximumApplications,maximumApplications);
                group.Sources.Add(addon);
                if(!group.Triggers.Contains(trigger))group.Triggers.Add(trigger);
            }
            foreach(var group in groups.Values)
            {
                if(CaptureWeaponCadence(group,player))continue;
                Capture(group.Prefab,player,string.Join(" / ",group.Triggers.ToArray())+" · 부여 성공 기준 · 디버프 부여 제외 판정은 제외 · 여러 경로의 같은 효과를 중복 합산하지 않음",group.Applications,group.MaximumApplications);
            }
        }
        internal static int FullMagicWoundBonus(PlayerAvatar player)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            if(snapshot==null||!snapshot.FullConditions)return 0;
            var controller=player.GetComponent<WeaponControllerSimple>();
            var weapon=controller?controller.currentWeapon:null;
            if(!weapon||weapon.addons==null)return 0;
            int best=0;
            foreach(var addon in weapon.addons)
            {
                if(!addon||(AddonEnabled!=null&&!(bool)AddonEnabled.GetValue(addon)))continue;
                CharacterDebuff prefab=null;
                var direct=addon as WeaponAddonCommon_DebuffAttack;
                var special=addon as WeaponAddonCommon_SpecialAttackDebuff;
                var repeated=addon as WeaponAddonCommon_DebuffAttack_OnlySpecialAttack;
                if(direct)prefab=direct.debuffPrefab;
                else if(special)prefab=special.debuffPrefab;
                else if(repeated)prefab=repeated.debuffPrefab;
                var wound=prefab as CharacterDebuff_MagicWound;
                if(wound)best=Math.Max(best,Math.Max(0,wound.magicDamageBonusPerStack)*Math.Max(0,wound.maxStackCount));
            }
            return best;
        }
        private static bool CaptureWeaponCadence(SourceGroup group,PlayerAvatar player)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            if(snapshot.Dps==null||!SupportsCadence(group.Prefab))return false;
            foreach(var source in group.Sources)
            {
                var direct=source as WeaponAddonCommon_DebuffAttack;
                if(direct&&direct.debuffPercent>=100)continue;
                var chance=source as WeaponAddonCommon_SpecialAttackDebuff;
                if(chance&&chance.debuffPercent>=100)continue;
                if(source is WeaponAddonCommon_DebuffAttack_OnlySpecialAttack)continue;
                // Random application requires a distribution of stack/lifetime
                // states, not a deterministic schedule scaled by probability.
                return false;
            }
            foreach(var cycle in snapshot.Dps.Cycles)
            {
                if(cycle.Unavailable!=null)continue;
                string problem=null;
                foreach(var source in group.Sources)
                    problem=problem??(source is WeaponAddonCommon_DebuffAttack?cycle.DirectDebuffProblem:cycle.SpecialDebuffProblem);
                if(problem!=null)
                {
                    snapshot.Debuffs.Add(new DebuffDamagePreview{Name=cycle.Name+" · "+group.Prefab.ID,Unavailable=problem});
                    continue;
                }
                var offsets=new List<double>();
                foreach(var source in group.Sources)
                {
                    var contacts=source is WeaponAddonCommon_DebuffAttack?cycle.DirectDebuffContacts:cycle.SpecialDebuffContacts;
                    foreach(var contact in contacts)
                    {
                        var repeated=source as WeaponAddonCommon_DebuffAttack_OnlySpecialAttack;
                        double count=repeated?Math.Max(0,Math.Ceiling(repeated.debuffCount)):1;
                        if(repeated&&contact.SwingId==repeated.additionalDebuffProjectildSwingID)count+=Math.Max(0,repeated.additionalDebuffCount);
                        if(!DpsNumbers.Finite(count)||count>8192||offsets.Count+count>1000000)
                            throw new InvalidOperationException("Weapon debuff application count too large");
                        for(int i=0;i<count;i++)offsets.Add(contact.Time);
                    }
                }
                if(offsets.Count==0)continue;
                int first=snapshot.Debuffs.Count;
                Capture(group.Prefab,player,"",1,1);
                var times=offsets.ToArray();
                for(int i=first;i<snapshot.Debuffs.Count;i++)
                {
                    var row=snapshot.Debuffs[i];row.Name=cycle.Name+" · "+row.Name;
                    AttachCadence(row,group.Prefab,player,cycle.Seconds,times);
                }
            }
            return true;
        }
        private static bool SupportsCadence(CharacterDebuff prefab)
        {
            return prefab is CharacterDebuff_Burn||prefab is CharacterDebuff_Plasma||prefab is CharacterDebuff_Poison||prefab is CharacterDebuff_Wound||prefab is CharacterDebuff_Electric||prefab is CharacterDebuff_Frostbite;
        }
        private static void AttachCadence(DebuffDamagePreview row,CharacterDebuff prefab,PlayerAvatar player,double period,double[] offsets)
        {
            row.ApplicationPeriod=period;row.ApplicationOffsets=offsets;
            bool burn=prefab is CharacterDebuff_Burn,plasma=prefab is CharacterDebuff_Plasma;
            row.AddedStacks=burn||plasma?Math.Max(0,1+player.GetCustomStatUnsafe("BURNADD")):1;
            int maximum;
            if(plasma)maximum=2+player.GetCustomStatUnsafe("BURNSTACK")+player.GetCustomStatUnsafe("ELECTRICSTACK");
            else if(burn)maximum=2+player.GetCustomStatUnsafe("BURNSTACK");
            else if(prefab is CharacterDebuff_Poison)maximum=1+player.GetCustomStatUnsafe("POISONSTACK");
            else if(prefab is CharacterDebuff_Wound)maximum=4+player.GetCustomStatUnsafe("WOUNDSTACK");
            else if(prefab is CharacterDebuff_Frostbite)
            {
                maximum=Math.Max(0,5-player.GetCustomStatUnsafe("FREEZETHRESHOLD"));
                row.ResetAtMaximum=true;
            }
            else maximum=2+player.GetCustomStatUnsafe("ELECTRICSTACK");
            row.MaximumStacks=Math.Max(0,maximum);
            row.RenewOnApplication=prefab.renewDurationOnStacked;
        }
        // Each row is an alternative scenario, never an additive damage term.
        // Deduplicate after capping: an enhanced batch may already reach max stacks.
        private static List<KeyValuePair<int,string>> StackScenarios(int perApplication,int maximum,int applications,int maximumApplications)
        {
            var rows=new List<KeyValuePair<int,string>>();
            int initial=(int)Math.Min(maximum,(double)perApplication*applications);
            int enhanced=(int)Math.Min(maximum,(double)perApplication*maximumApplications);
            rows.Add(new KeyValuePair<int,string>(initial," 기본 부여 ("+applications+"회 성공)"));
            if(enhanced>initial)rows.Add(new KeyValuePair<int,string>(enhanced," 추가 부여 조건 충족 ("+maximumApplications+"회 성공)"));
            if(maximum>Math.Max(initial,enhanced))rows.Add(new KeyValuePair<int,string>(maximum," 최대 중첩 유지"));
            return rows;
        }
        private static void Capture(CharacterDebuff prefab,PlayerAvatar player,string condition,int applications,int maximumApplications)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            if((prefab.ID=="BURN"||prefab.ID=="ELECTRIC")&&player.GetCustomStatUnsafe("PLASMAACTIVE")>0)
                prefab=UnitDatabase.GetDebuff("PLASMA");
            if(!prefab){snapshot.Debuffs.Add(new DebuffDamagePreview{Name="디버프",Unavailable="변환된 디버프 데이터 없음"});return;}
            var plasma=prefab as CharacterDebuff_Plasma;
            if(plasma){CapturePlasma(plasma,player,condition,applications,maximumApplications);return;}
            var burn=prefab as CharacterDebuff_Burn;var poison=prefab as CharacterDebuff_Poison;
            var wound=prefab as CharacterDebuff_Wound;var electric=prefab as CharacterDebuff_Electric;
            var frost=prefab as CharacterDebuff_Frostbite;var magicWound=prefab as CharacterDebuff_MagicWound;
            if(magicWound)
            {
                int bonus=Math.Max(0,magicWound.magicDamageBonusPerStack)*Math.Max(0,magicWound.maxStackCount);
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name="마법 상처",Unavailable="자체 피해 없음 · 대상에게 주는 마법 피해를 최대 "+bonus+"% 증가",Condition=condition});
                return;
            }
            if(!burn&&!poison&&!wound&&!electric&&!frost)
            {snapshot.Debuffs.Add(new DebuffDamagePreview{Name=prefab.ID,Unavailable="디버프 전용 피해·지속 시간 연결 필요",Condition=condition});return;}
            float raw;EDamageElementalType element;double interval;
            int maximum,initial=1,durationBonus=player.GetCustomStat(ECustomStat.DebuffDuration);
            double duration=prefab.defaultDuration*(1+durationBonus/100d);
            string name=burn?(player.GetCustomStatUnsafe("BLUEBURNCHANGE")>0?"푸른 화상":"화상"):poison?"중독":wound?"출혈":frost?"동상":"감전";
            if(burn)
            {
                raw=CharacterDebuff_Burn.CalculateTickDamage(player,out element);
                double speed=1+player.GetCustomStatUnsafe("BURNSPEED")/100d;
                interval=speed>0?burn.tickTimer.time/speed:0;
                maximum=Math.Max(0,2+player.GetCustomStatUnsafe("BURNSTACK"));
                initial=Math.Min(maximum,Math.Max(0,1+player.GetCustomStatUnsafe("BURNADD")));
                durationBonus+=player.GetCustomStatUnsafe("BURNDURATION");
                duration=prefab.defaultDuration*(1+durationBonus/100d);
            }
            else if(poison)
            {
                // isSystemDamage suppresses damage feedback/received-damage
                // bookkeeping and bypasses protection points. Native outgoing
                // damage and armor math are unchanged for this target model.
                raw=poison.damage*(1+player.GetCustomStatUnsafe("POISONDAMAGE")/100f);
                element=EDamageElementalType.Chaos;interval=poison.poisonTickTimer.time;
                maximum=Math.Max(0,1+player.GetCustomStatUnsafe("POISONSTACK"));initial=Math.Min(initial,maximum);
            }
            else if(wound)
            {
                raw=wound.damage*(1+player.GetCustomStatUnsafe("WOUNDDAMAGE")/100f);
                element=EDamageElementalType.Chaos;interval=wound.tickTimer.time;
                maximum=Math.Max(0,4+player.GetCustomStatUnsafe("WOUNDSTACK"));initial=Math.Min(initial,maximum);
            }
            else if(frost)
            {
                int damagePercent=player.GetCustomStatUnsafe("FROSTBITEDAMAGE");
                int threshold=Math.Max(0,5-player.GetCustomStatUnsafe("FREEZETHRESHOLD"));
                maximum=Math.Max(0,threshold-1);initial=Math.Min(initial,maximum);
                CaptureFreeze(frost,player,condition,threshold);
                if(damagePercent<=0||maximum<=0)
                {
                    snapshot.Debuffs.Add(new DebuffDamagePreview{Name=name,Unavailable="자체 피해 없음 · 최대 중첩 도달 시 빙결로 전환",Condition=condition});
                    return;
                }
                raw=player.GetCustomStat(ECustomStat.IceDamage)*damagePercent/100f;
                element=EDamageElementalType.Ice;
                var timer=FrostDamageTimer==null?null:FrostDamageTimer.GetValue(frost) as Timer;
                interval=timer==null?0:timer.time;
                condition+=" · "+threshold+"중첩 도달 시 빙결로 전환 후 동상 종료";
            }
            else
            {
                double baseDuration=prefab.defaultDuration-player.GetCustomStatUnsafe("ELECTRICQUICKNESS")/10d;
                duration=baseDuration*(1+durationBonus/100d);
                if(baseDuration<=.001||duration<=0)
                {snapshot.Debuffs.Add(new DebuffDamagePreview{Name=name,Unavailable="감전 지속시간의 프레임 단위 처리 필요"});return;}
                raw=player.GetCustomStat(ECustomStat.LightningDamage)*electric.statDamagePercent*.01f*(1+player.GetCustomStatUnsafe("ELECTRICDAMAGE")/100f)*(float)(duration/(baseDuration-.001));
                element=EDamageElementalType.Lightning;interval=duration;
                maximum=Math.Max(0,2+player.GetCustomStatUnsafe("ELECTRICSTACK"));initial=Math.Min(initial,maximum);
                condition+=" · 중간 재부여 없이 만료 · 재부여하면 당시 누적 시간 비율로 피해 후 누적 초기화";
                if(player.GetCustomStatUnsafe("ELECTRICLUCK")>0)
                {
                    int luckRow=snapshot.Rows.Count;
                    float luckRaw=player.GetCustomStat(ECustomStat.LightningDamage)*electric.statDamagePercent*.01f
                        *(1+player.GetCustomStatUnsafe("ELECTRICDAMAGE")/100f)*(1+durationBonus/100f);
                    snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=luckRaw,Element=EDamageElementalType.Lightning,ElementalEffect=true}});
                    snapshot.Debuffs.Add(new DebuffDamagePreview{Name="감전 행운 추가 피해",Row=luckRow,Stacks=1,
                        ApplicationChance=Math.Min(1,player.GetCustomStatUnsafe("ELECTRICLUCK")/100d),
                        Condition="디버프 부여 성공 시 별도 확률 발동 · 최대 중첩에서도 발동 · 현재 중첩 수를 곱하지 않음"});
                }
            }
            foreach(var scenario in StackScenarios(initial,maximum,applications,maximumApplications))
            {
                int count=scenario.Key;
                int row=snapshot.Rows.Count;
                snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=raw*count,Element=element,ElementalEffect=true}});
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name=name,Row=row,Stacks=count,OnExpiration=electric!=null,
                    FullOnly=count!=(int)Math.Min(maximum,(double)initial*applications),CurrentOnly=count!=maximum,
                    Interval=interval,Duration=Math.Max(0,duration),
                    ResetAtMaximum=frost!=null,
                    Condition=condition+" · "+(prefab.renewDurationOnStacked?"재부여 시 지속시간 갱신":"재부여 시 지속시간 유지")});
            }
        }
        private static void CaptureFreeze(CharacterDebuff_Frostbite frost,PlayerAvatar player,string condition,int threshold)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            var freeze=frost.freezeDebuffPrefab;
            if(!freeze||threshold<=0)
            {
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name="빙결 피해",Unavailable="빙결 피해 데이터 없음",Condition=condition});
                return;
            }
            float raw=player.GetCustomStat(ECustomStat.IceDamage)*freeze.damageMultiplier*(1+player.GetCustomStatUnsafe("FREEZEDAMAGE")/100f);
            int row=snapshot.Rows.Count;
            snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=raw,Element=EDamageElementalType.Ice,ElementalEffect=true}});
            snapshot.Debuffs.Add(new DebuffDamagePreview{Name="빙결 피해",Row=row,Stacks=1,OneShotAtMaximum=true,
                MaximumStacks=threshold,Duration=frost.defaultDuration*(1+player.GetCustomStat(ECustomStat.DebuffDuration)/100d),
                StunDuration=freeze.defaultDuration*(1+player.GetCustomStat(ECustomStat.DebuffDuration)/100d),Condition=condition});
        }
        private static void CapturePlasma(CharacterDebuff_Plasma prefab,PlayerAvatar player,string condition,int applications,int maximumApplications)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            int bonus=player.GetCustomStat(ECustomStat.DebuffDuration)+player.GetCustomStatUnsafe("BURNDURATION");
            double basis=prefab.defaultDuration-player.GetCustomStatUnsafe("ELECTRICQUICKNESS")/10d;
            double duration=basis*(1+bonus/100d);
            if(basis<=.001||duration<=0){snapshot.Debuffs.Add(new DebuffDamagePreview{Name="플라즈마",Unavailable="플라즈마 지속시간의 프레임 단위 처리 필요"});return;}
            EDamageElementalType element;
            float tickRaw=CharacterDebuff_Plasma.CalculateTickDamage(player,out element);
            // Native accumulated component intentionally performs integer / 2.
            float accumulatedRaw=((player.GetCustomStat(ECustomStat.FireDamage)+player.GetCustomStat(ECustomStat.LightningDamage))/2)*prefab.statDamagePercent*.01f;
            accumulatedRaw*= (1+(player.GetCustomStatUnsafe("BURNDAMAGE")+player.GetCustomStatUnsafe("ELECTRICDAMAGE"))/100f)*(1+player.GetCustomStatUnsafe("PLASMADAMAGE")/100f);
            int maximum=Math.Max(0,2+player.GetCustomStatUnsafe("BURNSTACK")+player.GetCustomStatUnsafe("ELECTRICSTACK"));
            int initial=Math.Min(maximum,Math.Max(0,1+player.GetCustomStatUnsafe("BURNADD")));
            double speed=1+player.GetCustomStatUnsafe("BURNSPEED")/100d;
            string description=condition+" · 화상·감전이 플라즈마로 통합 · "+(prefab.renewDurationOnStacked?"재부여 시 지속 갱신·누적 피해 후 초기화":"재부여 시 지속 유지");
            foreach(var scenario in StackScenarios(initial,maximum,applications,maximumApplications))
            {
                int count=scenario.Key;
                int row=snapshot.Rows.Count;
                snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=tickRaw*count,Element=element,ElementalEffect=true}});
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name="플라즈마 주기 피해",Row=row,Stacks=count,
                    FullOnly=count!=(int)Math.Min(maximum,(double)initial*applications),CurrentOnly=count!=maximum,
                    Duration=duration,Interval=speed>0?prefab.tickTimer.time/speed:0,Condition=description});
                row=snapshot.Rows.Count;
                snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=accumulatedRaw*count*(float)(duration/(basis-.001)),Element=EDamageElementalType.Lightning,ElementalEffect=true}});
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name="플라즈마 누적 피해",Row=row,Stacks=count,
                    FullOnly=count!=(int)Math.Min(maximum,(double)initial*applications),CurrentOnly=count!=maximum,
                    Duration=duration,Interval=duration,OnExpiration=true,Condition=description+" · 중간 재부여 없는 만료 기준"});
            }
            int luck=player.GetCustomStatUnsafe("ELECTRICLUCK");
            if(luck>0)
            {
                int row=snapshot.Rows.Count;
                snapshot.Rows.Add(new[]{new DamageTooltip.Hit{Raw=accumulatedRaw*(1+bonus/100f),Element=EDamageElementalType.Lightning,ElementalEffect=true}});
                snapshot.Debuffs.Add(new DebuffDamagePreview{Name="플라즈마 행운 추가 피해",Row=row,Stacks=1,ApplicationChance=Math.Min(1,luck/100d),
                    Condition="부여 성공마다 추가 확률 발동 · 최대 중첩에서도 발동 · 현재 중첩 수를 곱하지 않음"});
            }
        }
    }
}
