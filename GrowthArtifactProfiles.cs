using System.Reflection;
using System.Text;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class GrowthArtifactProfiles
    {
        private static readonly FieldInfo QuestCounter=AccessTools.Field(typeof(Charm_GrowthStatusInstance),"questCounter");
        private static readonly FieldInfo BasicAdded=AccessTools.Field(typeof(Charm_GrowthCrossbow),"addedBasicDamage");
        private static readonly FieldInfo SpecialAdded=AccessTools.Field(typeof(Charm_GrowthCrossbow),"addedSpecialDamage");
        private static int At(int[] values,int index){return values.Length==0?0:values.SafeRandomAccess(index);}
        internal static bool TryDescribe(Charm_Basic source,Charm_Basic live,int level,out string description)
        {
            description=null;
            if(!(source is Charm_GrowthCrossbow)&&!(source is Charm_GrowthKatana)&&!(source is Charm_GrowthGuard)&&!(source is Charm_GrowthMoveSpeed))return false;
            var growth=(Charm_GrowthStatusInstance)source;
            int index=source.LevelToIdx(level);
            var text=new StringBuilder(ArtifactDamageProfiles.NativeStats(growth,level));
            if(growth.hasGrowthQuest)
            {
                int current=live is Charm_GrowthStatusInstance?(int)QuestCounter.GetValue(live):0;
                text.Append("\n성장 진행 ").Append(current).Append(" / ").Append(growth.growthQuestGoal);
                if(growth.reward)text.Append(" · 완료 후 ").Append(growth.reward.Name).Append("(으)로 교체");
                text.AppendLine("\n성장 진행 수치는 공격력에 직접 곱하지 않음");
            }
            var crossbow=source as Charm_GrowthCrossbow;
            if(crossbow)
            {
                if(growth.hasGrowthQuest)text.AppendLine("성장 조건: 석궁 재장전 완료");
                if(crossbow.hasReloadBuff)
                {
                    int basic=At(crossbow.basicAttackDamageByLevel,index),special=At(crossbow.specialAttackDamageByLevel,index);
                    var active=live as Charm_GrowthCrossbow;
                    text.Append("재장전 후 ").Append(crossbow.reloadBuffDuration.ToString("0.###")).Append("초 · 평타 피해 +").Append(basic).Append("% · 특수 공격 피해 +").Append(special).AppendLine("%");
                    text.Append("미발동: 각각 +0% · 현재 실제 적용: 평타 +").Append(active&&active.IsEffectEnabled?(int)BasicAdded.GetValue(active):0).Append("% / 특수 +").Append(active&&active.IsEffectEnabled?(int)SpecialAdded.GetValue(active):0).AppendLine("%");
                    text.AppendLine("재발동은 지속시간 갱신 · 중복 중첩 없음 · 현재 보너스는 공격 계산에 이미 반영");
                }
            }
            var katana=source as Charm_GrowthKatana;
            if(katana)
            {
                if(growth.hasGrowthQuest)text.AppendLine("성장 조건: 반격 성공");
                text.Append("납도·발도 무적 시간 보너스 ").Append(At(katana.sheathInvincibleBonusPercentByLevel,index).ToString("+0;-0;0")).AppendLine("% · 별도 직접 피해 없음");
            }
            var guard=source as Charm_GrowthGuard;
            if(guard)
            {
                if(growth.hasGrowthQuest)text.AppendLine(guard.countOnlyPerfectGuard?"성장 조건: 완벽한 가드 성공":"성장 조건: 가드 성공");
                text.Append("가드 저항 보너스 ").Append(At(guard.guardCostReductionByLevel,index).ToString("+0;-0;0")).Append(" · 가드 각도 보너스 ").Append(At(guard.guardRangeBonusByLevel,index).ToString("+0;-0;0")).AppendLine(" · 별도 직접 피해 없음");
            }
            var move=source as Charm_GrowthMoveSpeed;
            if(move&&growth.hasGrowthQuest)text.Append("성장 조건: 이동 속도 배율 ").Append(move.moveSpeedPercentForGrowth).AppendLine("% 도달 · 거리 누적이 아님");
            description=text.ToString().TrimEnd();return true;
        }
    }
}
