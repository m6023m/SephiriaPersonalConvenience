using System;
using System.Text;
using UnityEngine;
using HarmonyLib;

namespace SephiriaDicePreview
{
    // Each rule is isolated so its verification dependency can be invalidated independently.
    internal static class ArtifactDamageProfiles
    {
        private static readonly System.Reflection.FieldInfo ForkStack=AccessTools.Field(typeof(Charm_TuningForks),"currentStack");
        private static readonly System.Reflection.FieldInfo AutoCaster=AccessTools.Field(typeof(Charm_AutoMagic),"casterObject");
        internal static bool TryDescribe(Charm_Basic source, Charm_Basic live, int level, PlayerAvatar player, out string text,ItemEntity entity=null)
        {
            text=null;
            if(BasicArtifactProfiles.TryDescribe(source,live,level,out text))return true;
            if(ResourceArtifactProfiles.TryDescribe(source,live,level,player,out text))return true;
            if(GrowthArtifactProfiles.TryDescribe(source,live,level,out text))return true;
            if(source is Charm_SummonGreenBat)return PlanetDamageProfiles.TryDescribe((Charm_SummonGreenBat)source,live as Charm_SummonGreenBat,entity,level,player,out text);
            if(source is Charm_SummonUnit && FollowerDamageProfiles.TryCharm((Charm_SummonUnit)source,live as Charm_SummonUnit,level,player,out text))return true;
            if(source is Charm_MiniBallista && FollowerDamageProfiles.TryBallista((Charm_MiniBallista)source,live as Charm_MiniBallista,level,player,out text))return true;
            if(source is Charm_LeadNPC && FollowerDamageProfiles.TryLead((Charm_LeadNPC)source,live as Charm_LeadNPC,level,player,out text))return true;
            if(source is Charm_Magic && MagicDamageProfiles.TryDescribe((Charm_Magic)source,live as Charm_Magic,level,player,out text))return true;
            if(source is Charm_AutoMagic){text=AutomaticMagic((Charm_AutoMagic)source,live as Charm_AutoMagic,level,player);return true;}
            if(source is Charm_AirSlash)text=ChargingDamageProfiles.AirSlash((Charm_AirSlash)source,live,level,player);
            else if(source is Charm_IceSpear)text=ChargingDamageProfiles.Spear((Charm_IceSpear)source,live,level,player);
            else if(source is Charm_IceBow)text=ChargingDamageProfiles.Bow((Charm_IceBow)source,live as Charm_IceBow,level,player);
            else if(source is Charm_IceHammer)text=NativeStats((Charm_IceHammer)source,level)+"\n"+ChargingDamageProfiles.Hammer((Charm_IceHammer)source,live,level,player);
            else if(source is Charm_FireChakram)text=Chakram((Charm_FireChakram)source,live,level,player);
            else if(source is Charm_Kunai)text=Kunai((Charm_Kunai)source,live,level,player);
            else if(source is Charm_FireBulletOnHit)text=BulletOnHit((Charm_FireBulletOnHit)source,live,level,player);
            else if(source is Charm_FireBulletInRange)text=BulletInRange((Charm_FireBulletInRange)source,live,level,player);
            else if(source is Charm_FireFeather)text=Feather((Charm_FireFeather)source,live,level,player);
            else if(source is Charm_ElectricEarring)text=Earring((Charm_ElectricEarring)source,live,level,player);
            else if(source is Charm_FlamePlantRoot)text=PlantRoot((Charm_FlamePlantRoot)source,live,level,player);
            else if(source is Charm_FrostiumRing)text=Frostium((Charm_FrostiumRing)source,live as Charm_FrostiumRing,level,player);
            else if(source is Charm_PallasCard)text=Pallas((Charm_PallasCard)source,live,level,player);
            else if(source is Charm_Guillotine)text=Guillotine((Charm_Guillotine)source,live,level,player);
            else if(source is Charm_GreenGi)text=GreenGi((Charm_GreenGi)source,live,level,player);
            else if(source is Charm_Golem_Gun)text=GolemGun((Charm_Golem_Gun)source,live,level,player);
            else if(source is Charm_Golem_Laser)text=GolemLaser((Charm_Golem_Laser)source,live,level,player);
            else if(source is Charm_RockElephant)text=RockElephant((Charm_RockElephant)source,live,level,player);
            else if(source is Charm_DashDamage)text=DashImpact((Charm_DashDamage)source,live,level,player);
            else if(source is Charm_CreateBulletOnSweep)text=SweepBullet((Charm_CreateBulletOnSweep)source,live,level,player);
            else if(source is Charm_FreezeNormalSlash)text=FreezeSlash((Charm_FreezeNormalSlash)source,live,level,player);
            else if(source is Charm_IceBat)text=IceBat((Charm_IceBat)source,live,level,player);
            else if(source is Charm_EchoOfTheGlacier)text=Glacier((Charm_EchoOfTheGlacier)source,live,level,player);
            else if(source is Charm_TheTyphoonSheetmusic)text=Typhoon((Charm_TheTyphoonSheetmusic)source,live,level,player);
            else if(source is Charm_GrowthParry)text=GrowthParry((Charm_GrowthParry)source,live,level,player);
            else if(source is Charm_FlameGround_Meteor)text=FlameMeteor((Charm_FlameGround_Meteor)source,live,level,player);
            else if(source is Charm_UpCharmDamage)text=LinkedArtifactBonus((Charm_UpCharmDamage)source,live as Charm_UpCharmDamage,level,player);
            else if(source is Charm_Reddew)text=RedDew((Charm_Reddew)source,live,level,player);
            else if(source is Charm_IceSword)text=IceRelic((Charm_IceSword)source,live,level,player);
            else if(source is Charm_TuningForks)text=TuningFork((Charm_TuningForks)source,live as Charm_TuningForks,level,player);
            else if(source is Charm_BoltMagicMultiShot)text=BoltMultiShot((Charm_BoltMagicMultiShot)source,level);
            else if(source is Charm_TooCloseDamage)text=CloseTarget((Charm_TooCloseDamage)source,live as Charm_TooCloseDamage,level);
            else if(source is Charm_FirstAttackBonusDamage)text=FirstHit((Charm_FirstAttackBonusDamage)source,level);
            else if(source is Charm_BurnTargetDamageBonus)text=BurnTarget((Charm_BurnTargetDamageBonus)source,level);
            else if(source is Charm_MiniBossFight)text=MiniBossGrowth((Charm_MiniBossFight)source,level);
            else if(source is Charm_NearLevelDamage)text=NeighbourLevels((Charm_NearLevelDamage)source,live as Charm_NearLevelDamage,level,player);
            else if(source is Charm_DashAttackStack)text=AttackStack((Charm_DashAttackStack)source,live as Charm_DashAttackStack,level);
            else if(source is Charm_AddStatByDefense)text=DefenseConversion((Charm_AddStatByDefense)source,level,player);
            else if(source is Charm_AddStatByAnotherStat)text=StatConversion((Charm_AddStatByAnotherStat)source,level,player);
            else if(source is Charm_ReservedMPBonus)text=ReservedMagic((Charm_ReservedMPBonus)source,level);
            else if(source.GetType()==typeof(Charm_StatusInstance))text=NativeStats((Charm_StatusInstance)source,level);
            else if(source is Charm_LakeSpirit)text=LakeSpirit((Charm_LakeSpirit)source,live,level,player);
            else if(source is Charm_ShadowEye)text=ShadowEye((Charm_ShadowEye)source,live,level,player);
            else if(source is Charm_NearMagicBullet)text=LinkedMagic((Charm_NearMagicBullet)source,live,level,player);
            else if(source is Charm_GuardCounter)text=GuardCounter((Charm_GuardCounter)source,live,level,player);
            else if(source is Charm_FlameSwordFall)text=FlameSwordReturn((Charm_FlameSwordFall)source,live,player);
            else if(source is Charm_IncreaseLastAttackDamage_SwordAndShield)
            {
                var c=(Charm_IncreaseLastAttackDamage_SwordAndShield)source;
                float bonus=c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level));
                text="마지막 평타 추가 피해 +"+bonus.ToString("0.###")+"%\n일반 평타: +0% · 마지막 평타 또는 강제 마지막 판정: +"+bonus.ToString("0.###")+"%\n다른 생성 시 추가 피해와 합산 · 활성 상태에서는 무기 본타 계산에 반영";
            }
            else if(source is Charm_FinalComboCritical)
            {
                var c=(Charm_FinalComboCritical)source;
                float bonus=c.criticalBonusPercentByLevel.SafeRandomAccess(c.LevelToIdx(level));
                text="마지막 평타 치명타 확률 +"+bonus.ToString("0.###")+"%p\n일반 평타: +0%p · 마지막 평타 또는 강제 마지막 판정에 적용\n일반/치명타 각각의 피해량은 바뀌지 않으며 치명타 발생 확률만 증가";
            }
            else if(source is Charm_CritAndRanged)
            {
                var c=(Charm_CritAndRanged)source;var active=live as Charm_CritAndRanged;
                text=NativeStats(c,level)+"\n주변 "+c.disableRange.ToString("0.###")+" 미만 거리에 공격 가능한 적이 있을 때 치명타 피해 보정 "+c.critDamageOnApplied.ToString("+0;-0;0")+"%p\n최소 "+Math.Min(0,c.critDamageOnApplied)+"%p · 최대 "+Math.Max(0,c.critDamageOnApplied)+"%p\n현재 보정 "+(active&&active.IsEffectEnabled&&active.applied?c.critDamageOnApplied:0).ToString("+0;-0;0")+"%p\n현재 적용된 보정은 공격 피해에 이미 합산";
            }
            return text!=null;
        }
        private static float RootBonus(Charm_Basic live,PlayerAvatar p)
        {
            return 1+(live && live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f;
        }
        private static string RedDew(Charm_Reddew c,Charm_Basic live,int level,PlayerAvatar p)
        {
            // Native GetHighestDamageElementalType consumes RNG for ties. The numeric value is
            // identical for every tied element, so a preview must not invoke that selector.
            int highest=Math.Max(Math.Max(p.GetCustomStat(ECustomStat.PhysicalDamage),p.GetCustomStat(ECustomStat.FireDamage)),Math.Max(p.GetCustomStat(ECustomStat.IceDamage),p.GetCustomStat(ECustomStat.LightningDamage)));
            var hit=new DamageTooltip.Hit{Raw=highest,Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)},CanCritical=false};
            return NativeStats(c,level)+"\n붉은 이슬 · 대상 1명·폭발 1회\n"+DamageTooltip.Hits(p,hit)+"\n가장 높은 속성 능력치 "+highest+" 기준 · 동률 속성은 피해 수치 동일\n치명타·처형 적중 후 발동 · 이 폭발 자체는 치명타 불가\n재사용 간격 "+c.coolDownTimer.time.ToString("0.###")+"초 · 자신의 폭발로 재발동하지 않음";
        }
        private static string BulletOnHit(Charm_FireBulletOnHit c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var fire=c.fireData as NewWeaponFireData_Bullet;
            if(!fire)return "피격 후 발사 · 전용 발사 데이터 계산 필요";
            var hit=new DamageTooltip.Hit{Raw=c.damageByLevel.SafeRandomAccess(c.LevelToIdx(level)),Factors=new[]{RootBonus(live,p),fire.damageMultiplier,fire.CalculateFinalDamageMultiplier(0)}};
            hit.Element=fire.damageElementalType;
            return "피격 후 투사체 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,fire.bulletPrefab,"발사")+"\n피격 생존 시 "+c.countByLevel.SafeRandomAccess(c.LevelToIdx(level))+"발 · 재사용 간격 "+c.coolDownTimer.time.ToString("0.###")+"초\n무작위 방향으로 발사 · 실제 적중한 충돌·폭발만 합산";
        }
        private static string BulletInRange(Charm_FireBulletInRange c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int[] values={p.GetCustomStat(ECustomStat.PhysicalDamage),p.GetCustomStat(ECustomStat.FireDamage),p.GetCustomStat(ECustomStat.IceDamage),p.GetCustomStat(ECustomStat.LightningDamage)};
            int highest=Math.Max(Math.Max(values[0],values[1]),Math.Max(values[2],values[3]));
            string[] names={"물리","화염","냉기","번개"};var text=new StringBuilder("범위 투척 · 최고 속성의 투사체 1발 · 일반 / 치명타\n");
            for(int i=0;i<values.Length;i++)if(values[i]==highest&&i<c.bulletPrefabs.Count)
            {
                var hit=new DamageTooltip.Hit{Raw=highest,Element=(EDamageElementalType)i,Factors=new[]{c.damageRatioByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}};
                text.AppendLine(ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefabs[i],names[i]));
            }
            text.Append("발사 ").Append(c.countByLevel.SafeRandomAccess(c.LevelToIdx(level))).Append("발 · 재사용 ").Append(c.coolDownTimer.time.ToString("0.###")).AppendLine("초");
            text.Append("발사 간격 ").Append((c.fireInterval-c.fireIntervalError).ToString("0.###")).Append("~").Append((c.fireInterval+c.fireIntervalError).ToString("0.###")).AppendLine("초");
            return text.Append("최고 속성이 같으면 매 발 해당 속성 중 무작위 선택 · 각 종류의 충돌·폭발을 따로 표시").ToString();
        }
        private static string Feather(Charm_FireFeather c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=c.featherDamage,Factors=new[]{RootBonus(live,p)}};
            hit.Element=EDamageElementalType.Fire;
            return NativeStats(c,level)+"\n화염 깃털 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"깃털")+"\n서로 다른 적 최대 "+c.numberOfTargetByLevel.SafeRandomAccess(c.LevelToIdx(level))+"명에게 1발씩\n준비 시간 "+c.featherEnableTimer.time.ToString("0.###")+"초 · 대상 탐색 간격 "+c.searchEnemyTimer.time.ToString("0.###")+"초\n적중 시 화상 부여 · 별도 화상 피해는 탄환 피해와 구분";
        }
        private static string Earring(Charm_ElectricEarring c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.LightningDamage),Factors=new[]{c.damagePercent/100f,1+p.GetCustomStatUnsafe("ELECTRICEARRINGDAMAGE")/100f,RootBonus(live,p)}};
            hit.Element=EDamageElementalType.Lightning;
            int count=Math.Max(0,c.countByLevel.SafeRandomAccess(c.LevelToIdx(level))+p.GetCustomStatUnsafe("ELECTRICEARRINGCOUNT"));
            return NativeStats(c,level)+"\n번개 귀걸이 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"낙뢰")+"\n최대 "+count+"발 · 발사 간격 0.25초 · 같은 적이 다시 선택될 수 있음\n재사용 시간 "+c.cooldownTimer.time.ToString("0.###")+"초 · 대상이 없으면 발사 중단\n적중 시 디버프 부여"+(p.GetCustomStatUnsafe("ELECTRICEARRINGSWEEP")>0?"\n특수 공격으로 대기 중인 낙뢰를 준비 상태로 전환":"");
        }
        private static string PlantRoot(Charm_FlamePlantRoot c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.FireDamage),Factors=new[]{c.fireDamageRatioByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}};
            hit.Element=EDamageElementalType.Fire;
            return NativeStats(c,level)+"\n화염 뿌리 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"화염")+"\n격노(FURY) 특수 공격 적중 시 생성 · 발동 간격 0.32초";
        }
        private static string Frostium(Charm_FrostiumRing c,Charm_FrostiumRing live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=c.damageByLevel.SafeRandomAccess(c.LevelToIdx(level)),Factors=new[]{RootBonus(live,p)}};
            hit.Element=c.elementalType;
            bool fast=live&&(bool)AccessTools.Field(typeof(Charm_FrostiumRing),"enabledFast").GetValue(live);
            string element=KeywordDatabase.Convert("<tag="+c.elementalType+"Damage>");
            return element+" 반지 추가타 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n무기 직접 공격 또는 마법이 살아 있는 적에게 적중하면 발동\n기본 발동 간격 "+c.cooldownTimer.time.ToString("0.###")+"초 · 빠른 배치 효과 시 "+(c.cooldownTimer.time/3f).ToString("0.###")+"초\n현재 빠른 배치 효과: "+(fast?"활성":"비활성")+"\n추가타 자체는 무기·마법 공격 피해 보너스를 적용하지 않음";
        }
        private static string Pallas(Charm_PallasCard c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var text=new StringBuilder("카드 1장 · 일반 / 치명타\n");
            float root=RootBonus(live,p);
            for(int i=0;i<c.bulletSmallPrefab.Length;i++)text.AppendLine(ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Raw=c.bulletDamage,Factors=new[]{root,1f/3}},c.bulletSmallPrefab[i],"작은 카드 종류 "+(i+1)));
            for(int i=0;i<c.bulletBigPrefab.Length;i++)text.AppendLine(ProjectileDamageProfiles.Describe(p,new DamageTooltip.Hit{Raw=c.bulletDamage,Factors=new[]{root,1f/3,2f}},c.bulletBigPrefab[i],"큰 카드 종류 "+(i+1)));
            var controller=p.GetComponent<WeaponControllerSimple>();float weight=controller&&controller.currentWeapon?controller.currentWeapon.AttackWeightPerSwing:1;
            float chance=(c.defaultChance+c.throwChanceByLevel.SafeRandomAccess(c.LevelToIdx(level))*Mathf.Clamp(p.GetCustomStat(ECustomStat.Luck),0,9999))*weight;
            text.Append("발동 시 3장 · 각 장마다 큰 카드 확률 20%\n행운·현재 무기 기준 발동 확률 ").Append(Mathf.Clamp(chance,0,100).ToString("0.##")).Append("% · 발동 간격 ").Append(c.throwIntervalTimer.time.ToString("0.###")).AppendLine("초");
            return text.Append("기본 피해를 3장에 나누고 큰 카드만 2배 · 종류별로 실제 적중한 충돌·폭발만 합산").ToString();
        }
        private static string GolemGun(Charm_Golem_Gun c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStatUnsafe(c.relatedStat),Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}};
            hit.Element=c.damageElementalType;
            float speed=(p.GetCustomStatUnsafe("ATTACKSPEED")+100)/100f;
            return "골렘 포 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"탄환")+"\n현재 발사 간격 "+(speed>0?(c.defaultAttackIntervalTimer.time/speed).ToString("0.###")+"초":"진행 불가")+"\n공격 입력을 유지할 때 발사 · 플레이어가 발사자로 지정됨\n동료 소환 피해 배율을 별도로 더하지 않음";
        }
        private static string FreezeSlash(Charm_FreezeNormalSlash c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level);
            float raw=p.GetCustomStat(ECustomStat.PhysicalDamage)*c.phSlashDamageMultiplierByLevel.SafeRandomAccess(idx)/100f+p.GetCustomStat(ECustomStat.IceDamage)*c.slashDamageMultiplierByLevel.SafeRandomAccess(idx)/100f;
            var hit=new DamageTooltip.Hit{Raw=raw,Factors=new[]{1+p.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,RootBonus(live,p)},ExtraCritical=p.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE")};
            hit.Element=EDamageElementalType.Ice;
            var controller=p.GetComponent<WeaponControllerSimple>();float weight=controller&&controller.currentWeapon?controller.currentWeapon.AttackWeightPerSwing:1;
            return "빙결 추가 베기 1타 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n물리·냉기 비례 항 합산 후 무기·평타·최종 무기 보너스 적용\n무기 치명타 피해는 추가하되 직접 공격 증폭은 적용하지 않음\n평타 적중 시 발동 확률 "+Mathf.Clamp(c.slashChanceByLevel.SafeRandomAccess(idx)*weight,0,100).ToString("0.#")+"% · 발동 후 다음 갱신부터 "+c.slashIntervalTimer.time.ToString("0.###")+"초 대기";
        }
        private static string IceBat(Charm_IceBat c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.IceDamage)*c.iceDamage,Factors=new[]{.01f,RootBonus(live,p),(1+p.GetCustomStat(ECustomStat.AttackSpeed)/100f)*(c.attackSpeedBonusByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f),.5f}};
            hit.Element=c.bulletPrefab?c.bulletPrefab.elementalType:EDamageElementalType.Ice;
            return NativeStats(c,level)+"\n얼음 박쥐 투사체 1발 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n대상당 2발 · 두 발 모두 적중: "+DamageTooltip.Hits(p,hit,hit)+"\n자신이 부여한 동상 상태의 적에게 발사 · 탐색 반경 12\n발동 간격 "+c.attackTimer.time.ToString("0.###")+"초 · 두 번째 발사까지 0.125초\n공격 속도는 피해 배율에 적용 · 추가 디버프 접촉 발동을 억제";
        }
        private static string Glacier(Charm_EchoOfTheGlacier c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int upgrade=p.GetCustomStatUnsafe("ECHOOFTHEGLACIERUPGRADE");
            string damage=upgrade>0?DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.IceDamage)*upgrade,Element=EDamageElementalType.Ice,Factors=new[]{.01f,RootBonus(live,p)}}):"0 (현재 강화 없음: 동상만 부여)";
            return NativeStats(c,level)+"\n빙하 메아리 대상 1명·1회 · 일반 / 치명타\n"+damage+"\n범위 내 적에게 동상 부여 · 발동 간격 "+c.frostbiteTimeByLevel.SafeRandomAccess(c.LevelToIdx(level)).ToString("0.###")+"초"+(p.GetCustomStatUnsafe("ECHOOFTHEGLACIERPARRY")>0?"\n패리 성공 시 추가 발동":"")+"\n강화의 직접 피해와 동상 부여 효과를 구분";
        }
        private static string Typhoon(Charm_TheTyphoonSheetmusic c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level);
            return NativeStats(c,level)+"\n태풍 악보 번개 추가타 · 일반 / 치명타\n"+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=c.damageByLevel.SafeRandomAccess(idx),Element=EDamageElementalType.Lightning,Factors=new[]{RootBonus(live,p)}})+"\n무기 직접 공격이 살아 있는 적에게 적중할 때 1회\n먹구름 공격 속도 +"+c.cloudAttackSpeedByLevel.SafeRandomAccess(idx)+"% · 이 추가타 자체는 무기 공격으로 취급하지 않음";
        }
        private static string GrowthParry(Charm_GrowthParry c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int highest=Math.Max(Math.Max(p.GetCustomStatUnsafe("PHYSICALDAMAGE"),p.GetCustomStatUnsafe("FIREDAMAGE")),Math.Max(p.GetCustomStatUnsafe("ICEDAMAGE"),p.GetCustomStatUnsafe("LIGHTNINGDAMAGE")));
            float factor=c.parryDamageByLevel.Length>0?c.parryDamageByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f:0;
            return NativeStats(c,level)+"\n성장 패리 탄환 대상 1명·1타 · 일반 / 치명타\n"+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=highest,Factors=new[]{factor,RootBonus(live,p)},Weapon=true})+"\n최고 속성 수치 기준 · 원본 탄환은 무기 직접 공격 판정\n패리 성공 후 주변 대상에게 발사"+(c.hasExtraTriggers?"\n평타 마지막 공격의 첫 적중과 격노 적중으로도 발동\n격노 추가 발동 간격 "+c.extraTriggerCooldownTimer.time.ToString("0.###")+"초":"");
        }
        private static string FlameMeteor(Charm_FlameGround_Meteor c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.FireDamage),Factors=new[]{c.damagesByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,1+p.GetCustomStatUnsafe("REDSNAKEEYEDAMAGEBONUS")/100f,RootBonus(live,p)}};
            hit.Element=EDamageElementalType.Fire;
            return "화염 운석 1개 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"운석")+"\n발동당 "+c.countByLevel.SafeRandomAccess(c.LevelToIdx(level))+"개 · 기본 재사용 "+c.cooldownTimer.time.ToString("0.###")+"초\n매 운석마다 대상을 다시 탐색하므로 같은 적이 다시 선택될 수 있음\n충돌·폭발·장판은 각 실제 판정만 합산 · 적중 시 화상 부여";
        }
        private static string LinkedArtifactBonus(Charm_UpCharmDamage c,Charm_UpCharmDamage live,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level),basic=c.damageBonusByLevel.SafeRandomAccess(idx),extra=c.hasDependencyCondition?c.dependencyDamageBonusByLevel.SafeRandomAccess(idx):0;
            string text="연결 아티팩트 피해 증가\n기본 +"+basic+"%"+(c.hasDependencyCondition?"\n희귀도 조건까지 충족 시 +"+(basic+extra)+"%":"")+"\n연결된 공격형 아티팩트의 원시 피해 단계에서 반영\n전체 플레이어 공격에 일괄 적용하지 않음";
            if(live&&live.Item!=null&&p.Inventory)
            {
                var item=p.Inventory.FindItem(new ItemPosition(live.Item.XIdx+c.xOffset,live.Item.YIdx+c.yOffset));
                var seen=new System.Collections.Generic.HashSet<int>();seen.Add(live.GetInstanceID());
                while(item!=null&&item.Charm is Charm_UpCharmDamage)
                {
                    var link=(Charm_UpCharmDamage)item.Charm;
                    if(!seen.Add(link.GetInstanceID()))return text+"\n현재 연결: 순환 연결로 최종 공격 장비 없음";
                    item=p.Inventory.FindItem(new ItemPosition(item.XIdx+link.xOffset,item.YIdx+link.yOffset));
                }
                if(item==null||!item.Charm||!live.IsDependencyValid(item.Charm))return text+"\n현재 연결 칸: 적용 대상 없음";
                var entity=ItemDatabase.FindItemById(item.EntityID);
                int current=basic+(c.hasDependencyCondition&&entity&&entity.rarity<=c.maxRarity?extra:0);
                text+="\n최종 연결 장비에 이 효과로 +"+current+"% · 해당 장비의 피해 계산에 포함";
            }
            return text;
        }
        private static string DashImpact(Charm_DashDamage c,Charm_Basic live,int level,PlayerAvatar p)
        {
            float raw=p.GetCustomStat(ECustomStat.PhysicalDamage)*c.physicalDashDamage.SafeRandomAccess(c.LevelToIdx(level));
            float root=RootBonus(live,p);
            return "돌진 추가 판정 1회 · 일반 / 치명타\n돌진 비용 없음: "+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=raw,Factors=new[]{.01f,root}})+"\n돌진 비용 지불: "+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=raw,Factors=new[]{.01f,root,2f}})+"\n비용을 낸 돌진은 2배 · 무기 돌진 공격과 별도의 아티팩트 타격";
        }
        private static string SweepBullet(Charm_CreateBulletOnSweep c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool orbit=c.orbitBulletPrefab&&p.GetCustomStatUnsafe(c.orbitStatId)>0;
            var hit=new DamageTooltip.Hit{Raw=c.defaultDamage+p.GetCustomStat(c.effectElemental)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,Factors=new[]{RootBonus(live,p)}};
            hit.Element=c.damageElementalType;
            return "특수 공격 후 추가 탄환 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,orbit?c.orbitBulletPrefab:c.bulletPrefab,orbit?"회전 탄환":"일반 탄환")+"\n"+(orbit?"회전 강화 활성: 특수 공격마다 생성 · 일반 재사용 대기 검사 생략":"발동 간격 "+c.cooldownTimer.time.ToString("0.###")+"초")+"\n적중 시 지정 디버프 부여 · 실제 탄환의 충돌·폭발만 합산";
        }
        private static string GolemLaser(Charm_Golem_Laser c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStatUnsafe(c.relatedStat),Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}};
            hit.Element=c.damageElementalType;
            return "골렘 레이저 1회 판정 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.laserPrefab,"레이저")+"\n공격 유지 "+c.delayTime.ToString("0.###")+"초 뒤 생성 · 공격을 멈추면 종료\n생성 시점의 피해로 유지 · 실제 재타격 횟수만 합산\n플레이어가 발사자로 지정되어 동료 피해 배율을 별도로 더하지 않음";
        }
        private static string RockElephant(Charm_RockElephant c,Charm_Basic live,int level,PlayerAvatar p)
        {
            // Native SpawnFlag clamps the root-adjusted raw damage before spawning each stone.
            float raw=(p.GetCustomStat(ECustomStat.DamageReduction)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level)))*.01f*RootBonus(live,p);
            int count=c.defaultStoneBulletCount;
            if(c.additionalStoneBulletCountByATKSpeed>0)count+=p.GetCustomStat(ECustomStat.AttackSpeed)/c.additionalStoneBulletCountByATKSpeed;
            var hit=new DamageTooltip.Hit{Raw=Math.Max(1,raw)};
            return "돌 탄환 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.stoneBulletPrefab,"돌")+"\n방어력 비례 피해 · 보정 후 원시 피해 최소 1\n현재 발사 "+Math.Max(0,count)+"발 · 공격 속도 "+c.additionalStoneBulletCountByATKSpeed+"마다 1발 추가 (정수 나눗셈)\n기본 재사용 "+c.coolDownTimer.time.ToString("0.###")+"초 · 비용을 낸 돌진마다 남은 시간 "+c.coolDownReductionOnDash.ToString("0.###")+"초 감소\n대상까지 도달해 실제 맞힌 탄환만 합산";
        }
        private static string Guillotine(Charm_Guillotine c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            float a=p.GetCustomStat(ECustomStat.LightningDamage),b=p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage);
            float low=KeywordDatabase.GetConstValue("guillotineLowElementalDamageRatio")/100f;
            var hit=new DamageTooltip.Hit{Raw=c.defaultDamage+(Math.Max(a,b)+Math.Min(a,b)*low)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,Factors=new[]{1+p.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,RootBonus(live,p)}};
            hit.Element=fire?EDamageElementalType.FireAndLightning:EDamageElementalType.IceAndLightning;
            float speed=1+p.GetCustomStatUnsafe("CHARGINGCHARMBONUS")*.01f;
            return "작두 칼날 1개·지정 대상 1회 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n번개와 "+(fire?"화염":"냉기")+" 중 높은 수치 + 낮은 수치 × "+low.ToString("0.###")+"\n파동 "+Math.Max(0,1+p.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"))+"회 · 파동당 서로 다른 적 최대 "+c.bladeTargetCount+"명\n기본 주기 "+c.triggerCooldown.ToString("0.###")+"초 · 현재 자연 충전 "+(speed>0?(c.triggerCooldown/speed).ToString("0.###")+"초":"진행 불가")+"\n감전 피해 적중 1회당 남은 충전 시간 "+c.shockCooldownReduction.ToString("0.###")+" 감소\n두 속성을 가진 한 번의 피해 · 두 타격으로 중복 합산하지 않음";
        }
        private static string GreenGi(Charm_GreenGi c,Charm_Basic live,int level,PlayerAvatar p)
        {
            float raw=c.defaultDamage+p.GetCustomStat(ECustomStat.PhysicalDamage)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f;
            var text=new StringBuilder(NativeStats(c,level)+"\n회피 성공 후 방향별 반격 · 일반 / 치명타\n");
            for(int i=0;i<c.fireData.Length;i++)
            {
                var fire=c.fireData[i];if(!fire)continue;
                var hit=new DamageTooltip.Hit{Raw=raw,Factors=new[]{RootBonus(live,p),fire.damageMultiplier,fire.CalculateFinalDamageMultiplier(0)}};
                hit.Element=fire.damageElementalType;
                var bullet=fire as NewWeaponFireData_Bullet;
                if(bullet)text.AppendLine(ProjectileDamageProfiles.Describe(p,hit,bullet.bulletPrefab,"방향 "+(i+1)));
                else if(fire.GetType()==typeof(NewWeaponFireData_MeleeAttack))text.Append("방향 ").Append(i+1).Append(": ").AppendLine(DamageTooltip.Hits(p,hit));
                else text.Append("방향 ").Append(i+1).AppendLine(": 전용 발사 방식 계산 필요");
            }
            return text.Append("공격이 온 방향에 맞는 반격 하나만 생성 · 모든 방향의 피해를 합산하지 않음").ToString();
        }
        private static string Chakram(Charm_FireChakram c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level),enhanced=p.GetCustomStatUnsafe("ENHANCEDCHAKRAM");
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(ECustomStat.LightningDamage)*c.damagePercentByLevel.SafeRandomAccess(idx),Factors=new[]{.01f,enhanced>0?1+enhanced/100f:1,RootBonus(live,p)}};
            hit.Element=EDamageElementalType.Lightning;
            float speed=c.angleSpeed;
            if(p.GetCustomStatUnsafe("ATKSPDCHAKRAM")>0)speed+=speed*p.GetCustomStatUnsafe("ATTACKSPEED")/100f;
            return "차크람 1개·1회 적중 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n번개 속성 · "+c.bulletCountByLevel.SafeRandomAccess(idx)+"개 회전\n같은 차크람의 같은 대상 재타격은 0.24초 초과 후 가능\n회전 속도 "+speed.ToString("0.##")+"도/초 · 궤도에 실제 닿은 횟수만 합산\n디버프 부여 확률 "+c.debuffPercentByLevel.SafeRandomAccess(idx)+"%";
        }
        private static string Kunai(Charm_Kunai c,Charm_Basic live,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level);
            var hit=new DamageTooltip.Hit{Raw=c.defaultDamage+(p.GetCustomStat(ECustomStat.CriticalDamageBonus)+50)*c.damagePercentByLevel.SafeRandomAccess(idx)/100f,Factors=new[]{RootBonus(live,p)}};
            var controller=p.GetComponent<WeaponControllerSimple>();
            float weight=controller&&controller.currentWeapon?controller.currentWeapon.AttackWeightPerSwing:1;
            float chance=Mathf.Clamp(c.throwChanceByLevel.SafeRandomAccess(idx)*weight,0,100);
            int fury=Math.Max(0,p.GetCustomStatUnsafe("KUNAIFURY"));
            return "쿠나이 1발 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"투척")+"\n기본 치명타 피해율 +50과 일반 치명타 피해 보너스로 원시 피해 계산\n공격 시작 시 발동 확률 "+chance.ToString("0.#")+"% · 재사용 간격 "+c.cooldownTimer.time.ToString("0.###")+"초\n격노 투사체 생성 시 추가 "+fury+"발 · 일반 발동의 재사용 대기와 별도";
        }
        private static string IceRelic(Charm_IceSword c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            int extraMp=Math.Max(0,p.MaxMp-KeywordDatabase.GetConstValue("PLAYERDEFAULTMP"));
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage),Factors=new[]{c.damageRatio_IceElemental*.01f,(float)extraMp,c.damageRatio_Mp*.01f,1+p.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,RootBonus(live,p),1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f}};
            hit.Element=fire?EDamageElementalType.Fire:EDamageElementalType.Ice;
            string text=NativeStats(c,level)+"\n회전 검 1개·1회 적중 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n"+(fire?"화염":"냉기")+" 피해 × 추가 최대 MP "+extraMp+" 기준\n추가 최대 MP 0일 때 피해 0 · 이 효과 자체의 고정 상한 없음\n새 검 생성 비용 MP "+c.mpCost+" · 최대 "+c.swordCount+"개 · 기본 수명 "+c.swordLifeTime.ToString("0.###")+"초";
            if(c.iceSwordPrefab)text+="\n동일 검의 같은 대상 재타격 간격 "+c.iceSwordPrefab.attackIntervalTime.ToString("0.###")+"초 · 범위에서 벗어나면 재타격 기록 해제";
            return text+"\n각 검이 실제 맞힌 횟수만 합산 · 생성 시점 피해로 유지";
        }
        private static string TuningFork(Charm_TuningForks c,Charm_TuningForks live,int level,PlayerAvatar p)
        {
            float[] values={p.GetCustomStat(ECustomStat.PhysicalDamage),p.GetCustomStat(ECustomStat.FireDamage),p.GetCustomStat(ECustomStat.IceDamage),p.GetCustomStat(ECustomStat.LightningDamage)};
            Array.Sort(values);int count=Mathf.Clamp(c.calculateElementalCount,1,4);float total=0;
            for(int i=0;i<count;i++)total+=values[values.Length-1-i];
            int current=live?DamageStatPreview.ReadTuningForkStack(p,live,(int)ForkStack.GetValue(live)):0;
            Func<int,DamageTooltip.Hit> hit=stack=>new DamageTooltip.Hit{Raw=total/count,Element=EDamageElementalType.Chaos,Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,1+p.GetCustomStat(ECustomStat.MagicDamageBonus)/100f,1+p.GetCustomStat(ECustomStat.CooldownRecoverySpeed)/100f,RootBonus(live,p),(float)stack},ExtraCritical=p.GetCustomStat(ECustomStat.MagicCriticalDamageBonus)};
            return "소리굽쇠 · 폭발 1회 · 일반 / 치명타\n미충전: 0\n최소 충전 1회: "+DamageTooltip.Hits(p,hit(1))+"\n현재 "+current+"중첩: "+DamageTooltip.Hits(p,hit(current))+"\n최대 "+c.maxStack+"중첩: "+DamageTooltip.Hits(p,hit(c.maxStack))+"\n상위 "+count+"개 속성의 평균·마법 피해·재사용 회복 속도 반영\n마법 사용으로 충전 · 충전 간격 "+c.cooldownTimer.time.ToString("0.###")+"초\n살아 있는 적에게 무기 직접 공격 적중 시 모든 중첩 소비";
        }
        private static string CloseTarget(Charm_TooCloseDamage c,Charm_TooCloseDamage live,int level)
        {
            float bonus=c.additionalDamagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level));
            float range=c.range*(live && live.enabledWide?2:1);
            return "근거리 조건부 증폭\n거리 "+range+" 이내: 원래 피해 × "+(1+bonus/100f).ToString("0.###")+"\n범위 밖: × 1\n능력치 창과 별개로 적중 시 적용\n본타·속성 추가타·아티팩트 공격에 적용";
        }
        private static string FirstHit(Charm_FirstAttackBonusDamage c,int level)
        {
            float bonus=c.damageBonusByLevel.SafeRandomAccess(c.LevelToIdx(level));
            return "첫 공격 조건부 증폭\n적 HP 100%: 원래 피해 × "+(1+bonus/100f).ToString("0.###")+"\n이미 피해를 받은 적: × 1\n허수아비에는 적용되지 않음\n각 추가타는 적중 시점의 적 HP로 판정";
        }
        private static string BurnTarget(Charm_BurnTargetDamageBonus c,int level)
        {
            int bonus=c.damageBonusByLevel.SafeRandomAccess(c.LevelToIdx(level));
            return "화상 대상 무기 공격 증폭\n조건 미충족: +0%\n화상 중인 적: +"+bonus+"%\n무기 직접 공격에만 적용\n별도 속성 추가타에는 적용되지 않음\n같은 단계의 추가 피해 보정과 합산";
        }
        private static string MiniBossGrowth(Charm_MiniBossFight c,int level)
        {
            int per=c.addDamageByLevel.SafeRandomAccess(c.LevelToIdx(level));int cap=KeywordDatabase.GetConstValue("miniBossKillCharmCountLimit");
            int count=0;if(DungeonManager.Instance)DungeonManager.Instance.dungeonEnvironment.TryGetValue("MiniBossKillCount",out count);
            count=Math.Min(count,cap);
            return "미니보스 처치 누적 피해 증가\n최소: +0%\n최대: +"+(cap*per)+"% ("+cap+"회)\n현재: +"+(count*per)+"% ("+count+"회)\n활성 효과는 모든 피해 증가에 이미 포함";
        }
        private static string NeighbourLevels(Charm_NearLevelDamage c,Charm_NearLevelDamage live,int level,PlayerAvatar p)
        {
            float per=c.allDamageBonusByLevel.SafeRandomAccess(c.LevelToIdx(level));
            var text=new StringBuilder("인접 8칸의 아티팩트 레벨 합산\n레벨 1당 모든 피해 +"+per.ToString("0.#")+"%\n최소: +0%\n최대: 인접 장비의 최대 레벨에 따라 변동");
            if(live && live.Item!=null)
            {
                float current=0,max=0;
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(dx!=0||dy!=0)
                {
                    var item=p.Inventory.FindItem(new ItemPosition(live.Item.XIdx+dx,live.Item.YIdx+dy));
                    if(item!=null&&item.Charm) {current+=Math.Min(item.Charm.DisplayedLevel,item.Charm.maxLevel)*per;max+=item.Charm.maxLevel*per;}
                }
                text.Append("\n현재 배치: +").Append(Math.Floor(current)).Append("%\n현재 배치 최대: +").Append(Math.Floor(max)).Append('%');
            }
            return text.ToString();
        }
        internal static string NativeStats(Charm_StatusInstance c,int level)
        {
            var text=new StringBuilder("이 장비의 능력치 효과\n");int idx=c.LevelToIdx(level);
            foreach(var group in c.stats)
            {
                int value=group.valuesByLevel.SafeRandomAccess(idx);
                if(value==0&&group.hideIfStatValueIsZero)continue;
                var stat=StatusDatabase.CreateStatusEntity(group.statusID,value);
                if(stat!=null)text.AppendLine(KeywordDatabase.Convert(stat.ToString(true,false,false,Color.white)));
            }
            text.Append("활성 장비의 능력치는 현재 속성·공격 계산에 포함\n미장착 장비는 현재 능력치에 합산하지 않음");
            return text.ToString();
        }
        private static string LakeSpirit(Charm_LakeSpirit c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.MaxMp,Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)},CriticalRateMultiplier=1+c.critDamageAmpByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f};
            hit.Element=c.elementalType;
            return "호수 정령 · 일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,c.bulletPrefab,"정령 공격")+"\n최대 MP "+p.MaxMp+" 기준\n현재 남은 MP와 피해량은 무관\n치명타 피해율 증폭 및 반올림 반영\n발동 비용 MP "+Mathf.RoundToInt(p.MaxMp*c.mpPercent/100f);
        }
        private static string ShadowEye(Charm_ShadowEye c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var hit=new DamageTooltip.Hit{Raw=p.GetCustomStatUnsafe("EVASION"),Factors=new[]{.01f,c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))*.01f,RootBonus(live,p),c.shadowEyeAttack.damageMultiplier}};
            hit.Element=c.shadowEyeAttack.damageElementalType;
            return "그림자 눈 · 발동 1타 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n회피 능력치와 공격 계수를 함께 적용\n치명타로 충전한 후 발동";
        }
        private static string AutomaticMagic(Charm_AutoMagic source,Charm_AutoMagic live,int level,PlayerAvatar player)
        {
            string text="자동 마법 시전 · 기본 대기 "+source.cooldownByLevel.SafeRandomAccess(source.LevelToIdx(level)).ToString("0.###")+"초";
            if(!live||!live.IsEffectEnabled)return text+"\n장착 후 바로 아래 칸의 활성 마법탄 마법서를 연결하면 피해량 표시";
            var caster=AutoCaster.GetValue(live) as AutoMagicCaster;
            // Read the caster's actual cached link, rather than guessing from an inventory
            // layout that may not yet have propagated through OnCharmEffectRefreshed.
            var magic=caster?caster.magicCharm:null;
            if(!magic||!magic.ContainedMagic||!magic.ContainedMagic.magicPrefab||!magic.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Bolt>())
                return text+"\n현재 연결된 마법탄 마법서 없음 · 바로 아래 칸의 활성 마법탄 마법서만 사용";
            text+="\n연결 마법: "+magic.ContainedMagic.Name;
            if(magic.CanCast(player,false,false)!=ECanUseSkillResult.Succeeded)return text+"\n연결된 마법이 현재 사용 불가";
            string damage;
            if(!MagicDamageProfiles.TryDescribe(magic,magic,magic.limitedEffectEnabledLevel,player,out damage,0))
                return text+"\n연결 마법의 전용 피해 계산 필요";
            text+="\n"+damage+"\nMP·마법서 사용 횟수 소비 없음 · 소모 MP 비례 피해는 0 기준";
            text+="\n시전 준비 0.6초 · 발사 후 별도 대기 "+magic.ContainedMagic.cooldownTime.ToString("0.###")+"초";
            text+="\n대상이 있을 때 별도 대기 → 기본 대기 → 시전 준비 순서 · 다중 시전 효과는 원본대로 적용";
            return text;
        }
        private static string LinkedMagic(Charm_NearMagicBullet c,Charm_Basic live,int level,PlayerAvatar p)
        {
            var text=new StringBuilder("연결된 마법 속성별 투사체 1발 / 치명타\n");
            string[] names={"물리","화염","냉기","번개"};ECustomStat[] stats={ECustomStat.PhysicalDamage,ECustomStat.FireDamage,ECustomStat.IceDamage,ECustomStat.LightningDamage};
            for(int i=0;i<stats.Length;i++)text.Append(names[i]).Append(": ").AppendLine(DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=p.GetCustomStat(stats[i]),Element=(EDamageElementalType)i,Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}}));
            text.Append("비어 있는 연결 마법서: ").AppendLine(DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=1,Factors=new[]{c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f,RootBonus(live,p)}}));
            text.Append("연결된 마법서의 속성에 해당하는 값만 적용\n여러 발은 각각 적중한 경우에만 합산");return text.ToString();
        }
        private static string BoltMultiShot(Charm_BoltMagicMultiShot c,int level)
        {
            int idx=c.LevelToIdx(level);float ratio=c.multiShotDamageRatioByLevel.SafeRandomAccess(idx);
            return "마법탄 다중 발사\n한 번에 3발 · 1발 피해 × "+ratio.ToString("0.###")+"\n소비 MP 보정 +"+c.additionalCostPercent.SafeRandomAccess(idx)+"%\n모두 적중할 때 배율 합계 × "+(3*ratio).ToString("0.###")+"\n추가 MP에 따른 피해 증가도 해당 마법서 상세에 반영\n같은 효과가 여러 개면 마지막으로 연결된 피해 배율 사용";
        }
        private static string FlameSwordReturn(Charm_FlameSwordFall c,Charm_Basic live,PlayerAvatar p)
        {
            var combo=p.Inventory?p.Inventory.FindComboEffect("FLAMESWORD") as ComboEffect_FlameSword:null;
            if(!combo)return "화염검 회수\n화염검 콤보를 활성화하면 회수 피해를 계산합니다.\n바닥에 남아 있는 검이 없으면 적중하지 않습니다.";
            // FireCasting rounds the root/Solis bonus before sending it to the return projectile.
            float bonus=RootBonus(live,p);
            if(p.GetCustomStatUnsafe(c.solisImberStatId)>0)bonus*=1.5f;
            float returnFactor=1+Mathf.RoundToInt((bonus-1)*100)/100f;
            bool frost=p.GetCustomStatUnsafe("FLAMESWORDFROST")>0;
            float raw=p.GetCustomStat(frost?ECustomStat.IceDamage:ECustomStat.FireDamage);
            float magic=p.GetCustomStatUnsafe("FLAMESWORDMAGICDAMAGE")>0?1+p.GetCustomStat(ECustomStat.MagicDamageBonus)/100f:1;
            var factors=new[]{1+p.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f,combo.damagePercent/100f,magic,returnFactor};
            var normal=new DamageTooltip.Hit{Raw=raw,Element=frost?EDamageElementalType.Ice:EDamageElementalType.Fire,Factors=factors,ExtraCritical=p.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE")};
            var luckyFactors=new System.Collections.Generic.List<float>(factors);
            luckyFactors.Add((100+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent"))/100f);
            var lucky=new DamageTooltip.Hit{Raw=raw,Element=normal.Element,Factors=luckyFactors.ToArray(),ExtraCritical=normal.ExtraCritical};
            var text=new StringBuilder("회수 검 1개 적중 · 일반 / 치명타\n");
            text.Append(frost?"냉기":"화염").Append(": ").AppendLine(DamageTooltip.Hits(p,normal));
            text.Append("행운 발동 시: ").AppendLine(DamageTooltip.Hits(p,lucky));
            text.Append("행운 확률: ").Append(Mathf.Clamp(p.GetCustomStatUnsafe("FLAMESWORDLUCK"),0,100)).AppendLine("%");
            text.Append("검마다 대상당 1회 적중 · 여러 검이 맞으면 각각 합산\n검이 없거나 회수 경로에 적이 없으면 총 피해 0\n적 방어 적용 전 피해");
            if(!combo.isEnabled)text.Append("\n현재 화염검 콤보 비활성: 발동 불가");
            return text.ToString();
        }
        private static string AttackStack(Charm_DashAttackStack c,Charm_DashAttackStack live,int level)
        {
            int idx=c.LevelToIdx(level);
            return "시간 누적 최종 무기 피해 증가\n최소 +0%\n현재 +"+(live?live.currentBonus:0)+"%\n최대 +"+c.maxByLevel.SafeRandomAccess(idx)+"%\n"+c.tickTimer.time.ToString("0.###")+"초마다 +"+c.addByLevel.SafeRandomAccess(idx)+"%\n실제 적을 무기 공격으로 맞히면 초기화\n허수아비 적중은 초기화하지 않음\n현재 활성 보너스는 무기 피해에 이미 포함";
        }
        private static string ReservedMagic(Charm_ReservedMPBonus c,int level)
        {
            return "MP 예약으로 마법 피해 증가\n예약 MP "+c.mpReserve+"\n활성 시 마법 피해 +"+c.damageByLevel.SafeRandomAccess(c.LevelToIdx(level))+"%\n남은 MP에 비례하는 효과가 아닌 고정 보너스\n활성 보너스는 마법 피해 계산에 이미 포함";
        }
        private static string DefenseConversion(Charm_AddStatByDefense c,int level,PlayerAvatar p)
        {
            int per=c.addByLevel.SafeRandomAccess(c.LevelToIdx(level));int defense=p.GetCustomStat(ECustomStat.DamageReduction);
            int value=per*Mathf.FloorToInt(defense/10f);
            return NativeStats(c,level)+"\n방어력 10마다 화염·냉기·번개 각각 +"+per+"\n방어력 0 기준: +0\n현재 방어력 "+defense+": 각 "+value.ToString("+0;-0;0")+"\n최대: 이 효과 자체의 고정 상한 없음\n10 미만의 나머지는 버림 · 원본 효과는 1초 간격 갱신";
        }
        private static string StatConversion(Charm_AddStatByAnotherStat c,int level,PlayerAvatar p)
        {
            int idx=c.LevelToIdx(level),step=c.perBaseStatByLevel.SafeRandomAccess(idx),current=p.GetCustomStatUnsafe(c.baseStatNameUnsafe);
            var text=new StringBuilder(NativeStats(c,level));
            var basis=StatusDatabase.CreateStatusEntity(c.baseStatID,step);
            text.Append("\n능력치 환산: ").Append(basis==null?c.baseStatID:KeywordDatabase.Convert(basis.ToString(true,false,false,Color.white)));
            text.Append("\n최대: 이 효과 자체의 고정 상한 없음\n기준 능력치 0일 때: +0\n현재 기준으로 얻는 보너스:");
            if(step<=0)return text.Append("\n원본 환산 단위가 올바르지 않습니다.").ToString();
            int count=Mathf.FloorToInt(current/(float)step);
            foreach(var target in c.targetStats)
            {
                var stat=StatusDatabase.CreateStatusEntity(target.statID,target.addByLevel.SafeRandomAccess(idx)*count);
                if(stat!=null)text.Append('\n').Append(KeywordDatabase.Convert(stat.ToString(true,false,false,Color.white)));
            }
            text.Append("\n단위 미만의 나머지는 버림 · 원본 효과는 1초 간격 갱신");return text.ToString();
        }
        private static string GuardCounter(Charm_GuardCounter c,Charm_Basic live,int level,PlayerAvatar p)
        {
            float raw=p.GetCustomStat(ECustomStat.PhysicalDamage);float ratio=c.damageRatioByLevel.SafeRandomAccess(c.LevelToIdx(level));
            if(p.GetCustomStatUnsafe(c.ripostelaserStatId)>0)
                return "반격 레이저 1타 / 치명타\n"+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=raw,Factors=new[]{ratio,RootBonus(live,p),c.ripostelaserDamageRatio}});
            float fire=c.guardCounterFireData?c.guardCounterFireData.damageMultiplier:0;
            return "직접 가드 반격 / 치명타\n"+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=raw,Factors=new[]{ratio,fire}})+"\n보조 방패 반격 / 치명타\n"+DamageTooltip.Hits(p,new DamageTooltip.Hit{Raw=raw,Factors=new[]{ratio,RootBonus(live,p),fire}});
        }
    }
    [HarmonyPatch]
    internal static class TooltipTuningForkStackChangedPatch
    {
        private static readonly System.Reflection.FieldInfo Stack=AccessTools.Field(typeof(Charm_TuningForks),"currentStack");
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Charm_TuningForks),"OnCreateMagic");
            yield return AccessTools.Method(typeof(Charm_TuningForks),"HandleAttackUnit");
            yield return AccessTools.Method(typeof(Charm_TuningForks),"OnDisabledEffect");
            yield return AccessTools.Method(typeof(Charm_TuningForks),"OnUpdate");
        }
        private static int State(Charm_TuningForks fork){return ((int)Stack.GetValue(fork)<<1)|(fork.cooldownTimer.Check()?1:0);}
        private static void Prefix(Charm_TuningForks __instance,out int __state){__state=State(__instance);}
        private static void Postfix(Charm_TuningForks __instance,int __state)
        {
            if(State(__instance)!=__state&&__instance.NetworkAvatar==DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);
        }
    }
}
