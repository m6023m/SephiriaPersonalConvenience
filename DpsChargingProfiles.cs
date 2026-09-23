using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class DpsChargingProfiles
    {
        internal static bool Capture(DpsSnapshot snapshot,Charm_Basic source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var slash=source as Charm_AirSlash;
            if(slash){AirSlash(snapshot,slash,live,level,player);return true;}
            var spear=source as Charm_IceSpear;
            if(spear){Spear(snapshot,spear,live,level,player);return true;}
            var bow=source as Charm_IceBow;
            if(bow){Bow(snapshot,bow,live,level,player);return true;}
            var hammer=source as Charm_IceHammer;
            if(hammer){Hammer(snapshot,hammer,live,level,player);return true;}
            return false;
        }
        internal static double ChargeSeconds(ChargingCharm charge,PlayerAvatar player)
        {
            if(!charge)return double.NaN;
            int haste=0;
            if(!charge.ignoreCooldownBonus)
            {
                if(charge.airSlashCooldownBonus)haste+=player.GetCustomStatUnsafe("AIRSLASHHASTE");
                if(charge.voluspaCooldownBonus)haste+=player.GetCustomStatUnsafe("VOLUSPAHASTE");
            }
            double duration=haste==0?charge.defaultChargeTimer:charge.defaultChargeTimer/(1+haste*.01);
            duration=Math.Max(.1,duration);
            double speed=1+player.GetCustomStatUnsafe("CHARGINGCHARMBONUS")*.01;
            return speed>0?duration/speed:double.PositiveInfinity;
        }
        private static void AddVolley(DpsSnapshot.Cycle cycle,PlayerAvatar player,Charm_Basic live,float raw,float extra,
            GameObject prefab,int projectiles,bool firstIsRepeat=false,double countMultiplier=1)
        {
            int count=Math.Max(0,1+player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"))*Math.Max(0,projectiles);
            if(count==0||countMultiplier==0)return;
            var first=ChargingDamageProfiles.Pay(player,player.MP,firstIsRepeat);
            AddPaid(cycle,player,live,raw,extra,prefab,first,count*countMultiplier);
            double chance=(ChargingDamageProfiles.RepeatPercent(player)/100d);
            if(chance>0)
            {
                var repeat=ChargingDamageProfiles.Pay(player,first.Remaining,true);
                AddPaid(cycle,player,live,raw,extra,prefab,repeat,count*countMultiplier*chance);
            }
        }
        private static void AddPaid(DpsSnapshot.Cycle cycle,PlayerAvatar player,Charm_Basic live,float raw,float extra,
            GameObject prefab,ChargingDamageProfiles.Payment payment,double count)
        {
            if(count<=0)return;
            var hit=new DamageTooltip.Hit{Raw=raw,
                Element=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0?EDamageElementalType.Fire:EDamageElementalType.Ice,
                Factors=new[]{1+player.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,extra,ArtifactDamageProfiles.RootBonus(live,player),payment.Multiplier},AfterFactors=payment.Flat};
            // Expand one actual projectile before expectation weighting. Shared
            // target lists and per-projectile hit limits are nonlinear in count.
            var result=DpsProjectiles.Artifact(hit,prefab,player);
            if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return;}
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*count,part.IgnoreDefense);
        }
        private static bool SetCharge(DpsSnapshot.Cycle cycle,ChargingCharm charge,PlayerAvatar player)
        {
            cycle.Cooldown=ChargeSeconds(charge,player);
            if(!DpsNumbers.Finite(cycle.Cooldown)||cycle.Cooldown<=0)
            {cycle.Unavailable="현재 충전 속도로 발동 불가";return false;}
            return true;
        }
        private static void AirSlash(DpsSnapshot snapshot,Charm_AirSlash source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="충전 참격 DPS"};snapshot.Cycles.Add(cycle);
            if(!SetCharge(cycle,source.chargingCharm,player))return;
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon){cycle.Unavailable="충전 후 발동 간격 계산에 장착 무기 필요";return;}
            var timing=DpsTiming.Basic(weapon,player);
            if(timing.Unavailable!=null){cycle.Unavailable=timing.Unavailable;return;}
            cycle.Seconds=timing.Seconds;
            var offsets=new List<double>();foreach(var attack in timing.Attacks)if(attack.Kind==0)offsets.Add(attack.Time%timing.Seconds);
            if(offsets.Count==0){cycle.Condition="현재 평타 방식에서 충전 참격 발동 없음";return;}
            offsets.Sort();cycle.TriggerOffsets=offsets.ToArray();
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0,small=player.GetCustomStatUnsafe("AIRSLASHMINI")>0;
            var prefab=fire?(small?source.bulletPrefab_Flame_Small:source.bulletPrefab_Flame):(small?source.bulletPrefab_Small:source.bulletPrefab);
            float raw=source.defaultDamage+player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f;
            AddVolley(cycle,player,live,raw,1+player.GetCustomStatUnsafe("AIRSLASHDAMAGE")/100f,prefab,1);
            cycle.Condition="충전 후 다음 평타 발사 때 발동 · 증폭·추가 발동 확률 포함 · 매 충전 발동 시 현재 MP 확보";
        }
        private static void Spear(DpsSnapshot snapshot,Charm_IceSpear source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="충전 창 자동 DPS"};snapshot.Cycles.Add(cycle);
            if(!SetCharge(cycle,source.chargingCharm,player))return;
            cycle.Seconds=cycle.Cooldown;
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            var prefab=fire?source.bulletPrefab_Flame:source.bulletPrefab;
            int index=source.LevelToIdx(level),projectiles=source.fireCountByLevel.SafeRandomAccess(index);
            float raw=source.defaultDamage+player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*source.damagePercentByLevel.SafeRandomAccess(index)/100f;
            float extra=1+Math.Max(0,player.GetCustomStatUnsafe("VOLUSPAUPGRADE"))/100f;
            AddVolley(cycle,player,live,raw,extra,prefab,projectiles);
            cycle.Condition="전투 중 충전마다 전타 적중 · 증폭·추가 발동 확률 포함 · 매 발동 시 현재 MP 확보";
            if(player.GetCustomStatUnsafe("ICESPEARWITHWEAPONATTACK")<=0)return;
            int threshold=KeywordDatabase.GetConstValue("staffAttackToActiveIceSpearCount");
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon||threshold<=0)
            {snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="무기 적중 추가 창 DPS",Unavailable="무기 적중 누적 간격 데이터 필요"});return;}
            var timings=new[]{DpsTiming.Basic(weapon,player),DpsTiming.Dash(weapon,player),DpsTiming.Special(weapon,player)};
            var names=new[]{"평타","돌진","특공"};
            for(int i=0;i<timings.Length;i++)
            {
                var additional=new DpsSnapshot.Cycle{Name=names[i]+" 적중 추가 창 DPS",Seconds=timings[i].Seconds};snapshot.Cycles.Add(additional);
                string problem;double hits=DpsCapture.DirectHitCount(weapon,player,timings[i],out problem);
                if(problem!=null){additional.Unavailable=problem;continue;}
                AddVolley(additional,player,live,raw,extra,prefab,projectiles,true,hits/threshold);
                additional.Condition="직접 공격 "+threshold+"회마다 · 자동 충전 DPS에 추가 · 매 발동 시 현재 MP 확보";
            }
        }
        private static void Bow(DpsSnapshot snapshot,Charm_IceBow source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="충전 활 완전 장전 DPS"};snapshot.Cycles.Add(cycle);
            if(!SetCharge(cycle,source.chargingCharm,player))return;
            cycle.Seconds=cycle.Cooldown;
            int arrows=Math.Max(0,source.arrowReloadLimit);
            if(arrows==0){cycle.Condition="장전 가능한 화살 없음";return;}
            cycle.BowAmmo=arrows;cycle.BowFireInterval=source.fireInterval;
            cycle.BowReload=source.arrowReloadTime/(1+Math.Max(0,player.GetCustomStatUnsafe("CHARGINGCHARMBONUS"))*.01);
            cycle.BowRepeatChance=(ChargingDamageProfiles.RepeatPercent(player)/100d);
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            float raw=source.defaultDamage+player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f;
            AddVolley(cycle,player,live,raw,1,fire?source.bulletPrefab_Flame:source.bulletPrefab,arrows);
            cycle.Condition="완전 장전·충전 후 전량 발사 반복 · 추가 발사 종료까지 대기 · 추가 발사의 장전량 차감 포함 · 매 발동 시 현재 MP 확보";
        }
        private static void Hammer(DpsSnapshot snapshot,Charm_IceHammer source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="돌진 연계 충전 망치 DPS"};snapshot.Cycles.Add(cycle);
            bool extra=player.GetCustomStatUnsafe("DASHATTACKICEHAMMER")>0;
            cycle.Cooldown=ChargeSeconds(source.chargingCharm,player);
            bool charging=DpsNumbers.Finite(cycle.Cooldown)&&cycle.Cooldown>0;
            if(!charging&&!extra){cycle.Unavailable="현재 충전 속도로 지속 발동 불가";return;}
            var timing=DpsTiming.MovementDash(player,extra);
            if(timing.Unavailable!=null){cycle.Unavailable=timing.Unavailable;return;}
            double interval=timing.Seconds;
            int count=Math.Max(0,1+player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"));
            double chance=(ChargingDamageProfiles.RepeatPercent(player)/100d);
            // Finish both volleys before starting the next rotation, so their
            // ordered MP payments cannot interleave with a new paid dash.
            interval=Math.Max(interval,chance>0?count*.5+.22:count*.25);
            if(!DpsNumbers.Finite(interval)||interval<=0){cycle.Unavailable="돌진 반복 시간 데이터 없음";return;}
            double dashes=charging?Math.Max(1,Math.Ceiling((cycle.Cooldown-1e-9)/interval)):1;
            cycle.Seconds=dashes*interval;
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0,scythe=player.GetCustomStatUnsafe("ICEHAMMERSCYTHE")>0;
            float raw=source.defaultDamage+player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f;
            float factor=scythe?.5f:1;
            var prefab=scythe?(fire?source.bulletPrefab_Scythe_Flame:source.bulletPrefab_Scythe):(fire?source.bulletPrefab_Flame:source.bulletPrefab);
            var small=scythe?prefab:(fire?source.bullet75Prefab_Flame:source.bullet75Prefab);
            var initialExtra=ChargingDamageProfiles.Pay(player,player.MP,true);
            if(!charging)
            {
                AddPaid(cycle,player,live,raw,factor*.6f,small,initialExtra,count);
                if(chance>0)AddPaid(cycle,player,live,raw,factor*.6f,small,ChargingDamageProfiles.Pay(player,initialExtra.Remaining,true),count*chance);
                cycle.Condition="충전 진행 불가 · 비용을 낸 이동 돌진의 추가 망치만 반복 · 추가 발사 종료 후 돌진 · 매 돌진 시작 시 현재 MP 확보";
                return;
            }
            var primary=ChargingDamageProfiles.Pay(player,extra?initialExtra.Remaining:player.MP,false);
            AddPaid(cycle,player,live,raw,factor,prefab,primary,count);
            if(extra)
            {
                AddPaid(cycle,player,live,raw,factor*.6f,small,initialExtra,count*dashes);
                if(chance>0)
                {
                    var extraOnEmpty=ChargingDamageProfiles.Pay(player,initialExtra.Remaining,true);
                    AddPaid(cycle,player,live,raw,factor*.6f,small,extraOnEmpty,count*(dashes-1)*chance);
                    var extraOnReady=ChargingDamageProfiles.Pay(player,primary.Remaining,true);
                    AddPaid(cycle,player,live,raw,factor*.6f,small,extraOnReady,count*chance);
                    // The extra and primary retriggers roll independently. The
                    // extra's coroutine is queued first and pays before primary.
                    var repeatWithoutExtra=ChargingDamageProfiles.Pay(player,primary.Remaining,true);
                    var repeatAfterExtra=ChargingDamageProfiles.Pay(player,extraOnReady.Remaining,true);
                    AddPaid(cycle,player,live,raw,factor,prefab,repeatWithoutExtra,count*chance*(1-chance));
                    AddPaid(cycle,player,live,raw,factor,prefab,repeatAfterExtra,count*chance*chance);
                }
            }
            else if(chance>0)
                AddPaid(cycle,player,live,raw,factor,prefab,ChargingDamageProfiles.Pay(player,primary.Remaining,true),count*chance);
            cycle.Condition="돌진 회복·추가 발사 종료 후 반복 · 충전 완료 뒤 첫 돌진에 본 공격 · 매 돌진 시작 시 현재 MP 확보";
            if(extra)cycle.Condition+="\n돌진 비용을 낸 경우 기준 · 추가 망치 → 충전 망치 → 추가 발동 순서로 MP 차감";
        }
    }
}
