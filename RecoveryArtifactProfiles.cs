using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal static class RecoveryArtifactProfiles
    {
        internal static bool TryDescribe(Charm_Basic source,Charm_Basic live,int level,PlayerAvatar player,out string text)
        {
            text=null;int index=source.LevelToIdx(level);
            if(source is Charm_AlchemyFlask)
            {
                var flask=(Charm_AlchemyFlask)source;
                text=ArtifactDamageProfiles.NativeStats(flask,level)+"\n물약을 마셨을 때 버프 적용\n"+Buff(flask.buffPrefab,flask.amplifyValuesByLevel.SafeRandomAccess(index),player);
            }
            else if(source is Charm_Exploitation)
            {
                var hand=(Charm_Exploitation)source;
                float amount=hand.healAmountByLevel.SafeRandomAccess(index),bonus=hand.bonusHealAmountByLevel.SafeRandomAccess(index);
                bool low=player.GetCustomStat(ECustomStat.DamageReduction)<=hand.defenseLimit;
                text=ArtifactDamageProfiles.NativeStats(hand,level)+"\n평타·돌진·특수 공격 적중 시 HP 회복\n기본 요청량 "+amount.ToString("0.###")+" · 방어력 "+hand.defenseLimit+" 이하에서 +"+bonus.ToString("0.###")+"\n현재 방어력 기준 회복 요청량 "+(amount+(low?bonus:0)).ToString("0.###")+"\n발동 간격 "+hand.cooldownTimer.time.ToString("0.###")+"초 · 허수아비 제외 · 직접 추가 피해 없음";
            }
            else if(source is Charm_FrozenMemory)
            {
                var memory=(Charm_FrozenMemory)source;
                text="동상 부여 시 자신에게 냉기 버프 적용\n"+Buff(memory.frostTouchBuffPrefab,1,player,memory.stackByLevel.SafeRandomAccess(index));
            }
            else if(source is Charm_ElruNaptimePillow)
            {
                var pillow=(Charm_ElruNaptimePillow)source;
                text="살아 있는 상태로 전투 종료 시 HP 회복 요청량 "+pillow.healByLevel.SafeRandomAccess(index)+"\n회복 보정·최대 HP 상한은 원본 회복 처리 적용 · 직접 추가 피해 없음";
            }
            else if(source is Charm_Endure)
            {
                var lid=(Charm_Endure)source;
                text=ArtifactDamageProfiles.NativeStats(lid,level)+"\n물리 계열 치명타·처형 적중 시 버프 적용\n"+Buff(lid.buff,1,player);
            }
            else if(source is Charm_BrokenMirror)
            {
                var mirror=(Charm_BrokenMirror)source;
                text=ArtifactDamageProfiles.NativeStats(mirror,level)+"\n살아 있는 상태로 층 입장 시 보호막 "+mirror.shieldByLevel.SafeRandomAccess(index)+" 부여\n장착 해제 시 해당 보호막 제거 · 직접 추가 피해 없음";
            }
            else if(source is Charm_DashShield)
            {
                var mirror=(Charm_DashShield)source;
                text=ArtifactDamageProfiles.NativeStats(mirror,level)+"\n돌진 시작 시 보호막 "+mirror.shieldAmount.SafeRandomAccess(index)+" 부여\n지속 "+mirror.shieldDurationTimer.time.ToString("0.###")+"초 · 발동 간격 "+mirror.cooldownTimer.time.ToString("0.###")+"초\n직접 추가 피해 없음 · 보호막이 남은 상태로 재발동하면 지속시간 갱신";
            }
            else if(source is Charm_EvasionRestore)
            {
                var band=(Charm_EvasionRestore)source;
                text=ArtifactDamageProfiles.NativeStats(band,level)+"\n회피 성공 시 돌진 횟수 "+band.restoreDashCountByLevel.SafeRandomAccess(index)+"회 회복\n현재 돌진 상한 내에서 회복 · 별도 추가 피해 없음";
            }
            else if(source is Charm_GainShieldWhileUsingMP)
            {
                var letter=(Charm_GainShieldWhileUsingMP)source;
                text="전투 중 MP 사용 누적 "+letter.mpThreshold+"마다 보호막 "+letter.shieldsByLevel.SafeRandomAccess(index)+" 부여\n지속 "+letter.shieldTime.ToString("0.###")+"초 · 초과 누적량은 다음 발동에 유지\n실제 차감량 대신 MP 사용 이벤트의 요청량을 누적 · 직접 추가 피해 없음";
            }
            else if(source is Charm_HitInvincible)
            {
                var bread=(Charm_HitInvincible)source;
                text="피격 후 무적 시간 "+bread.addTimeByLevel.SafeRandomAccess(index).ToString("+0.###;-0.###;0")+"초\n공격 피해나 공격 중 무적 시간을 변경하는 효과는 아님";
            }
            else if(source is Charm_Chintamani)
            {
                var stone=(Charm_Chintamani)source;
                text="치명 피해를 받으면 HP를 0으로 만든 뒤 최대 HP의 "+stone.healPercent+"% 회복 및 부활 무적\n장비 위치에 이번 모험 임시 레벨 +"+stone.addLevel+" 부여 후 장비 소비\n직접 공격 피해 없음 · 부활 전에는 소비 후의 레벨 효과를 미리 적용하지 않음";
            }
            else if(source is Charm_MpHeal)
            {
                var bucket=(Charm_MpHeal)source;
                int percent=bucket.mpHealAmountByLevel.SafeRandomAccess(index);
                int speed=Math.Max(0,KeywordDatabase.GetConstValue("mpHealCooldownBonusBy1Mp")*player.GetCustomStat(ECustomStat.MPRegen));
                text="사용 시 최대 MP의 "+percent+"% 회복\n현재 최대 MP 기준 회복 요청량 "+(player.MaxMp*percent/100f).ToString("0.###")+" · 최대 MP 상한 적용\n현재 MP 재생 기준 재사용 시간 "+(bucket.coolDownTimer.time*100f/(100+speed)).ToString("0.###")+"초\n직접 공격 피해 없음 · 피해 계산을 열어도 회복 능력을 사용하지 않음";
            }
            else if(source is Charm_MPShieldActive)
            {
                var shield=(Charm_MPShieldActive)source;
                text="사용 시 MP 보호막 활성화 · 지속 "+shield.shieldDuration.ToString("0.###")+"초\n기본 재사용 시간 "+shield.coolDownTimer.time.ToString("0.###")+"초\n공격 피해를 직접 늘리지 않음 · 이후 피격에 의한 MP 변화는 피격 시점에 결정";
            }
            else return false;
            text+="\n표시 레벨 기준 · "+(live&&live.IsEffectEnabled?"현재 활성 효과는 기존 능력치에 포함":"현재 효과 비활성·미장착");
            return true;
        }
        private static string Buff(CharacterBuff prefab,float amplification,PlayerAvatar player,int? maximumOverride=null)
        {
            if(!prefab)return "연결된 버프 데이터 없음";
            int maxStack=maximumOverride??prefab.MaxStackCount;
            var text=new StringBuilder("기본 지속 ").Append(prefab.defaultDuration.ToString("0.###")).Append("초 · 최대 ").Append(maxStack).AppendLine("중첩");
            Dictionary<string,int> values;
            if(BuffStatProjection.TryRead(prefab,1,amplification,out values))
            {
                if(values.Count==0)text.AppendLine("능력치 직접 증가 없음");
                else
                {
                    text.AppendLine("미발동: 이 버프의 능력치 변화 0\n발동 1중첩의 능력치 변화:");
                    AppendStats(text,values);
                    Dictionary<string,int> maximum;
                    if(maxStack>1&&BuffStatProjection.TryRead(prefab,maxStack,amplification,out maximum))
                    {text.AppendLine("최대 중첩의 능력치 변화:");AppendStats(text,maximum);}
                }
            }
            else text.AppendLine("전용 버프 수치 계산 미지원 · 추가 분석 필요");
            var current=player.GetBuffInstanceByPrefab(prefab);
            text.Append("현재 버프: ").Append(current?current.CurrentStack+"중첩":"없음").Append(" · 현재 적용 효과는 공격 계산에 포함\n이 장비의 발동 조건을 풀 버프 미리보기에서 임의로 충족시키지 않음");
            return text.ToString();
        }
        private static void AppendStats(StringBuilder text,Dictionary<string,int> values)
        {
            foreach(var entry in values)
            {
                string name=entry.Key==DamageStatPreview.MoveSpeedPercentDeltaKey?"이동 속도 배율 (%p)":entry.Key==DamageStatPreview.ReservedMpDeltaKey?"예약 MP":entry.Key==DamageStatPreview.MaxMpDeltaKey?"최대 MP":entry.Key==DamageStatPreview.MaxHpDeltaKey?"최대 HP":entry.Key==DamageStatPreview.FinalHpDeltaKey?"최종 최대 HP (%)":null;
                if(name!=null){text.AppendLine(name+" "+entry.Value.ToString("+0;-0;0"));continue;}
                var status=StatusDatabase.CreateStatusEntity(entry.Key,entry.Value);
                text.AppendLine(status!=null?KeywordDatabase.Convert(status.ToString(true,false,false,Color.white)):entry.Key+" "+entry.Value.ToString("+0;-0;0"));
            }
        }
    }
}
