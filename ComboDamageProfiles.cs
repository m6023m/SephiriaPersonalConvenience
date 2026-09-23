using System;
using System.Text;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class ComboDamageProfiles
    {
        internal static string Detail(ComboEffectBase effect,int comboCount,PlayerAvatar player)
        {
            var text=new StringBuilder("<color=#81E6C4>콤보 공격 피해 · 일반 / 치명타</color>\n");
            var cloud=effect as ComboEffect_DarkCloud;
            if(cloud&&comboCount>=cloud.darkCloudComboCount)AppendCloud(text,player);
            else
            {
                var sword=effect as ComboEffect_FlameSword;
                var debuff=effect as ComboEffect_Debuff;
                if(sword&&comboCount>=sword.flameSwordComboCount)AppendSword(text,sword,player);
                else if(debuff&&comboCount>=debuff.debuffActivateComboCount)
                {
                    text.AppendLine("속성 공격 적중 시 부여되는 디버프 피해");
                    DebuffDamagePreview.CaptureArtifact(debuff.debuffPrefab,player,"콤보 효과",1);
                }
                else text.AppendLine("현재 단계에서 자체 공격 피해 없음 · 능력치 효과는 최종 스탯에 반영");
            }
            text.Append("<color=#B9C4D4>활성화된 현재 콤보 단계 기준 · 적 방어 적용 전</color>");
            return text.ToString();
        }
        private static DamageTooltip.Hit CloudHit(PlayerAvatar player,float multiplier)
        {
            float lightning=player.GetCustomStat(ECustomStat.LightningDamage);
            float raw;EDamageElementalType element;
            if(player.GetCustomStatUnsafe("DARKCLOUDICE")>0)
            {
                float ice=player.GetCustomStat(ECustomStat.IceDamage);
                raw=Math.Max(lightning,ice)*KeywordDatabase.GetConstValue("darkCloudDamagePercent")/100f+
                    Math.Min(lightning,ice)*KeywordDatabase.GetConstValue("darkCloudDamagePercentIce")/100f;
                element=EDamageElementalType.IceAndLightning;
            }
            else {raw=lightning*KeywordDatabase.GetConstValue("darkCloudDamagePercent")/100f;element=EDamageElementalType.Lightning;}
            raw*=1+player.GetCustomStatUnsafe("DARKCLOUDDAMAGE")/100f;
            return new DamageTooltip.Hit{Raw=raw*multiplier,Element=element};
        }
        private static void AppendCloud(StringBuilder text,PlayerAvatar player)
        {
            string name=player.GetCustomStatUnsafe("DARKCLOUDICE")>0?"눈구름":"먹구름";
            int luck=Mathf.Clamp(player.GetCustomStatUnsafe("DARKCLOUDLUCK"),0,100);
            if(luck<100)text.Append(name).Append(" 일반 방전: ").AppendLine(DamageTooltip.Hits(player,CloudHit(player,1)));
            if(luck>0)text.Append(name).Append(" 강화 방전 (").Append(luck).Append("%): ").AppendLine(DamageTooltip.Hits(player,CloudHit(player,2)));
        }
        private static DamageTooltip.Hit SwordHit(ComboEffect_FlameSword sword,PlayerAvatar player,float multiplier)
        {
            bool frost=player.GetCustomStatUnsafe("FLAMESWORDFROST")>0;
            var element=frost?EDamageElementalType.Ice:EDamageElementalType.Fire;
            float raw=player.GetCustomStat(frost?ECustomStat.IceDamage:ECustomStat.FireDamage)*(1+player.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f)*sword.damagePercent/100f;
            if(player.GetCustomStatUnsafe("FLAMESWORDMAGICDAMAGE")>0)raw*=1+player.GetCustomStat(ECustomStat.MagicDamageBonus)/100f;
            return new DamageTooltip.Hit{Raw=raw*multiplier,Element=element,ExtraCriticalChance=player.GetCustomStatUnsafe("FLAMESWORDCRITICAL"),ExtraCritical=player.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE")};
        }
        private static void AppendSword(StringBuilder text,ComboEffect_FlameSword sword,PlayerAvatar player)
        {
            string name=player.GetCustomStatUnsafe("FLAMESWORDFROST")>0?"얼음 태양검":"태양검";
            int luck=Mathf.Clamp(player.GetCustomStatUnsafe("FLAMESWORDLUCK"),0,100);
            float lucky=(100+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent"))/100f;
            if(luck<100)text.Append(name).Append(" 일반 1발: ").AppendLine(DamageTooltip.Hits(player,SwordHit(sword,player,1)));
            if(luck>0)text.Append(name).Append(" 행운 1발 (").Append(luck).Append("%): ").AppendLine(DamageTooltip.Hits(player,SwordHit(sword,player,lucky)));
        }
        internal static void CaptureDps(DpsSnapshot snapshot,ComboEffectBase effect,int comboCount,PlayerAvatar player)
        {
            var cloud=effect as ComboEffect_DarkCloud;
            if(cloud&&comboCount>=cloud.darkCloudComboCount){CloudDps(snapshot,cloud,player);return;}
            var sword=effect as ComboEffect_FlameSword;
            if(sword&&comboCount>=sword.flameSwordComboCount){SwordDps(snapshot,sword,player);return;}
            var debuff=effect as ComboEffect_Debuff;
            if(debuff&&comboCount>=debuff.debuffActivateComboCount)
            {
                double speed=comboCount>=debuff.debuffHasteComboCount?1+debuff.debuffHastePercent/100d:1;
                double period=speed>0?debuff.debuffActivateCooldownTime/speed:0;
                DebuffDamagePreview.CaptureArtifact(debuff.debuffPrefab,player,"콤보 효과",1,period,0);
                return;
            }
            snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="콤보 효과 DPS",Unavailable="현재 단계에서 자체 공격 피해 없음 · 능력치 효과는 무기 DPS에 반영"});
        }
        private static void CloudDps(DpsSnapshot snapshot,ComboEffect_DarkCloud cloud,PlayerAvatar player)
        {
            double speed=cloud.lightningIntervalSpeed*(1+player.GetCustomStatUnsafe("DARKCLOUDSPEED")/100d);
            if(player.GetCustomStatUnsafe("DARKCLOUDATKSPEEDBONUS")>0)
                speed+=cloud.lightningIntervalSpeed*player.GetCustomStat(ECustomStat.AttackSpeed)*player.GetCustomStatUnsafe("DARKCLOUDATKSPEEDBONUS")/10000d;
            string name=player.GetCustomStatUnsafe("DARKCLOUDICE")>0?"눈구름":"먹구름";
            var cycle=new DpsSnapshot.Cycle{Name=name+" 콤보 DPS",Seconds=speed>0?cloud.cloudTimer.time/speed:0};snapshot.Cycles.Add(cycle);
            if(cycle.Seconds<=0){cycle.Unavailable="먹구름 방전 간격 데이터 없음";return;}
            int shots=Math.Max(1,player.GetCustomStatUnsafe("DARKCLOUDMULTISHOT"));double luck=Mathf.Clamp(player.GetCustomStatUnsafe("DARKCLOUDLUCK"),0,100)/100d;
            int row=DamageTooltip.CurrentCapture.Rows.Count;DamageTooltip.CurrentCapture.Rows.Add(new[]{CloudHit(player,1)});
            if(luck<1)cycle.Terms.Add(new DpsSnapshot.Term{Row=row,Count=shots*(1-luck)});
            row=DamageTooltip.CurrentCapture.Rows.Count;DamageTooltip.CurrentCapture.Rows.Add(new[]{CloudHit(player,2)});
            if(luck>0)cycle.Terms.Add(new DpsSnapshot.Term{Row=row,Count=shots*luck});
            cycle.Condition="먹구름과 범위 내 대상이 계속 유지되는 기준";
        }
        private static void SwordDps(DpsSnapshot snapshot,ComboEffect_FlameSword sword,PlayerAvatar player)
        {
            int capacity=Math.Max(0,sword.maxSword+player.GetCustomStatUnsafe("FLAMESWORDMAX"));
            int shots=Math.Max(1,1+player.GetCustomStatUnsafe("FLAMESWORDADDITIONALATTACK")+player.GetCustomStatUnsafe("FLAMESWORDADDITIONALATTACKFROMWEAPON"));
            string name=player.GetCustomStatUnsafe("FLAMESWORDFROST")>0?"얼음 태양검":"태양검";
            var cycle=new DpsSnapshot.Cycle{Name=name+" 콤보 DPS"};snapshot.Cycles.Add(cycle);
            if(capacity<=0||sword.rechargeTime<=0){cycle.Unavailable="화염검 충전 수 또는 충전 시간 데이터 없음";return;}
            shots=Math.Min(shots,capacity);cycle.Seconds=Math.Max(sword.minCooldownTime,shots*sword.rechargeTime/capacity);
            double luck=Mathf.Clamp(player.GetCustomStatUnsafe("FLAMESWORDLUCK"),0,100)/100d;
            float lucky=(100+KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent"))/100f;
            int row=DamageTooltip.CurrentCapture.Rows.Count;DamageTooltip.CurrentCapture.Rows.Add(new[]{SwordHit(sword,player,1)});
            if(luck<1)cycle.Terms.Add(new DpsSnapshot.Term{Row=row,Count=shots*(1-luck)});
            row=DamageTooltip.CurrentCapture.Rows.Count;DamageTooltip.CurrentCapture.Rows.Add(new[]{SwordHit(sword,player,lucky)});
            if(luck>0)cycle.Terms.Add(new DpsSnapshot.Term{Row=row,Count=shots*luck});
            cycle.Condition="직접 공격을 계속 적중시키고 소모한 화염검을 충전하는 기준";
        }
    }
}
