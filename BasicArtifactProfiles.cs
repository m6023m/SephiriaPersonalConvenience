namespace SephiriaDicePreview
{
    internal static class BasicArtifactProfiles
    {
        internal static bool TryDescribe(Charm_Basic source,Charm_Basic live,int level,out string text)
        {
            text=null;int index=source.LevelToIdx(level);
            var mp=source as Charm_IncreaseMP;
            var hp=source as Charm_IncreaseHP;
            var attackSpeed=source as Charm_IncreaseAttackSpeed;
            var dash=source as Charm_DashAttackDamage;
            var move=source as Charm_IncreaseMoveSpeed;
            var regen=source as Charm_IncreaseMPRegen;
            var range=source as Charm_IncreaseMeleeAttackRange;
            var critical=source as Charm_IncreaseCriticalChance_NormalAttack;
            var all=source as Charm_IncreaseAllDamage;
            var followerSpeed=source as Charm_FollowerAttackSpeed;
            if(source is Charm_WarmStone)text="치명타 추가 피해 "+((Charm_WarmStone)source).criticalDamageByLevel.SafeRandomAccess(index).ToString("+0;-0;0")+"%p\n치명타 확률이 아닌 피해율 증가 · 다른 치명타 추가 피해와 합산";
            else if(source is Charm_Lantern)text="캐릭터를 따라가는 조명 장비 · 원본에 직접 피해·피해 능력치 변경 없음";
            else if(source is Charm_BrokenSapphire)
            {
                var sapphire=(Charm_BrokenSapphire)source;
                var active=live as Charm_BrokenSapphire;
                text=ArtifactDamageProfiles.NativeStats(sapphire,level)+"\n회피 장벽 재생 간격 "+sapphire.timeByLevel.SafeRandomAccess(index)+"초\n장벽은 절대 회피를 부여하며 직접 추가 피해 없음";
                if(active&&active.IsEffectEnabled)text+="\n현재 장벽: "+(active.NetworkenabledEvasionBarrier?"활성":"재생 대기");
            }
            else if(source is Charm_Lightning_Range)text=ArtifactDamageProfiles.NativeStats((Charm_Lightning_Range)source,level)+"\n별도 추가타 없음 · 범위 설명 배열만으로 피해 배율을 추가하지 않음";
            else if(source is Charm_FlameDash)text="돌진·이동 연출 변경 · 이 장비의 원본 돌진 콜백은 이펙트만 생성\n별도 불길 장판이나 피해를 생성하지 않음";
            else if(source is Charm_HomingMagic)text=ArtifactDamageProfiles.NativeStats((Charm_HomingMagic)source,level)+"\n유도 동작은 적중 여부에 영향 · 동일한 공격의 1회 적중 피해를 별도로 곱하지 않음";
            else if(source is Charm_QuickCast)text=ArtifactDamageProfiles.NativeStats((Charm_QuickCast)source,level)+"\n시전 동작 생략 효과 · 이 장비의 보조 시전 연출은 별도 공격을 생성하지 않음";
            else if(source is Charm_MagicianCoin)
            {
                var coin=(Charm_MagicianCoin)source;
                text=ArtifactDamageProfiles.NativeStats(coin,level)+"\n"+coin.semanticName.ToString()+" 드롭 보정 +"+(coin.semanticDropWeight*KeywordDatabase.GetConstValue("adaptiveItemDropUnitPercent"))+"%\n드롭 보정은 공격 피해에 적용하지 않음";
            }
            else if(source is Charm_CompanionChaos)
                text=ArtifactDamageProfiles.NativeStats((Charm_CompanionChaos)source,level)+"\n같은 행의 동료 소환 장비가 만드는 동료의 공격을 혼돈 속성으로 판정\n기본 공격력을 고정 배율로 올리지 않음 · 속성별 치명타·대상 조건에는 혼돈 판정 적용";
            else if(source is Charm_FrozenEgg)
            {
                var egg=(Charm_FrozenEgg)source;
                text=ArtifactDamageProfiles.NativeStats(egg,level)+"\n자신과 동료의 냉기 계열 공격 치명타 확률 +"+(egg.criticalChanceByLevel.SafeRandomAccess(index)*100f).ToString("0.###")+"%p (원본 계산값)\n원본은 설명 수치를 100으로 나누지 않고 확률에 더함\n일반·치명타 각각의 피해량은 변경하지 않음 · 혼합·혼돈 속성도 원본 속성 판정 사용";
            }
            else if(source is Charm_FairyJar)
            {
                var jar=(Charm_FairyJar)source;
                text="장식물 분류가 아닌 파괴 가능한 물체를 부술 때 "+jar.orbCreateChanceByLevel.SafeRandomAccess(index).ToString("0.###")+"% 확률로 HP 구슬 생성\n구슬 기본 회복 요청량 15 · 원본 실행값 기준\n직접 공격 피해 없음 · 구슬을 실제 획득하기 전 HP를 미리 회복하지 않음";
            }
            else if(source is Charm_Rapier)
            {
                var rapier=(Charm_Rapier)source;
                text=ResourceArtifactProfiles.Stat(ECustomStat.BasicAttackDamageBonus,rapier.basicDamageByLevel.SafeRandomAccess(index))+"\n"+
                    ResourceArtifactProfiles.Stat(ECustomStat.SpecialAttackDamageBonus,rapier.specialDamageByLevel.SafeRandomAccess(index))+"\n"+
                    ResourceArtifactProfiles.Stat(ECustomStat.DashAttackDamageBonus,rapier.dashDamageByLevel.SafeRandomAccess(index))+"\n각 공격 종류의 피해 증가 능력치에 합산 · 별도 추가타 없음";
            }
            else if(source is Charm_CrossbowDashAmmo)
            {
                var ammo=(Charm_CrossbowDashAmmo)source;
                text=ArtifactDamageProfiles.NativeStats(ammo,level)+"\n돌진 비용을 실제 지불한 경우 석궁 탄약 +"+ammo.ammoByLevel.SafeRandomAccess(index)+"\n무료 돌진에는 탄약 보충 없음 · 보충 자체가 화살을 발사하지 않음";
            }
            else if(source is Charm_DashAttackInvincible)
            {
                var wax=(Charm_DashAttackInvincible)source;
                text=ArtifactDamageProfiles.NativeStats(wax,level)+"\n준비된 상태에서 돌진 공격 동작 시작 시 무적 버프 부여\n기본 재사용 대기 "+wax.buffCooldown.time.ToString("0.###")+"초";
                if(wax.invincibleBuffPrefab)text+=" · 버프 기본 지속 "+wax.invincibleBuffPrefab.defaultDuration.ToString("0.###")+"초";
                text+="\n이 발동 자체는 추가 공격을 생성하지 않음";
            }
            else if(source is Charm_SubShield)
            {
                var shield=(Charm_SubShield)source;
                text="보조 방패 방어 각도 "+shield.defenseRange.ToString("0.###")+"° · 파괴 후 복구 시간 "+shield.restoreTimerByLevel.SafeRandomAccess(index).ToString("0.###")+"초\n가드 내구도 소모 = 최대(50, 받는 공격 수치 × 경직 수치 / 100)\n방패 자체는 반격 피해를 생성하지 않음";
                if(shield.subShieldPrefab)text+="\n최대 내구도 "+shield.subShieldPrefab.maxGuardPower.ToString("0.###")+" · 평상시 초당 내구도 회복 "+shield.subShieldPrefab.guardRestoreSpeed_Idle.ToString("0.###");
            }
            else if(source is Charm_KillMaxHP)
            {
                var growth=(Charm_KillMaxHP)source;
                var current=live as Charm_KillMaxHP;
                int cap=growth.maxHPLimitByLevel.SafeRandomAccess(index);
                text="평타·돌진 공격으로 처치 시 처치 횟수 누적\n최대 HP 비율 증가 = 최소(레벨별 상한, 처치 횟수 / 3의 정수 부분)\n최소 +0%p · 표시 레벨 상한 +"+cap+"%p";
                if(current)
                {
                    int applied=(int)HarmonyLib.AccessTools.Field(typeof(Charm_KillMaxHP),"added").GetValue(current);
                    text+="\n현재 누적 "+current.killCount+"회 · 저장된 증가량 +"+applied+"%p";
                }
                text+="\n현재 최대 HP는 원본이 적용한 값을 사용 · 앞으로 얻을 증가량을 현재 피해에 더하지 않음";
            }
            else if(source is Charm_MagmaBead)
                text=ArtifactDamageProfiles.NativeStats((Charm_MagmaBead)source,level)+"\n이 장비 클래스의 별도 발동 공격 없음 · 위 능력치 효과를 각 공격 계산에 반영";
            else if(source is Charm_StatusDebuff)
            {
                var debuff=(Charm_StatusDebuff)source;
                text=ArtifactDamageProfiles.NativeStats(debuff,level);
                if(debuff.debuffStatId=="PLANETDEBUFF")
                    text+="\n행성 적중 시 화염·냉기·번개 상태이상을 각각 "+KeywordDatabase.GetConstValue("planetDebuffPercent").ToString("0.###")+"% 기준으로 독립 판정\n상태이상 피해는 탄환의 직접 피해와 별도";
                else if(debuff.debuffStatId=="PLANETDEBUFF_ICE"||debuff.debuffStatId=="PLANETDEBUFF_LIGHTNING")
                    text+="\n이 버전의 원본 행성 공격은 장비가 부여하는 상태이상 조건을 사용하지 않음\n위 속성 능력치만 피해 계산에 반영 · 상태이상 추가 피해는 합산하지 않음";
                else text+="\n추가 상태이상 조건의 발동 경로 확인 필요 · 확정 피해에 합산하지 않음";
            }
            else if(source is Charm_PlanetComet)
                text=ArtifactDamageProfiles.NativeStats((Charm_PlanetComet)source,level)+"\n공전 이동 연출 · 원본 클래스에 별도 공격 생성 없음";
            else if(source is Charm_PlanetModule)
                text=ArtifactDamageProfiles.NativeStats((Charm_PlanetModule)source,level)+"\n인접한 8방향 슬롯의 행성 소환 장비 강화\n강화 행성은 해당 행성 상세보기의 강화 피해 적용 · 망원경 자체의 별도 추가타 없음";
            else if(source is Charm_CarrotCharm)
            {
                var carrot=(Charm_CarrotCharm)source;
                text="기본 최대 HP +"+carrot.addHpByLevel.SafeRandomAccess(index)+"\n기본 최대 MP +"+carrot.addMpByLevel.SafeRandomAccess(index)+"\n최종 최대 자원 배율은 이후 적용 · 자원 비례 공격은 현재 최종값 사용";
            }
            else if(source is Charm_ShortContract)
            {
                var contract=(Charm_ShortContract)source;
                text="동료 최대 HP 비율 +"+contract.hpPercentByLevel.SafeRandomAccess(index)+"%p\n현재 동료와 이후 합류하는 동료에 적용 · 동료 공격 피해를 직접 늘리지 않음";
            }
            else if(source is Charm_Shieldmate)
            {
                var mate=(Charm_Shieldmate)source;
                text=ArtifactDamageProfiles.NativeStats(mate,level)+"\n특수 공격 비용 감소 능력치 +"+mate.sweepCostReductionByLevel.SafeRandomAccess(index)+"%\n특수 공격의 원본 비용 계산에 합산 · 피해량 자체에 같은 비율을 곱하지 않음\nMP 소비량 비례 공격은 감소 후 소비량으로 계산";
            }
            else if(source is Charm_IncreaseMpRegenOnGuard)
            {
                var spirit=(Charm_IncreaseMpRegenOnGuard)source;
                text="검·방패의 가드 동작 중 "+ResourceArtifactProfiles.Stat(ECustomStat.MPRegen,spirit.addMapRegenByLevel.SafeRandomAccess(index))+"\n가드 중 이동 속도 배율 -"+(spirit.moveSpeed*100f).ToString("0.###")+"%p\n가드 해제 시 원복 · 정령 연출 자체에는 공격 판정 없음";
            }
            else if(source is Charm_SpeedRun)
            {
                var stairs=(Charm_SpeedRun)source;
                text="층 입장 후 "+stairs.buffDuration+"초 동안 이동 속도 +"+((int)stairs.speedByLevel.SafeRandomAccess(index))+"% · 공격 속도 +"+((int)stairs.attackSpeedByLevel.SafeRandomAccess(index))+"%\n효과가 남은 상태로 다음 층에 입장하면 시간 갱신\n공격 속도 피해 전환이 없다면 1타 피해는 동일 · 현재 적용된 능력치는 공격 계산에 포함";
            }
            else if(source is Charm_FireFly)
            {
                var fly=(Charm_FireFly)source;var active=live as Charm_FireFly;
                text="반딧불이 활성 시 "+ResourceArtifactProfiles.Stat(ECustomStat.LightningDamage,fly.lightningDamageByLevel.SafeRandomAccess(index))+"\n피격 시 보너스 해제 · 귀환 타이머 "+fly.fireFlyReturnTimer.time.ToString("0.###")+"초\n최소 +0 · 최대 +"+fly.lightningDamageByLevel.SafeRandomAccess(index)+" · 현재 "+(active&&active.onApplied?"활성":"비활성")+"\n반딧불이 연출 자체는 추가 공격을 생성하지 않음";
            }
            else if(source is Charm_LightningBread)
            {
                var bread=(Charm_LightningBread)source;
                text=ArtifactDamageProfiles.NativeStats(bread,level)+"\n다른 진영의 적 처치 시 "+bread.dropPercent+"% 확률로 먹구름 회복 아이템 생성\n먹구름의 빵 회복 보너스 +"+bread.cloudByLevel.SafeRandomAccess(index)+"\n드롭·회복 효과에 별도 직접 피해 없음";
            }
            else if(source is Charm_FlameSwordReturn)
                text=ArtifactDamageProfiles.NativeStats((Charm_FlameSwordReturn)source,level)+"\n떨어지는 화염검 회수 아이템이 플레이어 방향으로 돌아오도록 변경\n회수 아이템 이동에는 공격 판정 없음 · 공격형 화염검 회수 스킬과 별개";
            else if(source is Charm_SwordOfLight)
                text=ArtifactDamageProfiles.NativeStats((Charm_SwordOfLight)source,level)+"\n주위를 따라가는 검은 장비 연출 · 연출 오브젝트가 별도 공격을 생성하지 않음";
            else if(source is Charm_IceWings)
                text=ArtifactDamageProfiles.NativeStats((Charm_IceWings)source,level)+"\n날개는 장비 연출 · 연결 버프 필드만으로 버프나 추가 공격이 적용되지는 않음";
            else if(source is Charm_ThunderousSteps)
            {
                var steps=(Charm_ThunderousSteps)source;
                text="전방에 디버프 부여 영역 생성 · 기본 재사용 "+steps.coolDownTimer.time.ToString("0.###")+"초\n같은 사용에서 대상당 1회 부여 · 장면의 번개 개수를 피해 횟수로 합산하지 않음\n원본 실행 경로에 직접 피해 판정 없음";
                if(steps.debuffPrefab)text+="\n부여 디버프: "+steps.debuffPrefab.ID+" · 기본 지속 "+steps.debuffPrefab.defaultDuration.ToString("0.###")+"초";
            }
            else if(source is Charm_KatanaEnhancedDashAttackActivator)
            {
                var scroll=(Charm_KatanaEnhancedDashAttackActivator)source;
                text="도 강화 돌진 준비 시간 "+scroll.enhancedAttackTimer.time.ToString("0.###")+"초\n준비 후 첫 돌진 투사체 피해 +"+scroll.damagePercentByLevel.SafeRandomAccess(index).ToString("0.###")+"%\n투사체 추가 피해율에 합산 · 첫 생성 시 충전 소비\n강화 공격의 속성은 현재 원본 무기 상태를 사용 · 일반 돌진 전체에 반복 적용하지 않음";
            }
            else if(source is Charm_PointedBat)
            {
                var bat=(Charm_PointedBat)source;
                text=ArtifactDamageProfiles.NativeStats(bat,level)+"\n공격 판정 성공 시 "+bat.chance.ToString("0.###")+"% 확률로 해당 피해 "+bat.damageDecreaseRatio.ToString("0.###")+"% 감소\n발동 시 원래 피해 × "+(1-bat.damageDecreaseRatio/100f).ToString("0.###")+" · 추가 피해 생성 없음\n확률 발동 최소·최대를 공격 계산에 따로 표시 · 동료에게 리더 효과로 전달되지 않음";
            }
            else if(source is Charm_WarmGlove)
            {
                var glove=(Charm_WarmGlove)source;
                text="동상 대상 냉기 계열 공격 피해 +"+glove.damageBonusByLevel.SafeRandomAccess(index)+"%\n자신과 동료의 해당 공격에 적용 · 혼합 속성·혼돈은 원본 속성 판정 사용\n대상에게 동상 디버프가 없으면 증가량 0 · 치명타 여부와 별개";
            }
            else if(source is Charm_SweepRange)
            {
                var sweep=(Charm_SweepRange)source;
                text=ResourceArtifactProfiles.Stat(ECustomStat.SpecialAttackRange,sweep.sweepRangeByLevel.SafeRandomAccess(index))+"\n"+ResourceArtifactProfiles.Stat(ECustomStat.SpecialAttackDamageBonus,sweep.sweepDamageByLevel.SafeRandomAccess(index))+"\n특수 공격 능력치에 합산 · 별도 추가타 없음";
                if(sweep.hasSweepQuest)text+="\n대검 특수 공격 처치 "+sweep.sweepQuestKillCount+"회 달성 시 장비 변환"+(sweep.reward?": "+sweep.reward.Name:"")+" · 변환 전에는 보상 장비 효과를 적용하지 않음";
            }
            else if(source is Charm_IncreaseGoldDropRate)
            {
                var gold=(Charm_IncreaseGoldDropRate)source;
                text=ResourceArtifactProfiles.Stat(ECustomStat.MoneyDrop,gold.goldDropBonusByLevel.SafeRandomAccess(index))+"\n골드 드롭 능력치에 합산 · 보유 골드를 직접 늘리지 않음\n골드 비례 피해는 현재 보유량을 사용";
            }
            else if(source is Charm_LightningPouch)
            {
                var pouch=(Charm_LightningPouch)source;
                text=ArtifactDamageProfiles.NativeStats(pouch,level)+"\n돌진 공격 적중 시 먹구름 "+pouch.cloudByLevel.SafeRandomAccess(index)+"개 회복\n회복 간격 "+pouch.cooldownByLevel.SafeRandomAccess(index).ToString("0.###")+"초 · 먹구름 콤보 활성 필요\n회복 자체에는 피해 없음 · 이후 실제 사용한 구름의 공격만 별도로 계산";
            }
            else if(source is Charm_FlameSwordAuto)
            {
                var furnace=(Charm_FlameSwordAuto)source;
                text=ArtifactDamageProfiles.NativeStats(furnace,level)+"\n화염검 "+furnace.restoreFlameSwordTimeByLevel.SafeRandomAccess(index).ToString("0.###")+"초마다 1개 회복\n화염검 효과 활성·보유 상한 미만일 때 회복 시간이 진행\n회복 자체에는 피해 없음 · 검을 발사하거나 소비하는 공격에서 실제 보유량 반영";
            }
            else if(source is Charm_SnowNeckless)
            {
                var snow=(Charm_SnowNeckless)source;
                text=ArtifactDamageProfiles.NativeStats(snow,level)+"\n새로 부여한 동상 증폭값을 "+snow.moveSpeedByLevel.SafeRandomAccess(index)+"로 설정\n원본 동상에서 이 값은 이동 속도 감소에만 사용 · 동상 주기 피해에 곱하지 않음";
            }
            else if(all)text=ArtifactDamageProfiles.NativeStats(all,level);
            else if(followerSpeed)text=ArtifactDamageProfiles.NativeStats(followerSpeed,level)+"\n동료 공격 속도 "+followerSpeed.attackSpeedPercentByLevel.SafeRandomAccess(index).ToString("+0;-0;0")+"%\n현재 동료와 이후 합류한 동료에 적용 · 공격 속도를 피해로 전환하는 효과가 없으면 1타 피해는 동일";
            else if(mp)text="기본 최대 MP "+mp.addMPByLevel.SafeRandomAccess(index).ToString("+0;-0;0")+"\n최종 최대 MP 배율은 이후 적용 · 최대 MP를 참조하는 공격은 현재 최종 최대 MP로 계산";
            else if(hp)text="기본 최대 HP "+hp.addHPByLevel.SafeRandomAccess(index).ToString("+0;-0;0")+"\n체력 비례·체력 조건 효과는 현재 HP/최대 HP를 기준으로 계산 · 별도 직접 타격 없음";
            else if(attackSpeed)text=ResourceArtifactProfiles.Stat(ECustomStat.AttackSpeed,attackSpeed.atkSpeedByLevel.SafeRandomAccess(index))+"\n공격 간격에 영향 · 공격 속도를 피해로 전환하는 무기에서는 해당 전환식에 반영";
            else if(dash)text=ResourceArtifactProfiles.Stat(ECustomStat.DashAttackDamageBonus,dash.bonusByLevel.SafeRandomAccess(index))+"\n돌진 공격의 피해 증가 능력치에 합산 · 평타·특수 공격 배율에 임의 적용하지 않음";
            else if(regen)text=ResourceArtifactProfiles.Stat(ECustomStat.MPRegen,regen.addMPRegenByLevel.SafeRandomAccess(index))+"\nMP 회복 능력치 효과 · 현재 남은 MP와 최대 MP를 직접 늘리는 효과는 아님";
            else if(range)text=ResourceArtifactProfiles.Stat(ECustomStat.WeaponRange,range.rangeByLevel.SafeRandomAccess(index))+"\n무기 공격 범위 능력치에 합산 · 범위를 피해로 전환하는 별도 효과가 없으면 1타 피해는 동일";
            else if(move)
            {
                text="이동 속도 배율 "+(move.moveSpeedByLevel.SafeRandomAccess(index)*100f).ToString("+0.###;-0.###;0")+"%p\n다른 이동 속도 배율과 합산 · 직접 추가타 없음";
                if(move.restrictRun)text+="\n장착 중 달리기·돌진 제한";
                if(move.restrictFastMove)text+="\n장착 중 빠른 이동 제한";
            }
            else if(critical)text="직접 공격 치명타 확률 "+critical.criticalBonusPercentByLevel.SafeRandomAccess(index).ToString("+0.###;-0.###;0")+"%p\n평타만이 아니라 원본 DirectAttack 판정에 적용 · 일반/치명타 각각의 1회 피해량은 동일";
            else return false;
            text+="\n표시 레벨의 장비 효과 · "+(live&&live.IsEffectEnabled?"현재 장착 효과는 능력치 또는 공격 판정에 이미 반영":"현재 효과 비활성·미장착");
            return true;
        }
    }
}
