using System;
using System.Reflection;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class ResourceArtifactProfiles
    {
        private static readonly FieldInfo BurnApplied=AccessTools.Field(typeof(Charm_BurnWeaponDamage),"added");
        private static readonly FieldInfo TabletApplied=AccessTools.Field(typeof(Charm_CriticalChanceIncreaseWithTablets),"enabledCriticalBonus");
        private static readonly FieldInfo WaterStack=AccessTools.Field(typeof(Charm_WaterBag),"currentStack");
        private static readonly FieldInfo ReducedMagic=AccessTools.Field(typeof(Charm_ReduceMPCost),"currentMagicCharm");
        private static readonly FieldInfo CooldownMagic=AccessTools.Field(typeof(Charm_RightSpellCooldownHelper),"currentMagicCharm");
        private static readonly FieldInfo WingBonus=AccessTools.Field(typeof(Charm_Wings),"addedStatValue");
        private static readonly FieldInfo MpLossCount=AccessTools.Field(typeof(Charm_GainBuffOnMPLoss),"lossMpCounter");
        private static readonly FieldInfo BeltCount=AccessTools.Field(typeof(Charm_WoodenBox),"applied");
        private static readonly FieldInfo BeltLevel=AccessTools.Field(typeof(Charm_WoodenBox),"appliedLevel");
        internal static bool TryDescribe(Charm_Basic source,Charm_Basic live,int level,PlayerAvatar player,out string text)
        {
            text=null;
            if(RecoveryArtifactProfiles.TryDescribe(source,live,level,player,out text))return true;
            if(source is Charm_FlamePlanet)
            {
                bool plasma=player.GetCustomStatUnsafe("PLASMAACTIVE")>0;
                EDamageElementalType element;
                float tick=plasma?CharacterDebuff_Plasma.CalculateTickDamage(player,out element):CharacterDebuff_Burn.CalculateTickDamage(player,out element);
                text="행성 기본 피해에 "+(plasma?"플라즈마":"화상")+" 틱 상당의 피해 가산\n현재 1틱 기준 원시 가산량 "+tick.ToString("0.###")+"\n일반 등급 1틱: +"+tick.ToString("0.###")+"\n고급 2틱: +"+(tick*2).ToString("0.###")+"\n희귀 3틱: +"+(tick*3).ToString("0.###")+"\n전설·영원 4틱: +"+(tick*4).ToString("0.###")+"\n가산 후 해당 행성의 아티팩트·행성 피해·강화 배율 적용\n위 수치는 최종 피해가 아닌 행성 기본 피해 가산량 · 각 행성 상세보기에서 최종 피해 계산";
                return true;
            }
            var belt=source as Charm_WoodenBox;
            if(belt)
            {
                int per=belt.apPerQuickSlotCharmByLevel.SafeRandomAccess(belt.LevelToIdx(level));
                var active=live as Charm_WoodenBox;
                int count=active&&active.IsEffectEnabled?(int)BeltCount.GetValue(active):0;
                int appliedLevel=active?(int)BeltLevel.GetValue(active):-1;
                int applied=count>0&&appliedLevel>=0?active.apPerQuickSlotCharmByLevel.SafeRandomAccess(appliedLevel)*count:0;
                text="앞쪽 6개 슬롯의 아티팩트 1개당 화염·냉기·번개 각각 +"+per+"\n최소: 빈 슬롯 6개에서 +0 · 최대: 아티팩트 6개에서 각 +"+(6*per)+"\n현재 원본 적용 개수 "+count+" · 실제 적용 중 각 +"+applied+"\n무기·기타 아이템은 개수에서 제외 · 현재 적용값은 속성 공격 계산에 이미 포함";
                return true;
            }
            var storm=source as Charm_BlazingStormcloud;
            if(storm)
            {
                text=ArtifactDamageProfiles.NativeStats(storm,level)+"\n먹구름 공격에 맞은 대상에게 화상 부여 · 먹구름 공격 피해와 별도\n";
                if(!storm.debuffPrefab){text+="화상 데이터 없음";return true;}
                EDamageElementalType element;
                float tick=CharacterDebuff_Burn.CalculateTickDamage(player,out element);
                int maximum=2+player.GetCustomStatUnsafe("BURNSTACK");
                var one=new DamageTooltip.Hit{Raw=tick,Element=element,ElementalEffect=true};
                text+="미부여: 0\n화상 1중첩·1틱 / 치명타: "+DamageTooltip.Hits(player,one);
                if(maximum>0)text+="\n현재 중첩 상한 "+maximum+"·1틱 / 치명타: "+DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=tick,Element=element,ElementalEffect=true,Factors=new[]{(float)maximum}});
                float speed=1+player.GetCustomStatUnsafe("BURNSPEED")/100f;
                text+=speed>0?"\n현재 틱 간격 "+(storm.debuffPrefab.tickTimer.time/speed).ToString("0.###")+"초":"\n현재 화상 속도로는 틱 시간이 진행되지 않음";
                text+="\n증폭값 1 기준 · 실제 대상의 중첩·기존 증폭에 따라 변동\n화상 진화·푸른 화상·화상 피해·디버프 피해 보정 반영 · 적중 횟수를 총 지속 피해로 단순 합산하지 않음";
                return true;
            }
            var blocking=source as Charm_AddBuffFromBlocking;
            if(blocking)
            {
                int amount=blocking.basicAttackDamageByLevel.SafeRandomAccess(blocking.LevelToIdx(level));
                text=(blocking.onOnlyPerfectGuard?"완벽한 가드 성공":"가드 성공 (보조 방패 포함)")+" 시 평타 피해 버프 "+amount.ToString("+0;-0;0")+"%\n최소: 발동 전 +0% · 조건 충족 후 표시 레벨 보너스 적용";
                if(blocking.buffPrefab)
                {
                    var buff=player.GetBuffInstanceByPrefab(blocking.buffPrefab);
                    text+="\n기본 지속 "+blocking.buffPrefab.defaultDuration.ToString("0.###")+"초 · 현재 버프 "+(buff?buff.CurrentStack+"중첩":"없음");
                }
                text+="\n현재 활성 버프는 공격 계산에 이미 포함 · 풀 버프 계산에서 가드 성공을 임의로 가정하지 않음";
                return true;
            }
            var mpLoss=source as Charm_GainBuffOnMPLoss;
            if(mpLoss)
            {
                var active=live as Charm_GainBuffOnMPLoss;
                int counter=active?(int)MpLossCount.GetValue(active):0;
                text="전투 중 MP 사용 누적 "+mpLoss.requiredMP+"마다 버프 1회 적용\n현재 누적 "+counter+" · 비전투 상태에서 누적 초기화\n한 번에 기준의 여러 배를 사용하면 해당 횟수만큼 적용 후 나머지 누적 유지";
                if(mpLoss.buffPrefab)
                {
                    var buff=player.GetBuffInstanceByPrefab(mpLoss.buffPrefab);
                    text+="\n중첩 최소 0 / 현재 "+(buff?buff.CurrentStack:0)+" / 최대 "+mpLoss.buffPrefab.MaxStackCount+"\n기본 지속 "+mpLoss.buffPrefab.defaultDuration.ToString("0.###")+"초";
                    if(mpLoss.buffPrefab is CharacterBuff_IncreaseCriticalChance_FromMpLossCharm)
                        text+="\n중첩당 치명타 확률 +"+mpLoss.buffAmplify.ToString("0.###")+"%p · 최대 +"+(mpLoss.buffAmplify*mpLoss.buffPrefab.MaxStackCount).ToString("0.###")+"%p";
                    else text+="\n버프 적용 배율 "+mpLoss.buffAmplify.ToString("0.###");
                }
                text+="\n실제 차감량이 아닌 MP 사용 이벤트의 요청량을 누적 · 무소모 효과 중에도 누적 가능\n현재 적용된 능력치는 피해 계산에 포함 · 버프 발동 자체는 별도 직접 타격 없음";
                return true;
            }
            var cheer=source as Charm_TheFlagOfCheer;
            if(cheer)
            {
                text="깃발 범위 안의 아군 공격 속도 +"+cheer.speedByLevel.SafeRandomAccess(cheer.LevelToIdx(level))+"%\n범위 "+cheer.effectRadius+" · 지속 "+cheer.flagTime+"초 · 재사용 "+cheer.coolDownTimer.time.ToString("0.###")+"초\n범위를 벗어나거나 깃발이 끝나면 보너스 해제 · 직접 추가타 없음\n공격 속도를 피해로 전환하는 장비가 있으면 실제 적용된 공격 속도로 계산";
                return true;
            }
            var wings=source as Charm_Wings;
            if(wings)
            {
                float step=wings.attackSpeedUnitByLevel.SafeRandomAccess(wings.LevelToIdx(level));
                int speed=player.GetCustomStat(ECustomStat.AttackSpeed);
                if(step<=0){text="공격 속도 전환 기준 확인 필요";return true;}
                int computed=speed>0?(int)(speed/step)*wings.statMultiplier:0;
                var active=live as Charm_Wings;
                int applied=active&&active.IsEffectEnabled?(int)WingBonus.GetValue(active):0;
                text=ArtifactDamageProfiles.NativeStats(wings,level)+"\n양수 공격 속도 "+step.ToString("0.###")+"%마다 전체 피해 보너스 "+wings.statMultiplier.ToString("+0;-0;0")+"%\n단위 미만은 절삭 · 공격 속도 0 이하에서는 +0% · 이 효과 자체의 고정 상한 없음\n현재 공격 속도 "+speed+"%로 표시 레벨 계산: "+computed.ToString("+0;-0;0")+"%\n현재 장착 레벨에서 실제 적용 중: "+applied.ToString("+0;-0;0")+"%\n0.5초 초과 간격으로 갱신 · 현재 적용 보너스는 공격 피해에 이미 포함";
                return true;
            }
            var trade=source as Charm_TradeMoney;
            if(trade)
            {
                int money=trade.addMoneyByLevel.SafeRandomAccess(trade.LevelToIdx(level));
                int total=money+(int)(money*(player.GetCustomStat(ECustomStat.MoneyDrop)/100f));
                text=ArtifactDamageProfiles.NativeStats(trade,level)+"\n거래 상대별 첫 거래 시작 시 기본 골드 +"+money+"\n현재 골드 획득 보정 적용: +"+total+" · 이미 거래한 상대에게는 +0\n직접 추가 피해 없음 · 골드 비례 피해는 실제 획득 후 보유 골드로 갱신";
                return true;
            }
            var paper=source as Charm_WhitePaper;
            if(paper)
            {
                text=ArtifactDamageProfiles.NativeStats(paper,level)+"\n대상 칸에서 같은 분류가 "+paper.match+"개 이상이면 해당 분류 획득";
                var active=live as Charm_WhitePaper;
                if(active&&active.IsEffectEnabled)
                {
                    var names=new System.Collections.Generic.List<string>();
                    foreach(string id in active.assignedCategory){var category=ItemDatabase.FindItemCategory(id);names.Add(category?category.Name:id);}
                    text+="\n현재 획득 분류: "+(names.Count>0?string.Join(" · ",names.ToArray()):"없음");
                }
                text+="\n분류로 활성화된 콤보는 현재 계산에 포함 · 별도 추가 타격이나 임의 피해 배율 없음";
                return true;
            }
            var elementalCritical=source as Charm_Burn;
            if(elementalCritical)
            {
                string[] elements={"물리","화염","냉기","번개","혼돈","일반","냉기·번개","화염·냉기","화염·번개"};
                int element=(int)elementalCritical.targetElementalType;
                string name=element>=0&&element<elements.Length?elements[element]:elementalCritical.targetElementalType.ToString();
                text=ArtifactDamageProfiles.NativeStats(elementalCritical,level)+"\n"+name+" 속성으로 판정되는 공격의 치명타 추가 피해 +"+elementalCritical.addCriticalDamageByLevel.SafeRandomAccess(elementalCritical.LevelToIdx(level))+"%p\n자신과 동료의 해당 속성 공격에 적용 · 혼합 속성과 혼돈 판정은 원본 속성 비교 규칙을 따름\n별도 화상이나 추가타를 생성하는 효과는 아님";
                return true;
            }
            var lightning=source as Charm_Lightning_BasicAttack;
            var flux=source as Charm_CompanionCloud;
            if(flux)
            {
                int calls=flux.cloudCountByLevel.SafeRandomAccess(flux.LevelToIdx(level));
                text=ArtifactDamageProfiles.NativeStats(flux,level)+"\n동료의 "+KeywordDatabase.Convert("<tag=Elemental_"+flux.elementalType+">")+" 계열 적중 시 먹구름 방전 "+calls+"묶음 시도\n동료마다 발동 간격 "+flux.minCooldown.ToString("0.###")+"초 · 각 묶음에 먹구름 다중 방전 적용\n방전 피해의 공격자는 동료가 아닌 장비 소유자"+DarkCloud(player);
                return true;
            }
            if(lightning)
            {
                var controller=player.GetComponent<WeaponControllerSimple>();
                float weight=controller&&controller.currentWeapon?controller.currentWeapon.AttackWeightPerSwing:1;
                float chance=UnityEngine.Mathf.Clamp(lightning.lightningPercentByLevel.SafeRandomAccess(lightning.LevelToIdx(level))*weight,0,100);
                text=ArtifactDamageProfiles.NativeStats(lightning,level)+"\n평타·돌진 공격 적중 시 "+chance.ToString("0.###")+"% 확률로 먹구름 방전 시도\n발동 간격 "+lightning.cooldownTimeByLevel.SafeRandomAccess(lightning.LevelToIdx(level)).ToString("0.###")+"초 · 특수 공격 적중은 이 발동 대상에서 제외";
                text+=DarkCloud(player);
                return true;
            }
            var freeze=source as Charm_Freeze;
            if(freeze)
            {
                text=ArtifactDamageProfiles.NativeStats(freeze,level)+"\n자신이 동상을 부여할 때 "+UnityEngine.Mathf.Clamp(freeze.freezePercents.SafeRandomAccess(freeze.LevelToIdx(level)),0,100)+"% 확률로 같은 동상 중첩 +1\n살아 있는 대상의 종료되지 않은 자신의 동상만 해당 · 추가 중첩 0~1개\n중첩 상한과 재부여 처리는 동상 원본 규칙 적용 · 추가 중첩으로 이 장비의 확률 판정을 재귀 반복하지 않음\n직접 추가타 없음 · 동상으로 바뀐 후속 피해 조건은 별도";
                return true;
            }
            var stun=source as Charm_Stun;
            if(stun)
            {
                text="직접 공격 시 "+UnityEngine.Mathf.Clamp(stun.stunPercent.SafeRandomAccess(stun.LevelToIdx(level)),0,100)+"% 확률로 기절 시도\n현재 능력치 기준 요청 지속시간 "+(1+player.GetCustomStat(ECustomStat.DebuffDuration)/100f).ToString("0.###")+"초\n평타에만 한정되지 않고 DirectAttack 판정에 적용 · 대상의 기절 가능 여부는 원본 규칙 적용\n별도 직접 피해 없음 · 기절을 세는 피해 증폭 효과가 있으면 해당 조건에 반영";
                return true;
            }
            var reduce=source as Charm_ReduceMPCost;
            if(reduce)
            {
                int amount=reduce.reducePercentByLevel.SafeRandomAccess(reduce.LevelToIdx(level));
                text="바로 왼쪽 마법의 MP 비용 보정 −"+amount+"%p\n다른 비용 보정과 합산 후 원본 반올림 적용 · 총 감소 100% 이상 또는 마법 비용 면제 시 0 MP";
                var active=live as Charm_ReduceMPCost;
                var magic=active&&active.IsEffectEnabled?ReducedMagic.GetValue(active) as Charm_Magic:null;
                if(magic&&magic.ContainedMagic)text+="\n현재 연결: "+magic.ContainedMagic.Name+" · 실제 시전 비용 MP "+magic.GetCost(player,magic.CurrentLevelToIdx());
                else text+="\n현재 연결된 활성 마법 없음";
                text+="\n피해와 풀 버프 계산은 연결된 마법의 원본 비용을 사용 · 이 감소량을 다시 차감하지 않음\n사용 MP에 비례하는 공격은 감소한 시전 비용에 따라 피해도 달라질 수 있음";
                return true;
            }
            var helper=source as Charm_RightSpellCooldownHelper;
            if(helper)
            {
                int amount=helper.cooldownRecoveryByLevel.SafeRandomAccess(helper.LevelToIdx(level));
                text="바로 오른쪽 마법의 재사용 회복 속도 +"+amount+"%p\n다른 재사용 회복 속도와 합산 · 대기 시간을 같은 비율로 직접 빼지 않음";
                var active=live as Charm_RightSpellCooldownHelper;
                var magic=active&&active.IsEffectEnabled?CooldownMagic.GetValue(active) as Charm_Magic:null;
                if(magic&&magic.ContainedMagic)
                {
                    float factor=(100+player.GetCustomStat(ECustomStat.CooldownRecoverySpeed)+magic.AdditionalcooldownRecoverySpeed)*.01f;
                    text+="\n현재 연결: "+magic.ContainedMagic.Name+"\n현재 진행 주기의 대기 시간 "+magic.cooldownTimeInThisCycle.ToString("0.###")+"초";
                    text+=factor>0?"\n현재 능력치로 시작할 다음 주기 "+(magic.ContainedMagic.cooldownTime/factor).ToString("0.###")+"초":"\n현재 회복 속도로 다음 주기 대기 시간의 유한값을 계산할 수 없음";
                }
                else text+="\n현재 연결된 활성 마법 없음";
                text+="\n마법 1회 적중 피해량은 동일 · 이미 시작된 주기와 다음 주기를 구분";
                return true;
            }
            var debuffDamage=source as Charm_DebuffDamage;
            if(debuffDamage)
            {
                int per=debuffDamage.additionalDamage.SafeRandomAccess(debuffDamage.LevelToIdx(level));
                text="대상 디버프 개체 1개당 입력 피해 +"+per+"% · 기절도 1개로 추가\n중첩 수를 합산하지 않으며 다른 시전자가 부여한 디버프도 포함\n최소: 대상 디버프·기절 없음 → ×1\nN개 조건의 배율: 1 + N × "+per+" / 100 · 이 효과 자체의 개수 상한 없음\n같은 효과가 여러 개 활성화되어 있으면 각 배율을 순서대로 곱함\n단일 타격 상세에 디버프 1개 조건의 피해를 별도 표시 · 대상 미지정 기본 피해에는 임의 적용하지 않음";
                return true;
            }
            var hpDamage=source as Charm_IncreaseAllDamageByHP;
            if(hpDamage)
            {
                int bonus=hpDamage.damagePercentByLevel.SafeRandomAccess(hpDamage.LevelToIdx(level));
                bool condition=DamageStatPreview.ReadHp(player)/player.MaxHp>=hpDamage.overHpPercent/100f;
                text="HP "+hpDamage.overHpPercent+"% 이상일 때 전체 피해 보너스 "+bonus.ToString("+0;-0;0")+"%\n최소 "+Math.Min(0,bonus)+"% / 최대 "+Math.Max(0,bonus)+"%\n현재 체력 조건: "+(condition?"충족":"미충족")+"\n다른 전체 피해 보너스와 합산 · 현재 활성 보너스는 피해 계산에 이미 포함\n풀 버프의 HP 소모·최대 HP 변화 이후에는 해당 체력 비율로 조건 재계산";
                return true;
            }
            var far=source as Charm_FarChimDamage;
            if(far)
            {
                text=ArtifactDamageProfiles.NativeStats(far,level)+"\n거리 "+far.farDistance.ToString("0.###")+" 이상에서 적중하면 지연 피해 디버프 부여\n속성 효과 피해·디버프 무시 공격 제외 · 대상 디버프 면역 시 부여 불가";
                var debuff=far.chimDamageBuffPrefab as CharacterDebuff_ChimDamage;
                if(!debuff||debuff.GetType()!=typeof(CharacterDebuff_ChimDamage)||debuff.addDamageTimer.time<=0)
                {text+="\n연결된 지연 피해 공식 확인 필요";return true;}
                float period=debuff.addDamageTimer.time;
                float initial=debuff.addDamageTimer.GetTimer();
                float duration=debuff.defaultDuration*(1+player.GetCustomStat(ECustomStat.DebuffDuration)/100f);
                int bonus=string.IsNullOrEmpty(debuff.bonusDamageId)?0:player.GetCustomStatUnsafe(debuff.bonusDamageId);
                text+="\n원시 피해 = 20 × 누적 시간 / "+period.ToString("0.###")+" + "+bonus+"\n새로 부여한 뒤 1초 누적 후 해제 / 치명타\n"+
                    DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=20*((initial+1)/period)+bonus,Element=EDamageElementalType.Normal,ElementalEffect=true});
                text+="\n갱신 없이 자연 종료 (약 "+Math.Max(0,duration).ToString("0.###")+"초) 예상 / 치명타\n"+
                    DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=20*((initial+Math.Max(0,duration))/period)+bonus,Element=EDamageElementalType.Normal,ElementalEffect=true});
                text+="\n본타에 즉시 더해지지 않으며 디버프가 끝날 때 1회 발생 · 실제 종료 프레임의 경과 시간에 따라 소폭 차이";
                if(debuff.renewDurationOnStacked)text+="\n재부여 시 종료 시간은 갱신되지만 누적 피해 시간은 초기화되지 않음 · 반복 갱신에 의한 피해 상한 없음";
                else text+="\n재부여로 종료 시간이 갱신되지 않음";
                text+="\n해제 시점의 능력치를 사용 · 현재 값은 지금 능력치가 유지되는 경우의 예상";
                return true;
            }
            var multiple=source as Charm_MPMultipleCast;
            if(multiple)
            {
                int threshold=multiple.multipleCastMPThresholdByLevel.SafeRandomAccess(multiple.LevelToIdx(level));
                bool qualifies=player.MaxMp>=threshold;
                text=ArtifactDamageProfiles.NativeStats(multiple,level)+"\n최대 MP "+threshold+" 이상이면 마법 추가 시전 +"+multiple.multicast+"회\n현재 최대 MP "+player.MaxMp+" · 표시 레벨 조건 "+(qualifies?"충족":"미충족")+"\n이 장비의 최소 추가 0회 / 최대 추가 "+multiple.multicast+"회\n현재 MP 잔량이 아닌 최대 MP 기준 · 다른 활성 연속 시전 효과와 횟수 합산\n추가 시전마다 MP를 사용하며 0.15초 간격으로 발동 · 1회 적중 피해를 직접 증폭하지 않음";
                if(!live||!live.IsEffectEnabled)text+="\n현재 효과 비활성 · 위 조건을 충족해도 현재 시전 횟수에는 추가하지 않음";
                return true;
            }
            var row=source as Charm_3Elemental_ByRow;
            if(row)
            {
                int amount=row.addElementalStatByLevel.SafeRandomAccess(row.LevelToIdx(level));
                var lines=new System.Text.StringBuilder(ArtifactDamageProfiles.NativeStats(row,level));
                lines.Append("\n배치 행에 따라 속성 능력치와 분류 변경");
                for(int i=0;i<row.lineCategory.Length;i++)
                    lines.Append("\n").Append(i+1).Append("번째 행 유형: ").Append(RowElement(row.lineCategory[i])).Append(' ').Append(amount.ToString("+0;-0;0"));
                lines.Append("\n").Append(row.lineCategory.Length).Append("행마다 반복 · 비율 증가가 아닌 기본 속성 능력치 증가");
                var active=live as Charm_3Elemental_ByRow;
                if(active&&active.Item!=null)lines.Append("\n현재 ").Append(active.Item.YIdx+1).Append("행 · ").Append(RowElement(active.assignedCategory)).Append(active.IsEffectEnabled?" · 현재 속성 피해에 이미 반영":" · 효과 비활성");
                text=lines.ToString();return true;
            }
            var flameWeapon=source as Charm_FlameWeapon;
            if(flameWeapon)
            {
                text=ArtifactDamageProfiles.NativeStats(flameWeapon,level)+"\n특수 공격 휘두르기 시 자신의 위치에 불길 생성\n"+FlameGround(player,flameWeapon.radiusByLevel.SafeRandomAccess(flameWeapon.LevelToIdx(level)));
                return true;
            }
            var fireGround=source as Charm_FireDamageGround;
            if(fireGround)
            {
                text="화염 속성 공격 적중 시 "+fireGround.fireDamagePercent.SafeRandomAccess(fireGround.LevelToIdx(level))+"% 확률로 적중 위치에 불길 생성\n속성 효과 피해로는 재발동하지 않음\n"+FlameGround(player,fireGround.cell);
                return true;
            }
            var water=source as Charm_WaterBag;
            if(water)
            {
                int maximum=water.mpStackByLevel.SafeRandomAccess(water.LevelToIdx(level));
                var active=live as Charm_WaterBag;
                int stored=active?(int)WaterStack.GetValue(active):0;
                int applied=active&&active.IsEffectEnabled?UnityEngine.Mathf.Clamp(stored,0,active.mpStackByLevel.SafeRandomAccess(active.CurrentLevelToIdx())):0;
                text="물주머니의 최대 MP 중첩 효과\nMP 사용 1회당 기본 최대 MP +1 · 소모량에 비례하지 않음\n표시 레벨의 최소 +0 / 최대 +"+maximum+"\n현재 저장 중첩 "+stored+" · 현재 장착 레벨의 적용량 +"+applied;
                text+="\n발동 간격 "+water.coolTime.time.ToString("0.###")+"초";
                if(active&&active.IsEffectEnabled)text+=" · "+(stored>=active.mpStackByLevel.SafeRandomAccess(active.CurrentLevelToIdx())?"현재 레벨 중첩 상한 도달":active.coolTime.Check()?"다음 MP 사용 시 발동 가능":"재사용 대기 중");
                text+="\nMP 무소모 효과 중에도 양수 MP 사용 이벤트가 발생하면 중첩 증가\n현재 MP를 회복하지 않음 · 최대 MP 비례 피해에는 반영 · 현재 적용량은 능력치에 이미 포함";
                return true;
            }
            var scythe=source as Charm_ScytheOfBerut;
            if(scythe)
            {
                text=ArtifactDamageProfiles.NativeStats(scythe,level)+"\n처형 판정 활성화\n최종 치명타 확률에서 100%를 초과한 만큼 처형 확률 발생 (최소 0%, 최대 100%)\n처형 피해 = 치명타 적용 전 피해 × (1 + 치명타 추가 피해율 × 2)\n즉사 효과가 아님 · 처형 활성 시 공격 상세에 처형 피해를 별도 표시";
                return true;
            }
            var execute=source as Charm_MinHPKill;
            if(execute)
            {
                text="일반 적 체력 마무리 효과\n처리 전 (적 현재 HP − 입력 피해) / 적 최대 HP ≤ "+execute.minHPPercentByLevel.SafeRandomAccess(execute.LevelToIdx(level)).ToString("0.###")+"%일 때\n입력 피해에 적 최대 HP를 추가한 뒤 원본 피해 처리를 계속함\n미니보스·보스 제외 · 최소 추가 피해 0, 조건 충족 시 대상 최대 HP만큼 추가\n대상 체력에 따라 달라지므로 고정된 공격당 피해에 임의 합산하지 않음";
                return true;
            }
            var spread=source as Charm_AttackChim;
            if(spread)
            {
                text=ArtifactDamageProfiles.NativeStats(spread,level)+"\n디버프 부여 시 "+spread.chancePercentByLevel.SafeRandomAccess(spread.LevelToIdx(level)).ToString("0.###")+"% 확률로 주변 다른 적 1명에게 같은 디버프 부여\n대상 반경 "+spread.sightRadius.ToString("0.###")+" · 재시도 간격 "+spread.cooldownTimer.time.ToString("0.###")+"초\n확률 실패도 재사용 대기에 포함 · 별도 직접 타격 없음 · 전파된 디버프 종류에 따라 후속 피해가 달라짐";
                return true;
            }
            var balance=source as Charm_FireIce;
            if(balance)
            {
                int index=balance.LevelToIdx(level);
                int main=balance.mainStat.SafeRandomAccess(index),opposite=balance.oppositeStat.SafeRandomAccess(index);
                text="배치 위치에 따른 속성 변화\n왼쪽 1~3열: "+Stat(balance.leftStatName,main)+" / "+Stat(balance.rightStatName,opposite)+"\n오른쪽 4열 이후: "+Stat(balance.leftStatName,opposite)+" / "+Stat(balance.rightStatName,main);
                if(live&&live.Item!=null)text+="\n현재 배치: "+(live.Item.XIdx<=2?"왼쪽":"오른쪽")+" · "+(live.IsEffectEnabled?"현재 속성값에 이미 반영":"효과 비활성");
                else text+="\n미장착: 두 배치의 효과를 표시하며 현재 능력치는 변경하지 않음";
                text+="\n한쪽 증가량만 더하지 않고 반대쪽 감소량도 각 속성 피해 계산에 반영";
                return true;
            }
            var exchange=source as Charm_FireIceWeapon;
            if(exchange)
            {
                text=ArtifactDamageProfiles.NativeStats(exchange,level)+"\n왼쪽 1~3열: 얼음 유물 검을 화염 속성 계산으로 전환\n오른쪽 4열 이후: 화염검을 냉기 속성 계산으로 전환";
                if(live&&live.Item!=null)text+="\n현재 배치: "+(live.Item.XIdx<=2?"왼쪽":"오른쪽")+" · "+(live.IsEffectEnabled?"전환 활성":"효과 비활성");
                text+="\n별도 추가타를 생성하지 않음 · 전환된 속성의 능력치로 해당 검 피해를 계산";
                return true;
            }
            var horn=source as Charm_KirinHorn;
            if(horn)
            {
                text=ArtifactDamageProfiles.NativeStats(horn,level)+"\n번개 속성으로 판정되는 공격의 치명타 확률 +"+horn.addCriticalByLevel.SafeRandomAccess(horn.LevelToIdx(level))+"%p\n자신과 동료의 해당 공격에 적용 · 일반/치명타 1회 피해량 자체는 바뀌지 않음";
                return true;
            }
            var burn=source as Charm_BurnWeaponDamage;
            if(burn)
            {
                int per=burn.addDamageByStackedBurn.SafeRandomAccess(burn.LevelToIdx(level));
                var active=live as Charm_BurnWeaponDamage;
                int current=active&&active.IsEffectEnabled?(int)BurnApplied.GetValue(active):0;
                text="자신이 부여한 디버프 중첩당 최종 무기 피해 "+per.ToString("+0;-0;0")+"%\n중첩 0개: +0% · 이 효과 자체의 중첩 상한 없음\n현재 장착 레벨에서 실제 적용 중: "+current.ToString("+0;-0;0")+"%\n원본은 화상만이 아니라 자신이 부여한 모든 디버프 중첩을 합산\n갱신 간격 "+burn.checkTimer.time.ToString("0.###")+"초 · 현재 적용값은 무기 피해에 이미 포함";
                // Read the native cached contribution instead of scanning every
                // creature/debuff again on each tooltip snapshot.
                return true;
            }
            var tablets=source as Charm_CriticalChanceIncreaseWithTablets;
            if(tablets)
            {
                int per=tablets.criticalBonusByLevel.SafeRandomAccess(tablets.LevelToIdx(level));
                int count=player.Inventory?player.Inventory.CurrentStoneTabletsCount:0;
                var active=live as Charm_CriticalChanceIncreaseWithTablets;
                int applied=active&&active.IsEffectEnabled?(int)TabletApplied.GetValue(active):0;
                text="석판 1개당 치명타 확률 +"+(per/100f).ToString("0.##")+"%p\n최소: 0개일 때 +0%p · 이 효과 자체의 개수 상한 없음\n현재 석판 "+count+"개 × 표시 레벨 효과: +"+(count*per/100f).ToString("0.##")+"%p\n현재 장착 레벨에서 실제 적용 중: +"+(applied/100f).ToString("0.##")+"%p\n치명타 발생 확률만 증가 · 일반/치명타 1회의 피해량은 동일";
                return true;
            }
            var cork=source as Charm_EnhancedPotionCork;
            if(cork)
            {
                bool condition=DamageStatPreview.ReadHp(player)/player.MaxHp<=cork.hpRatio/100f;
                text="HP "+cork.hpRatio+"% 이하에서 HP 물약 효과 "+cork.potionBonus.ToString("+0;-0;0")+"%\n최소 "+Math.Min(0,cork.potionBonus)+"% · 최대 "+Math.Max(0,cork.potionBonus)+"%\n현재 체력 조건: "+(condition?"충족":"미충족")+"\n직접 피해 증가 없음 · 물약 사용 후 바뀐 체력 조건은 별도로 적용";
                return true;
            }
            return false;
        }
        private static string DarkCloud(PlayerAvatar player)
        {
            string text="";
            var cloud=player.Inventory?player.Inventory.FindComboEffect("DARKCLOUD") as ComboEffect_DarkCloud:null;
            if(!cloud||!cloud.isEnabled){text+="\n현재 활성 먹구름 콤보 없음 · 추가 피해 발동 불가";return text;}
            float bolt=player.GetCustomStat(ECustomStat.LightningDamage);
            float ratio=KeywordDatabase.GetConstValue("darkCloudDamagePercent")/100f;
            float raw;
            if(player.GetCustomStatUnsafe("DARKCLOUDICE")>0)
            {
                float ice=player.GetCustomStat(ECustomStat.IceDamage);
                raw=Math.Max(bolt,ice)*ratio+Math.Min(bolt,ice)*(KeywordDatabase.GetConstValue("darkCloudDamagePercentIce")/100f);
            }
            else raw=bolt*ratio;
            float bonus=player.GetCustomStatUnsafe("DARKCLOUDDAMAGE")/100f;
            raw+=raw*bonus;
            int luck=UnityEngine.Mathf.Clamp(player.GetCustomStatUnsafe("DARKCLOUDLUCK"),0,100);
            var element=player.GetCustomStatUnsafe("DARKCLOUDICE")>0?EDamageElementalType.IceAndLightning:EDamageElementalType.Lightning;
            if(luck<100)text+="\n일반 방전 1회 / 치명타\n"+DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=raw,Element=element});
            if(luck>0)text+="\n강화 방전 1회 (확률 "+luck+"%) / 치명타\n"+DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=raw*2,Element=element});
            text+="\n현재 먹구름 "+cloud.darkCloud+" / "+cloud.MaxDarkCloud+" · 방전 시도 "+Math.Max(1,player.GetCustomStatUnsafe("DARKCLOUDMULTISHOT"))+"회 · 간격 0.133초\n먹구름 0개 또는 범위 내 유효 대상 없음: 추가 피해 0\n방전마다 무작위 대상을 선택하고 먹구름 소모 여부를 판정 · 모든 방전을 한 대상의 확정 피해로 합산하지 않음";
            return text;
        }
        internal static string Stat(ECustomStat stat,int value)
        {
            var entity=StatusDatabase.CreateStatusEntity(stat.ToString().ToUpperInvariant(),value);
            return entity!=null?KeywordDatabase.Convert(entity.ToString(true,false,false,UnityEngine.Color.white)):stat+" "+value.ToString("+0;-0;0");
        }
        private static string RowElement(string category)
        {
            switch(category)
            {
                case "EMBER":return "화염";
                case "GLACIER":return "냉기";
                case "MAGITECH":return "번개";
                case "STURDY":return "물리";
                default:return string.IsNullOrEmpty(category)?"미지정":category;
            }
        }
        private static string FlameGround(PlayerAvatar player,float radius)
        {
            if(!player.HasFlameGround()||player.GetCustomStat(ECustomStat.FlameGroundDisable)>0)
                return "현재 불길 생성 불가 · 불길 기능 활성화 필요\n기본 반경 "+radius.ToString("0.###")+" · 기본 지속 3틱";
            var ground=player.GetFlameGround();
            int ticks=(int)(3*ground.durationPercent/100f);
            return "불길 1틱 / 치명타\n"+DamageTooltip.Hits(player,new DamageTooltip.Hit{Raw=2,Element=EDamageElementalType.Fire,ResourceAmount=player.GetCustomStat(ECustomStat.FireDamage),ResourcePerUnit=.5f,ElementalEffect=true})+
                "\n원시 피해: 2 + 화염 능력치 × 0.5\n반경 "+(radius*ground.rangePercent/100f).ToString("0.###")+" · 0.25초 간격 · 지속 "+ticks+"틱\n겹친 장판의 피해를 중복 합산하지 않음 · 적이 장판에 머무르는 시간에 따라 적중 횟수 변동";
        }
    }
}
