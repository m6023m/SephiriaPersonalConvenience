using System;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class DpsMagicProfiles
    {
        private static readonly System.Reflection.FieldInfo MultipleCast=AccessTools.Field(typeof(SkillController),"OnGetMultipleCastCount");
        private static readonly System.Reflection.FieldInfo GlobalCooldown=AccessTools.Field(typeof(SkillController),"globalCooldownTimer");
        private static readonly System.Reflection.FieldInfo BallDuration=AccessTools.Field(typeof(ActiveSkill_Ball),"durationTimer");
        private static readonly System.Reflection.FieldInfo BallFire=AccessTools.Field(typeof(ActiveSkill_Ball),"fireTimer");
        internal static bool Capture(DpsSnapshot snapshot,Charm_Magic source,Charm_Magic live,int level,PlayerAvatar player)
        {
            if(!source||!source.ContainedMagic||!source.ContainedMagic.magicPrefab)return false;
            var bolt=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Bolt>();
            var ball=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Ball>();
            var boomerang=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_LightningBoomerang>();
            var arrow=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Arrow>();
            var fire=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_FireBullet>();
            var rain=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_ArrowRain>();
            var meteor=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_MeteorShower>();
            var armor=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_LightningArmor>();
            var area=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Projectile>();
            if(!bolt&&!ball&&!boomerang&&!arrow&&!fire&&!rain&&!meteor&&!armor&&!area)
            {
                var active=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill>();
                if(active&&!active.IsAttackMagic())
                {
                    snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="마법 DPS",Unavailable="자체 공격 없음 · 강화 효과는 해당 공격 DPS에 반영"});
                    return true;
                }
                return false;
            }
            string cycleName=bolt?"마법탄 DPS":ball?"회전 마법구 DPS":boomerang?"번개 부메랑 DPS":arrow?"마법 화살 DPS":
                fire?"마법 투사체 DPS":rain?"화살비 DPS":meteor?"메테오 샤워 DPS":armor?"천둥 갑옷 DPS":"범위 마법 DPS";
            var cycle=new DpsSnapshot.Cycle{Name=cycleName};snapshot.Cycles.Add(cycle);
            int index=source.LevelToIdx(level);
            int cost=player.GetCustomStatUnsafe("NOMAGICCOST")>0?0:(live?live:source).GetCost(player,index);
            float power=ArtifactDamageProfiles.RootBonus(live,player);
            DamageTooltip.Hit hit;UnityEngine.GameObject prefab=null;double shotsPerCast;
            if(bolt)
            {
                hit=MagicDamageProfiles.BaseHit(player,bolt.defaultDamageByLevel.SafeRandomAccess(index),bolt.relatedDamage,bolt.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,bolt.elementalType);
                if(bolt.relatedToBasicAttackDamageBonus)MagicDamageProfiles.AddFactor(hit,1+player.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f);
                float ratio;bool multiple=MagicDamageProfiles.BoltMultiShot(live,out ratio);
                if(multiple)MagicDamageProfiles.AddFactor(hit,ratio);
                shotsPerCast=Math.Max(1,Math.Ceiling(bolt.fireCount))*(multiple?3:1);prefab=bolt.boltBulletPrefab;
            }
            else if(ball)
            {
                hit=MagicDamageProfiles.BaseHit(player,ball.defaultDamageByLevel.SafeRandomAccess(index),ball.relatedDamage,ball.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,EDamageElementalType.Fire);
                hit.ExtraCriticalChance=ball.criticalChanceBonusByLevel.SafeRandomAccess(index);
                // Eight projectiles aimed at the same selected point, without
                // a shared hit list. They must be launched before despawn.
                double life=((Timer)BallDuration.GetValue(ball)).time;
                double interval=((Timer)BallFire.GetValue(ball)).time;
                double wait=ball.waitTimer.time;
                if(!DpsNumbers.Finite(life)||!DpsNumbers.Finite(interval)||!DpsNumbers.Finite(wait)||interval<=0||wait<0)
                {cycle.Unavailable="회전 마법구의 생성·소멸 시간 데이터 없음";return true;}
                shotsPerCast=Math.Min(8,Math.Max(0,Math.Floor((life-wait)/interval+1e-7)));prefab=ball.ballPrefab;
            }
            else if(boomerang)
            {
                hit=MagicDamageProfiles.BaseHit(player,boomerang.defaultDamageByLevel.SafeRandomAccess(index),boomerang.relatedDamage,boomerang.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,boomerang.elementalType);
                shotsPerCast=1;prefab=boomerang.bulletPrefab;
            }
            else if(arrow)
            {
                hit=MagicDamageProfiles.BaseHit(player,arrow.defaultDamageByLevel.SafeRandomAccess(index),arrow.relatedDamage,arrow.damagePercentByLevel.SafeRandomAccess(index),power,cost,true,arrow.elementalType);
                // The arrows in one spread share a target list. A single enemy
                // can therefore be damaged once per volley, not once per arrow.
                shotsPerCast=Math.Max(1,Math.Ceiling(arrow.fireCount));prefab=arrow.arrowBulletPrefab;
            }
            else if(fire)
            {
                hit=MagicDamageProfiles.BaseHit(player,fire.defaultDamageByLevel.SafeRandomAccess(index),fire.relatedDamage,fire.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,fire.damageElementalType);
                shotsPerCast=Math.Max(0,fire.bulletCount);prefab=fire.bulletPrefabByPower.SafeRandomAccess(0);
            }
            else if(rain)
            {
                hit=MagicDamageProfiles.BaseHit(player,rain.defaultDamageByLevel.SafeRandomAccess(index),rain.relatedDamage,rain.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,rain.damageElementalType);
                shotsPerCast=Math.Max(0,rain.bulletCount);prefab=rain.bulletPrefabByPower.SafeRandomAccess(0);
            }
            else if(meteor)
            {
                hit=MagicDamageProfiles.BaseHit(player,meteor.defaultDamageByLevel.SafeRandomAccess(index),meteor.relatedDamage,meteor.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,meteor.elementalType);
                // Native increments the counter before checking `counter < count`.
                // The serialized count therefore produces count-1 spawn attempts.
                shotsPerCast=Math.Max(0,meteor.numberOfMeteorsByLevel.SafeRandomAccess(index)-1);prefab=meteor.meteorBulletPrefab;
            }
            else if(armor)
            {
                hit=MagicDamageProfiles.BaseHit(player,armor.damagesByLevel.SafeRandomAccess(index),"LIGHTNINGDAMAGE",armor.damagePercentByLevel.SafeRandomAccess(index),1,cost,false,EDamageElementalType.Lightning);
                if(armor.damageTickTimer.time<=0||armor.durationTimer.time<=0)
                {cycle.Unavailable="천둥 갑옷의 지속·타격 간격 데이터 없음";return true;}
                shotsPerCast=Math.Floor(armor.durationTimer.time/armor.damageTickTimer.time+1e-7);
            }
            else
            {
                hit=MagicDamageProfiles.BaseHit(player,area.defaultDamageByLevel.SafeRandomAccess(index),area.relatedDamage,area.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,area.damageElementalType);
                shotsPerCast=Math.Max(0,area.bulletCount);
            }
            int magicWoundBonus=DebuffDamagePreview.FullMagicWoundBonus(player);
            if(magicWoundBonus>0)MagicDamageProfiles.AddFactor(hit,1+magicWoundBonus/100f);
            string castProblem;int casts=MultipleCasts(player,out castProblem);
            var controller=player.GetComponent<SkillController>();
            if(castProblem!=null){cycle.Unavailable=castProblem;return true;}
            double shots=shotsPerCast*casts;
            if(!DpsNumbers.Finite(shots)||shots>1000000){cycle.Unavailable="마법탄 발사 수 데이터 확인 필요";return true;}
            if(boomerang)
            {
                var bullet=prefab?prefab.GetComponent<Bullet>():null;
                var move=prefab?prefab.GetComponent<BulletMoveModule_LightningBoomerang>():null;
                var destroy=prefab?prefab.GetComponent<BulletDestroyModule>():null;
                if(!bullet||!move||!destroy||destroy.GetType()!=typeof(BulletDestroyModule_DestroyImmediate)
                    ||bullet.collosionType!=Bullet.ECollisionTiming.Stay||bullet.damageDealtType!=Bullet.EDamageDealtType.Normal
                    ||bullet.isHomingTargetEnabled||move.destroyTimerError!=0)
                {cycle.Unavailable="부메랑의 경로·소멸·반복 적중 규칙 연결 필요";return true;}
                cycle.Boomerang=new DpsMagicNumbers.BoomerangCycle{StartSpeed=move.startSpeed,ReturnSpeed=move.returnSpeed,Acceleration=move.acceleration,
                    Stop=move.useStopTime?move.stopTimer.time:0,SpeedScale=bullet.speedScale,LifeLimit=move.destroyOnTime?move.destroyTimer.time:double.PositiveInfinity,
                    ContactInterval=bullet.collisionStayDamageIntervalTimer.time,Capacity=source.ContainedMagic.ammo,Casts=casts};
                DpsCapture.Add(cycle,ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio*bullet.DamageMultiplier,
                    damageType:bullet.IsOverrideDamageType?bullet.DamageType:EDamageType.Projectile),shots,bullet.ignoreDefense);
            }
            else if(area)
            {
                if(area.projectilePrefabs==null||area.projectilePrefabs.Length==0)
                {cycle.Unavailable="범위 마법 투사체 데이터 없음";return true;}
                foreach(var variant in area.projectilePrefabs)
                {
                    var judgement=variant?variant.GetComponent<SpecialProjectile_AreaJudgement>():null;
                    if(!judgement){cycle.Unavailable="범위 마법의 반복 판정 데이터 없음";return true;}
                    var part=ProjectileDamageProfiles.Scale(hit,judgement.defaultDamageRatio,damageType:EDamageType.Slice);
                    DpsCapture.Add(cycle,part,shots*judgement.attackCount/area.projectilePrefabs.Length,judgement.ignoreDefense);
                }
            }
            else if(armor)
                DpsCapture.Add(cycle,hit,shots);
            else
            {
                var projectile=DpsProjectiles.Artifact(hit,prefab,player,shots);
                if(projectile.Unavailable!=null){cycle.Unavailable=projectile.Unavailable;return true;}
                foreach(var part in projectile.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            }
            double recovery=(100+player.GetCustomStat(ECustomStat.CooldownRecoverySpeed)+(live?live.additionalcooldownRecoverySpeedSynced:source.additionalcooldownRecoverySpeedSynced))/100d;
            if(!DpsNumbers.Finite(recovery)||recovery<=0||source.ContainedMagic.ammo<=0||!DpsNumbers.Finite(source.ContainedMagic.cooldownTime)||source.ContainedMagic.cooldownTime<0)
            {cycle.Unavailable="마법 탄약을 지속 회복할 수 없음";return true;}
            double animation;
            cycle.Unavailable=CastSeconds(source.ContainedMagic,player,out animation);
            if(cycle.Unavailable!=null)return true;
            if(!controller||GlobalCooldown==null){cycle.Unavailable="마법 공통 재사용 대기 데이터 없음";return true;}
            double globalCooldown=((Timer)GlobalCooldown.GetValue(controller)).time;
            if(!DpsNumbers.Finite(globalCooldown)||globalCooldown<0){cycle.Unavailable="마법 공통 재사용 대기 데이터 확인 필요";return true;}
            // GCD begins at BeginCast, overlapping the animation and per-book
            // recharge; it is not an extra pause appended to every cycle.
            animation=Math.Max(animation,globalCooldown);
            cycle.Seconds=Math.Max(animation,source.ContainedMagic.cooldownTime/recovery);
            if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0)
            {cycle.Unavailable="마법 반복 시전 간격을 계산할 수 없음";return true;}
            if(cycle.Boomerang!=null)
            {
                if(source.ContainedMagic.cooldownTime==0)
                {cycle.Unavailable="재사용 대기 없는 부메랑의 프레임별 탄약 회복 연결 필요";return true;}
                cycle.Boomerang.Animation=animation;cycle.Boomerang.Cooldown=source.ContainedMagic.cooldownTime/recovery;
                cycle.Condition="제자리·장애물 없는 왕복 · 유효 수명 내 접촉 기회 전부 적중 · 귀환 시 탄약 회복";
                return true;
            }
            cycle.Condition="탄약 회복·시전 동작 포함 · 연사와 다중 발사 전부 같은 대상 적중";
            return true;
        }
        internal static bool CaptureRandomBolt(Charm_Magic magic,PlayerAvatar player,out DpsProjectiles.RateGroup group,out string problem)
        {
            group=null;problem=null;
            if(!magic||!magic.ContainedMagic||!magic.ContainedMagic.magicPrefab)return false;
            var bolt=magic.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Bolt>();
            if(!bolt)return false;
            int index=magic.LevelToIdx(magic.limitedEffectEnabledLevel);
            var hit=MagicDamageProfiles.BaseHit(player,bolt.defaultDamageByLevel.SafeRandomAccess(index),bolt.relatedDamage,
                bolt.damagePercentByLevel.SafeRandomAccess(index),ArtifactDamageProfiles.RootBonus(magic,player),0,false,bolt.elementalType);
            MagicDamageProfiles.AddFactor(hit,1+player.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f);
            int magicWoundBonus=DebuffDamagePreview.FullMagicWoundBonus(player);
            if(magicWoundBonus>0)MagicDamageProfiles.AddFactor(hit,1+magicWoundBonus/100f);
            float ratio;bool multiple=MagicDamageProfiles.BoltMultiShot(magic,out ratio);
            if(multiple)MagicDamageProfiles.AddFactor(hit,ratio);
            string castProblem;int casts=MultipleCasts(player,out castProblem);
            if(castProblem!=null){problem=castProblem;return true;}
            double shots=Math.Max(1,Math.Ceiling(bolt.fireCount))*(multiple?3:1)*casts;
            var projectile=DpsProjectiles.Artifact(hit,bolt.boltBulletPrefab,player,shots);
            if(projectile.Unavailable!=null){problem=projectile.Unavailable;return true;}
            double recovery=(100+player.GetCustomStat(ECustomStat.CooldownRecoverySpeed)+magic.additionalcooldownRecoverySpeedSynced)/100d;
            if(recovery<=0||!DpsNumbers.Finite(recovery)){problem="마법탄 탄약 회복 속도 데이터 없음";return true;}
            double cooldown=magic.ContainedMagic.cooldownTime;
            if(!DpsNumbers.Finite(cooldown)||cooldown<0){problem="마법탄 탄약 회복 시간 데이터 없음";return true;}
            group=new DpsProjectiles.RateGroup{Capacity=cooldown==0?double.PositiveInfinity:recovery/cooldown};
            foreach(var part in projectile.Parts)group.Parts.Add(new DpsProjectiles.Part{Hit=part.Hit,Count=part.Count,IgnoreDefense=part.IgnoreDefense});
            return true;
        }
        private static int MultipleCasts(PlayerAvatar player,out string problem)
        {
            problem=null;int casts=1;
            var controller=player.GetComponent<SkillController>();
            var callbacks=controller&&MultipleCast!=null?MultipleCast.GetValue(controller) as Delegate:null;
            if(callbacks!=null)foreach(var callback in callbacks.GetInvocationList())
            {
                var modifier=callback.Target as Charm_MPMultipleCast;
                if(!modifier){problem="다중 마법 시전 효과 연결 필요";return 0;}
                if(player.MaxMp>=modifier.multipleCastMPThresholdByLevel.SafeRandomAccess(modifier.CurrentLevelToIdx()))casts+=modifier.multicast;
            }
            return Math.Max(1,casts);
        }
        private static string CastSeconds(ActiveSkillEntity magic,PlayerAvatar player,out double seconds)
        {
            seconds=0;
            if(magic.castingType==ECastingType.Charge)return "충전 단계별 마법 시전 시간 연결 필요";
            if(!magic.requireCastingAnimation||player.GetCustomStatUnsafe("MAGICQUICKCAST")>0)
            {
                // Phase0 fires; a later Update closes phase1 before the next
                // cast. Snapshot the observed frame interval once, never poll
                // frame time in the worker or divide by a zero cast duration.
                double frame=UnityEngine.Time.unscaledDeltaTime;
                if(UnityEngine.Time.timeScale>0&&UnityEngine.Time.smoothDeltaTime>0)
                    frame=UnityEngine.Time.smoothDeltaTime/UnityEngine.Time.timeScale;
                if(!DpsNumbers.Finite(frame)||frame<=0)return "즉시 시전의 프레임 간격 데이터 없음";
                seconds=2*frame;
                return null;
            }
            var controller=player.GetComponent<WeaponControllerSimple>();
            var weapon=controller?controller.currentWeapon:null;
            var animator=controller?controller.animator:null;
            if(!weapon||!animator||!animator.runtimeAnimatorController||animator.runtimeAnimatorController.name!="AnimationWrapper")return "마법 시전 애니메이션 데이터 없음";
            return DpsAnimationTiming.MagicSeconds(weapon.animatorLayer,animator.speed,
                DpsAnimationTiming.BasicSpeed(weapon,player),out seconds);
        }
    }
}
