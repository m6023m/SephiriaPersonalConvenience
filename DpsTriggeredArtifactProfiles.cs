using System;
using UnityEngine;

namespace SephiriaDicePreview
{
    // Damage whose native trigger is shared with another combat system. This
    // layer copies only serialized/native timing and damage facts. It never
    // invokes callbacks, spawns objects, or reads combat state on the worker.
    internal static class DpsTriggeredArtifactProfiles
    {
        internal static bool Capture(DpsSnapshot snapshot,Charm_Basic source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var fork=source as Charm_TuningForks;
            if(fork){TuningFork(snapshot,fork,live,level,player);return true;}
            var storm=source as Charm_BlazingStormcloud;
            if(storm){Stormcloud(snapshot,storm,player);return true;}
            var far=source as Charm_FarChimDamage;
            if(far){FarChim(snapshot,far,live,player);return true;}
            var sword=source as Charm_IceSword;
            if(sword){IceSword(snapshot,sword,live,player);return true;}
            var eye=source as Charm_ShadowEye;
            if(eye){ShadowEye(snapshot,eye,live,level,player);return true;}
            var fall=source as Charm_FlameSwordFall;
            if(fall){FlameSwordFall(snapshot,fall,live,level,player);return true;}
            return false;
        }

        private static void TuningFork(DpsSnapshot snapshot,Charm_TuningForks source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="소리굽쇠 지속 DPS",Seconds=source.cooldownTimer.time};snapshot.Cycles.Add(cycle);
            if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0)
            {cycle.Unavailable="소리굽쇠 충전 간격 데이터 없음";return;}
            float[] values={player.GetCustomStat(ECustomStat.PhysicalDamage),player.GetCustomStat(ECustomStat.FireDamage),
                player.GetCustomStat(ECustomStat.IceDamage),player.GetCustomStat(ECustomStat.LightningDamage)};
            Array.Sort(values);int count=Mathf.Clamp(source.calculateElementalCount,1,4);float total=0;
            for(int i=0;i<count;i++)total+=values[values.Length-1-i];
            var hit=new DamageTooltip.Hit{Raw=total/count,Element=EDamageElementalType.Chaos,Magic=true,
                Factors=new[]{source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))/100f,
                    1+player.GetCustomStat(ECustomStat.MagicDamageBonus)/100f,
                    1+player.GetCustomStat(ECustomStat.CooldownRecoverySpeed)/100f,
                    ArtifactDamageProfiles.RootBonus(live,player)}};
            DpsCapture.Add(cycle,hit,1);
            cycle.Condition="충전 가능할 때 마법 사용 · 다음 무기 직접 적중에 즉시 소비";
        }

        private static void Stormcloud(DpsSnapshot snapshot,Charm_BlazingStormcloud source,PlayerAvatar player)
        {
            var burn=source.debuffPrefab;
            var cycle=new DpsSnapshot.Cycle{Name="먹구름 화상 지속 DPS"};snapshot.Cycles.Add(cycle);
            if(!burn||burn.tickTimer.time<=0){cycle.Unavailable="먹구름 화상 틱 데이터 없음";return;}
            double speed=1+player.GetCustomStatUnsafe("BURNSPEED")/100d;
            if(!DpsNumbers.Finite(speed)||speed<=0){cycle.Unavailable="현재 화상 속도로 틱 진행 불가";return;}
            cycle.Seconds=burn.tickTimer.time/speed;
            EDamageElementalType element;float raw=CharacterDebuff_Burn.CalculateTickDamage(player,out element);
            int stacks=Math.Max(0,2+player.GetCustomStatUnsafe("BURNSTACK"));
            DpsCapture.Add(cycle,new DamageTooltip.Hit{Raw=raw,Element=element,ElementalEffect=true},stacks);
            cycle.Condition="먹구름이 같은 대상을 계속 공격해 화상 최대 중첩 유지";
        }

        private static void FarChim(DpsSnapshot snapshot,Charm_FarChimDamage source,Charm_Basic live,PlayerAvatar player)
        {
            var debuff=source.chimDamageBuffPrefab as CharacterDebuff_ChimDamage;
            var cycle=new DpsSnapshot.Cycle{Name="원거리 지연 피해 DPS"};snapshot.Cycles.Add(cycle);
            if(!debuff||debuff.GetType()!=typeof(CharacterDebuff_ChimDamage)||debuff.addDamageTimer.time<=0)
            {cycle.Unavailable="원거리 지연 피해 데이터 없음";return;}
            double duration=debuff.defaultDuration*(1+player.GetCustomStat(ECustomStat.DebuffDuration)/100d);
            if(!DpsNumbers.Finite(duration)||duration<=0){cycle.Unavailable="지연 피해 지속 시간 데이터 없음";return;}
            cycle.Seconds=duration;
            int bonus=string.IsNullOrEmpty(debuff.bonusDamageId)?0:player.GetCustomStatUnsafe(debuff.bonusDamageId);
            float raw=(float)(20*((debuff.addDamageTimer.GetTimer()+duration)/debuff.addDamageTimer.time)+bonus);
            // Native delayed damage is created directly by the debuff and does
            // not pass through Charm_Basic.CalculateDamage/root charm bonus.
            DpsCapture.Add(cycle,new DamageTooltip.Hit{Raw=raw,Element=EDamageElementalType.Normal,ElementalEffect=true},1);
            cycle.Condition="거리 조건 적중 후 재부여 없이 자연 종료되는 주기를 반복";
        }

        private static void IceSword(DpsSnapshot snapshot,Charm_IceSword source,Charm_Basic live,PlayerAvatar player)
        {
            bool fire=player.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            int extraMp=Math.Max(0,player.MaxMp-KeywordDatabase.GetConstValue("PLAYERDEFAULTMP"));
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage),
                Element=fire?EDamageElementalType.Fire:EDamageElementalType.Ice,
                Factors=new[]{source.damageRatio_IceElemental*.01f,(float)extraMp,source.damageRatio_Mp*.01f,
                    1+player.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,
                    1+player.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f,ArtifactDamageProfiles.RootBonus(live,player)}};
            double interval=source.iceSwordPrefab?source.iceSwordPrefab.attackIntervalTime:0;
            if(interval<=0||source.swordLifeTime<=0)
            {snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="회전 검 DPS",Unavailable="회전 검 수명·재타격 데이터 없음"});return;}
            int count=Math.Max(0,source.swordCount);
            var one=new DpsSnapshot.Cycle{Name="회전 검 1개 DPS",Seconds=interval};snapshot.Cycles.Add(one);DpsCapture.Add(one,hit,1);
            var maximum=new DpsSnapshot.Cycle{Name="회전 검 최대 유지 DPS",Seconds=interval};snapshot.Cycles.Add(maximum);DpsCapture.Add(maximum,hit,count);
            one.Condition="검과 같은 대상이 계속 겹치는 동안의 재타격 간격";
            maximum.Condition="충전 아티팩트 발동과 MP 소모로 동시 생성 한도 유지";
        }

        private static void ShadowEye(DpsSnapshot snapshot,Charm_ShadowEye source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="그림자 눈 반격 DPS",Seconds=1,ExternalRateLabel="충전 완료 후 초당 피격 횟수"};snapshot.Cycles.Add(cycle);
            if(!source.shadowEyeAttack){cycle.Unavailable="그림자 눈 반격 데이터 없음";return;}
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStatUnsafe("EVASION"),
                Factors=new[]{.01f,source.damagePercentByLevel.SafeRandomAccess(source.LevelToIdx(level))*.01f,
                    ArtifactDamageProfiles.RootBonus(live,player)}};
            var prepared=ProjectileDamageProfiles.Scale(hit,source.shadowEyeAttack.damageMultiplier*source.shadowEyeAttack.CalculateFinalDamageMultiplier(0));
            prepared.Element=source.shadowEyeAttack.damageElementalType;
            var sample=new DamageTooltip.WeaponAttackSample{Fire=source.shadowEyeAttack,Hits=new System.Collections.Generic.List<DamageTooltip.Hit>{prepared}};
            var result=DpsProjectiles.Weapon(sample,player,false);cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            cycle.Condition="치명타·처형 적중으로 충전 완료 후 받은 공격을 회피하며 반격";
        }

        private static void FlameSwordFall(DpsSnapshot snapshot,Charm_FlameSwordFall source,Charm_Basic live,int level,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name="화염검 회수 DPS"};snapshot.Cycles.Add(cycle);
            var combo=player.Inventory?player.Inventory.FindComboEffect("FLAMESWORD") as ComboEffect_FlameSword:null;
            if(!combo||!combo.isEnabled){cycle.Unavailable="화염검 콤보가 활성화되면 회수 피해 계산 가능";return;}
            int swords=Math.Max(0,combo.maxSword+player.GetCustomStatUnsafe("FLAMESWORDMAX"));
            double cooldown=source.cooldownTimeByLevel.SafeRandomAccess(source.LevelToIdx(level));
            cycle.Seconds=Math.Max(cooldown,combo.rechargeTime);
            if(swords==0){cycle.Unavailable="회수 가능한 화염검 없음";return;}
            if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0){cycle.Unavailable="화염검 회수·재충전 시간 데이터 없음";return;}
            bool frost=player.GetCustomStatUnsafe("FLAMESWORDFROST")>0;
            float baseDamage=player.GetCustomStat(frost?ECustomStat.IceDamage:ECustomStat.FireDamage)*
                (1+player.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f)*combo.damagePercent/100f;
            if(player.GetCustomStatUnsafe("FLAMESWORDMAGICDAMAGE")>0)baseDamage*=1+player.GetCustomStat(ECustomStat.MagicDamageBonus)/100f;
            float fallFactor=ArtifactDamageProfiles.RootBonus(live,player);
            if(player.GetCustomStatUnsafe(source.solisImberStatId)>0)fallFactor*=1.5f;
            double luck=Math.Max(0,Math.Min(1,player.GetCustomStatUnsafe("FLAMESWORDLUCK")/100d));
            var normal=new DamageTooltip.Hit{Raw=baseDamage,Element=frost?EDamageElementalType.Ice:EDamageElementalType.Fire,
                Factors=new[]{fallFactor},ExtraCriticalChance=player.GetCustomStatUnsafe("FLAMESWORDCRITICAL")/100f,
                ExtraCritical=player.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE")};
            DpsCapture.Add(cycle,normal,swords*(1-luck),player.GetCustomStatUnsafe("FLAMESWORDIGNOREDEFENSE"));
            if(luck>0)
            {
                var lucky=ProjectileDamageProfiles.Scale(normal,1+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent")/100f);
                DpsCapture.Add(cycle,lucky,swords*luck,player.GetCustomStatUnsafe("FLAMESWORDIGNOREDEFENSE"));
            }
            cycle.Condition="화염검을 전부 사용·회수하며 개별 재충전까지 포함";
        }
    }
}
