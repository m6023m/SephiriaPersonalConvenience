using System;
using System.Text;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class MagicDamageProfiles
    {
        private static readonly System.Reflection.FieldInfo MagicCreated=HarmonyLib.AccessTools.Field(typeof(Charm_Magic),"OnCreateMagic");
        private static readonly System.Reflection.FieldInfo LightningArmorTick=HarmonyLib.AccessTools.Field(typeof(CharacterBuff_LightningArmor),"damageTickTimer");
        internal static string DescribeBuffAttack(CharacterBuff buff,float amplified,PlayerAvatar player)
        {
            var lightning=buff as CharacterBuff_LightningArmor;
            if(!lightning)return null;
            // OnUpdate_Server constructs this hit directly: no AP, charm power,
            // LightningDamage scaling, or stack multiplier is applied to its raw value.
            var hit=new DamageTooltip.Hit{Raw=amplified,Element=EDamageElementalType.Lightning,ElementalEffect=true,
                ExtraCritical=player.GetCustomStat(ECustomStat.MagicCriticalDamageBonus)};
            float interval=((Timer)LightningArmorTick.GetValue(lightning)).time;
            return "번개 갑옷 · 범위 내 대상당 주기 피해: "+DamageTooltip.Hits(player,hit)+
                "\n발동 간격 "+interval.ToString("0.###")+"초 · 범위 "+lightning.range.ToString("0.##")+
                "\n평타와 별도로 발동 · 범위 내 체류 시간에 따라 적중 횟수 변동";
        }
        internal static bool TryDescribe(Charm_Magic source,Charm_Magic live,int level,PlayerAvatar p,out string text,int? usedMpOverride=null,bool forceBasicAttackBonus=false)
        {
            text=null;
            if(!source.ContainedMagic || !source.ContainedMagic.magicPrefab)return false;
            var skill=source.ContainedMagic.magicPrefab.GetComponent<ActiveSkill>();
            if(!skill)return false;
            int index=source.LevelToIdx(level);
            int cost=usedMpOverride??(live?live:source).GetCost(p,index);
            float power=1+(live && live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f;
            var summon=skill as ActiveSkill_Summon;
            if(summon)return FollowerDamageProfiles.TrySummon(summon,index,power,cost,p,out text);
            var buff=skill as ActiveSkill_Buff;
            if(buff)
            {
                string periodic=DescribeBuffAttack(buff.buffPrefab,1,p);
                if(periodic!=null)
                {
                    text=periodic+"\n"+skill.GetEffectString(index,false,0)+"\n시전 비용 MP "+cost;
                    return true;
                }
                text="버프 마법 · 직접 공격 피해 없음\n"+skill.GetEffectString(index,false,0)+"\n시전 비용 MP "+cost+"\n활성화된 능력치 보너스는 현재 공격 계산에 포함";
                return true;
            }
            var heal=skill as ActiveSkill_HealSelf;
            if(heal)
            {
                text="회복 마법 · 직접 공격 피해 없음\n기본 회복량 "+heal.healTableByLevel.SafeRandomAccess(index).ToString("0.#")+"\n시전 후 최대 MP 예약량 +"+cost+"\n현재 빈 HP와 회복 보정에 따라 실제 회복량 변동";
                return true;
            }
            var smoke=skill as ActiveSkill_Smokescreen;
            if(smoke)
            {
                text="연막 · 직접 공격 피해 없음\n"+skill.GetEffectString(index,false,0)+"\n범위 "+smoke.smokeRadius.ToString("0.#")+" · 지속 "+smoke.smokescreenTimer.time.ToString("0.#")+"초\n시전 비용 MP "+cost;
                return true;
            }
            if(skill is ActiveSkill_HammerTrigger)
            {
                text="장치 작동 마법 · 직접 공격 피해 없음\n해당 속성의 마법 장치를 작동시킵니다.\n시전 비용 MP "+cost;
                return true;
            }
            var bolt=skill as ActiveSkill_Bolt;
            if(bolt)
            {
                var hit=BaseHit(p,bolt.defaultDamageByLevel.SafeRandomAccess(index),bolt.relatedDamage,bolt.damagePercentByLevel.SafeRandomAccess(index),power,cost,false);
                hit.Element=bolt.elementalType;
                if(bolt.relatedToBasicAttackDamageBonus||forceBasicAttackBonus)AddFactor(hit,1+p.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f);
                float multiRatio;bool multi=BoltMultiShot(live,out multiRatio);
                if(multi)AddFactor(hit,multiRatio);
                text=BulletText(p,hit,bolt.boltBulletPrefab,"마법탄",cost)+"\n기본 연사: "+Math.Max(1,Mathf.CeilToInt(bolt.fireCount))+"회";
                if(multi)text+="\n다중 발사 적용: 한 번에 3발 · 1발 피해 × "+multiRatio.ToString("0.###")+"\n각각 실제 적중한 경우에만 합산";
                return true;
            }
            var arrow=skill as ActiveSkill_Arrow;
            if(arrow)
            {
                // Arrow applies charm power to its elemental term and again to the total.
                var hit=BaseHit(p,arrow.defaultDamageByLevel.SafeRandomAccess(index),arrow.relatedDamage,arrow.damagePercentByLevel.SafeRandomAccess(index),power,cost,true);
                hit.Element=arrow.elementalType;
                text=BulletText(p,hit,arrow.arrowBulletPrefab,"마법 화살",cost)+"\n기본 연사: "+Math.Max(1,Mathf.CeilToInt(arrow.fireCount))+"회 · 한 번에 "+Math.Max(1,arrow.multiShotCount)+"발\n화살들이 적중 목록을 공유하므로 발사 수를 그대로 총 피해에 곱하지 않습니다.";
                return true;
            }
            var ball=skill as ActiveSkill_Ball;
            if(ball)
            {
                var hit=BaseHit(p,ball.defaultDamageByLevel.SafeRandomAccess(index),ball.relatedDamage,ball.damagePercentByLevel.SafeRandomAccess(index),power,cost,false);
                hit.Element=EDamageElementalType.Fire;
                text=BulletText(p,hit,ball.ballPrefab,"회전 마법구",cost)+"\n생성 8개 · 각각 실제 적중한 경우에만 합산\n이 공격의 추가 치명타 확률 +"+ball.criticalChanceBonusByLevel.SafeRandomAccess(index)+"%";
                return true;
            }
            var fire=skill as ActiveSkill_FireBullet;
            if(fire)
            {
                var result=new StringBuilder();
                for(int tier=0;tier<fire.bulletPrefabByPower.Length;tier++)
                {
                    var hit=BaseHit(p,fire.defaultDamageByLevel.SafeRandomAccess(index),fire.relatedDamage,fire.damagePercentByLevel.SafeRandomAccess(index),power,cost,false);
                    hit.Element=fire.damageElementalType;
                    result.AppendLine(BulletText(p,hit,fire.bulletPrefabByPower[tier],"충전 단계 "+tier,cost));
                }
                result.Append("기본 발사 수: ").Append(fire.bulletCount).Append("발 · 각각 실제 적중한 경우에만 합산");
                text=result.ToString();return true;
            }
            var rain=skill as ActiveSkill_ArrowRain;
            if(rain)
            {
                var result=new StringBuilder();
                for(int tier=0;tier<rain.bulletPrefabByPower.Length;tier++)
                    result.AppendLine(BulletText(p,BaseHit(p,rain.defaultDamageByLevel.SafeRandomAccess(index),rain.relatedDamage,rain.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,rain.damageElementalType),rain.bulletPrefabByPower[tier],"화살비 충전 단계 "+tier,cost));
                result.Append("기본 발사 수: ").Append(rain.bulletCount).Append("발\n동상 부여 확률 ").Append(rain.debuffPercent.ToString("0.#")).Append("% · 상태이상은 적중 후 별도 적용");
                text=result.ToString();return true;
            }
            var stoning=skill as ActiveSkill_OinkShamanStoning;
            if(stoning)
            {
                text=BulletText(p,BaseHit(p,stoning.defaultDamageByLevel.SafeRandomAccess(index),stoning.relatedDamage,stoning.damagePercentByLevel.SafeRandomAccess(index),power,cost,false),stoning.stoningBulletPrefab,"바위 파동",cost)+"\n충전 단계가 높을수록 파동 범위 증가\n파동끼리 적중 목록 공유 · 전체 파동 수를 한 적의 피해로 곱하지 않음";
                return true;
            }
            var boomerang=skill as ActiveSkill_LightningBoomerang;
            if(boomerang)
            {
                text=BulletText(p,BaseHit(p,boomerang.defaultDamageByLevel.SafeRandomAccess(index),boomerang.relatedDamage,boomerang.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,boomerang.elementalType),boomerang.bulletPrefab,"번개 부메랑",cost)+"\n왕복 경로의 실제 적중 횟수에 따라 합산";
                return true;
            }
            var meteor=skill as ActiveSkill_MeteorShower;
            if(meteor)
            {
                text=BulletText(p,BaseHit(p,meteor.defaultDamageByLevel.SafeRandomAccess(index),meteor.relatedDamage,meteor.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,meteor.elementalType),meteor.meteorBulletPrefab,"유성",cost)+"\n시전당 유성 "+meteor.numberOfMeteorsByLevel.SafeRandomAccess(index)+"개 · 각각 실제 적중한 경우에만 합산";
                return true;
            }
            var armor=skill as ActiveSkill_LightningArmor;
            if(armor)
            {
                // Unlike the other spells, this native OnStartMagic never multiplies by power.
                var hit=BaseHit(p,armor.damagesByLevel.SafeRandomAccess(index),"LIGHTNINGDAMAGE",armor.damagePercentByLevel.SafeRandomAccess(index),1,cost,false);
                hit.Element=EDamageElementalType.Lightning;
                text="번개 갑옷 대상 1명당 1회 · 일반 / 치명타\n"+DamageTooltip.Hits(p,hit)+"\n"+armor.damageTickTimer.time.ToString("0.###")+"초 간격 · "+armor.durationTimer.time.ToString("0.###")+"초 지속\n한 번에 서로 다른 적 최대 3명에게 연쇄\n같은 적에게 연쇄 3회분을 합산하지 않음\n시전 비용 MP "+cost+" 기준 · 적 방어 적용 전";
                return true;
            }
            var projectile=skill as ActiveSkill_Projectile;
            if(projectile)
            {
                var result=new StringBuilder("무작위 투사체 종류별 1회 적중 · 일반 / 치명타\n");
                for(int variant=0;variant<projectile.projectilePrefabs.Length;variant++)
                {
                    var prefab=projectile.projectilePrefabs[variant];
                    var area=prefab?prefab.GetComponent<SpecialProjectile_AreaJudgement>():null;
                    if(!area)return false;
                    result.Append("종류 ").Append(variant+1).Append(": ").AppendLine(DamageTooltip.Hits(p,BaseHit(p,projectile.defaultDamageByLevel.SafeRandomAccess(index),projectile.relatedDamage,projectile.damagePercentByLevel.SafeRandomAccess(index),power,cost,false,projectile.damageElementalType)));
                    result.Append("투사체당 판정 ").Append(area.attackCount).AppendLine("회");
                }
                result.Append("생성 ").Append(projectile.bulletCount).Append("개 · 범위 안에서 실제 적중한 횟수만 합산\n시전 비용 MP ").Append(cost).Append(" 기준 · 적 방어 적용 전");
                text=result.ToString();return true;
            }
            return false;
        }
        private static bool BoltMultiShot(Charm_Magic live,out float ratio)
        {
            ratio=1;bool applied=false;
            var callbacks=live && MagicCreated!=null?MagicCreated.GetValue(live) as Delegate:null;
            if(callbacks==null)return false;
            // SetMultiShot replaces, rather than multiplies, an earlier subscriber's ratio.
            foreach(var callback in callbacks.GetInvocationList())
            {
                var modifier=callback.Target as Charm_BoltMagicMultiShot;
                if(!modifier)continue;
                ratio=modifier.multiShotDamageRatioByLevel.SafeRandomAccess(modifier.CurrentLevelToIdx());applied=true;
            }
            return applied;
        }
        private static DamageTooltip.Hit BaseHit(PlayerAvatar p,float baseDamage,string stat,float percent,float power,int cost,bool powerOnElement,EDamageElementalType element=EDamageElementalType.Physical)
        {
            return new DamageTooltip.Hit{
                Raw=baseDamage,Element=element,ResourceAmount=p.GetCustomStat(stat),ResourcePerUnit=percent*.01f*(powerOnElement?power:1),
                Factors=new[]{1+p.GetCustomStat(ECustomStat.MagicDamageBonus)*.01f,1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")*.01f,cost>0?1+cost/10f*p.GetCustomStatUnsafe("MAGICMP")*.01f:1,power},
                ExtraCritical=p.GetCustomStat(ECustomStat.MagicCriticalDamageBonus)
            };
        }
        private static void AddFactor(DamageTooltip.Hit hit,float factor)
        {
            var factors=new System.Collections.Generic.List<float>(hit.Factors);factors.Add(factor);hit.Factors=factors.ToArray();
        }
        private static string BulletText(PlayerAvatar p,DamageTooltip.Hit hit,GameObject prefab,string name,int cost)
        {
            return "일반 / 치명타\n"+ProjectileDamageProfiles.Describe(p,hit,prefab,name)+"\n시전 비용 MP "+cost+" 기준 · 적 방어 적용 전";
        }
    }
}
