using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class DpsArtifactProfiles
    {
        private static readonly System.Reflection.FieldInfo FlamePlantCooldown=HarmonyLib.AccessTools.Field(typeof(Charm_FlamePlantRoot),"cooldownTimer");
        internal static bool Capture(DpsSnapshot snapshot,ItemEntity entity,Charm_Basic source,Charm_Basic live,int level,PlayerAvatar player)
        {
            if(DpsTriggeredArtifactProfiles.Capture(snapshot,source,live,level,player))return true;
            if(DpsMagicProfiles.Capture(snapshot,source as Charm_Magic,live as Charm_Magic,level,player))return true;
            if(DpsChargingProfiles.Capture(snapshot,source,live,level,player))return true;
            var planet=source as Charm_SummonGreenBat;
            if(planet){Planet(snapshot,entity,planet,live as Charm_SummonGreenBat,level,player);return true;}
            var companion=source as Charm_SummonUnit;
            if(companion&&DpsFollowerProfiles.Capture(snapshot,companion,live as Charm_SummonUnit,level,player))return true;
            var lead=source as Charm_LeadNPC;
            if(lead&&DpsFollowerProfiles.CaptureLead(snapshot,lead,live as Charm_LeadNPC,level,player))return true;
            var ballista=source as Charm_MiniBallista;
            if(ballista&&DpsFollowerProfiles.CaptureBallista(snapshot,ballista,live as Charm_MiniBallista,level,player))return true;
            var frostium=source as Charm_FrostiumRing;
            if(frostium){FrostiumRing(snapshot,frostium,live as Charm_FrostiumRing,level,player);return true;}
            var dashDamage=source as Charm_DashDamage;
            if(dashDamage){DashDamage(snapshot,dashDamage,live,level,player);return true;}
            var sweepBullet=source as Charm_CreateBulletOnSweep;
            if(sweepBullet){SweepBullet(snapshot,sweepBullet,live,level,player);return true;}
            var plantRoot=source as Charm_FlamePlantRoot;
            if(plantRoot){FlamePlantRoot(snapshot,plantRoot,live,level,player);return true;}
            var greenGi=source as Charm_GreenGi;
            if(greenGi){GreenGi(snapshot,greenGi,live,level,player);return true;}
            var growth=source as Charm_GrowthParry;
            if(growth){GrowthParry(snapshot,growth,live,level,player);return true;}
            var meteor=source as Charm_FlameGround_Meteor;
            if(meteor){Meteor(snapshot,meteor,live,level,player);return true;}
            var elephant=source as Charm_RockElephant;
            if(elephant){RockElephant(snapshot,elephant,live,level,player);return true;}
            var range=source as Charm_FireBulletInRange;
            if(range){BulletInRange(snapshot,range,live,level,player);return true;}
            var lake=source as Charm_LakeSpirit;
            if(lake){LakeSpirit(snapshot,lake,live,level,player);return true;}
            var flameBall=source as Charm_FlameBall;
            if(flameBall){FlameBall(snapshot,flameBall,live,player);return true;}
            var guillotine=source as Charm_Guillotine;
            if(guillotine){Guillotine(snapshot,guillotine,live,level,player);return true;}
            var gun=source as Charm_Golem_Gun;
            if(gun){GolemGun(snapshot,gun,live,level,player);return true;}
            var laser=source as Charm_Golem_Laser;
            if(laser){GolemLaser(snapshot,laser,live,level,player);return true;}
            var pallas=source as Charm_PallasCard;
            if(pallas){Pallas(snapshot,pallas,live,level,player);return true;}
            var guard=source as Charm_GuardCounter;
            if(guard){Guard(snapshot,guard,live,level,player);return true;}
            var bat=source as Charm_IceBat;
            if(bat){IceBat(snapshot,bat,live,level,player);return true;}
            var glacier=source as Charm_EchoOfTheGlacier;
            if(glacier){Glacier(snapshot,glacier,live,level,player);return true;}
            var chakram=source as Charm_FireChakram;
            if(chakram){Chakram(snapshot,chakram,live,level,player);return true;}
            var dew=source as Charm_Reddew;
            if(dew){RedDew(snapshot,dew,live,level,player);return true;}
            var freeze=source as Charm_FreezeNormalSlash;
            if(freeze){FreezeSlash(snapshot,freeze,live,level,player);return true;}
            return false;
        }
        private static void FrostiumRing(DpsSnapshot snapshot,Charm_FrostiumRing source,Charm_FrostiumRing live,int level,PlayerAvatar player)
        {
            bool fast=live&&(bool)HarmonyLib.AccessTools.Field(typeof(Charm_FrostiumRing),"enabledFast").GetValue(live);
            var cycle=new DpsSnapshot.Cycle{Name="속성 반지 추가타 DPS",Seconds=source.cooldownTimer.time/(fast?3d:1d)};
            snapshot.Cycles.Add(cycle);
            if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0)
            {cycle.Unavailable="속성 반지 발동 간격 데이터 없음";return;}
            var hit=new DamageTooltip.Hit{Raw=source.damageByLevel.SafeRandomAccess(source.LevelToIdx(level)),Element=source.elementalType,
                Factors=new[]{ArtifactDamageProfiles.RootBonus(live,player)}};
            DpsCapture.Add(cycle,hit,1);
        }
        private static void DashDamage(DpsSnapshot snapshot,Charm_DashDamage source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var timing=DpsTiming.MovementDash(player,true);
            var cycle=new DpsSnapshot.Cycle{Name="돌진 충격파 DPS",Seconds=timing.Seconds,Unavailable=timing.Unavailable};
            snapshot.Cycles.Add(cycle);if(cycle.Unavailable!=null)return;
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.PhysicalDamage),Element=EDamageElementalType.Physical,
                Factors=new[]{source.physicalDashDamage.SafeRandomAccess(source.LevelToIdx(level))/100f,2f,ArtifactDamageProfiles.RootBonus(live,player)}};
            var result=DpsProjectiles.ArtifactMelee(hit,source.meleeCollisionPrefab);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void SweepBullet(DpsSnapshot snapshot,Charm_CreateBulletOnSweep source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            var cycle=new DpsSnapshot.Cycle{Name="특공 연계 투사체 DPS"};snapshot.Cycles.Add(cycle);
            if(!weapon){cycle.Unavailable="장착 무기의 특수 공격 주기 필요";return;}
            var timing=DpsTiming.Special(weapon,player);cycle.Seconds=timing.Seconds;cycle.Unavailable=timing.Unavailable;
            if(cycle.Unavailable!=null)return;
            var offsets=new List<double>();
            foreach(var attack in timing.Attacks)if(attack.Time>=0&&attack.Time<timing.Seconds)offsets.Add(attack.Time);
            offsets.Sort();for(int i=offsets.Count-1;i>0;i--)if(Math.Abs(offsets[i]-offsets[i-1])<=1e-9)offsets.RemoveAt(i);
            if(offsets.Count==0){cycle.Unavailable="특수 공격의 투사체 생성 이벤트 없음";return;}
            cycle.TriggerOffsets=offsets.ToArray();
            bool orbit=source.orbitBulletPrefab&&player.GetCustomStatUnsafe(source.orbitStatId)>0;
            cycle.Cooldown=orbit?0:source.cooldownTimer.time;
            var hit=new DamageTooltip.Hit{Raw=source.defaultDamage,ResourceAmount=player.GetCustomStat(source.effectElemental),
                ResourcePerUnit=source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,Element=source.damageElementalType,
                Factors=new[]{ArtifactDamageProfiles.RootBonus(live,player)}};
            var result=DpsProjectiles.Artifact(hit,orbit?source.orbitBulletPrefab:source.bulletPrefab,player);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void FlamePlantRoot(DpsSnapshot snapshot,Charm_FlamePlantRoot source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="화염초 뿌리 DPS"};snapshot.Cycles.Add(cycle);
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon){cycle.Unavailable="장착 무기의 특수 공격 적중 주기 필요";return;}
            cycle.Unavailable=DpsCapture.SpecialContactGroups(weapon,player,cycle);if(cycle.Unavailable!=null)return;
            var timer=FlamePlantCooldown!=null?FlamePlantCooldown.GetValue(source) as Timer:null;
            if(timer==null||timer.time<0){cycle.Unavailable="화염초 뿌리 발동 간격 데이터 없음";return;}
            cycle.Cooldown=timer.time;
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.FireDamage),Element=EDamageElementalType.Fire,
                Factors=new[]{source.fireDamageRatioByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            var result=DpsProjectiles.Artifact(hit,source.bulletPrefab,player);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void GreenGi(DpsSnapshot snapshot,Charm_GreenGi source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="녹색 도복 반격 DPS",Seconds=1,ExternalRateLabel="초당 회피 성공 횟수"};
            snapshot.Cycles.Add(cycle);
            if(source.fireData==null||source.fireData.Length==0){cycle.Unavailable="회피 반격 방향별 공격 데이터 없음";return;}
            var hit=new DamageTooltip.Hit{Raw=source.defaultDamage,ResourceAmount=player.GetCustomStat(ECustomStat.PhysicalDamage),
                ResourcePerUnit=source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,
                Factors=new[]{ArtifactDamageProfiles.RootBonus(live,player)}};
            double directionWeight=1d/source.fireData.Length;
            foreach(var fire in source.fireData)
            {
                if(!fire){cycle.Unavailable="회피 반격 공격 데이터 없음";return;}
                var prepared=ProjectileDamageProfiles.Scale(hit,fire.damageMultiplier*fire.CalculateFinalDamageMultiplier(0));
                prepared.Element=fire.damageElementalType;
                var sample=new DamageTooltip.WeaponAttackSample{Fire=fire,Hits=new List<DamageTooltip.Hit>{prepared}};
                var result=DpsProjectiles.Weapon(sample,player,false);
                if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return;}
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*directionWeight,part.IgnoreDefense);
            }
        }
        private static void GrowthParry(DpsSnapshot snapshot,Charm_GrowthParry source,Charm_Basic live,int level,PlayerAvatar player)
        {
            int highest=Math.Max(Math.Max(player.GetCustomStatUnsafe("PHYSICALDAMAGE"),player.GetCustomStatUnsafe("FIREDAMAGE")),
                Math.Max(player.GetCustomStatUnsafe("ICEDAMAGE"),player.GetCustomStatUnsafe("LIGHTNINGDAMAGE")));
            var hit=new DamageTooltip.Hit{Raw=highest,Element=EDamageElementalType.Chaos,Weapon=true,
                Factors=new[]{source.parryDamageByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            var parry=new DpsSnapshot.Cycle{Name="패리 탄환 DPS",Seconds=1,ExternalRateLabel="초당 패리 성공 횟수"};
            snapshot.Cycles.Add(parry);DpsCapture.Add(parry,hit,1);
            if(!source.hasExtraTriggers)return;
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon)
            {
                snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="마지막 평타 연계 DPS",Unavailable="장착 무기 필요"});
                snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="격노 연계 DPS",Unavailable="장착 무기 필요"});return;
            }
            var basicTiming=DpsTiming.Basic(weapon,player);
            string problem;bool final=DpsCapture.HasWeaponContact(weapon,player,basicTiming,0,weapon.finalComboIdx,out problem);
            var basic=new DpsSnapshot.Cycle{Name="마지막 평타 연계 DPS",Seconds=basicTiming.Seconds,Unavailable=problem};snapshot.Cycles.Add(basic);
            if(problem==null&&final)DpsCapture.Add(basic,hit,1);
            else if(problem==null)basic.Unavailable="마지막 평타의 직접 적중 판정 없음";
            var fury=new DpsSnapshot.Cycle{Name="격노 연계 DPS",Cooldown=source.extraTriggerCooldownTimer.time};snapshot.Cycles.Add(fury);
            fury.Unavailable=DpsCapture.SpecialContactGroups(weapon,player,fury,"FURY");
            if(fury.Unavailable==null)DpsCapture.Add(fury,hit,1);
        }
        private static void Planet(DpsSnapshot snapshot,ItemEntity entity,Charm_SummonGreenBat source,Charm_SummonGreenBat live,int level,PlayerAvatar player)
        {
            var bat=source.greenbatPrefab?source.greenbatPrefab.GetComponent<GreenBat>():null;
            if(!bat){snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="행성 DPS",Unavailable="행성 공격 프리팹 없음"});return;}
            int count=source is Charm_SummonRedPlanet?((Charm_SummonRedPlanet)source).countByLevel.SafeRandomAccess(source.LevelToIdx(level)):bat.fireCount;
            double speed=1+player.GetCustomStatUnsafe("PLANETATTACKSPEED")/100d;
            double cooldown=bat.fireIntervalTimer.time/speed;
            double shotGap=bat.sequenceFiringTimer.time*(bat.enableRandomSequenceFiringTimer?.6:1);
            if(count<=0||speed<=0||cooldown<0||shotGap<=0)
            {snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="행성 DPS",Unavailable="행성 발사 시간 데이터 확인 필요"});return;}
            double burst=count*shotGap;
            bool enhanced=live?live.IsEnhanced:source.IsEnhanced;
            var hit=PlanetDamageProfiles.CaptureHit(source,live,entity,level,player,bat);
            var projectile=DpsProjectiles.Artifact(hit,PlanetDamageProfiles.CapturePrefab(bat,enhanced,hit.ResourceAmount>0),player,count);
            if(projectile.Unavailable!=null)
            {snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="행성 DPS",Unavailable=projectile.Unavailable});return;}
            AddPlanetCycle(snapshot,"행성 자동 DPS",cooldown,burst,projectile,null,0,0);
            if(player.GetCustomStat(ECustomStat.SUPERPLANET)<=0)return;
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon)return;
            AddPlanetCycle(snapshot,"평타 중 행성 DPS",cooldown,burst,projectile,DpsTiming.Basic(weapon,player),weapon.AttackWeightPerSwing*.95,cooldown);
            AddPlanetCycle(snapshot,"돌진 중 행성 DPS",cooldown,burst,projectile,DpsTiming.Dash(weapon,player),weapon.AttackWeightPerSwing,cooldown);
            AddPlanetCycle(snapshot,"특공 중 행성 DPS",cooldown,burst,projectile,DpsTiming.Special(weapon,player),weapon.AttackWeightPerSwing*.95,cooldown);
        }
        private static void AddPlanetCycle(DpsSnapshot snapshot,string name,double cooldown,double burst,DpsProjectiles.Result projectile,DpsTiming.Cycle trigger,double ratio,double scaledCooldown)
        {
            var cycle=new DpsSnapshot.Cycle{Name=name,Seconds=cooldown+burst};snapshot.Cycles.Add(cycle);
            if(trigger!=null)
            {
                if(trigger.Unavailable!=null){cycle.Unavailable=trigger.Unavailable;return;}
                if(trigger.Begins.Count==0){cycle.Unavailable="이 동작의 공격 시작 이벤트 없음";return;}
                cycle.RechargeSearch=burst;cycle.RechargeAttackPeriod=trigger.Seconds;
                cycle.RechargeAttacks=trigger.Begins.ToArray();cycle.RechargeAttackReduction=scaledCooldown*ratio;
                cycle.RechargeAccumulates=true;
            }
            foreach(var part in projectile.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        // The game already exposes cooldown duration independently of its
        // networked cooldown ratio. This read-only method also handles level
        // dependent durations without copying each artifact's formula.
        private static double ActiveCooldown(Charm_Active source,int level)
        {
            bool changesByLevel;
            return source.GetCooldownTime(out changesByLevel,level,0);
        }
        private static void Meteor(DpsSnapshot snapshot,Charm_FlameGround_Meteor source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="붉은 뱀의 눈 DPS",Seconds=source.cooldownTimer.time+source.searchTimer.time};snapshot.Cycles.Add(cycle);
            if(source.cooldownTimer.time<0||source.searchTimer.time<0||cycle.Seconds<=0)
            {cycle.Unavailable="유성 발사 주기 데이터 없음";return;}
            int count=source.countByLevel.SafeRandomAccess(source.LevelToIdx(level));
            if(count<=0)return;
            bool changingCadence=DamageTooltip.CurrentCapture.FullConditions&&player.GetCustomStatUnsafe("REDSNAKEEYEATKCOOLDOWNBONUS")>0;
            DebuffDamagePreview.CaptureArtifact(source.debuffPrefab,player,"붉은 뱀의 눈",count,cycle.Seconds,.2,changingCadence?snapshot.Cycles.Count-1:-1);
            float raw=player.GetCustomStat(ECustomStat.FireDamage)*source.damagesByLevel.SafeRandomAccess(source.LevelToIdx(level))*.01f;
            raw+=raw*player.GetCustomStatUnsafe("REDSNAKEEYEDAMAGEBONUS")/100f;
            raw*=ArtifactDamageProfiles.RootBonus(live,player);
            // Each iteration searches afresh, so one enemy can receive every
            // meteor. Volley delay overlaps the next cooldown, not an extra
            // additive pause between sustained activations.
            var result=DpsProjectiles.Artifact(new DamageTooltip.Hit{Raw=raw,Element=EDamageElementalType.Fire},source.bulletPrefab,player,count);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            cycle.Condition="대상 1명 · 자연 재사용 대기 · 부가 화상과 별도";
            if(DamageTooltip.CurrentCapture.FullConditions&&player.GetCustomStatUnsafe("REDSNAKEEYEATKCOOLDOWNBONUS")>0)
            {
                var controller=player.GetComponent<WeaponControllerSimple>();
                var weapon=controller?controller.currentWeapon:null;
                if(!weapon){cycle.Unavailable="유성 재충전 계산에 장착 무기 필요";return;}
                var timing=DpsTiming.Basic(weapon,player);
                if(timing.Unavailable!=null||timing.Begins.Count==0)
                {cycle.Unavailable=timing.Unavailable??"유성 재충전을 위한 공격 시작 이벤트 없음";return;}
                cycle.RechargeAttacks=timing.Begins.ToArray();cycle.RechargeAttackPeriod=timing.Seconds;
                cycle.RechargeAttackReduction=player.GetCustomStatUnsafe("REDSNAKEEYEATKCOOLDOWNBONUS")/10d;
                cycle.RechargeSearch=source.searchTimer.time;
            }
        }
        private static void RockElephant(DpsSnapshot snapshot,Charm_RockElephant source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="바위코끼리 DPS",Seconds=ActiveCooldown(source,level)};snapshot.Cycles.Add(cycle);
            if(cycle.Seconds<=0){cycle.Unavailable="바위코끼리 재사용 간격 필요";return;}
            if(source.additionalStoneBulletCountByATKSpeed==0){cycle.Unavailable="바위 탄환 수 계산 상수 없음";return;}
            int count=source.defaultStoneBulletCount+player.GetCustomStat(ECustomStat.AttackSpeed)/source.additionalStoneBulletCountByATKSpeed;
            if(count<=0)return;
            float raw=player.GetCustomStat(ECustomStat.DamageReduction)*source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))*.01f;
            raw=Math.Max(1,raw*ArtifactDamageProfiles.RootBonus(live,player));
            var result=DpsProjectiles.Artifact(new DamageTooltip.Hit{Raw=raw,Element=EDamageElementalType.Physical},source.stoneBulletPrefab,player,count);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            cycle.Condition="자연 재사용 대기 기준 · 연사 중 다음 시전 가능";
            if(DamageTooltip.CurrentCapture.FullConditions&&source.coolDownReductionOnDash>0)
            {
                var dash=DpsTiming.MovementDash(player,true);
                if(dash.Unavailable==null&&dash.Seconds>0)
                {cycle.RechargeDashPeriod=dash.Seconds;cycle.RechargeDashReduction=source.coolDownReductionOnDash;}
            }
        }
        private static void BulletInRange(DpsSnapshot snapshot,Charm_FireBulletInRange source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="오색사탕 유리병 DPS",Seconds=ActiveCooldown(source,level)};snapshot.Cycles.Add(cycle);
            if(cycle.Seconds<=0){cycle.Unavailable="범위 투척 재사용 간격 필요";return;}
            int count=source.countByLevel.SafeRandomAccess(source.LevelToIdx(level));
            if(count<=0)return;
            int[] stats={player.GetCustomStat(ECustomStat.PhysicalDamage),player.GetCustomStat(ECustomStat.FireDamage),player.GetCustomStat(ECustomStat.IceDamage),player.GetCustomStat(ECustomStat.LightningDamage)};
            int highest=Math.Max(Math.Max(stats[0],stats[1]),Math.Max(stats[2],stats[3]));
            int choices=0;foreach(int value in stats)if(value==highest)choices++;
            for(int element=0;element<stats.Length;element++)if(stats[element]==highest)
            {
                if(source.bulletPrefabs==null||element>=source.bulletPrefabs.Count){cycle.Unavailable="최고 속성의 탄환 데이터 없음";return;}
                var hit=new DamageTooltip.Hit{Raw=highest,Element=(EDamageElementalType)element,
                    Factors=new[]{source.damageRatioByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
                var result=DpsProjectiles.Artifact(hit,source.bulletPrefabs[element],player);
                if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return;}
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*count/choices,part.IgnoreDefense);
            }
            // Native can start another coroutine once cooldown recovers; the
            // existing volley is not cancelled. Random intra-volley intervals
            // shift contact times, not the long-run number fired per cooldown.
            cycle.Condition="최고 속성 동률은 발사마다 균등 선택 · 재사용 대기와 연사 동시 진행";
        }
        private static void LakeSpirit(DpsSnapshot snapshot,Charm_LakeSpirit source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="호수 정령 DPS",Seconds=ActiveCooldown(source,level)};snapshot.Cycles.Add(cycle);
            if(cycle.Seconds<=0){cycle.Unavailable="호수 정령 프레임별 재사용 간격 필요";return;}
            // The .25s spawn delay overlaps cooldown, and does not extend each
            // steady-state cast period. Damage scales with maximum, not spent MP.
            var hit=new DamageTooltip.Hit{Raw=player.MaxMp,Element=source.elementalType,
                Factors=new[]{source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)},
                CriticalRateMultiplier=1+source.critDamageAmpByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f};
            var result=DpsProjectiles.Artifact(hit,source.bulletPrefab,player);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            cycle.Condition="재사용 대기마다 시전 · 최대 MP 비례 피해 · 시전 자원 보충";
        }
        private static void FlameBall(DpsSnapshot snapshot,Charm_FlameBall source,Charm_Basic live,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="화염구 DPS"};snapshot.Cycles.Add(cycle);
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon){cycle.Unavailable="장착 무기의 평타 주기 필요";return;}
            var timing=DpsTiming.Basic(weapon,player);cycle.Seconds=timing.Seconds;cycle.Unavailable=timing.Unavailable;
            if(cycle.Unavailable!=null)return;
            int count=0;foreach(var attack in timing.Attacks)if(attack.Kind==0)count++;
            if(count==0){cycle.Seconds=1;return;}
            var hit=new DamageTooltip.Hit{Raw=1,Element=EDamageElementalType.Physical,Factors=new[]{ArtifactDamageProfiles.RootBonus(live,player)}};
            // Native root has faction mask 0: it and same-mask blasts cannot
            // hurt ordinary monsters. Child spawners set hostile factions anew.
            var result=DpsProjectiles.Artifact(hit,source.flameBallPrefab,player,count,false);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void GolemGun(DpsSnapshot snapshot,Charm_Golem_Gun source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="골렘 포 DPS",Condition="공격 입력 유지 · 탄환 전타 적중 · 플레이어 피해 보정 적용"};snapshot.Cycles.Add(cycle);
            double speed=1+player.GetCustomStatUnsafe("ATTACKSPEED")/100d;
            if(speed<=0||source.defaultAttackIntervalTimer.time<=0){cycle.Unavailable="현재 공격 속도로 지속 발사 불가";return;}
            cycle.Seconds=source.defaultAttackIntervalTimer.time/speed;
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStatUnsafe(source.relatedStat),Element=source.damageElementalType,
                Factors=new[]{source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            var result=DpsProjectiles.Artifact(hit,source.bulletPrefab,player);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void Guillotine(DpsSnapshot snapshot,Charm_Guillotine source,Charm_Basic live,int level,PlayerAvatar player)
        {
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            float lightning=player.GetCustomStat(ECustomStat.LightningDamage),other=player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage);
            float low=KeywordDatabase.GetConstValue("guillotineLowElementalDamageRatio")/100f;
            var hit=new DamageTooltip.Hit{Raw=source.defaultDamage+(Math.Max(lightning,other)+Math.Min(lightning,other)*low)*source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,
                Element=fire?EDamageElementalType.FireAndLightning:EDamageElementalType.IceAndLightning,
                Factors=new[]{1+player.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            int waves=source.bladeTargetCount>0?Math.Max(0,1+player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY")):0;
            double speed=1+player.GetCustomStatUnsafe("CHARGINGCHARMBONUS")/100d;
            var natural=new DpsSnapshot.Cycle{Name="작두 자동 충전 DPS",Seconds=1,
                Condition="감전 가속 없는 자연 충전 · 지정 대상 1명 · 파동마다 1타 · 서로 다른 대상 수는 곱하지 않음"};
            snapshot.Cycles.Add(natural);
            if(source.triggerCooldown<=0)natural.Unavailable="프레임 단위 작두 발동 간격 필요";
            else if(speed>0)
            {natural.Seconds=source.triggerCooldown/speed;DpsCapture.Add(natural,hit,waves);}
            else natural.Condition="현재 자연 충전으로는 지속 발동 없음";
            if(source.shockCooldownReduction!=0)
            {
                var accelerated=new DpsSnapshot.Cycle{Name="감전 가속 시 작두 DPS",Seconds=1,ExternalRateLabel="초당 작두 발동 횟수",
                    Condition="DEBUFF_ELECTRIC 피해 적중마다 남은 충전 "+source.shockCooldownReduction.ToString("0.###")+"초 감소 · 실제 발동 빈도에 따라 변동"};
                snapshot.Cycles.Add(accelerated);DpsCapture.Add(accelerated,hit,waves);
            }
        }
        private static void GolemLaser(DpsSnapshot snapshot,Charm_Golem_Laser source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="골렘 레이저 지속 DPS",
                Condition="초기 준비 "+source.delayTime.ToString("0.###")+"초 이후 공격 입력 유지 · 같은 대상 접촉 유지"};snapshot.Cycles.Add(cycle);
            var bullet=source.laserPrefab?source.laserPrefab.GetComponent<Bullet>():null;
            if(!bullet||bullet.collosionType!=Bullet.ECollisionTiming.Stay||bullet.damageDealtType!=Bullet.EDamageDealtType.Normal||bullet.collisionStayDamageIntervalTimer.time<=0)
            {cycle.Unavailable="레이저 지속 판정 간격 데이터 필요";return;}
            cycle.Seconds=bullet.collisionStayDamageIntervalTimer.time;
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStatUnsafe(source.relatedStat),Element=source.damageElementalType,
                Factors=new[]{source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            var prepared=ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio*bullet.DamageMultiplier,
                damageType:bullet.IsOverrideDamageType?bullet.DamageType:EDamageType.Projectile);
            DpsCapture.Add(cycle,prepared,1,bullet.ignoreDefense);
        }
        private static void FreezeSlash(DpsSnapshot snapshot,Charm_FreezeNormalSlash source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="빙결 참격 DPS",Cooldown=source.slashIntervalTimer.time};snapshot.Cycles.Add(cycle);
            var controller=player.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon){cycle.Unavailable="발동 간격 계산에 장착 무기 필요";return;}
            string timingProblem=DpsCapture.BasicContactGroups(weapon,player,cycle);
            if(timingProblem!=null)
            {
                cycle.Seconds=1;cycle.TriggerOffsets=null;cycle.TriggerCounts=null;
                cycle.ExternalRateLabel="초당 빙결 참격 발동 횟수";
                cycle.Condition=timingProblem+" · 발동 후 "+source.slashIntervalTimer.time.ToString("0.###")+"초 대기";
            }
            else if(cycle.TriggerOffsets.Length==0)
            {
                cycle.TriggerOffsets=null;cycle.TriggerCounts=null;
                cycle.Condition="현재 평타 방식은 기본 공격 적중 효과를 발동하지 않음";return;
            }
            else cycle.Condition="평타 전타 적중 · 발사·접촉 간격 기준 · 동시 타격은 각각 발동 가능 · 프레임 지연 제외";
            int index=source.LevelToIdx(level);
            cycle.Probability=source.slashChanceByLevel.SafeRandomAccess(index)*weapon.AttackWeightPerSwing/100d;
            var collision=source.collisionPrefab?source.collisionPrefab.GetComponent<MeleeCollision>():null;
            if(!collision){cycle.Unavailable="빙결 참격 판정 데이터 없음";return;}
            float raw=player.GetCustomStat(ECustomStat.PhysicalDamage)*source.phSlashDamageMultiplierByLevel.SafeRandomAccess(index)/100f
                +player.GetCustomStat(ECustomStat.IceDamage)*source.slashDamageMultiplierByLevel.SafeRandomAccess(index)/100f;
            var hit=new DamageTooltip.Hit{Raw=raw,Element=EDamageElementalType.Ice,ExtraCritical=player.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE"),
                ExtraCriticalChance=player.GetCustomStatUnsafe("WEAPONCRITICAL")/100f,
                Factors=new[]{1+player.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,1+player.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f,
                    1+player.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,ArtifactDamageProfiles.RootBonus(live,player),collision.defaultDamageRatio},AfterFactors=collision.additionalDamage};
            int count=DpsProjectiles.MeleeHitCount(collision);
            DpsCapture.Add(cycle,hit,count,collision.ignoreDefense);
        }
        private static DpsSnapshot.Cycle BeginProc(DpsSnapshot snapshot,PlayerAvatar player,double cooldown,out WeaponSimple weapon)
        {
            var cycle=new DpsSnapshot.Cycle{Name="평타 연계 아티팩트 DPS",Cooldown=cooldown};snapshot.Cycles.Add(cycle);
            var controller=player.GetComponent<WeaponControllerSimple>();weapon=controller?controller.currentWeapon:null;
            if(!weapon){cycle.Unavailable="발동 간격 계산에 장착 무기 필요";return cycle;}
            var basic=DpsTiming.Basic(weapon,player);
            if(basic.Unavailable!=null||basic.Begins.Count==0){cycle.Unavailable=basic.Unavailable??"평타 시작으로 발동하지 않는 공격 방식";return cycle;}
            cycle.Seconds=basic.Seconds;cycle.TriggerOffsets=basic.Begins.ToArray();
            return cycle;
        }
        private static void Pallas(DpsSnapshot snapshot,Charm_PallasCard source,Charm_Basic live,int level,PlayerAvatar player)
        {
            WeaponSimple weapon;var cycle=BeginProc(snapshot,player,source.throwIntervalTimer.time,out weapon);
            if(cycle.Unavailable!=null)return;
            int index=Math.Max(0,Math.Min(source.throwChanceByLevel.Length-1,source.LevelToIdx(level)));
            cycle.Probability=(source.defaultChance+source.throwChanceByLevel[index]*Math.Max(0,Math.Min(9999,player.GetCustomStat(ECustomStat.Luck))))*weapon.AttackWeightPerSwing/100d;
            float root=ArtifactDamageProfiles.RootBonus(live,player);
            Cards(cycle,source.bulletSmallPrefab,new DamageTooltip.Hit{Raw=source.bulletDamage,Factors=new[]{root,1f/3}},.8,player);
            Cards(cycle,source.bulletBigPrefab,new DamageTooltip.Hit{Raw=source.bulletDamage,Factors=new[]{root,1f/3,2f}},.2,player);
            cycle.Condition="3장 전타 적중 · 큰 카드 20%·종류 선택 확률·재사용 대기 반영";
        }
        private static void Cards(DpsSnapshot.Cycle cycle,GameObject[] prefabs,DamageTooltip.Hit hit,double probability,PlayerAvatar player)
        {
            if(prefabs==null||prefabs.Length==0){cycle.Unavailable="카드 투사체 데이터 없음";return;}
            // Each card independently selects one prefab from the selected size
            // pool. Alternatives are expectations, never additional projectiles.
            foreach(var prefab in prefabs)
            {
                var result=DpsProjectiles.Artifact(hit,prefab,player);
                if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return;}
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*3*probability/prefabs.Length,part.IgnoreDefense);
            }
        }
        private static void Guard(DpsSnapshot snapshot,Charm_GuardCounter source,Charm_Basic live,int level,PlayerAvatar player)
        {
            int index=Math.Max(0,Math.Min(source.damageRatioByLevel.Length-1,source.LevelToIdx(level)));
            float raw=player.GetCustomStat(ECustomStat.PhysicalDamage),ratio=source.damageRatioByLevel[index];
            float root=ArtifactDamageProfiles.RootBonus(live,player);
            if(player.GetCustomStatUnsafe(source.ripostelaserStatId)>0)
            {
                var laser=new DpsSnapshot.Cycle{Name="반격 레이저 DPS",Seconds=1,ExternalRateLabel="초당 가드 성공 횟수",Condition="직접 가드·보조 방패 가드 합산 · 반격이 적중하는 경우"};
                snapshot.Cycles.Add(laser);
                DpsCapture.Add(laser,new DamageTooltip.Hit{Raw=raw,Factors=new[]{ratio,root,source.ripostelaserDamageRatio}},1);
                return;
            }
            for(int shield=0;shield<2;shield++)
            {
                var cycle=new DpsSnapshot.Cycle{Name=shield==0?"직접 가드 반격 DPS":"보조 방패 반격 DPS",Seconds=1,
                    ExternalRateLabel=shield==0?"초당 직접 가드 성공 횟수":"초당 보조 방패 가드 성공 횟수",
                    Condition="가드 성공마다 예약 · 반격이 적중하는 경우"};
                snapshot.Cycles.Add(cycle);
                var fire=source.guardCounterFireData;
                if(!fire){cycle.Unavailable="반격 공격 데이터 없음";continue;}
                var hit=new DamageTooltip.Hit{Raw=raw,Element=fire.damageElementalType,Factors=new[]{ratio,shield==0?1:root,fire.damageMultiplier,fire.CalculateFinalDamageMultiplier(0)}};
                ProjectileDamageProfiles.PrepareMelee(hit,fire);
                var result=DpsProjectiles.Weapon(new DamageTooltip.WeaponAttackSample{Fire=fire,Hits=new List<DamageTooltip.Hit>{hit}},player,false);
                cycle.Unavailable=result.Unavailable;
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            }
        }
        private static void IceBat(DpsSnapshot snapshot,Charm_IceBat source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="얼음 박쥐 DPS",Seconds=source.attackTimer.time,Condition="내가 동상을 부여한 대상 1명 · 전투·이동 가능 상태 · 2발 모두 적중"};
            snapshot.Cycles.Add(cycle);
            int index=Math.Max(0,Math.Min(source.attackSpeedBonusByLevel.Length-1,source.LevelToIdx(level)));
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.IceDamage)*source.iceDamage,
                Element=source.bulletPrefab?source.bulletPrefab.elementalType:EDamageElementalType.Ice,
                Factors=new[]{.01f,ArtifactDamageProfiles.RootBonus(live,player),(1+player.GetCustomStat(ECustomStat.AttackSpeed)/100f)*(source.attackSpeedBonusByLevel[index]/100f),.5f}};
            // Attack speed modifies damage here, not the one-second launch timer.
            DpsCapture.Add(cycle,hit,2);
        }
        private static void Glacier(DpsSnapshot snapshot,Charm_EchoOfTheGlacier source,Charm_Basic live,int level,PlayerAvatar player)
        {
            int index=Math.Max(0,Math.Min(source.frostbiteTimeByLevel.Length-1,source.LevelToIdx(level)));
            var cycle=new DpsSnapshot.Cycle{Name="빙하 메아리 지속 DPS",Seconds=source.frostbiteTimeByLevel[index],Condition="범위 안 대상 1명 · 동상 자체의 피해와 구분"};
            snapshot.Cycles.Add(cycle);
            int upgrade=player.GetCustomStatUnsafe("ECHOOFTHEGLACIERUPGRADE");
            if(upgrade<=0){cycle.Condition="현재 강화 없음 · 직접 피해 0";return;}
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.IceDamage)*upgrade,Element=EDamageElementalType.Ice,Factors=new[]{.01f,ArtifactDamageProfiles.RootBonus(live,player)}};
            DpsCapture.Add(cycle,hit,1);
            if(player.GetCustomStatUnsafe("ECHOOFTHEGLACIERPARRY")>0)
            {
                var parry=new DpsSnapshot.Cycle{Name="패리 추가 발동 DPS",Seconds=1,ExternalRateLabel="초당 패리 성공 횟수",Condition="주기 발동과 별도 · 범위 안 대상 1명"};
                snapshot.Cycles.Add(parry);DpsCapture.Add(parry,hit,1);
            }
        }
        private static void Chakram(DpsSnapshot snapshot,Charm_FireChakram source,Charm_Basic live,int level,PlayerAvatar player)
        {
            int index=source.LevelToIdx(level);
            int damageIndex=Math.Max(0,Math.Min(source.damagePercentByLevel.Length-1,index));
            int countIndex=Math.Max(0,Math.Min(source.bulletCountByLevel.Length-1,index));
            int enhanced=player.GetCustomStatUnsafe("ENHANCEDCHAKRAM"),count=source.bulletCountByLevel[countIndex];
            var cycle=new DpsSnapshot.Cycle{Name="차크람 DPS",Seconds=1,ExternalRateLabel="초당 실제 적중 횟수",
                Condition=count+"개 합산 적중 횟수 기준 · 같은 차크람은 같은 적에게 0.24초 초과 후 재적중"};
            snapshot.Cycles.Add(cycle);
            if(count<=0){cycle.ExternalRateLabel=null;cycle.Condition="차크람 없음 · 피해 0";return;}
            DpsCapture.Add(cycle,new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.LightningDamage)*source.damagePercentByLevel[damageIndex],Element=EDamageElementalType.Lightning,
                Factors=new[]{.01f,enhanced>0?1+enhanced/100f:1,ArtifactDamageProfiles.RootBonus(live,player)}},1);
        }
        private static void RedDew(DpsSnapshot snapshot,Charm_Reddew source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var stats=new[]{ECustomStat.PhysicalDamage,ECustomStat.FireDamage,ECustomStat.IceDamage,ECustomStat.LightningDamage};
            var elements=new[]{EDamageElementalType.Physical,EDamageElementalType.Fire,EDamageElementalType.Ice,EDamageElementalType.Lightning};
            var values=new int[4];int highest=int.MinValue,tied=0;
            for(int i=0;i<4;i++){values[i]=player.GetCustomStat(stats[i]);highest=Math.Max(highest,values[i]);}
            for(int i=0;i<4;i++)if(values[i]==highest)tied++;
            int index=Math.Max(0,Math.Min(source.damagePercentByLevel.Length-1,source.LevelToIdx(level)));
            float root=ArtifactDamageProfiles.RootBonus(live,player);
            var actual=new DpsSnapshot.Cycle{Name="붉은 이슬 DPS",Seconds=1,ExternalRateLabel="초당 실제 폭발 횟수",Condition="치명타·처형 적중 후 발동 · 같은 폭발로 재발동하지 않음"};
            snapshot.Cycles.Add(actual);
            DpsSnapshot.Cycle maximum=null;
            if(source.coolDownTimer.time>0)
            {
                maximum=new DpsSnapshot.Cycle{Name="발동 조건 충족 시 최대 지속 DPS",Seconds=source.coolDownTimer.time,Condition="재사용 대기마다 치명타·처형을 공급할 때의 상한 · 실제 무기 DPS와 구분"};
                snapshot.Cycles.Add(maximum);
            }
            for(int i=0;i<4;i++)if(values[i]==highest)
            {
                // Native chooses a tied element randomly. Do not roll its RNG;
                // evaluate each tied element then weight after armor/conditions.
                var hit=new DamageTooltip.Hit{Raw=highest,Element=elements[i],CanCritical=false,Factors=new[]{source.damagePercentByLevel[index]/100f,root}};
                DpsCapture.Add(actual,hit,1d/tied);
                if(maximum!=null)DpsCapture.Add(maximum,hit,1d/tied);
            }
        }
    }
}
