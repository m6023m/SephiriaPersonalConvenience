using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaDicePreview
{
    // Snapshot-only adapter. Never spawn a projectile or invoke combat callbacks.
    internal static class DpsProjectiles
    {
        internal sealed class Part
        {
            internal DamageTooltip.Hit Hit;
            internal double Count;
            internal int IgnoreDefense;
        }
        internal sealed class RateGroup
        {
            internal double Capacity;
            internal readonly List<Part> Parts=new List<Part>();
        }
        internal sealed class RatePool
        {
            internal double Probability=1;
            internal readonly List<RateGroup> Groups=new List<RateGroup>();
        }
        internal sealed class Result
        {
            internal readonly List<Part> Parts=new List<Part>();
            internal readonly List<RatePool> RatePools=new List<RatePool>();
            internal string Unavailable,Condition,GroundProblem;
            internal double DirectContacts;
            internal double DirectDamageHits;
            internal string ContactTimingProblem;
            internal readonly List<double> ContactOffsets=new List<double>();
            internal readonly List<double> GroundOffsets=new List<double>();
            internal readonly List<DebuffContact> DebuffContacts=new List<DebuffContact>();
            internal string DebuffContactProblem;
            internal readonly List<DebuffContact> GlobalOnlyDebuffContacts=new List<DebuffContact>();
            internal string GlobalDebuffProblem;
            internal bool RequiresC4Planting;
            internal bool CountsPerSecond;
            internal double DebuffExcludedHits;
        }
        internal sealed class DebuffContact
        {
            internal double Time;
            internal string SwingId;
        }
        internal static Result Artifact(DamageTooltip.Hit hit,GameObject prefab,PlayerAvatar player,double count=1,bool rootTargetsEnemy=true)
        {
            var result=new Result();
            BulletParts(result,hit,prefab,count,false,false,new HashSet<int>(),0,rootTargetsEnemy);
            if(!player.HasFlameGround()||player.GetCustomStat(ECustomStat.FlameGroundDisable)>0)result.GroundOffsets.Clear();
            else if(result.GroundProblem!=null)result.Unavailable=result.GroundProblem;
            else if(result.GroundOffsets.Count>0)result.Unavailable="아티팩트 장판 발동 간격 연결 필요";
            return result;
        }
        internal static Result ArtifactMelee(DamageTooltip.Hit hit,MeleeCollision melee)
        {
            var result=new Result();
            if(!melee){result.Unavailable="근접 판정 데이터 없음";return result;}
            int count=MeleeHitCount(melee);
            var prepared=ProjectileDamageProfiles.Scale(hit,melee.defaultDamageRatio);
            if(prepared.Follower!=null)prepared.Follower.AfterFactors+=melee.additionalDamage;
            else prepared.AfterFactors+=melee.additionalDamage;
            prepared=ProjectileDamageProfiles.SelectMeleeDamage(prepared,melee);
            result.Parts.Add(new Part{Hit=prepared,Count=count,IgnoreDefense=melee.ignoreDefense});
            return result;
        }
        internal static Result Weapon(DamageTooltip.WeaponAttackSample sample,PlayerAvatar player,bool shared)
        {
            var result=new Result();
            if(!sample.Fire||sample.Hits==null||sample.Hits.Count==0){result.Unavailable="공격 데이터 연결 필요";return result;}
            var fire=sample.Fire;
            var melee=fire as NewWeaponFireData_MeleeAttack;
            if(melee)
            {
                var projectile=melee.projectilePrefab?melee.projectilePrefab.GetComponent<MeleeCollision>():null;
                if(!projectile){result.Unavailable="근접 판정 데이터 없음";return result;}
                int count=MeleeHitCount(projectile);
                result.Parts.Add(new Part{Hit=sample.Hits[0],Count=count,IgnoreDefense=projectile.ignoreDefense});
                result.DirectContacts=count;
                result.DirectDamageHits=count;
                if(count>1&&projectile.multiHitIntervalTimer.time<=0)result.ContactTimingProblem="프레임별 재타격 간격 필요";
                else for(int i=0;i<count;i++)result.ContactOffsets.Add(i*(double)projectile.multiHitIntervalTimer.time);
                // Native MeleeCollision.GetDamageInstance leaves ignoreDebuff
                // false; the fire definition replaces the prefab's swing ID.
                foreach(double offset in result.ContactOffsets)
                    result.DebuffContacts.Add(new DebuffContact{Time=offset,SwingId=fire.swingID});
                result.DebuffContactProblem=result.ContactTimingProblem;
                if(projectile is MeleeCollision_Circle_Distance)result.Condition="근접 안쪽 판정 기준";
            }
            else if(fire is NewWeaponFireData_SpecialProjectile)
            {
                SpecialParts(result,sample,player,shared);
                CaptureSpecialDebuffContacts(result,(NewWeaponFireData_SpecialProjectile)fire);
            }
            else if(fire is NewWeaponFireData_Summon)
            {
                SummonParts(result,sample,(NewWeaponFireData_Summon)fire,player);
            }
            else
            {
                GameObject prefab=null;double count=1;
                var bullet=fire as NewWeaponFireData_Bullet;
                var burst=fire as NewWeaponFireData_BulletBurst;
                var spread=fire as NewWeaponFireData_BulletSpread;
                if(bullet)prefab=bullet.bulletPrefab;
                else if(burst){prefab=burst.bulletPrefab;count=Math.Max(0,burst.burstRound);}
                else if(spread)
                {
                    prefab=spread.bulletPrefab;
                    var elemental=spread as NewWeaponFireData_BulletSpread_ElementalScaled;
                    int extra=elemental?elemental.GetExtraBulletCount(player):0;
                    if(extra>0)count=Math.Max(1,(spread.betweenAngleDegrees>0?Math.Ceiling(spread.spreadAngle/spread.betweenAngleDegrees):1)+extra);
                    else if(spread.spreadAngle<=0)count=0;
                    else if(spread.betweenAngleDegrees<=0){result.Unavailable="분산 간격 데이터 없음";return result;}
                    else count=Math.Ceiling(spread.spreadAngle/spread.betweenAngleDegrees);
                }
                else {result.Unavailable="특수 투사체·소환 지속 시간 연결 필요: "+fire.GetType().Name;return result;}
                BulletParts(result,sample.Hits[0],prefab,count,shared,true,new HashSet<int>());
                CaptureBulletContacts(result,prefab,count,shared,burst?Math.Max(0,burst.burstIntervalMiliseconds)/1000d:0);
                if(result.ContactTimingProblem==null&&result.ContactOffsets.Count==result.DirectContacts)
                {
                    // Normal Bullet contact creates a fresh DamageInstance
                    // without ignoreDebuff. Destruction children require their
                    // own callback/ID schedule; do not borrow the root's ID.
                    foreach(double offset in result.ContactOffsets)
                        result.DebuffContacts.Add(new DebuffContact{Time=offset,SwingId=fire.swingID});
                }
                else result.DebuffContactProblem=result.ContactTimingProblem??"파생 투사체의 디버프 적중 주기 연결 필요";
                if(burst&&burst.burstRound>1&&result.GroundOffsets.Count>0)
                {
                    var first=result.GroundOffsets.ToArray();
                    for(int round=1;round<burst.burstRound;round++)foreach(double offset in first)
                        result.GroundOffsets.Add(offset+round*Math.Max(0,burst.burstIntervalMiliseconds)/1000d);
                }
            }
            // Additional elemental damage is a separate damage instance per
            // successful direct contact, not multiplied by the projectile ratio.
            // It inherits owner armor-ignore, not projectile armor-ignore.
            for(int i=1;i<sample.Hits.Count;i++)
                result.Parts.Add(new Part{Hit=sample.Hits[i],Count=result.DirectContacts});
            if(!player.HasFlameGround()||player.GetCustomStat(ECustomStat.FlameGroundDisable)>0)result.GroundOffsets.Clear();
            else if(result.GroundProblem!=null)result.Unavailable=result.GroundProblem;
            return result;
        }
        private static void CaptureSpecialDebuffContacts(Result result,NewWeaponFireData_SpecialProjectile fire)
        {
            var parent=fire.projectilePrefab?fire.projectilePrefab.GetComponent<ProjectileBase>():null;
            if(!parent){result.DebuffContactProblem="특수 투사체 데이터 없음";return;}
            Type type=parent.GetType();
            // FlameSword's child uses a null weapon callback. It contributes
            // only global direct-hit events, captured with its delayed spawn.
            if(type==typeof(SpecialProjectile_FlameSword))return;
            if(type==typeof(SpecialProjectile_C4BombExplosion))return;
            if(type==typeof(SpecialProjectile_WoundExplosion))return;
            // These native implementations either attack with their own
            // callback/ID or forward both to ordinary spawned bullets. Their
            // damage instances leave ignoreDebuff false. Child explosions must
            // still pass the complete contact-count/timing check below.
            bool known=type==typeof(SpecialProjectile_SamuraiDemonStinger)||type==typeof(SpecialProjectile_BladeZone)
                ||type==typeof(SpecialProjectile_SpreadAOE)||type==typeof(SpecialProjectile_AreaJudgement)
                ||type==typeof(SpecialProjectile_ThrowingShield)||type==typeof(SpecialProjectile_ThrowCloudBottle)
                ||type==typeof(SpecialProjectile_FuryMP);
            if(!known){result.DebuffContactProblem="특수 투사체별 디버프 부여 판정 연결 필요";return;}
            if(result.ContactTimingProblem!=null){result.DebuffContactProblem=result.ContactTimingProblem;return;}
            if(result.ContactOffsets.Count!=result.DirectContacts)
            {result.DebuffContactProblem="파생 투사체의 디버프 적중 주기 연결 필요";return;}
            foreach(double offset in result.ContactOffsets)
                result.DebuffContacts.Add(new DebuffContact{Time=offset,SwingId=fire.swingID});
        }
        private static void SpecialParts(Result result,DamageTooltip.WeaponAttackSample sample,PlayerAvatar player,bool shared)
        {
            var fire=(NewWeaponFireData_SpecialProjectile)sample.Fire;
            var parent=fire.projectilePrefab?fire.projectilePrefab.GetComponent<ProjectileBase>():null;
            if(!parent){result.Unavailable="특수 투사체 데이터 없음";return;}
            var c4=parent as SpecialProjectile_C4BombExplosion;
            if(c4&&c4.GetType()==typeof(SpecialProjectile_C4BombExplosion))
            {
                // ApplyCreatedAttack already captured fixed + physical-scaled
                // terms and the defense multiplier. This is ONE attached bomb;
                // DpsCapture supplies planting time and bombs per rotation.
                result.Parts.Add(new Part{Hit=ProjectileDamageProfiles.Scale(sample.Hits[0],c4.defaultDamageRatio,damageType:EDamageType.Slice),Count=1,IgnoreDefense=c4.ignoreDefense});
                result.DirectContacts=1;result.DirectDamageHits=1;
                result.DebuffExcludedHits=1;result.RequiresC4Planting=true;
                result.ContactOffsets.Add(0);
                return;
            }
            var planets=parent as SpecialProjectile_PlanetaryZone;
            if(planets&&planets.GetType()==typeof(SpecialProjectile_PlanetaryZone))
            {
                int planetCount=0,levels=0;
                if(player.Inventory)foreach(var item in player.Inventory.FindItemByCategory("PLANET"))
                {
                    var charm=item.Charm;if(!charm)continue;
                    if(charm is Charm_SummonGreenBat)planetCount++;
                    levels+=Mathf.Min(charm.DisplayedLevel,charm.maxLevel);
                }
                if(planetCount==0){result.Condition="보유 행성 없음";return;}
                if(planets.bulletFireInterval<=0){result.Unavailable="행성 탄환의 프레임별 발사 간격 필요";return;}
                var planetary=ProjectileDamageProfiles.Scale(sample.Hits[0],1);
                planetary.Raw=Math.Max(1,levels*5);planetary.ResourceAmount=0;planetary.ResourcePerUnit=0;planetary.AfterFactors=0;
                planetary.Factors=new[]{1+player.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                    1+player.GetCustomStat(ECustomStat.SpecialAttackDamageBonus)/100f,
                    sample.UsedMp>0?1+player.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f:1,
                    1+player.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,planets.defaultDamageRatio};
                double shots=DpsNumbers.PlanetaryShots(planetCount,planets.lifeTimer.time,planets.bulletFireInterval,shared);
                // Expand one actual shot first; probability is applied only to
                // evaluated damage, including per-hit minimum and flat damage.
                BulletParts(result,planetary,planets.bulletPrefab,1,shared,true,new HashSet<int>(),planets.ignoreDefense);
                foreach(var part in result.Parts)part.Count*=shots;
                result.DirectContacts*=shots;result.DirectDamageHits*=shots;
                result.ContactTimingProblem="행성의 무작위 초기 발사 시각";
                if(result.GroundOffsets.Count>0)result.GroundProblem="무작위 행성 발사 장판의 중첩 시간 연결 필요";
                result.Condition="행성 수·레벨과 무작위 첫 발사를 반영한 평균 발사 수";
                return;
            }
            var randomBolt=parent as SpecialProjectile_RandomBolt;
            if(randomBolt&&randomBolt.GetType()==typeof(SpecialProjectile_RandomBolt))
            {
                var pool=new RatePool();
                if(player.Inventory)foreach(var entry in player.Inventory.charms)
                {
                    var magic=entry.Value as Charm_Magic;
                    if(!magic||magic.currentAmmo<=0)continue;
                    RateGroup group;string problem;
                    if(!DpsMagicProfiles.CaptureRandomBolt(magic,player,out group,out problem))continue;
                    if(problem!=null){result.Unavailable=problem;return;}
                    pool.Groups.Add(group);
                }
                if(pool.Groups.Count==0){result.Condition="사용 가능한 잔여 마법탄 없음";return;}
                result.RatePools.Add(pool);
                result.Condition="보유 마법탄의 탄약 회복률과 무작위 선택을 반영";
                return;
            }
            var woundExplosion=parent as SpecialProjectile_WoundExplosion;
            if(woundExplosion&&woundExplosion.GetType()==typeof(SpecialProjectile_WoundExplosion))
            {
                int stacks=DamageTooltip.CurrentCapture!=null&&DamageTooltip.CurrentCapture.FullConditions?
                    FullWeaponDebuffStacks(sample.Weapon,player):0;
                if(stacks<=0){result.Condition="소모할 내 디버프 중첩 없음";return;}
                var explosion=ProjectileDamageProfiles.Scale(sample.Hits[0],
                    KeywordDatabase.GetConstValue("woundExplosionDamage")*woundExplosion.defaultDamageRatio*stacks,
                    true,EDamageElementalType.Chaos,EDamageType.Slice);
                explosion.Weapon=true;
                if(explosion.Follower!=null)explosion.Follower.Direct=true;
                result.Parts.Add(new Part{Hit=explosion,Count=1,IgnoreDefense=woundExplosion.ignoreDefense});
                result.DirectContacts=1;result.DirectDamageHits=1;result.DebuffExcludedHits=1;
                result.ContactOffsets.Add(0);
                result.Condition="무기에 연결된 디버프를 최대 중첩으로 만든 뒤 전부 소모";
                return;
            }
            var flame=parent as SpecialProjectile_FlameSword;
            if(flame&&flame.GetType()==typeof(SpecialProjectile_FlameSword))
            {
                double luck=Math.Max(0,Math.Min(1,player.GetCustomStatUnsafe("FLAMESWORDLUCK")/100d));
                var normal=ProjectileDamageProfiles.Scale(sample.Hits[0],1+player.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f,false);
                normal.ExtraCritical=player.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE");
                normal.ExtraCriticalChance=player.GetCustomStatUnsafe("FLAMESWORDCRITICAL");
                FlameMeleePart(result,normal,flame.meleePrefab,player,shared,1-luck,flame.spawnTimer.time);
                var lucky=ProjectileDamageProfiles.Scale(normal,1+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent")/100f);
                FlameMeleePart(result,lucky,flame.meleePrefabLucky?flame.meleePrefabLucky:flame.meleePrefab,player,shared,luck,flame.spawnTimer.time);
                // The child is spawned with a null weapon onAttack callback.
                // Global direct-damage effects can still see the child hits.
                result.Condition="화염검 행운 확률을 반영한 평균 피해";
                return;
            }
            var stinger=parent as SpecialProjectile_SamuraiDemonStinger;
            if(stinger&&stinger.GetType()==typeof(SpecialProjectile_SamuraiDemonStinger))
            {
                double tick=stinger.attackCheckTimer.time,life=stinger.destroyTimer.time;
                if(!stinger.attackingCollider||life<=0){result.Condition="돌진 투사체의 유효 접촉 판정 없음";return;}
                if(tick<=0){result.Unavailable="돌진 투사체의 프레임별 접촉 간격 필요";return;}
                double contacts=Math.Max(0,Math.Ceiling(life/tick)-1);
                if(shared)contacts=Math.Min(1,contacts);
                if(contacts>8192){result.Unavailable="돌진 투사체 연속 접촉 수 요약 필요";return;}
                int contactCount=(int)contacts;
                // Native uses the raw projectile damage, without its serialized
                // ratio or additional damage/critical modifiers.
                var direct=ProjectileDamageProfiles.Scale(sample.Hits[0],1,false,damageType:EDamageType.Projectile);
                if(contactCount>0)result.Parts.Add(new Part{Hit=direct,Count=contactCount,IgnoreDefense=stinger.ignoreDefense});
                result.DirectContacts=contactCount;result.DirectDamageHits=contactCount;
                for(int i=1;i<=contactCount;i++)result.ContactOffsets.Add(i*tick);
                result.Condition=shared?"돌진 투사체 · 공유 판정으로 대상당 1회":"돌진 투사체 수명 동안 접촉 유지 · 적의 피격 콜라이더 1개 기준 · 실제 접촉 시간에 따라 감소";
                return;
            }
            var blade=parent as SpecialProjectile_BladeZone;
            if(blade&&blade.GetType()==typeof(SpecialProjectile_BladeZone))
            {
                int attacks=blade.bladeZoneAttackCount;
                int speed=player.GetCustomStat(ECustomStat.AttackSpeed);
                if(speed>0&&blade.addAttackCountPerAttackSpeed>0)attacks+=speed/blade.addAttackCountPerAttackSpeed;
                if(attacks<=0){result.Condition="회전 칼날 타격 횟수 0";return;}
                if(shared)attacks=1;
                if(attacks>8192){result.Unavailable="회전 칼날 타격 수 요약 필요";return;}
                double tick=blade.bladeZoneAttackTimer.time;
                if(tick<=0)result.ContactTimingProblem="회전 칼날의 프레임별 타격 간격 필요";
                for(int i=0;i<attacks;i++)
                {
                    var part=ProjectileDamageProfiles.Scale(sample.Hits[0],blade.defaultDamageRatio,damageType:EDamageType.Slice);
                    part=ProjectileDamageProfiles.Scale(part,1+i*blade.addDamagePercentPerAttack/100f);
                    part.Weapon=true;
                    result.Parts.Add(new Part{Hit=part,Count=1,IgnoreDefense=blade.ignoreDefense});
                    result.ContactOffsets.Add((i+1)*Math.Max(0,tick));
                }
                result.DirectContacts=attacks;result.DirectDamageHits=attacks;
                result.Condition="첫 판정부터 연속 적중 · 공격속도에 따른 횟수·판정 순서별 피해 증가 반영"+(shared?" · 공유 판정으로 대상당 1회":"");
                return;
            }
            var spreadArea=parent as SpecialProjectile_SpreadAOE;
            if(spreadArea&&spreadArea.GetType()==typeof(SpecialProjectile_SpreadAOE))
            {
                SpreadAreaParts(result,sample.Hits[0],spreadArea,shared);return;
            }
            var judgement=parent as SpecialProjectile_AreaJudgement;
            if(judgement&&judgement.GetType()==typeof(SpecialProjectile_AreaJudgement))
            {
                double tick=judgement.bladeZoneAttackTimer.time,life=judgement.destroyTimer.time;
                if(judgement.attackCount<=0||life<=0){result.Condition="범위 공격 횟수 또는 지속 시간 0";return;}
                if(tick<=0){result.Unavailable="범위 공격의 프레임별 타격 간격 필요";return;}
                // Destruction is checked before the attack timer each update:
                // a contact exactly at expiry is not part of the rotation.
                int hits=(int)Math.Min(judgement.attackCount,Math.Max(0,Math.Ceiling(life/tick)-1));
                if(shared)hits=Math.Min(1,hits);
                if(hits>8192){result.Unavailable="범위 공격 다단 타격 수 요약 필요";return;}
                for(int i=1;i<=hits;i++)result.ContactOffsets.Add(i*tick);
                AddSpecialContacts(result,sample.Hits[0],judgement,hits);
                result.Condition="범위 내 접촉 유지 · 소멸 시점 이전 타격만 포함"+(shared?" · 공유 판정으로 대상당 1회":"");
                return;
            }
            var cloud=parent as SpecialProjectile_ThrowCloudBottle;
            var fury=parent as SpecialProjectile_FuryMP;
            var shield=parent as SpecialProjectile_ThrowingShield;
            GameObject bullet;int count,perTick;double interval;
            var hit=sample.Hits[0];
            if(shield&&shield.GetType()==typeof(SpecialProjectile_ThrowingShield))
            {
                count=shield.defaultBulletCount;
                int bonus=player.GetCustomStat(ECustomStat.BasicAttackDamageBonus);
                int divisor=KeywordDatabase.GetConstValue("throwingShieldBulletCountBonusByBasicAttackDamage");
                if(bonus>0&&divisor<=0){result.Unavailable="방패 발사 수 계산 상수 확인 필요";return;}
                if(bonus>0)count+=(int)((float)bonus/divisor);
                if(count<=0){result.Condition="방패 발사 수 0";return;}
                perTick=count;interval=0;bullet=shield.shieldBulletPrefab;
                hit=ProjectileDamageProfiles.Scale(hit,shield.defaultDamageRatio);
                result.Condition="평타 피해 증가에 따른 방패 발사 수 반영 · 생성 방패 전부 적중 가정";
            }
            else if(cloud&&cloud.GetType()==typeof(SpecialProjectile_ThrowCloudBottle))
            {
                count=0;
                if(player.Inventory)foreach(var entry in player.Inventory.charms)
                {
                    var charm=entry.Value;
                    if(charm&&charm.IsEffectEnabled&&charm.Item!=null&&charm.Item.EntityID==cloud.countTargetEntityItemID)count++;
                }
                count=Math.Max(1,count);perTick=cloud.bulletSpawnCountPerTick;
                interval=cloud.bulletSpawnTimer.time;bullet=cloud.bulletPrefab;
                hit=ProjectileDamageProfiles.Scale(hit,cloud.defaultDamageRatio);
                result.Condition="활성 연계 장비 수에 따른 구름 병 탄환 · 생성 탄환 전부 적중 기준";
            }
            else if(fury&&fury.GetType()==typeof(SpecialProjectile_FuryMP))
            {
                int divisor=KeywordDatabase.GetConstValue(fury.bulletCountConstName);
                if(divisor<=0){result.Unavailable="MP 연사 발사 수 상수 확인 필요";return;}
                int rawCount=(int)((float)sample.UsedMp/divisor);
                count=Math.Max(Math.Max(1,KeywordDatabase.GetConstValue(fury.minimumProjectileCountConstName)),rawCount);
                perTick=fury.bulletSpawnCountPerTick+(rawCount>15?1:0);
                interval=fury.bulletSpawnTimer.time;
                bullet=hit.Element==EDamageElementalType.Fire?fury.bulletPrefab_Fire:hit.Element==EDamageElementalType.Ice?fury.bulletPrefab_Ice:hit.Element==EDamageElementalType.Lightning?fury.bulletPrefab_Lightning:fury.bulletPrefab_Physical;
                hit=ProjectileDamageProfiles.Scale(hit,KeywordDatabase.GetConstValue(fury.damageConstName)/100f);
                hit=ProjectileDamageProfiles.Scale(hit,1+(sample.UsedMp/50)*KeywordDatabase.GetConstValue(fury.damageBonusBy50MpConstName)/100f);
                hit=ProjectileDamageProfiles.Scale(hit,fury.defaultDamageRatio);
                result.Condition="현재 소비 MP 기준 연사 · 원형 분산 탄환 전부 적중 가정 · 실제 단일 대상 적중 수에 따라 감소";
            }
            else {result.Unavailable="특수 투사체의 타격 수·지속 시간 연결 필요: "+parent.GetType().Name;return;}
            if(perTick<=0){result.Condition="틱당 생성 수 0 · 발사 없음";return;}
            if(count>4096){result.Unavailable="특수 투사체 발사 수 요약 필요";return;}
            BulletParts(result,hit,bullet,count,shared,true,new HashSet<int>(),parent.ignoreDefense);
            var one=new Result();CaptureBulletContacts(one,bullet,1,shared,0);
            result.ContactTimingProblem=one.ContactTimingProblem;
            if(count>perTick&&interval<=0)result.ContactTimingProblem="프레임별 특수 투사체 발사 간격 필요";
            // Spawn time is common to a batch. Keep simultaneous attempts grouped
            // rather than pretending each projectile has its own interval.
            int contactRounds=shared?1:count;
            if((long)contactRounds*one.ContactOffsets.Count>8192)
                result.ContactTimingProblem="특수 투사체 다단 적중 간격 요약 필요";
            else for(int i=0;i<contactRounds;i++)foreach(double contact in one.ContactOffsets)
                result.ContactOffsets.Add(contact+(1+i/perTick)*Math.Max(0,interval));
            if(result.GroundOffsets.Count>0)
            {
                var ground=result.GroundOffsets.ToArray();result.GroundOffsets.Clear();
                for(int i=0;i<count;i++)foreach(double offset in ground)
                    result.GroundOffsets.Add(offset+(1+i/perTick)*Math.Max(0,interval));
            }
        }
        private static int FullWeaponDebuffStacks(WeaponSimple weapon,PlayerAvatar player)
        {
            if(!weapon||weapon.addons==null)return 0;
            var stacks=new Dictionary<string,int>();
            foreach(var addon in weapon.addons)
            {
                if(!addon)continue;
                CharacterDebuff prefab=null;
                var direct=addon as WeaponAddonCommon_DebuffAttack;
                var special=addon as WeaponAddonCommon_SpecialAttackDebuff;
                var repeated=addon as WeaponAddonCommon_DebuffAttack_OnlySpecialAttack;
                if(direct)prefab=direct.debuffPrefab;
                else if(special)prefab=special.debuffPrefab;
                else if(repeated)prefab=repeated.debuffPrefab;
                if(!prefab)continue;
                if((prefab.ID=="BURN"||prefab.ID=="ELECTRIC")&&player.GetCustomStatUnsafe("PLASMAACTIVE")>0)
                    prefab=UnitDatabase.GetDebuff("PLASMA");
                if(!prefab)continue;
                int maximum;
                if(prefab is CharacterDebuff_Plasma)maximum=2+player.GetCustomStatUnsafe("BURNSTACK")+player.GetCustomStatUnsafe("ELECTRICSTACK");
                else if(prefab is CharacterDebuff_Burn)maximum=2+player.GetCustomStatUnsafe("BURNSTACK");
                else if(prefab is CharacterDebuff_Poison)maximum=1+player.GetCustomStatUnsafe("POISONSTACK");
                else if(prefab is CharacterDebuff_Wound)maximum=4+player.GetCustomStatUnsafe("WOUNDSTACK");
                else if(prefab is CharacterDebuff_Electric)maximum=2+player.GetCustomStatUnsafe("ELECTRICSTACK");
                else if(prefab is CharacterDebuff_Frostbite)maximum=Math.Max(0,4-player.GetCustomStatUnsafe("FREEZETHRESHOLD"));
                else maximum=Math.Max(0,prefab.MaxStackCount);
                int previous;
                stacks.TryGetValue(prefab.ID,out previous);
                stacks[prefab.ID]=Math.Max(previous,maximum);
            }
            int total=0;foreach(int value in stacks.Values)total+=value;
            return total;
        }
        private static void SummonParts(Result result,DamageTooltip.WeaponAttackSample sample,NewWeaponFireData_Summon fire,PlayerAvatar player)
        {
            UnitAvatar unit;FollowerDamageProfiles.Hit follower;
            if(!FollowerDamageProfiles.CaptureWeaponSummon(fire,sample.Hits[0],player,out unit,out follower))
            {result.Unavailable="소환 동료 데이터 없음";return;}
            var amethyst=unit as Unit_Amethyst;var ai=unit.GetComponent<UnitAI_Amethyst>();
            if(!amethyst||unit.GetType()!=typeof(Unit_Amethyst)||!ai||!amethyst.laserPrefab)
            {result.Unavailable="소환 동료의 공격 주기 연결 필요: "+unit.GetType().Name;return;}
            double speed=(100+unit.GetCustomStat(ECustomStat.AttackSpeed)+player.GetCustomStatUnsafe("FOLLOWERATTACKSPEED"))/100d;
            if(speed<=0||ai.laserAttackTimer.time<=0||fire.summonLimit<=0)
            {result.Unavailable="소환 동료의 공격 간격 데이터 없음";return;}
            var shot=Artifact(new DamageTooltip.Hit{Follower=follower},amethyst.laserPrefab,player,fire.summonLimit*speed/ai.laserAttackTimer.time);
            result.Unavailable=shot.Unavailable;result.CountsPerSecond=true;
            foreach(var part in shot.Parts)result.Parts.Add(part);
            result.Condition="소환 한도 유지 · 동료 공격속도와 레이저 주기 반영";
        }
        private static void FlameMeleePart(Result result,DamageTooltip.Hit hit,GameObject prefab,PlayerAvatar player,bool shared,double probability,double spawnDelay)
        {
            if(probability<=0)return;
            var melee=prefab?prefab.GetComponent<MeleeCollision>():null;
            if(!melee){result.Unavailable="화염검 근접 판정 데이터 없음";return;}
            // Child and weapon-root melee use the same native repeat/lifetime data.
            int count=MeleeHitCount(melee);
            if(count>1&&melee.multiHitIntervalTimer.time<=0)
            {result.Unavailable="화염검 프레임별 다단 타격 간격 필요";return;}
            var prepared=ProjectileDamageProfiles.Scale(hit,melee.defaultDamageRatio);
            if(prepared.Follower!=null)prepared.Follower.AfterFactors+=melee.additionalDamage;
            else prepared.AfterFactors+=melee.additionalDamage;
            prepared=ProjectileDamageProfiles.SelectMeleeDamage(prepared,melee);
            result.Parts.Add(new Part{Hit=prepared,Count=count*probability,IgnoreDefense=melee.ignoreDefense+player.GetCustomStatUnsafe("FLAMESWORDIGNOREDEFENSE")});
            result.DirectDamageHits+=count*probability;
            if(spawnDelay<=0||!DpsNumbers.Finite(spawnDelay))result.GlobalDebuffProblem="화염검 생성 시각의 프레임 단위 처리 필요";
            else if(result.GlobalOnlyDebuffContacts.Count==0)
            {
                for(int i=0;i<count;i++)result.GlobalOnlyDebuffContacts.Add(new DebuffContact{Time=spawnDelay+i*(double)melee.multiHitIntervalTimer.time,SwingId=melee.swingId});
            }
            else
            {
                // Lucky/non-lucky damage is mutually exclusive. If both
                // variants have the same contacts, the application schedule
                // is deterministic even though the damage amount is not.
                bool same=result.GlobalOnlyDebuffContacts.Count==count;
                for(int i=0;same&&i<count;i++)same=result.GlobalOnlyDebuffContacts[i].Time==spawnDelay+i*(double)melee.multiHitIntervalTimer.time;
                if(!same)result.GlobalDebuffProblem="화염검 행운별 디버프 적중 주기 연결 필요";
            }
        }
        private static void AddSpecialContacts(Result result,DamageTooltip.Hit hit,ProjectileBase parent,int count)
        {
            if(count<=0)return;
            result.Parts.Add(new Part{Hit=ProjectileDamageProfiles.Scale(hit,parent.defaultDamageRatio,damageType:EDamageType.Slice),Count=count,IgnoreDefense=parent.ignoreDefense});
            result.DirectContacts=count;result.DirectDamageHits=count;
        }
        private static void SpreadAreaParts(Result result,DamageTooltip.Hit hit,SpecialProjectile_SpreadAOE area,bool shared)
        {
            int outward=Math.Max(0,area.spreadCount-1);
            if(outward>4096){result.Unavailable="확산 범위 단계 수 요약 필요";return;}
            double interval=Math.Max(0,area.attackIntervalAfterFirstAttack);
            double laterWarning=Math.Max(0,area.warningTimeAfterFirstAttack);
            double elapsed=0,lastOutward=0;
            for(int phase=1;phase<=outward;phase++)
            {
                double start=(phase-1)*interval;
                double warning=phase==1?Math.Max(0,area.warningTime):laterWarning;
                double planned=Math.Max(start,start+warning+area.warningToAttackDelay);
                lastOutward=planned;
                // Native coroutine does not move time backwards when a later
                // stage has a shorter warning than the first one.
                elapsed=Math.Max(elapsed,planned);
                bool hasArea=area.aoeCountPerIdx>0||(phase==1&&area.idxOneHasIdxZero);
                if(hasArea&&(!shared||result.ContactOffsets.Count==0))result.ContactOffsets.Add(elapsed);
            }
            if(area.returnAfterSpread)for(int step=0;step<outward-1;step++)
            {
                double start=lastOutward+step*interval;
                elapsed=Math.Max(elapsed,Math.Max(start,start+laterWarning+area.warningToAttackDelay));
                if(area.aoeCountPerIdx>0&&(!shared||result.ContactOffsets.Count==0))result.ContactOffsets.Add(elapsed);
            }
            AddSpecialContacts(result,hit,area,result.ContactOffsets.Count);
            result.Condition=shared?"확산·복귀 전체에서 같은 대상 1회":"확산·복귀 각 단계 적중 가정 · 같은 단계의 겹치는 범위는 1회 · 적 위치에 따라 실제 적중 감소";
        }
        private static void CaptureBulletContacts(Result result,GameObject prefab,double count,bool shared,double burstInterval)
        {
            var bullet=prefab?prefab.GetComponent<Bullet>():null;
            if(!bullet)return;
            var destroy=prefab.GetComponent<BulletDestroyModule>();
            var explosion=destroy as BulletDestroyModule_Explode;
            if(explosion&&destroy.GetType()==typeof(BulletDestroyModule_Explode)&&explosion.explodeRadius>0)
            {
                int explosionRounds=(int)count;
                if(explosionRounds<0||explosionRounds>4096||explosionRounds!=count){result.ContactTimingProblem="발사 수별 폭발 간격 필요";return;}
                if(shared)explosionRounds=Math.Min(1,explosionRounds);
                // The enemy distance decides when the mine explodes, but one
                // detonation still applies exactly once per fired mine. Its
                // absolute offset does not change a sustained one-shot cycle.
                for(int round=0;round<explosionRounds;round++)result.ContactOffsets.Add(round*burstInterval);
                return;
            }
            if(destroy&&destroy.GetType()!=typeof(BulletDestroyModule)&&destroy.GetType()!=typeof(BulletDestroyModule_DestroyImmediate)&&destroy.GetType()!=typeof(BulletDestroyModule_DelayDespawn)&&destroy.GetType()!=typeof(BulletDestroyModule_PhysicalProjectile))
            {result.ContactTimingProblem="접촉·파괴 효과의 실제 적중 간격에 따라 달라짐";return;}
            if(bullet.collosionType==Bullet.ECollisionTiming.None||bullet.damageDealtType!=Bullet.EDamageDealtType.Normal)return;
            int rounds=(int)count;
            if(rounds<0||rounds>4096||rounds!=count){result.ContactTimingProblem="발사 수별 적중 간격 필요";return;}
            if(shared)rounds=Math.Min(1,rounds);
            int hits=1;double interval=0;
            if(bullet.collosionType==Bullet.ECollisionTiming.Stay&&!shared)
            {
                if(!StayTiming(bullet,out hits,out interval))
                {result.ContactTimingProblem="지속 접촉 종료 조건 필요";return;}
            }
            if((double)rounds*hits>8192){result.ContactTimingProblem="지속 접촉 간격 요약 필요";return;}
            for(int round=0;round<rounds;round++)for(int hit=0;hit<hits;hit++)result.ContactOffsets.Add(round*burstInterval+hit*interval);
        }
        // Capture repeat rules once through the native base components. Damage
        // totals and debuff contact schedules must use the same lifetime limit.
        internal static int MeleeHitCount(MeleeCollision melee)
        {
            int count=Math.Max(1,melee.multiHit);
            double interval=melee.multiHitIntervalTimer.time;
            if(count>1&&interval>0)
                count=Math.Min(count,Math.Max(1,(int)Math.Ceiling(melee.durationTimer.time/interval)));
            return count;
        }
        private static bool StayTiming(Bullet bullet,out int hits,out double interval)
        {
            hits=0;interval=bullet.collisionStayDamageIntervalTimer.time;
            var movement=bullet.GetComponent<BulletMoveModule>();
            if(!movement||!movement.destroyOnTime||!DpsNumbers.Finite(interval)||interval<=0)return false;
            double lifetime=movement.destroyTimer.time;
            if(!DpsNumbers.Finite(lifetime)||lifetime<0)return false;
            int pierce=bullet.afterPierceOperation==Bullet.EAfterPierceOperation.Destroy?Math.Max(0,bullet.pierceCreatureCount):0;
            double shortest=Math.Max(0,lifetime-Math.Abs(movement.destroyTimerError));
            double longest=lifetime+Math.Abs(movement.destroyTimerError);
            double minimum=Math.Max(1,Math.Ceiling(shortest/interval));
            double maximum=Math.Max(1,Math.Ceiling(longest/interval));
            if(pierce>0){minimum=Math.Min(minimum,pierce);maximum=Math.Min(maximum,pierce);}
            if(minimum!=maximum)return false;
            double count=minimum;
            if(count>int.MaxValue)return false;
            hits=(int)count;return true;
        }
        private static void BulletParts(Result result,DamageTooltip.Hit hit,GameObject prefab,double count,bool shared,bool callbacks,HashSet<int> path,int inheritedIgnore=0,bool targetsEnemy=true)
        {
            if(count<=0)return;
            var bullet=prefab?prefab.GetComponent<Bullet>():null;
            if(!bullet){result.Unavailable="투사체 데이터 없음";return;}
            if(!path.Add(prefab.GetInstanceID())){result.Unavailable="반복 생성 투사체의 종료 조건 필요";return;}
            double contacts=shared?Math.Min(1,count):count;
            if(targetsEnemy&&bullet.collosionType==Bullet.ECollisionTiming.Stay&&!shared)
            {
                int hits;double interval;
                if(!StayTiming(bullet,out hits,out interval))
                {result.Unavailable="지속 접촉 종료 조건 필요";path.Remove(prefab.GetInstanceID());return;}
                contacts*=hits;
                result.Condition="투사체 수명 동안 접촉 유지 기준";
            }
            if(targetsEnemy&&bullet.collosionType!=Bullet.ECollisionTiming.None)
            {
                if(bullet.damageDealtType==Bullet.EDamageDealtType.Normal)
                {
                    result.Parts.Add(new Part{Hit=ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio*bullet.DamageMultiplier,damageType:bullet.IsOverrideDamageType?bullet.DamageType:EDamageType.Projectile),Count=contacts,IgnoreDefense=bullet.ignoreDefense+inheritedIgnore});
                    result.DirectDamageHits+=contacts;
                    if(callbacks)result.DirectContacts+=contacts;
                }
            }
            var destroy=prefab.GetComponent<BulletDestroyModule>();
            if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_Explode))
            {
                var explosion=(BulletDestroyModule_Explode)destroy;
                // Native explosion performs its own overlap query and does not
                // consult the contact shared-target list.
                if(targetsEnemy&&explosion.explodeRadius>0)
                {
                    result.Parts.Add(new Part{Hit=ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,true,explosion.inheritElementalType?(EDamageElementalType?)null:explosion.elementalType,explosion.explosionDamageType),Count=count,IgnoreDefense=bullet.ignoreDefense+inheritedIgnore});
                    if(callbacks)result.DirectContacts+=count;
                    result.DirectDamageHits+=count;
                }
                if(explosion.bulletPrefab)
                {
                    if(explosion.spreadDeltaAngle<=0)result.Unavailable="파편 분산 간격 데이터 없음";
                    else BulletParts(result,ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType),explosion.bulletPrefab,count*Math.Ceiling(360d/explosion.spreadDeltaAngle),false,false,path);
                }
                // flameGroundRadius is serialized but this native class does not
                // use it; only the dedicated ground-fire module creates patches.
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_CreateOtherBullet))
            {
                var other=(BulletDestroyModule_CreateOtherBullet)destroy;
                if(other.bulletPrefab)BulletParts(result,ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,false,EDamageElementalType.Physical),other.bulletPrefab,count,false,false,path);
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_CreateBulletSpread))
            {
                var spread=(BulletDestroyModule_CreateBulletSpread)destroy;
                if(spread.bulletPrefab)
                {
                    int divisor=spread.spreadDeltaAngle>0?(int)(360f/spread.spreadDeltaAngle):0;
                    if(spread.spreadDeltaAngle<=0||(spread.isDamageDivided&&divisor<=0))result.Unavailable="파편 분산 간격 데이터 없음";
                    else BulletParts(result,ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio/(spread.isDamageDivided?divisor:1),false,EDamageElementalType.Physical),spread.bulletPrefab,count*Math.Ceiling(360d/spread.spreadDeltaAngle),false,false,path);
                }
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_HeavyExplode))
            {
                var explosion=(BulletDestroyModule_HeavyExplode)destroy;
                if(targetsEnemy&&explosion.explodeRadius>0)
                {
                    result.Parts.Add(new Part{Hit=ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType,explosion.explosionDamageType),Count=count});
                    if(callbacks)result.DirectContacts+=count;
                    result.DirectDamageHits+=count;
                }
                if(explosion.bulletPrefab)
                {
                    if(explosion.spreadDeltaAngle<=0)result.Unavailable="파편 분산 간격 데이터 없음";
                    else BulletParts(result,ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType),explosion.bulletPrefab,count*Math.Ceiling(360d/explosion.spreadDeltaAngle),false,false,path);
                }
            }
            else if(destroy&&(destroy.GetType()==typeof(BulletDestroyModule_ProgressiveExplode)||destroy.GetType()==typeof(BulletDestroyModule_Linebomb)))
            {
                var wave=ProjectileDamageProfiles.Scale(hit,bullet.defaultDamageRatio,false,EDamageElementalType.Physical,EDamageType.Projectile);
                wave.Weapon=false;wave.Magic=false;if(wave.Follower!=null)wave.Follower.Direct=false;
                // The linebomb's shared target list includes preceding contact.
                // Its own subsequent blast positions share one hit set as well.
                double waveCount=shared&&destroy is BulletDestroyModule_Linebomb?(bullet.collosionType==Bullet.ECollisionTiming.None?Math.Min(1,count):0):count;
                if(targetsEnemy)result.Parts.Add(new Part{Hit=wave,Count=waveCount});
                if(destroy is BulletDestroyModule_ProgressiveExplode)result.GroundProblem="연쇄 폭발의 장판 생성 간격 연결 필요";
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_MakeGroundFire))
            {
                if(((BulletDestroyModule_MakeGroundFire)destroy).explodeRadius>0)result.GroundOffsets.Add(0);
            }
            else if(destroy&&destroy.GetType()!=typeof(BulletDestroyModule)&&destroy.GetType()!=typeof(BulletDestroyModule_DestroyImmediate)&&destroy.GetType()!=typeof(BulletDestroyModule_DelayDespawn)&&destroy.GetType()!=typeof(BulletDestroyModule_PhysicalProjectile))
                result.Unavailable="후속 투사체 판정 연결 필요: "+destroy.GetType().Name;
            var trail=prefab.GetComponent<BulletMoveModule_UniformVector2_MakeGroundFire>();
            if(trail&&trail.trailRadius>0)result.GroundProblem="이동 화염 장판 중첩 규칙 연결 필요";
            path.Remove(prefab.GetInstanceID());
        }
    }
}
