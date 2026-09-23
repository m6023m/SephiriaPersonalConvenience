using System;
using System.Collections.Generic;

namespace SephiriaDicePreview
{
    internal static class DpsCapture
    {
        private static readonly System.Reflection.FieldInfo KatanaGauge=HarmonyLib.AccessTools.Field(typeof(WeaponSimple_Katana),"currentKatanaGauge");
        internal static DpsSnapshot Start(PlayerAvatar player)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            if(snapshot==null)throw new InvalidOperationException("DPS capture requires a damage snapshot");
            int hp,attack,defense;
            DungeonManager.Instance.GetStageStatBonusAtPosition(player.transform.position,out hp,out attack,out defense);
            return snapshot.Dps=new DpsSnapshot{StageArmor=defense,OwnerArmorIgnore=player.GetCustomStatUnsafe("IGNOREDEFENSE")};
        }
        internal static string Weapon(WeaponSimple weapon,PlayerAvatar player)
        {
            var snapshot=Start(player);
            AddWeaponCycle(snapshot,"평타 DPS",DpsTiming.Basic(weapon,player),weapon,player);
            AddWeaponCycle(snapshot,"돌진 DPS",DpsTiming.Dash(weapon,player),weapon,player);
            var dagger=weapon as WeaponSimple_Dagger;
            if(dagger)
            {
                if(DpsTiming.DaggerPrimaryAvailable(dagger,player))
                    AddWeaponCycle(snapshot,dagger.throwDagger?"특공 DPS · 투척 단검":"특공 DPS · 패리",DpsTiming.DaggerPrimary(dagger,player),weapon,player);
                if(DpsTiming.DaggerFuryAvailable(dagger,player))
                    AddWeaponCycle(snapshot,"특공 DPS · 퓨리",DpsTiming.DaggerFury(dagger,player),weapon,player);
            }
            else AddWeaponCycle(snapshot,"특공 DPS",DpsTiming.Special(weapon,player),weapon,player);
            AddBigThrowingSpear(snapshot,weapon as WeaponSimple_QuartterStaff,player);
            WeaponDedicatedProfiles.CaptureDps(snapshot,weapon,player);
            DebuffDamagePreview.CaptureWeapon(weapon,player);
            return "";
        }
        internal static string Combo(ComboEffectBase effect,int comboCount,PlayerAvatar player)
        {
            var snapshot=Start(player);
            ComboDamageProfiles.CaptureDps(snapshot,effect,comboCount,player);
            return "";
        }
        private static void AddBigThrowingSpear(DpsSnapshot snapshot,WeaponSimple_QuartterStaff staff,PlayerAvatar player)
        {
            if(!staff||!staff.enableBigThrowingSpear)return;
            var cycle=new DpsSnapshot.Cycle{Name="특공 DPS · 큰 투척 창"};snapshot.Cycles.Add(cycle);
            int required=KeywordDatabase.GetConstValue("staffThrowingSpearBigRequiredStack");
            if(required<=0){cycle.Unavailable="큰 투척 창 재사용 간격 데이터 없음";return;}
            var timing=DpsTiming.Basic(staff,player);
            if(timing.Unavailable!=null){cycle.Unavailable=timing.Unavailable;return;}
            var cache=new Dictionary<int,double>();double contacts=0;
            foreach(var attack in timing.Attacks)
            {
                if(attack.Kind!=0)continue;
                double count;
                if(!cache.TryGetValue(attack.Index,out count))
                {
                    var basic=WeaponHit(staff,player,0,attack.Index,false);
                    if(basic.Unavailable!=null){cycle.Unavailable=basic.Unavailable;return;}
                    count=basic.DirectContacts;cache.Add(attack.Index,count);
                }
                contacts+=count;
            }
            if(contacts<=0){cycle.Unavailable="큰 투척 창을 충전하는 평타 적중 없음";return;}
            cycle.Seconds=timing.Seconds*required/contacts;
            var sample=DamageTooltip.CaptureWeaponAttack(staff,player,2,0,staff.bigThrowingSpearFireData,true);
            var special=DpsProjectiles.Weapon(sample,player,false);
            if(special.Unavailable!=null){cycle.Unavailable=special.Unavailable;return;}
            cycle.SpecialDebuffProblem=special.DebuffContactProblem;
            foreach(var contact in special.DebuffContacts)cycle.SpecialDebuffContacts.Add(contact);
            CaptureDirectDebuffContacts(cycle,special,0);
            foreach(var part in special.Parts)Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            if(special.GroundOffsets.Count>0)cycle.Unavailable="큰 투척 창 장판의 충전별 유지 시간 연결 필요";
            cycle.Condition="평타 적중으로 재충전하며 즉시 발사하는 지속 공격";
        }
        internal static void AddWeaponCycle(DpsSnapshot snapshot,string name,DpsTiming.Cycle timing,WeaponSimple weapon,PlayerAvatar player,bool allowC4Setup=true)
        {
            var cycle=new DpsSnapshot.Cycle{Name=name,Seconds=timing.Seconds,Unavailable=timing.Unavailable,Condition=timing.Condition};
            snapshot.Cycles.Add(cycle);
            if(cycle.Unavailable!=null)return;
            if(timing.Attacks.Count==0){cycle.Unavailable="피해를 주는 공격 없음";return;}
            var occurrences=new Dictionary<int,int>();
            var groundByAttack=new Dictionary<int,double[]>();
            foreach(var attack in timing.Attacks)
            {
                int key=attack.Kind*10000+attack.Index;
                int count;occurrences.TryGetValue(key,out count);occurrences[key]=count+1;
            }
            foreach(var occurrence in occurrences)
            {
                int kind=occurrence.Key/10000,index=occurrence.Key%10000;
                // Only katana overrides shared targeting, allocating an empty
                // list without altering live combat state.
                bool shared=kind==0&&weapon is WeaponSimple_Katana&&weapon.GetBasicAttackSharedTargetList(index)!=null;
                var hit=WeaponHit(weapon,player,kind,index,shared,timing.TempestStacks);
                if(hit.Unavailable!=null){cycle.Unavailable=hit.Unavailable;return;}
                if(hit.RequiresC4Planting)
                {
                    if(!allowC4Setup||occurrences.Count!=1||occurrence.Value!=1)
                    {cycle.Unavailable="복합 C4 기폭 동작의 부착 순서 연결 필요";return;}
                    AddC4Rotation(cycle,timing,hit,weapon,player);
                    return;
                }
                if(hit.Condition!=null)cycle.Condition=hit.Condition;
                if(kind==0||kind==1)
                {
                    cycle.PlantingContacts+=hit.DirectContacts*occurrence.Value;
                    if(hit.ContactTimingProblem!=null)cycle.PlantingContactProblem=hit.ContactTimingProblem;
                    if(hit.ContactOffsets.Count!=hit.DirectContacts)cycle.PlantingContactProblem="C4 부착 적중 시각 연결 필요";
                    foreach(var attack in timing.Attacks)if(attack.Kind==kind&&attack.Index==index)
                        foreach(double offset in hit.ContactOffsets)cycle.LastPlantingContact=Math.Max(cycle.LastPlantingContact,attack.Time+offset);
                }
                foreach(var attack in timing.Attacks)
                    if(attack.Kind==kind&&attack.Index==index)CaptureDirectDebuffContacts(cycle,hit,attack.Time);
                if(kind==2||kind==4)
                {
                    if(hit.DebuffContactProblem!=null)cycle.SpecialDebuffProblem=hit.DebuffContactProblem;
                    foreach(var attack in timing.Attacks)
                    {
                        if(attack.Kind!=kind||attack.Index!=index)continue;
                        foreach(var contact in hit.DebuffContacts)
                            cycle.SpecialDebuffContacts.Add(new DpsProjectiles.DebuffContact{Time=attack.Time+contact.Time,SwingId=contact.SwingId});
                    }
                }
                foreach(var part in hit.Parts)Add(cycle,part.Hit,part.Count*occurrence.Value*(hit.CountsPerSecond?cycle.Seconds:1),part.IgnoreDefense);
                foreach(var pool in hit.RatePools)AddRatePool(cycle,pool,occurrence.Value*pool.Probability/cycle.Seconds);
                if(hit.GroundOffsets.Count>0)groundByAttack.Add(occurrence.Key,hit.GroundOffsets.ToArray());
            }
            if(groundByAttack.Count>0&&player.HasFlameGround()&&player.GetCustomStat(ECustomStat.FlameGroundDisable)<=0)
            {
                var starts=new List<double>();var lengths=new List<double>();
                // Native checks collision before decrementing lifetime, so even
                // a zero-duration patch participates in the next global tick.
                int ticks=Math.Max(1,(int)(3*player.GetFlameGround().durationPercent/100f));
                foreach(var attack in timing.Attacks)
                {
                    double[] offsets;if(!groundByAttack.TryGetValue(attack.Kind*10000+attack.Index,out offsets))continue;
                    foreach(double offset in offsets){starts.Add(attack.Time+offset);lengths.Add(ticks*.25);}
                }
                var damage=DamageTooltip.CurrentCapture;
                cycle.GroundRow=damage.Rows.Count;
                damage.Rows.Add(new[]{new DamageTooltip.Hit{Raw=2,Element=EDamageElementalType.Fire,ResourceAmount=player.GetCustomStat(ECustomStat.FireDamage),ResourcePerUnit=.5f,ElementalEffect=true}});
                cycle.GroundStarts=starts.ToArray();cycle.GroundLengths=lengths.ToArray();
                cycle.Condition=(cycle.Condition==null?"":cycle.Condition+"\n")+"장판 범위 내 유지 · 발사 간격 기준 · 겹친 장판은 틱당 1회";
            }
            string artifactDamage=AttackLinkedArtifactDamage.DpsCondition(player);
            if(artifactDamage!=null)
                cycle.Condition=(cycle.Condition==null?"":cycle.Condition+"\n")+artifactDamage;
        }
        private static void AddRatePool(DpsSnapshot.Cycle cycle,DpsProjectiles.RatePool pool,double requestedRate)
        {
            if(requestedRate<=0||pool.Groups.Count==0)return;
            var remaining=new List<DpsProjectiles.RateGroup>(pool.Groups);
            var rates=new Dictionary<DpsProjectiles.RateGroup,double>();
            double left=requestedRate;
            while(remaining.Count>0&&left>0)
            {
                double share=left/remaining.Count;bool capped=false;
                for(int i=remaining.Count-1;i>=0;i--)
                {
                    var group=remaining[i];
                    if(group.Capacity<share)
                    {
                        double rate=Math.Max(0,group.Capacity);rates[group]=rate;left-=rate;
                        remaining.RemoveAt(i);capped=true;
                    }
                }
                if(capped)continue;
                foreach(var group in remaining)rates[group]=share;
                left=0;
            }
            foreach(var pair in rates)foreach(var part in pair.Key.Parts)
                Add(cycle,part.Hit,part.Count*pair.Value*cycle.Seconds,part.IgnoreDefense);
        }
        private static void AddC4Rotation(DpsSnapshot.Cycle cycle,DpsTiming.Cycle detonation,DpsProjectiles.Result explosion,WeaponSimple weapon,PlayerAvatar player)
        {
            int sources=0;
            if(weapon.addons!=null)foreach(var addon in weapon.addons)if(addon is WeaponAddonCommon_C4Bomb)sources++;
            if(sources==0){cycle.Condition="C4를 부착하는 효과 없음";return;}
            var setupSnapshot=new DpsSnapshot();
            AddWeaponCycle(setupSnapshot,"C4 부착",DpsTiming.Basic(weapon,player),weapon,player,false);
            var setup=setupSnapshot.Cycles[0];
            if(setup.Unavailable!=null){cycle.Unavailable=setup.Unavailable;return;}
            if(setup.PlantingContactProblem!=null){cycle.Unavailable=setup.PlantingContactProblem;return;}
            double bombs=setup.PlantingContacts*sources;
            int cap=KeywordDatabase.GetConstValue("crossbowC4BombMaxCountPerTarget");
            if(cap>0)bombs=Math.Min(cap,bombs);
            if(!DpsNumbers.Finite(bombs)||bombs!=Math.Floor(bombs)||bombs>8192)
            {cycle.Unavailable="C4 부착 수의 확률 분포 연결 필요";return;}
            if(bombs<=0){cycle.Condition="평타로 부착되는 C4 없음";return;}
            // One whole sustained basic rotation (including reload), then one
            // detonation. Do not average a random bomb count before the cap.
            // The 0.05s chain overlaps the special animation; wait only for any
            // portion extending beyond it before starting the next rotation.
            double fireTime=detonation.Attacks[0].Time;
            cycle.Seconds=Math.Max(setup.Seconds,setup.LastPlantingContact)+Math.Max(detonation.Seconds,fireTime+(bombs-1)*.05);
            cycle.Name+=" · 평타 부착 후 기폭";
            foreach(var term in setup.Terms)cycle.Terms.Add(term);
            foreach(var part in explosion.Parts)Add(cycle,part.Hit,part.Count*bombs,part.IgnoreDefense);
            cycle.GroundRow=setup.GroundRow;cycle.GroundStarts=setup.GroundStarts;cycle.GroundLengths=setup.GroundLengths;
            cycle.DirectDebuffProblem=setup.DirectDebuffProblem;cycle.SpecialDebuffProblem=setup.SpecialDebuffProblem;
            foreach(var contact in setup.DirectDebuffContacts)cycle.DirectDebuffContacts.Add(contact);
            foreach(var contact in setup.SpecialDebuffContacts)cycle.SpecialDebuffContacts.Add(contact);
            cycle.Condition="평타 한 주기의 부착·피해·재장전과 기폭 동작을 모두 포함 · 폭발은 디버프 부여 제외";
        }
        private static void CaptureDirectDebuffContacts(DpsSnapshot.Cycle cycle,DpsProjectiles.Result hit,double time)
        {
            if(hit.GlobalDebuffProblem!=null)cycle.DirectDebuffProblem=hit.GlobalDebuffProblem;
            else if(hit.DebuffContactProblem!=null)cycle.DirectDebuffProblem=hit.DebuffContactProblem;
            else if(hit.DebuffContacts.Count+hit.GlobalOnlyDebuffContacts.Count+hit.DebuffExcludedHits!=hit.DirectDamageHits)
                cycle.DirectDebuffProblem="파생 직접 공격의 디버프 부여 주기 연결 필요";
            foreach(var contact in hit.DebuffContacts)
                cycle.DirectDebuffContacts.Add(new DpsProjectiles.DebuffContact{Time=time+contact.Time,SwingId=contact.SwingId});
            foreach(var contact in hit.GlobalOnlyDebuffContacts)
                cycle.DirectDebuffContacts.Add(new DpsProjectiles.DebuffContact{Time=time+contact.Time,SwingId=contact.SwingId});
        }
        private static DpsProjectiles.Result WeaponHit(WeaponSimple weapon,PlayerAvatar player,int kind,int index,bool shared,int tempestStacks=0)
        {
            var shield=weapon as WeaponSimple_SwordAndShield;
            var tempest=shield?shield.overrideSweepAddon as WeaponAddonCommon_Tempest:null;
            if(kind==2&&tempest&&tempestStacks>0)
            {
                var fire=index==0?tempest.tempestFireData:WeaponAdditionalDamageProfiles.ShieldSpecialAttack(shield,index);
                if(!fire)return new DpsProjectiles.Result{Unavailable="폭풍 공격 데이터 없음"};
                var sample=DamageTooltip.CaptureWeaponAttack(weapon,player,kind,index,fire);
                if(sample.Hits==null||sample.Hits.Count==0)return new DpsProjectiles.Result{Unavailable="폭풍 공격 피해 연결 필요"};
                var main=DamageTooltip.TempestHit(weapon,player,tempest,tempestStacks,fire);
                WeaponAdditionalDamageProfiles.ApplyCreatedAttack(weapon,fire,kind,player,main);
                sample.Hits[0]=ProjectileDamageProfiles.SelectMeleeDamage(main,fire);
                return DpsProjectiles.Weapon(sample,player,shared);
            }
            if(kind==4)
            {
                // Synthetic event for mutually exclusive shield lightning
                // variants. Evaluate each hit before weighting (armor/flat).
                double lightningChance=Math.Max(0,Math.Min(1,player.GetCustomStatUnsafe("DARKCLOUDLUCK")/100d));
                var mixed=new DpsProjectiles.Result();
                if(lightningChance<1)Merge(mixed,WeaponHit(weapon,player,2,0,shared,tempestStacks),1-lightningChance);
                if(lightningChance>0)Merge(mixed,WeaponHit(weapon,player,2,1,shared,tempestStacks),lightningChance);
                return mixed;
            }
            if(kind==3)
            {
                var katana=weapon as WeaponSimple_Katana;
                if(!katana)return new DpsProjectiles.Result{Unavailable="발도 무기 데이터 없음"};
                bool charged=katana.greatDrawFireData&&(float)KatanaGauge.GetValue(katana)>=1;
                var fire=charged?katana.greatDrawFireData:katana.quickDrawFireData;
                if(!fire)return new DpsProjectiles.Result{Unavailable="발도 공격 데이터 없음"};
                var captured=DamageTooltip.CaptureWeaponAttack(weapon,player,0,index,fire);
                var drawResult=DpsProjectiles.Weapon(captured,player,false);
                drawResult.Condition=charged?"강화 발도 · 매 발동 시 게이지 가득 · 발도 완료 후 다시 납도":"일반 발도 · 발도 완료 후 다시 납도";
                return drawResult;
            }
            var crossbow=weapon as WeaponSimple_Crossbow;
            bool minigun=kind==2&&crossbow&&crossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.Minigun;
            NewWeaponFireData forced=minigun&&weapon.basicComboAttacks.Length>0?weapon.basicComboAttacks[0]:null;
            var staff=weapon as WeaponSimple_QuartterStaff;
            if(kind==0&&staff&&staff.isNormalAttackRolling)forced=staff.rollingFireData;
            if(kind==2&&shield&&!tempest&&!WeaponAdditionalDamageProfiles.ShieldSpecialAttack(shield,index))
                return new DpsProjectiles.Result{Condition="방어 동작 · 자체 피해 없음"};
            var normal=DamageTooltip.CaptureWeaponAttack(weapon,player,kind,index,forced);
            NewWeaponFireData[] alternate=null;float chance=0;string label=null;
            var normalIndices=new NewWeaponFireData[index+1];
            for(int i=0;i<normalIndices.Length;i++)normalIndices[i]=normal.Fire;
            bool hasAlternate=kind==0&&WeaponBuffPreview.AlternateBasic(weapon,player,normalIndices,out alternate,out chance,out label);
            if(minigun&&crossbow.lightningArrow)
            {alternate=new[]{crossbow.lightningArrow};chance=Math.Max(0,Math.Min(100,player.GetCustomStatUnsafe("LIGHTNINGCROSSBOW")));hasAlternate=chance>0;}
            if(!hasAlternate)
            {
                var single=DpsProjectiles.Weapon(normal,player,shared);
                if(kind==0&&crossbow)AddFrostRelicShots(single,crossbow,player,index);
                return single;
            }
            double probability=Math.Max(0,Math.Min(1,chance/100d));
            var result=new DpsProjectiles.Result();
            if(probability<1)Merge(result,DpsProjectiles.Weapon(normal,player,shared),1-probability);
            if(probability>0)
            {
                var selected=alternate[Math.Min(index,alternate.Length-1)];
                Merge(result,DpsProjectiles.Weapon(DamageTooltip.CaptureWeaponAttack(weapon,player,kind,index,selected),player,shared),probability);
            }
            return result;
        }
        private static void AddFrostRelicShots(DpsProjectiles.Result result,WeaponSimple_Crossbow weapon,PlayerAvatar player,int index)
        {
            if(player.GetCustomStatUnsafe("ICECROSSBOWBUFF")<=0||player.GetCustomStatUnsafe("ICECROSSBOWFROSTRELIC")<=0)return;
            int count=player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY");
            double chance=0;
            int retrigger=player.GetCustomStatUnsafe("CHARGINGCHARMRETRIGGERBYATTACKSPEED");
            int speed=player.GetCustomStatUnsafe("ATTACKSPEED");
            if(retrigger>0&&speed>0)
            {
                int divisor=KeywordDatabase.GetConstValue("chargingCharmRetriggerByAttackSpeed");
                if(divisor<=0){result.Unavailable="냉기 추가 발사 확률 데이터 없음";return;}
                // Native compares integer Random.Range(0,100) with a float.
                float threshold=(float)speed/divisor*retrigger;
                chance=Math.Max(0,Math.Min(100,Math.Ceiling(threshold)))/100;
            }
            if(count<0||(count==0&&chance==0))return;
            if(count>4096||!DpsNumbers.Finite(weapon.frostRelicExtraShotInterval)||weapon.frostRelicExtraShotInterval<=0)
            {result.Unavailable="냉기 추가 발사 간격 데이터 없음";return;}
            if(!weapon.iceArrow){result.Unavailable="냉기 추가 발사 투사체 없음";return;}
            var sample=DamageTooltip.CaptureWeaponAttack(weapon,player,0,index,weapon.iceArrow);
            var extra=DpsProjectiles.Weapon(sample,player,false);
            for(int shot=1;shot<=count;shot++)Merge(result,extra,1,shot*(double)weapon.frostRelicExtraShotInterval);
            if(chance>0)Merge(result,extra,chance,(count+1)*(double)weapon.frostRelicExtraShotInterval);
        }
        private static void Merge(DpsProjectiles.Result target,DpsProjectiles.Result source,double probability,double delay=0)
        {
            if(source.Unavailable!=null)target.Unavailable=source.Unavailable;
            if(source.Condition!=null)target.Condition=source.Condition;
            foreach(var part in source.Parts)target.Parts.Add(new DpsProjectiles.Part{Hit=part.Hit,Count=part.Count*probability,IgnoreDefense=part.IgnoreDefense});
            foreach(var pool in source.RatePools)
            {
                pool.Probability*=probability;
                target.RatePools.Add(pool);
            }
            target.DirectContacts+=source.DirectContacts*probability;
            target.DirectDamageHits+=source.DirectDamageHits*probability;
            target.DebuffExcludedHits+=source.DebuffExcludedHits*probability;
            target.RequiresC4Planting|=source.RequiresC4Planting;
            target.CountsPerSecond|=source.CountsPerSecond;
            if(source.DebuffContactProblem!=null)target.DebuffContactProblem=source.DebuffContactProblem;
            if(probability==1)foreach(var contact in source.DebuffContacts)
                target.DebuffContacts.Add(new DpsProjectiles.DebuffContact{Time=contact.Time+delay,SwingId=contact.SwingId});
            else if(source.DirectContacts>0)target.DebuffContactProblem="확률별 디버프 적중 주기 연결 필요";
            if(source.GlobalDebuffProblem!=null)target.GlobalDebuffProblem=source.GlobalDebuffProblem;
            if(probability==1)foreach(var contact in source.GlobalOnlyDebuffContacts)
                target.GlobalOnlyDebuffContacts.Add(new DpsProjectiles.DebuffContact{Time=contact.Time+delay,SwingId=contact.SwingId});
            else if(source.GlobalOnlyDebuffContacts.Count>0)target.GlobalDebuffProblem="확률별 파생 직접 공격의 디버프 주기 연결 필요";
            if(source.ContactTimingProblem!=null)target.ContactTimingProblem=source.ContactTimingProblem;
            if(probability==1)foreach(double offset in source.ContactOffsets)target.ContactOffsets.Add(offset+delay);
            else target.ContactTimingProblem="확률별 투사체의 적중 간격에 따라 달라짐";
            if(source.GroundOffsets.Count>0)
            {
                if(probability==1)foreach(double offset in source.GroundOffsets)target.GroundOffsets.Add(offset+delay);
                else target.Unavailable="확률로 생성되는 장판의 유지 시간 연결 필요";
            }
        }
        internal static double DirectHitCount(WeaponSimple weapon,PlayerAvatar player,DpsTiming.Cycle timing,out string unavailable)
        {
            unavailable=timing.Unavailable;
            if(unavailable!=null)return 0;
            var counts=new Dictionary<int,double>();double total=0;
            foreach(var attack in timing.Attacks)
            {
                int key=attack.Kind*10000+attack.Index;double count;
                if(!counts.TryGetValue(key,out count))
                {
                    bool shared=attack.Kind==0&&weapon is WeaponSimple_Katana&&weapon.GetBasicAttackSharedTargetList(attack.Index)!=null;
                    var result=WeaponHit(weapon,player,attack.Kind,attack.Index,shared,timing.TempestStacks);
                    if(result.Unavailable!=null){unavailable=result.Unavailable;return 0;}
                    count=result.DirectDamageHits;counts.Add(key,count);
                }
                total+=count;
            }
            return total;
        }
        internal static string BasicContactGroups(WeaponSimple weapon,PlayerAvatar player,DpsSnapshot.Cycle cycle)
        {
            var timing=DpsTiming.Basic(weapon,player);
            if(timing.Unavailable!=null)return timing.Unavailable;
            if(!DpsNumbers.Finite(timing.Seconds)||timing.Seconds<=0)return "평타 주기 데이터 없음";
            cycle.Seconds=timing.Seconds;
            var cache=new Dictionary<int,DpsProjectiles.Result>();var offsets=new List<double>();
            foreach(var attack in timing.Attacks)
            {
                if(attack.Kind!=0)continue; // Sheathing uses special callbacks.
                DpsProjectiles.Result result;
                if(!cache.TryGetValue(attack.Index,out result))
                {
                    bool shared=weapon is WeaponSimple_Katana&&weapon.GetBasicAttackSharedTargetList(attack.Index)!=null;
                    result=WeaponHit(weapon,player,0,attack.Index,shared);cache.Add(attack.Index,result);
                }
                if(result.Unavailable!=null||result.ContactTimingProblem!=null)return result.Unavailable??result.ContactTimingProblem;
                foreach(double offset in result.ContactOffsets)offsets.Add((attack.Time+offset)%timing.Seconds);
            }
            offsets.Sort();var grouped=new List<double>();var counts=new List<int>();
            foreach(double offset in offsets)
            {
                int last=grouped.Count-1;
                if(last>=0&&Math.Abs(grouped[last]-offset)<=1e-9)counts[last]++;
                else {grouped.Add(offset);counts.Add(1);}
            }
            cycle.TriggerOffsets=grouped.ToArray();cycle.TriggerCounts=counts.ToArray();
            return null;
        }
        internal static string SpecialContactGroups(WeaponSimple weapon,PlayerAvatar player,DpsSnapshot.Cycle cycle,string swingId=null)
        {
            var timing=DpsTiming.Special(weapon,player);
            if(timing.Unavailable!=null)return timing.Unavailable;
            if(!DpsNumbers.Finite(timing.Seconds)||timing.Seconds<=0)return "특수 공격 주기 데이터 없음";
            cycle.Seconds=timing.Seconds;
            var cache=new Dictionary<int,DpsProjectiles.Result>();var offsets=new List<double>();
            foreach(var attack in timing.Attacks)
            {
                if(attack.Kind!=2&&attack.Kind!=4)continue;
                int key=attack.Kind*10000+attack.Index;DpsProjectiles.Result result;
                if(!cache.TryGetValue(key,out result))
                {result=WeaponHit(weapon,player,attack.Kind,attack.Index,false,timing.TempestStacks);cache.Add(key,result);}
                string problem=swingId==null?result.ContactTimingProblem:result.DebuffContactProblem;
                if(result.Unavailable!=null||problem!=null)return result.Unavailable??problem;
                if(swingId==null)foreach(double offset in result.ContactOffsets)offsets.Add((attack.Time+offset)%timing.Seconds);
                else foreach(var contact in result.DebuffContacts)if(contact.SwingId==swingId)offsets.Add((attack.Time+contact.Time)%timing.Seconds);
            }
            offsets.Sort();var grouped=new List<double>();var counts=new List<int>();
            foreach(double offset in offsets)
            {
                int last=grouped.Count-1;
                if(last>=0&&Math.Abs(grouped[last]-offset)<=1e-9)counts[last]++;
                else {grouped.Add(offset);counts.Add(1);}
            }
            if(grouped.Count==0)return "특수 공격의 직접 적중 판정 없음";
            cycle.TriggerOffsets=grouped.ToArray();cycle.TriggerCounts=counts.ToArray();return null;
        }
        internal static bool HasWeaponContact(WeaponSimple weapon,PlayerAvatar player,DpsTiming.Cycle timing,int kind,int index,out string unavailable)
        {
            unavailable=timing.Unavailable;if(unavailable!=null)return false;
            foreach(var attack in timing.Attacks)if(attack.Kind==kind&&attack.Index==index)
            {
                bool shared=kind==0&&weapon is WeaponSimple_Katana&&weapon.GetBasicAttackSharedTargetList(index)!=null;
                var result=WeaponHit(weapon,player,kind,index,shared,timing.TempestStacks);
                unavailable=result.Unavailable??result.ContactTimingProblem;
                return unavailable==null&&result.ContactOffsets.Count>0;
            }
            return false;
        }
        internal static void Add(DpsSnapshot.Cycle cycle,DamageTooltip.Hit hit,double count,int ignore=0)
        {
            var snapshot=DamageTooltip.CurrentCapture;
            int row=snapshot.Rows.Count;snapshot.Rows.Add(new[]{hit});
            cycle.Terms.Add(new DpsSnapshot.Term{Row=row,Count=count,ProjectileArmorIgnore=ignore});
        }
        internal static string Artifact(ItemEntity entity,Charm_Basic live,int level,PlayerAvatar player)
        {
            var snapshot=Start(player);
            var source=entity&&entity.resourcePrefab?entity.resourcePrefab.GetComponent<Charm_Basic>():null;
            if(DpsArtifactProfiles.Capture(snapshot,entity,source,live,level,player))return "";
            if(source is Charm_TheTyphoonSheetmusic)
            {
                snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="무기 연계 피해",Unavailable="피해 계산식과 DPS는 장착 무기 상세보기에 합산"});
                return "";
            }
            var damage=DamageTooltip.CurrentCapture;
            int before=damage.Rows.Count;
            DamageTooltip.ArtifactText(entity,live,level,player);
            var cycle=new DpsSnapshot.Cycle{Name="아티팩트 DPS"};snapshot.Cycles.Add(cycle);
            if(damage.Rows.Count==before){cycle.Unavailable=source is IAttackableCharm?"조건부 공격 데이터 연결 필요":"자체 공격 없음 · 능력치 효과는 무기 DPS에 반영";return "";}
            var kunai=source as Charm_Kunai;
            if(kunai&&damage.Rows.Count==before+1)
            {
                var controller=player.GetComponent<WeaponControllerSimple>();
                var weapon=controller?controller.currentWeapon:null;
                if(!weapon){cycle.Unavailable="발동 간격 계산에 장착 무기 필요";return "";}
                var basic=DpsTiming.Basic(weapon,player);
                if(basic.Unavailable!=null||basic.Begins.Count==0){cycle.Unavailable=basic.Unavailable??"평타 시작 간격 데이터 없음";return "";}
                cycle.Seconds=basic.Seconds;cycle.TriggerOffsets=basic.Begins.ToArray();cycle.Cooldown=kunai.cooldownTimer.time;
                cycle.Probability=kunai.throwChanceByLevel[Math.Min(kunai.throwChanceByLevel.Length-1,Math.Max(0,kunai.LevelToIdx(level)))]*weapon.AttackWeightPerSwing/100d;
                var bullet=kunai.bulletPrefab?kunai.bulletPrefab.GetComponent<Bullet>():null;
                cycle.Terms.Add(new DpsSnapshot.Term{Row=before,ProjectileArmorIgnore=bullet?bullet.ignoreDefense:0});
                cycle.Condition="장착 무기 평타 지속 · 발동 확률·재사용 대기 반영";
                if(player.GetCustomStatUnsafe("KUNAIFURY")>0)cycle.Condition+=" · 격노 추가 발사는 별도";
                return "";
            }
            var earring=source as Charm_ElectricEarring;
            if(earring&&damage.Rows.Count==before+1)
            {
                cycle.Seconds=earring.cooldownTimer.time+earring.searchTimer.time;
                int count=earring.countByLevel[Math.Min(earring.countByLevel.Length-1,Math.Max(0,earring.LevelToIdx(level)))]+player.GetCustomStatUnsafe("ELECTRICEARRINGCOUNT");
                var bullet=earring.bulletPrefab?earring.bulletPrefab.GetComponent<Bullet>():null;
                cycle.Terms.Add(new DpsSnapshot.Term{Row=before,Count=Math.Max(0,count),ProjectileArmorIgnore=bullet?bullet.ignoreDefense:0});
                cycle.Condition="전투 중 자동 발동 · 같은 대상에게 전타 적중";
                return "";
            }
            var feather=source as Charm_FireFeather;
            if(feather&&damage.Rows.Count==before+1)
            {
                cycle.Seconds=feather.featherEnableTimer.time+feather.searchEnemyTimer.time;
                int targets=feather.numberOfTargetByLevel[Math.Min(feather.numberOfTargetByLevel.Length-1,Math.Max(0,feather.LevelToIdx(level)))];
                var bullet=feather.bulletPrefab?feather.bulletPrefab.GetComponent<Bullet>():null;
                cycle.Terms.Add(new DpsSnapshot.Term{Row=before,Count=targets>0?1:0,ProjectileArmorIgnore=bullet?bullet.ignoreDefense:0});
                cycle.Condition="범위 안의 대상 1명 기준 · 다른 적에게 날아가는 깃털 제외";
                return "";
            }
            cycle.Unavailable="발동 조건별 지속 시간 연결 필요";
            return "";
        }
    }
}
