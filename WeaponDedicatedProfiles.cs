using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SephiriaDicePreview
{
    // Attacks created by weapon add-ons do not appear in the three ordinary
    // input arrays. Keep their native trigger and cooldown rules in one place.
    internal static class WeaponDedicatedProfiles
    {
        internal static void Append(StringBuilder text,WeaponSimple weapon,PlayerAvatar player)
        {
            if(!weapon||weapon.addons==null)return;
            foreach(var addon in weapon.addons)
            {
                var automatic=addon as WeaponAddon_DashAttack;
                if(automatic)AppendDashAttack(text,weapon,player,automatic.dashAttackIdx,
                    "이동 돌진 자동 공격",automatic.autoAttackRadius,"비용을 낸 돌진 100% · 무료 돌진 50%");
                var ghost=addon as WeaponAddonKatana_SummonGhost;
                if(ghost)AppendGhost(text,weapon as WeaponSimple_Katana,player,ghost);
                var nastrond=addon as WeaponAddonKatana_Nastrond;
                if(nastrond)AppendNastrond(text,weapon,player,nastrond);
            }
        }
        private static void AppendDashAttack(StringBuilder text,WeaponSimple weapon,PlayerAvatar player,int index,string label,float radius,string condition)
        {
            var sample=AttackSample(weapon,player,1,index);
            if(sample.Hits==null||sample.Hits.Count==0){text.Append(label).AppendLine(": 공격 데이터 없음");return;}
            text.Append(label).Append(": ").AppendLine(DamageTooltip.Hits(player,sample.Hits.ToArray()));
            text.Append("탐색 반경 ").Append(radius.ToString("0.###")).Append(" · ").AppendLine(condition);
        }
        private static void AppendGhost(StringBuilder text,WeaponSimple_Katana weapon,PlayerAvatar player,WeaponAddonKatana_SummonGhost addon)
        {
            var ghost=addon.ghostPrefab?addon.ghostPrefab.GetComponent<KatanaGhost>():null;
            if(!weapon||!ghost){text.AppendLine("혼령 추가타: 공격 데이터 없음");return;}
            var sample=AttackSample(weapon,player,1,0);
            if(sample.Hits==null||sample.Hits.Count==0){text.AppendLine("혼령 추가타: 돌진 공격 데이터 없음");return;}
            text.Append("혼령 이동 돌진 추가타 1회: ").AppendLine(DamageTooltip.Hits(player,sample.Hits.ToArray()));
            text.Append("한 번 발동 시 ").Append(Math.Max(0,ghost.dashAttackCount)).Append("타 · 기본 재사용 대기 ")
                .Append(ghost.dashAttackIntervalTimer.time.ToString("0.###")).Append("초 · 공격 간격 ")
                .Append(ghost.attackIntervalTimer.time.ToString("0.###")).AppendLine("초");
            text.AppendLine("비용을 낸 이동 돌진 후 근처 적이 있을 때 발동");
            if(player.GetCustomStatUnsafe("KATANAGHOSTCOUNTER")>0)
                text.AppendLine("반격 성공 시 현재 돌진 횟수만큼 혼령 추가타 · 피격 의존 효과라 지속 DPS에서 제외");
        }
        private static DamageTooltip.WeaponAttackSample AttackSample(WeaponSimple weapon,PlayerAvatar player,int kind,int index)
        {
            if(DamageTooltip.CurrentCapture!=null)return DamageTooltip.CaptureWeaponAttack(weapon,player,kind,index);
            DamageTooltip.WeaponAttackSample sample=null;
            DamageTooltip.Capture(delegate{sample=DamageTooltip.CaptureWeaponAttack(weapon,player,kind,index);return string.Empty;},player);
            return sample;
        }
        private static DamageTooltip.Hit NastrondHit(WeaponSimple weapon,PlayerAvatar player,WeaponAddonKatana_Nastrond addon,int mp,bool repeat)
        {
            var pay=ChargingDamageProfiles.Pay(player,mp,repeat);
            var hit=new DamageTooltip.Hit{Raw=addon.defaultDamage+player.GetCustomStat(ECustomStat.IceDamage)*addon.damagePercent/100f,
                Element=EDamageElementalType.Ice,Factors=new[]{1+player.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,pay.Multiplier},AfterFactors=pay.Flat};
            WeaponAdditionalDamageProfiles.ApplyConditional(weapon,player,hit);return hit;
        }
        private static void AppendNastrond(StringBuilder text,WeaponSimple weapon,PlayerAvatar player,WeaponAddonKatana_Nastrond addon)
        {
            text.AppendLine("나스트론드 충전 폭발 1회 · 일반 / 치명타");
            int[] resources={0,Math.Max(0,player.MP),Math.Max(0,player.MaxMp)};
            string[] labels={"MP 0","현재 MP","MP 가득"};
            for(int i=0;i<resources.Length;i++)text.Append(labels[i]).Append(": ").AppendLine(DamageTooltip.Hits(player,NastrondHit(weapon,player,addon,resources[i],false)));
            int count=Math.Max(0,1+player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"));
            text.Append("한 번 발동 시 ").Append(count).Append("회 · 폭발 간격 0.35초 · 충전 ")
                .Append(addon.chargingCharm?DpsChargingProfiles.ChargeSeconds(addon.chargingCharm,player).ToString("0.###"):"확인 불가").AppendLine("초");
            text.AppendLine("충전 완료 후 특수 동작에 이어 평타를 사용하면 발동");
            int repeat=ChargingDamageProfiles.RepeatPercent(player);
            if(repeat>0)text.Append("추가 발동 확률 ").Append(Math.Min(100,repeat)).AppendLine("% · 추가 발동은 다시 추가 발동하지 않음");
        }
        internal static void CaptureDps(DpsSnapshot snapshot,WeaponSimple weapon,PlayerAvatar player)
        {
            if(!weapon||weapon.addons==null)return;
            foreach(var addon in weapon.addons)
            {
                var automatic=addon as WeaponAddon_DashAttack;
                if(automatic)AddDashCycle(snapshot,weapon,player,"이동 돌진 자동 공격 DPS",automatic.dashAttackIdx,1,DpsTiming.MovementDash(player,true));
                var ghostAddon=addon as WeaponAddonKatana_SummonGhost;
                if(ghostAddon)AddGhostCycle(snapshot,weapon as WeaponSimple_Katana,player,ghostAddon);
                var nastrond=addon as WeaponAddonKatana_Nastrond;
                if(nastrond)AddNastrondCycle(snapshot,weapon,player,nastrond);
                var ring=addon as WeaponAddonCommon_BurnRing;
                if(ring)AddBurnRingCycle(snapshot,weapon,player,ring);
                var needle=addon as WeaponAddonGreatsword_FireNeedleBullet;
                if(needle)AddNeedleCycles(snapshot,weapon,player,needle);
            }
        }
        private static void AddDashCycle(DpsSnapshot snapshot,WeaponSimple weapon,PlayerAvatar player,string name,int index,int count,DpsTiming.Cycle trigger)
        {
            var cycle=new DpsTiming.Cycle{Seconds=trigger.Seconds,Unavailable=trigger.Unavailable,Condition=trigger.Condition};
            if(cycle.Unavailable==null)for(int i=0;i<count;i++)cycle.Attacks.Add(new DpsTiming.Attack{Kind=1,Index=index,Time=0});
            DpsCapture.AddWeaponCycle(snapshot,name,cycle,weapon,player);
        }
        private static void AddGhostCycle(DpsSnapshot snapshot,WeaponSimple_Katana weapon,PlayerAvatar player,WeaponAddonKatana_SummonGhost addon)
        {
            var ghost=addon.ghostPrefab?addon.ghostPrefab.GetComponent<KatanaGhost>():null;
            if(!weapon||!ghost){snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="혼령 이동 돌진 추가타 DPS",Unavailable="혼령 공격 데이터 없음"});return;}
            var movement=DpsTiming.MovementDash(player,true);
            double cooldown=ghost.dashAttackIntervalTimer.time;
            if(ghost.reduceIntervalByDashCount)
            {
                int fixedDash=player.GetCustomStatUnsafe("FIXEDDASH"),dash=player.GetCustomStatUnsafe("DASHCOUNT");
                cooldown=Math.Max(0,cooldown-ghost.intervalReducePerDash*(fixedDash>0?fixedDash:dash));
            }
            if(movement.Unavailable==null)movement.Seconds=Math.Max(movement.Seconds,cooldown);
            AddDashCycle(snapshot,weapon,player,"혼령 이동 돌진 추가타 DPS",0,Math.Max(0,ghost.dashAttackCount),movement);
        }
        private static void AddNastrondCycle(DpsSnapshot snapshot,WeaponSimple weapon,PlayerAvatar player,WeaponAddonKatana_Nastrond addon)
        {
            var cycle=new DpsSnapshot.Cycle{Name="나스트론드 충전 폭발 DPS"};snapshot.Cycles.Add(cycle);
            cycle.Seconds=addon.chargingCharm?DpsChargingProfiles.ChargeSeconds(addon.chargingCharm,player):double.NaN;
            if(!DpsNumbers.Finite(cycle.Seconds)||cycle.Seconds<=0){cycle.Unavailable="충전 시간 데이터 없음";return;}
            int count=Math.Max(0,1+player.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"));
            if(count==0){cycle.Condition="충전 폭발 횟수 0";return;}
            var first=ChargingDamageProfiles.Pay(player,player.MP,false);
            DpsCapture.Add(cycle,NastrondHit(weapon,player,addon,player.MP,false),count);
            double chance=ChargingDamageProfiles.RepeatPercent(player)/100d;
            if(chance>0)DpsCapture.Add(cycle,NastrondHit(weapon,player,addon,first.Remaining,true),count*chance);
            cycle.Condition="충전 완료 즉시 특수 동작 후 평타 · 전타 적중 · MP 비용 순서와 추가 발동 확률 반영";
        }
        private static void AddBurnRingCycle(DpsSnapshot snapshot,WeaponSimple weapon,PlayerAvatar player,WeaponAddonCommon_BurnRing ring)
        {
            var cycle=new DpsSnapshot.Cycle{Name="화염 고리 DPS"};snapshot.Cycles.Add(cycle);
            double speed=1+player.GetCustomStatUnsafe("BURNSPEED")/100d;
            if(speed<=0||ring.burnRingTickTimer.time<=0){cycle.Unavailable="현재 화상 속도로 고리 판정이 진행되지 않음";return;}
            cycle.Seconds=ring.burnRingTickTimer.time/speed;
            var hit=new DamageTooltip.Hit{Raw=player.GetCustomStatUnsafe(ring.relatedStatUnsafe),
                Element=ring.elementalType,Factors=new[]{ring.damagePercent/100f,1+player.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                    1+player.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,1+player.GetCustomStatUnsafe("BURNDAMAGE")/100f}};
            WeaponAdditionalDamageProfiles.ApplyConditional(weapon,player,hit);
            DpsCapture.Add(cycle,hit,1);
            cycle.Condition="대상에게 "+KeywordDatabase.Convert("<tag="+ring.debuffKeywordName+">")+" 부여 후 범위 안 유지 · 지속시간 갱신";
        }
        private static void AddNeedleCycles(DpsSnapshot snapshot,WeaponSimple weapon,PlayerAvatar player,WeaponAddonGreatsword_FireNeedleBullet needle)
        {
            var damage=DamageTooltip.CurrentCapture;
            double chance=(damage.CriticalChance+damage.WeaponCriticalChance)/100d;
            int elemental=(int)EDamageElementalType.Physical;
            if(damage.ElementCriticalChance!=null&&elemental>=0&&elemental<damage.ElementCriticalChance.Length)chance+=damage.ElementCriticalChance[elemental]/100d;
            chance=Math.Max(0,Math.Min(1,chance));
            int critical=50+player.GetCustomStat(ECustomStat.CriticalDamageBonus)+player.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE");
            int amp=player.GetCustomStatUnsafe("WEAPONCRITICALDAMAGEAMPLIFY");if(amp>0)critical+=(int)(critical*amp/100f);
            int bullets=Math.Max(0,needle.needleBulletCount+Math.Max(0,(int)((critical-50)/20f)));
            var timings=new[]{DpsTiming.Basic(weapon,player),DpsTiming.Dash(weapon,player),DpsTiming.Special(weapon,player)};
            string[] names={"평타 가시 탄환 DPS","돌진 가시 탄환 DPS","특공 가시 탄환 DPS"};
            for(int i=0;i<timings.Length;i++)
            {
                var cycle=new DpsSnapshot.Cycle{Name=names[i],Seconds=timings[i].Seconds,Unavailable=timings[i].Unavailable};snapshot.Cycles.Add(cycle);
                if(cycle.Unavailable!=null)continue;
                string problem;double contacts=DpsCapture.DirectHitCount(weapon,player,timings[i],out problem);
                if(problem!=null){cycle.Unavailable=problem;continue;}
                var hit=new DamageTooltip.Hit{Raw=player.GetCustomStat(ECustomStat.PhysicalDamage),
                    Factors=new[]{needle.needleBulletDamageRatio,1+player.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                        1+player.GetCustomStat(ECustomStat.BasicAttackDamageBonus)/100f,1+player.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f}};
                WeaponAdditionalDamageProfiles.ApplyConditional(weapon,player,hit);
                var result=DpsProjectiles.Artifact(hit,needle.needleBulletPrefab,player);
                if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;continue;}
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*contacts*bullets*chance,part.IgnoreDefense);
                cycle.Condition="직접 공격의 치명타·처형 발동 확률 기대값 · 생성 탄환 전부 적중";
            }
        }
    }
}
