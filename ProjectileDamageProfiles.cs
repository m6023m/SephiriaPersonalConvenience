using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class ProjectileDamageProfiles
    {
        internal static string DescribeFury(PlayerAvatar player,DamageTooltip.Hit hit,SpecialProjectile_FuryMP fury,EDamageElementalType element,int usedMp)
        {
            int divisor=KeywordDatabase.GetConstValue(fury.bulletCountConstName);
            if(divisor<=0)return "MP 연사 발사 수 상수 확인 필요";
            int rawCount=(int)((float)usedMp/divisor);
            int count=Math.Max(Math.Max(1,KeywordDatabase.GetConstValue(fury.minimumProjectileCountConstName)),rawCount);
            int perTick=fury.bulletSpawnCountPerTick+(rawCount>15?1:0);
            if(perTick<=0)return "MP 연사 · 틱당 발사 수 0 · 발사 없음";
            var scaled=Scale(hit,KeywordDatabase.GetConstValue(fury.damageConstName)/100f);
            scaled=Scale(scaled,1+(usedMp/50)*KeywordDatabase.GetConstValue(fury.damageBonusBy50MpConstName)/100f);
            scaled=Scale(scaled,fury.defaultDamageRatio);
            var prefab=element==EDamageElementalType.Fire?fury.bulletPrefab_Fire:element==EDamageElementalType.Ice?fury.bulletPrefab_Ice:element==EDamageElementalType.Lightning?fury.bulletPrefab_Lightning:fury.bulletPrefab_Physical;
            return Describe(player,scaled,prefab,"MP 연사 1발")+"\n발사 "+count+"발 · "+fury.bulletSpawnTimer.time.ToString("0.###")+"초마다 최대 "+perTick+"발 · 실제 적중 수에 따라 총 피해 변동";
        }
        internal static bool DescribeWeaponFire(PlayerAvatar player,DamageTooltip.Hit hit,NewWeaponFireData fire,out string description)
        {
            description=null;
            GameObject prefab=null;
            string note="";
            if(fire.GetType()==typeof(NewWeaponFireData_Bullet))prefab=((NewWeaponFireData_Bullet)fire).bulletPrefab;
            else if(fire.GetType()==typeof(NewWeaponFireData_BulletSpread_ElementalScaled))
            {
                var scaled=(NewWeaponFireData_BulletSpread_ElementalScaled)fire;
                prefab=scaled.bulletPrefab;
                int extra=scaled.GetExtraBulletCount(player);
                int count;
                if(extra>0)
                {
                    // Enhanced native branch uses an integer loop even when the
                    // spread angle is zero; do not apply the base loop's early exit.
                    int baseCount=scaled.betweenAngleDegrees>0?Mathf.CeilToInt(scaled.spreadAngle/scaled.betweenAngleDegrees):1;
                    count=Math.Max(1,baseCount+extra);
                }
                else
                {
                    if(scaled.spreadAngle<=0){description="발사 범위 0 · 생성되는 탄환 없음";return true;}
                    if(scaled.betweenAngleDegrees<=0){description="분산 간격 데이터 확인 필요";return true;}
                    count=Mathf.CeilToInt(scaled.spreadAngle/scaled.betweenAngleDegrees);
                }
                note="\n속성 비례 분산 "+count+"발 · 현재 추가 "+extra+"발";
                note+="\n각 물리·화염·냉기·번개 수치에서 "+scaled.baseStatThreshold+" 초과분만 합산";
                if(scaled.statPerExtraBullet>0)
                    note+=" · 합계 "+scaled.statPerExtraBullet+"당 1발 추가\n추가 탄환 최소 0발 · 최대 "+(scaled.maxExtraBullets>0?scaled.maxExtraBullets+"발":"고정 상한 없음");
                else note+="\n속성 비례 추가 탄환 비활성";
                note+="\n탄환 수는 1발 피해를 나누지 않음 · 실제 적중한 탄환만 합산";
            }
            else if(fire.GetType()==typeof(NewWeaponFireData_BulletSpread))
            {
                var spread=(NewWeaponFireData_BulletSpread)fire;
                prefab=spread.bulletPrefab;
                if(spread.spreadAngle<=0){description="발사 범위 0 · 생성되는 탄환 없음";return true;}
                if(spread.betweenAngleDegrees<=0){description="분산 간격 데이터 확인 필요";return true;}
                int count=Mathf.CeilToInt(spread.spreadAngle/spread.betweenAngleDegrees);
                note="\n분산 "+count+"발 · 1발씩 계산 · 실제 적중 수에 따라 총 피해 변동";
            }
            else if(fire.GetType()==typeof(NewWeaponFireData_BulletBurst))
            {
                var burst=(NewWeaponFireData_BulletBurst)fire;
                prefab=burst.bulletPrefab;
                if(burst.burstRound<=0){description="발사 수 0 · 생성되는 탄환 없음";return true;}
                note="\n연속 "+burst.burstRound+"발 · 간격 "+Math.Max(0,burst.burstIntervalMiliseconds)+"ms · 실제 적중 수에 따라 총 피해 변동";
            }
            else if(fire.GetType()==typeof(NewWeaponFireData_SpecialProjectile))
            {
                var special=(NewWeaponFireData_SpecialProjectile)fire;
                var duplicate=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_DuplicateMagic>():null;
                if(duplicate&&duplicate.GetType()==typeof(SpecialProjectile_DuplicateMagic))
                {
                    var controller=player.GetComponent<SkillController>();
                    var last=DamageStatPreview.ReadLastMagic(player,controller?controller.GetLastUsedMagic():null);
                    if(!last){description="마법 복제 · 최근 사용한 마법 없음";return true;}
                    if(last.CanCast(player,false,false)!=ECanUseSkillResult.Succeeded){description="마법 복제 · 최근 마법이 현재 사용 불가";return true;}
                    string magic;
                    if(!MagicDamageProfiles.TryDescribe(last,last,last.limitedEffectEnabledLevel,player,out magic,0))magic="해당 마법의 전용 피해 계산 필요";
                    description="최근 마법 복제: "+last.ContainedMagic.Name+"\n"+magic+"\nMP·재사용 횟수 소비 없음 · 복제 후 최근 마법 기록 해제";
                    return true;
                }
                var randomBolt=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_RandomBolt>():null;
                if(randomBolt&&randomBolt.GetType()==typeof(SpecialProjectile_RandomBolt))
                {
                    var options=new StringBuilder();int count=0;
                    if(player.Inventory)foreach(var entry in player.Inventory.charms)
                    {
                        var magic=entry.Value as Charm_Magic;
                        if(!magic||!magic.ContainedMagic||!magic.ContainedMagic.magicPrefab||!magic.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Bolt>()||magic.currentAmmo<=0)continue;
                        count++;options.AppendLine(magic.ContainedMagic.Name);
                        if(magic.CanCast(player,true,false)!=ECanUseSkillResult.Succeeded){options.AppendLine("선택되어도 현재 사용 불가");continue;}
                        string damage;
                        if(MagicDamageProfiles.TryDescribe(magic,magic,magic.limitedEffectEnabledLevel,player,out damage,0,true))options.AppendLine(damage);
                        else options.AppendLine("해당 마법의 전용 피해 계산 필요");
                    }
                    description=count==0?"무작위 마법탄 · 사용 가능한 잔여 마법탄 없음":"무작위 마법탄 · 아래 "+count+"개 후보 중 하나\n"+options+"MP 소비 없음 · 선택한 마법의 재사용 횟수 소비 · 평타 피해 증가 적용";
                    return true;
                }
                var cloud=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_ThrowCloudBottle>():null;
                if(cloud&&cloud.GetType()==typeof(SpecialProjectile_ThrowCloudBottle))
                {
                    int count=0;
                    if(player.Inventory)foreach(var entry in player.Inventory.charms)
                    {
                        var charm=entry.Value;
                        if(charm&&charm.IsEffectEnabled&&charm.Item!=null&&charm.Item.EntityID==cloud.countTargetEntityItemID)count++;
                    }
                    count=Math.Max(1,count);
                    if(cloud.bulletSpawnCountPerTick<=0){description="구름 병 · 틱당 생성 수 0 · 발사 없음";return true;}
                    description=Describe(player,Scale(hit,cloud.defaultDamageRatio),cloud.bulletPrefab,"구름 병 1발")
                        +"\n활성 연계 장비 수 기준 "+count+"발 · 최소 1발"
                        +"\n"+cloud.bulletSpawnTimer.time.ToString("0.####")+"초마다 최대 "+cloud.bulletSpawnCountPerTick+"발 · 실제 적중 수에 따라 총 피해 변동";
                    return true;
                }
                var bomb=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_C4BombExplosion>():null;
                if(bomb&&bomb.GetType()==typeof(SpecialProjectile_C4BombExplosion))
                {
                    // ApplyCreatedAttack has already applied C4's fixed/physical/defense
                    // formula. The projectile ratio multiplies both resulting terms.
                    description="부착 폭탄 1개 폭발: "+DamageTooltip.Hits(player,Scale(hit,bomb.defaultDamageRatio))
                        +"\n내가 부착한 미폭발·미예약 폭탄만 기폭 · 폭탄이 없으면 피해 없음"
                        +"\n기폭 간격 0.05초 · 공유 적중 목록이 있으면 같은 대상의 후속 폭발 피해 제외";
                    return true;
                }
                var planets=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_PlanetaryZone>():null;
                if(planets&&planets.GetType()==typeof(SpecialProjectile_PlanetaryZone))
                {
                    int count=0,levels=0;
                    if(player.Inventory)foreach(var item in player.Inventory.FindItemByCategory("PLANET"))
                    {
                        var charm=item.Charm;if(!charm)continue;
                        if(charm is Charm_SummonGreenBat)count++;
                        levels+=Mathf.Min(charm.DisplayedLevel,charm.maxLevel);
                    }
                    if(count<=0){description="행성 구역 · 보유 행성 없음 · 발사 없음";return true;}
                    // OnSpawnFinalized replaces the incoming weapon damage entirely.
                    var planetHit=Scale(hit,1);
                    planetHit.Raw=Math.Max(1,levels*5);planetHit.ResourceAmount=0;planetHit.ResourcePerUnit=0;planetHit.AfterFactors=0;
                    planetHit.Factors=new[]{1+player.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                        1+player.GetCustomStat(ECustomStat.SpecialAttackDamageBonus)/100f,
                        1+player.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,planets.defaultDamageRatio};
                    description="행성 "+count+"개 · 행성 분류 장비 레벨 합 "+levels+"\n"
                        +Describe(player,planetHit,planets.bulletPrefab,"MP 미소비 시 행성 탄환 1발")+"\n"
                        +Describe(player,Scale(planetHit,1+player.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f),planets.bulletPrefab,"MP 소비 시 행성 탄환 1발")
                        +"\n행성별 발사 간격 "+planets.bulletFireInterval.ToString("0.###")+"초 · 지속 "+planets.lifeTimer.time.ToString("0.###")+"초 · 첫 발사 시점은 무작위";
                    return true;
                }
                var spreadArea=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_SpreadAOE>():null;
                if(spreadArea&&spreadArea.GetType()==typeof(SpecialProjectile_SpreadAOE))
                {
                    int outward=Math.Max(0,spreadArea.spreadCount-1);
                    int inward=spreadArea.returnAfterSpread?Math.Max(0,spreadArea.spreadCount-2):0;
                    if(outward==0){description="확산 범위 공격 · 생성 단계 없음";return true;}
                    description="확산 범위 공격 1판정: "+DamageTooltip.Hits(player,Scale(hit,spreadArea.defaultDamageRatio))
                        +"\n바깥 방향 "+outward+"단계 · 복귀 "+inward+"단계 · 첫 중앙 판정 "+(spreadArea.idxOneHasIdxZero?"있음":"없음")
                        +"\n같은 단계의 겹친 범위는 한 대상에 1회 · 공유 적중 목록이 있으면 다음 단계도 중복 제외";
                    return true;
                }
                var wound=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_WoundExplosion>():null;
                if(wound&&wound.GetType()==typeof(SpecialProjectile_WoundExplosion))
                {
                    var perStack=Scale(hit,KeywordDatabase.GetConstValue("woundExplosionDamage")*wound.defaultDamageRatio,true,EDamageElementalType.Chaos,EDamageType.Slice);
                    perStack.Weapon=true;
                    if(perStack.Follower!=null)perStack.Follower.Direct=true;
                    description="상처 폭발 · 소모할 디버프 0중첩: 발동 없음\n디버프 1중첩 기준: "+DamageTooltip.Hits(player,perStack)
                        +"\n원시 피해 = 공격 피해 × "+KeywordDatabase.GetConstValue("woundExplosionDamage")+" × 소모 중첩 수"
                        +"\n내가 대상에게 건 모든 디버프의 중첩을 합산해 소비 · 출혈에 한정되지 않음"
                        +"\n현재·최대 피해는 대상의 디버프 상태에 따라 결정 · 다른 플레이어가 건 디버프 제외";
                    return true;
                }
                var stinger=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_SamuraiDemonStinger>():null;
                if(stinger&&stinger.GetType()==typeof(SpecialProjectile_SamuraiDemonStinger))
                {
                    // Native AttackOnServer uses damage directly and does not copy the
                    // parent's defaultDamageRatio or extra critical modifiers.
                    description="돌진 투사체 1회 적중: "+DamageTooltip.Hits(player,Scale(hit,1,false))
                        +"\n접촉 검사 간격 "+stinger.attackCheckTimer.time.ToString("0.###")+"초 · 지속 제한 "+stinger.destroyTimer.time.ToString("0.###")+"초"
                        +"\n공유 적중 목록이 있으면 같은 대상의 중복 적중 제외 · 없으면 접촉 중 재판정 가능";
                    return true;
                }
                var shield=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_ThrowingShield>():null;
                if(shield&&shield.GetType()==typeof(SpecialProjectile_ThrowingShield))
                {
                    int count=shield.defaultBulletCount;
                    int bonus=player.GetCustomStat(ECustomStat.BasicAttackDamageBonus);
                    int divisor=KeywordDatabase.GetConstValue("throwingShieldBulletCountBonusByBasicAttackDamage");
                    if(bonus>0&&divisor<=0){description="방패 발사 수 계산 상수 확인 필요";return true;}
                    if(bonus>0)count+=(int)((float)bonus/divisor);
                    description=count<=0?"방패 발사 수 0":Describe(player,Scale(hit,shield.defaultDamageRatio),shield.shieldBulletPrefab,"던지는 방패 1발")
                        +"\n평타 피해 증가에 따른 발사 수 "+count+"발 · 적중 수에 따라 총 피해 변동";
                    return true;
                }
                var flame=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_FlameSword>():null;
                if(flame&&flame.GetType()==typeof(SpecialProjectile_FlameSword))
                {
                    // This spawner creates a fresh melee and only transfers the flame-sword
                    // critical bonus, not the parent projectile's extra critical modifiers.
                    var normal=Scale(hit,1+player.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f,false);
                    normal.ExtraCritical=player.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE");
                    var lucky=Scale(normal,1+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent")/100f);
                    description=DescribeFlameMelee(player,normal,flame.meleePrefab,"화염검 일반 1타")+"\n"
                        +DescribeFlameMelee(player,lucky,flame.meleePrefabLucky?flame.meleePrefabLucky:flame.meleePrefab,"화염검 행운 1타")
                        +"\n행운 확률 "+Mathf.Clamp(player.GetCustomStatUnsafe("FLAMESWORDLUCK"),0,100)+"% · 무작위 추첨 없이 두 경우를 표시";
                    return true;
                }
                var blade=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_BladeZone>():null;
                if(blade&&blade.GetType()==typeof(SpecialProjectile_BladeZone))
                {
                    int count=blade.bladeZoneAttackCount;
                    int speed=player.GetCustomStat(ECustomStat.AttackSpeed);
                    if(speed>0&&blade.addAttackCountPerAttackSpeed>0)count+=speed/blade.addAttackCountPerAttackSpeed;
                    if(count<=0){description="회전 칼날 · 공격 횟수 0";return true;}
                    var first=Scale(hit,blade.defaultDamageRatio);first.Weapon=true;
                    var last=Scale(first,1+(count-1)*blade.addDamagePercentPerAttack/100f);
                    description="회전 칼날 첫 판정: "+DamageTooltip.Hits(player,first)+"\n마지막 "+count+"번째 판정: "+DamageTooltip.Hits(player,last)
                        +"\n판정 간격 "+blade.bladeZoneAttackTimer.time.ToString("0.###")+"초 · 판정이 진행될 때마다 첫 피해의 "+blade.addDamagePercentPerAttack+"% 추가"
                        +"\n공격 속도에 따른 판정 횟수 반영 · 빗나간 판정도 다음 피해 증가에 포함";
                    return true;
                }
                var area=special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_AreaJudgement>():null;
                if(area&&area.GetType()==typeof(SpecialProjectile_AreaJudgement))
                {
                    if(area.attackCount<=0){description="범위 공격 · 공격 횟수 0";return true;}
                    description="범위 공격 1회: "+DamageTooltip.Hits(player,Scale(hit,area.defaultDamageRatio))
                        +"\n최대 "+area.attackCount+"회 · 간격 "+area.bladeZoneAttackTimer.time.ToString("0.###")+"초 · 지속 제한 "+area.destroyTimer.time.ToString("0.###")+"초"
                        +"\n지속 시간 종료 시 남은 판정 취소 · 범위 내 실제 적중 횟수로 계산";
                    return true;
                }
                return false;
            }
            else return false;
            description=Describe(player,hit,prefab,"본타 1발")+note;
            return true;
        }
        private static string DescribeFlameMelee(PlayerAvatar player,DamageTooltip.Hit hit,GameObject prefab,string label)
        {
            var melee=prefab?prefab.GetComponent<MeleeCollision>():null;
            if(!melee)return label+": 근접 판정 데이터 없음";
            var result=Scale(hit,melee.defaultDamageRatio);
            if(result.Follower!=null)result.Follower.AfterFactors+=melee.additionalDamage;
            else result.AfterFactors+=melee.additionalDamage;
            return label+": "+DescribeMelee(player,SelectMeleeDamage(result,melee),melee);
        }
        internal static void PrepareMelee(DamageTooltip.Hit hit,NewWeaponFireData fire,float? ratioOverride=null)
        {
            var data=fire as NewWeaponFireData_MeleeAttack;
            var melee=data&&data.projectilePrefab?data.projectilePrefab.GetComponent<MeleeCollision>():null;
            if(!melee)return;
            float ratio=ratioOverride??melee.defaultDamageRatio;
            if(hit.Follower!=null)
            {
                hit.Follower.AttackFactor*=ratio;
                hit.Follower.AfterFactors+=melee.additionalDamage;
            }
            else
            {
                var factors=new List<float>(hit.Factors??new float[0]);factors.Add(ratio);hit.Factors=factors.ToArray();
                hit.AfterFactors+=melee.additionalDamage;
            }
        }
        internal static string DescribeMelee(PlayerAvatar player,DamageTooltip.Hit prepared,NewWeaponFireData fire)
        {
            var data=fire as NewWeaponFireData_MeleeAttack;
            var melee=data&&data.projectilePrefab?data.projectilePrefab.GetComponent<MeleeCollision>():null;
            return DescribeMelee(player,prepared,melee);
        }
        internal static string DescribeMelee(PlayerAvatar player,DamageTooltip.Hit[] prepared,NewWeaponFireData fire)
        {
            var data=fire as NewWeaponFireData_MeleeAttack;
            var melee=data&&data.projectilePrefab?data.projectilePrefab.GetComponent<MeleeCollision>():null;
            string result=DamageTooltip.Hits(player,prepared);
            if(melee&&melee.multiHit>1)result+="\n"+melee.multiHit+"회 · 간격 "+melee.multiHitIntervalTimer.time.ToString("0.###")+"초";
            return result;
        }
        internal static DamageTooltip.Hit SelectMeleeDamage(DamageTooltip.Hit prepared,NewWeaponFireData fire)
        {
            var data=fire as NewWeaponFireData_MeleeAttack;
            var distance=data&&data.projectilePrefab?data.projectilePrefab.GetComponent<MeleeCollision_Circle_Distance>():null;
            return SelectMeleeDamage(prepared,distance);
        }
        internal static DamageTooltip.Hit SelectMeleeDamage(DamageTooltip.Hit prepared,MeleeCollision melee)
        {
            var distance=melee as MeleeCollision_Circle_Distance;
            if(!distance||DamageTooltip.CurrentCapture==null||!DamageTooltip.CurrentCapture.FullConditions)return prepared;
            var outside=Scale(prepared,1+distance.damageBonus);
            // Native outer contact does not include melee additionalDamage.
            if(outside.Follower!=null)outside.Follower.AfterFactors=0;
            else outside.AfterFactors=0;
            return outside;
        }
        private static string DescribeMelee(PlayerAvatar player,DamageTooltip.Hit prepared,MeleeCollision melee)
        {
            if(!melee)return DamageTooltip.Hits(player,prepared);
            string result=DamageTooltip.Hits(player,prepared);
            if(melee.multiHit>1)result+="\n"+melee.multiHit+"회 · 간격 "+melee.multiHitIntervalTimer.time.ToString("0.###")+"초";
            return result;
        }
        internal static string Describe(PlayerAvatar player,DamageTooltip.Hit hit,GameObject prefab,string label)
        {
            var text=new StringBuilder();
            DescribeInto(player,hit,prefab,label,text,new HashSet<int>());
            return text.ToString().TrimEnd();
        }
        internal static DamageTooltip.Hit Scale(DamageTooltip.Hit hit,float factor,bool keepCritical=true,EDamageElementalType? element=null,EDamageType? damageType=null)
        {
            if(hit.Follower!=null)
            {
                var follower=hit.Follower.Scaled(factor);
                if(element.HasValue)follower.DamageElement=element.Value;
                if(damageType.HasValue)follower.ElementalEffect=damageType.Value==EDamageType.ElementalEffectDamage;
                return new DamageTooltip.Hit{Follower=follower,
                    Magic=hit.Magic,ExtraCriticalChance=keepCritical?hit.ExtraCriticalChance:0,
                    ProjectileDamagePercent=keepCritical?hit.ProjectileDamagePercent:0,
                    CanCritical=keepCritical?hit.CanCritical:true,
                    ExtraCritical=keepCritical?hit.ExtraCritical:0,
                    CriticalRateMultiplier=keepCritical?hit.CriticalRateMultiplier:1};
            }
            var factors=new List<float>(hit.Factors??new float[0]);factors.Add(factor);
            return new DamageTooltip.Hit{Magic=hit.Magic,ExtraCriticalChance=keepCritical?hit.ExtraCriticalChance:0,Raw=hit.Raw,Element=element??hit.Element,ResourceAmount=hit.ResourceAmount,ResourcePerUnit=hit.ResourcePerUnit,Factors=factors.ToArray(),AfterFactors=hit.AfterFactors*factor,ProjectileDamagePercent=keepCritical?hit.ProjectileDamagePercent:0,Weapon=hit.Weapon,ElementalEffect=damageType.HasValue?damageType.Value==EDamageType.ElementalEffectDamage:hit.ElementalEffect,CanCritical=keepCritical?hit.CanCritical:true,ExtraCritical=keepCritical?hit.ExtraCritical:0,CriticalRateMultiplier=keepCritical?hit.CriticalRateMultiplier:1};
        }
        private static void DescribeInto(PlayerAvatar p,DamageTooltip.Hit hit,GameObject prefab,string label,StringBuilder text,HashSet<int> path)
        {
            var bullet=prefab?prefab.GetComponent<Bullet>():null;
            if(!bullet){text.Append(label).AppendLine(": 투사체 데이터 없음");return;}
            if(!path.Add(prefab.GetInstanceID())){text.Append(label).AppendLine(": 반복 생성 투사체 · 이전 1발 수치 참조");return;}
            // Receiving damage and dealing damage are independent native settings.
            if(bullet.collosionType!=Bullet.ECollisionTiming.None&&bullet.damageDealtType==Bullet.EDamageDealtType.Normal)
                text.Append(label).Append(" · 충돌 1회: ").AppendLine(DamageTooltip.Hits(p,Scale(hit,bullet.defaultDamageRatio*bullet.DamageMultiplier,damageType:bullet.IsOverrideDamageType?bullet.DamageType:EDamageType.Projectile)));
            else if(bullet.collosionType==Bullet.ECollisionTiming.None)
                text.Append(label).AppendLine(" · 접촉 피해 판정 없음");
            else text.Append(label).AppendLine(" · 접촉 기본 피해 0 · 후속 효과 별도");
            var destroy=prefab.GetComponent<BulletDestroyModule>();
            if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_Explode))
            {
                var explosion=(BulletDestroyModule_Explode)destroy;
                if(explosion.explodeRadius>0)
                    text.Append(label).Append(" · 폭발 1회: ").AppendLine(DamageTooltip.Hits(p,Scale(hit,bullet.defaultDamageRatio,true,explosion.inheritElementalType?(EDamageElementalType?)null:explosion.elementalType,explosion.explosionDamageType)));
                if(explosion.bulletPrefab)
                {
                    DescribeInto(p,Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType),explosion.bulletPrefab,label+" 폭발 후 파편 1발",text,path);
                    AppendSpreadCount(text,explosion.spreadDeltaAngle,false);
                }
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_CreateOtherBullet))
            {
                // The native method does not read its serialized damageMultiplier field.
                var other=(BulletDestroyModule_CreateOtherBullet)destroy;
                if(other.bulletPrefab)DescribeInto(p,Scale(hit,bullet.defaultDamageRatio,false,EDamageElementalType.Physical),other.bulletPrefab,label+" 소멸 후 생성",text,path);
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_HeavyExplode))
            {
                var explosion=(BulletDestroyModule_HeavyExplode)destroy;
                // HeavyExplode does not copy projectile-specific critical bonuses.
                if(explosion.explodeRadius>0)text.Append(label).Append(" · 대형 폭발 1회: ").AppendLine(DamageTooltip.Hits(p,Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType,explosion.explosionDamageType)));
                if(explosion.bulletPrefab)
                {
                    DescribeInto(p,Scale(hit,bullet.defaultDamageRatio,false,explosion.elementalType),explosion.bulletPrefab,label+" 대형 폭발 후 파편 1발",text,path);
                    AppendSpreadCount(text,explosion.spreadDeltaAngle,false);
                }
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_CreateBulletSpread))
            {
                var spread=(BulletDestroyModule_CreateBulletSpread)destroy;
                int divisor=spread.spreadDeltaAngle>0?(int)(360f/spread.spreadDeltaAngle):0;
                if(spread.bulletPrefab&&(!spread.isDamageDivided||divisor>0))
                {
                    DescribeInto(p,Scale(hit,bullet.defaultDamageRatio/(spread.isDamageDivided?divisor:1),false,EDamageElementalType.Physical),spread.bulletPrefab,label+" 분산탄 1발",text,path);
                    AppendSpreadCount(text,spread.spreadDeltaAngle,spread.isDamageDivided);
                }
            }
            else if(destroy&&(destroy.GetType()==typeof(BulletDestroyModule_ProgressiveExplode)||destroy.GetType()==typeof(BulletDestroyModule_Linebomb)))
            {
                var wave=Scale(hit,bullet.defaultDamageRatio,false,EDamageElementalType.Physical,EDamageType.Projectile);wave.Weapon=false;wave.Magic=false;
                if(wave.Follower!=null)wave.Follower.Direct=false;
                text.Append(label).Append(" · 연쇄 폭발의 대상 1명: ").AppendLine(DamageTooltip.Hits(p,wave));
                text.AppendLine("연쇄 폭발은 적중 목록 공유 · 같은 적에 중복 합산하지 않음");
            }
            else if(destroy&&destroy.GetType()==typeof(BulletDestroyModule_MakeGroundFire))
            {
                var ground=(BulletDestroyModule_MakeGroundFire)destroy;
                if(ground.explodeRadius>0&&hit.Follower==null)AppendGround(text,p,"소멸 후 화염 장판");
            }
            var trail=prefab.GetComponent<BulletMoveModule_UniformVector2_MakeGroundFire>();
            if(trail&&trail.trailRadius>0&&hit.Follower==null)AppendGround(text,p,"이동 궤적의 화염 장판");
            if(bullet.collosionType==Bullet.ECollisionTiming.Stay)
                text.Append("지속 접촉 판정 간격 ").Append(bullet.collisionStayDamageIntervalTimer.time.ToString("0.###")).AppendLine("초");
            path.Remove(prefab.GetInstanceID());
        }
        private static void AppendSpreadCount(StringBuilder text,float angle,bool divided)
        {
            if(angle<=0||float.IsNaN(angle)||float.IsInfinity(angle))
            {
                text.AppendLine("분산 각도 데이터 확인 필요");return;
            }
            // Native loop uses angle < 360, while its damage divisor truncates 360/angle.
            // These differ when the angle does not divide 360 exactly.
            text.Append("전체 방향 ").Append(Mathf.CeilToInt(360f/angle)).Append("발 생성");
            if(divided)text.Append(" · 1발 피해는 원시 피해 ÷ ").Append((int)(360f/angle));
            text.AppendLine(" · 같은 적에게 전부 적중한다고 합산하지 않음");
        }
        private static void AppendGround(StringBuilder text,PlayerAvatar p,string label)
        {
            if(!p.HasFlameGround()||p.GetCustomStat(ECustomStat.FlameGroundDisable)>0)return;
            text.Append(label).Append(" 1틱: ").AppendLine(DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=2,Element=EDamageElementalType.Fire,ResourceAmount=p.GetCustomStat(ECustomStat.FireDamage),ResourcePerUnit=.5f,ElementalEffect=true}));
            text.Append("0.25초 간격 · 지속 ").Append((int)(3*p.GetFlameGround().durationPercent/100f)).AppendLine("틱 · 겹친 장판은 같은 적에 틱당 1회만 적용");
        }
    }
}
