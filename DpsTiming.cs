using System;
using System.Collections.Generic;

namespace SephiriaDicePreview
{
    internal static class DpsTiming
    {
        internal sealed class Attack
        {
            internal int Kind,Index;
            internal double Time;
        }
        internal sealed class Cycle
        {
            internal string Unavailable,Condition;
            internal bool NoDamage;
            internal double Seconds;
            internal double AttackEnd=-1;
            internal int TempestStacks;
            internal readonly List<Attack> Attacks=new List<Attack>();
            internal readonly List<double> Begins=new List<double>();
        }
        internal static Cycle MovementDash(PlayerAvatar player,bool paid)
        {
            var result=new Cycle();var dash=player.CurrentDashModule;
            if(!dash||player.GetCustomStatUnsafe("DASHDISABLE")>0)
            {result.Unavailable="이동 돌진 사용 불가";return result;}
            var fast=dash as CharacterDash_FastMove;var blink=dash as CharacterDash_Blink;
            if(fast)result.Seconds=fast.dashingTimer.time;
            else if(blink)result.Seconds=blink.dashDelayTimer.time;
            else {result.Unavailable="이동 돌진 시간 데이터 없음";return result;}
            if(paid||player.GetCustomStatUnsafe("INFINITYDASH")<=0)
            {
                double recovery=1+player.GetCustomStat(ECustomStat.DashRecovery)/100d;
                if(recovery<=0||dash.MaxDashCount<=0){result.Unavailable="돌진 비용을 지속해서 회복할 수 없음";return result;}
                result.Seconds=Math.Max(result.Seconds,dash.cooldownTimer.time/recovery);
            }
            if(!DpsNumbers.Finite(result.Seconds)||result.Seconds<=0)result.Unavailable="이동 돌진 시간 데이터 없음";
            return result;
        }
        internal static Cycle Motions(WeaponSimple weapon,PlayerAvatar player,int kind,params string[] states)
        {
            var result=new Cycle();
            var controller=player.GetComponent<WeaponControllerSimple>();
            var animator=controller?controller.animator:null;
            if(!animator||!animator.runtimeAnimatorController||animator.runtimeAnimatorController.name!="AnimationWrapper")
            {result.Unavailable="애니메이션 시간 데이터 없음";return result;}
            double basic=DpsAnimationTiming.BasicSpeed(weapon,player);
            double special=(100+player.GetCustomStat(ECustomStat.SpecialAttackSpeed)+(weapon.specialAttackIsRelatedToAttackSpeed?player.GetCustomStat(ECustomStat.AttackSpeed):0))/100d;
            foreach(string state in states)
            {
                DpsMotions.Motion motion;
                if(!DpsMotions.States.TryGetValue(weapon.animatorLayer+":"+state,out motion))
                {result.Unavailable="동작 시간 데이터 없음: "+state;return result;}
                double speed;
                if(!DpsAnimationTiming.Scale(motion,animator.speed,kind==2?special:basic,out speed))
                {result.Unavailable="공격 동작이 진행되지 않음";return result;}
                foreach(var fire in motion.Fires)result.Attacks.Add(new Attack{Kind=fire.Kind,Index=fire.Index,Time=result.Seconds+fire.Time/speed});
                foreach(double begin in motion.Begins)result.Begins.Add(result.Seconds+begin/speed);
                foreach(double end in motion.Ends)result.AttackEnd=result.Seconds+end/speed;
                result.Seconds+=motion.Seconds/speed;
            }
            if(kind==1)
            {
                var dash=player.CurrentDashModule;
                if(!dash){result.Unavailable="돌진 회복 시간 데이터 없음";return result;}
                if(player.GetCustomStatUnsafe("INFINITYDASH")<=0)
                {
                    double recovery=(100+player.GetCustomStat(ECustomStat.DashRecovery))/100d;
                    if(recovery<=0||dash.MaxDashCount<=0){result.Unavailable="돌진을 지속해서 사용할 수 없음";return result;}
                    result.Seconds=Math.Max(result.Seconds,dash.cooldownTimer.time/recovery);
                }
            }
            return result;
        }
        private static string[] Series(string prefix,int count,params string[] tail)
        {
            var result=new string[count+tail.Length];
            for(int i=0;i<count;i++)result[i]=prefix+(i+1);
            Array.Copy(tail,0,result,count,tail.Length);return result;
        }
        internal static Cycle Basic(WeaponSimple weapon,PlayerAvatar player)
        {
            int move=weapon.attackMoveSet;
            var crossbow=weapon as WeaponSimple_Crossbow;
            if(crossbow)return CrossbowBasic(crossbow,player);
            var dagger=weapon as WeaponSimple_Dagger;
            if(dagger)return Motions(weapon,player,0,dagger.newBasicAttack?Series("Dagger_New_AttackNew",3):move==2?Series("Dagger_AttackThrow",4):Series("Dagger_New_Attack",5));
            var shield=weapon as WeaponSimple_SwordAndShield;
            if(shield&&move==0)return Motions(weapon,player,0,Series("SwordAndShield_New_Attack",3,"SwordAndShield_New_WaitForAttackEnd"));
            if(shield&&move==1)return Motions(weapon,player,0,Series("SwordAndShield_AttackCarrot",4,"SwordAndShield_AttackCarrot4_End"));
            var sword=weapon as WeaponSimple_GreatSword;
            if(sword)
            {
                if(WeaponBuffPreview.Transformed(sword))return Motions(weapon,player,0,"GreatSword_AttackSkel1","GreatSword_AttackSkel2");
                if(sword.tripleCombo)return Motions(weapon,player,0,"GreatSword_Attack1","GreatSword_Attack2","GreatSword_Attack3_Triple","GreatSword_Attack3_TripleFinal");
                return Motions(weapon,player,0,Series(move==1?"GreatSword_AttackIce":move==3?"GreatSword_AttackLaser":move==4?"GreatSword_Twin_Attack":move==10?"GreatSword_AttackRedWeasel":"GreatSword_Attack",move==10?2:3));
            }
            if(weapon is WeaponSimple_Katana)
            {
                if(move==1)return KatanaSheath((WeaponSimple_Katana)weapon,player);
                if(WeaponBuffPreview.EclipseBuff(weapon)||move==3)return Motions(weapon,player,0,Series("Katana_BigFireAttack",5,"Katana_BigFireAttack5_End"));
                if(move==2)return Motions(weapon,player,0,Series("Katana_Attack",4,"Katana_Attack_End"));
                if(move==4)return Motions(weapon,player,0,Series("Katana_Attack",2));
                if(move==0)return Motions(weapon,player,0,Series("Katana_Attack",3,"Katana_Attack3_End"));
            }
            var staff=weapon as WeaponSimple_QuartterStaff;
            if(staff)
            {
                if(staff.isNormalAttackRolling)
                {
                    double speed=1+player.GetCustomStatUnsafe("ATTACKSPEED")/100d;
                    if(speed<=0||staff.attackWeightPerSwing<=0)return new Cycle{Unavailable="회전 공격이 진행되지 않음"};
                    var rolling=new Cycle{Seconds=staff.attackWeightPerSwing/speed,Condition="회전 활성 후 지속 공격 기준"};
                    rolling.Attacks.Add(new Attack{Kind=0,Index=0});rolling.Begins.Add(0);return rolling;
                }
                if(staff.canCrystalExplosion)return Motions(weapon,player,0,"Staff_AttackCrystalExplosion");
                return Motions(weapon,player,0,Series(move==1?"Spear_Attack":move==2?"ThrowingSpear_Attack":"Staff_Attack",move==2?3:4));
            }
            return new Cycle{Unavailable="이 연계의 시간 계산 준비 중"};
        }
        private static Cycle CrossbowBasic(WeaponSimple_Crossbow weapon,PlayerAvatar player)
        {
            var result=new Cycle();
            double speed=1;
            if(player.GetCustomStatUnsafe("GRENADEATTACK")<=0)
            {
                int fixedSpeed=player.GetCustomStatUnsafe("FIXEDATTACKSPEED");
                if(fixedSpeed>0)speed=fixedSpeed/100d;
                else
                {
                    speed+=player.GetCustomStatUnsafe("ATTACKSPEED")/100d;
                    if(player.GetCustomStatUnsafe("CROSSBOWMG")>0){speed+=.6;result.Condition="연속 사격 가속이 최대인 상태";}
                    if(player.GetCustomStatUnsafe("MOVESPEEDTOATTACKSPEED")>0)speed+=Math.Max(0,player.moveSpeedMultiplier-1);
                    speed-=Math.Max(0,player.GetCustomStatUnsafe("ICEBUFF_DECREASEATTACKSPEED"))/100d;
                }
            }
            if(speed<=0){result.Unavailable="발사 간격이 진행되지 않음";return result;}
            double interval=weapon.fireIntervalTimer.time/speed;
            bool ice=player.GetCustomStatUnsafe("ICECROSSBOWBUFF")>0;
            if(ice&&weapon.infiniteAmmoDuringBuff)
            {
                result.Seconds=interval;result.Begins.Add(0);
                result.Attacks.Add(new Attack{Kind=0,Index=0,Time=0});return result;
            }
            int capacity=weapon.defaultMagazineCapacity+player.GetCustomStatUnsafe("CROSSBOWAMMO");
            int magazines=weapon.defaultMagazineCount+player.GetCustomStatUnsafe("CROSSBOWADDITIONALMAGAZINE");
            int fixedAmmo=player.GetCustomStatUnsafe("FIXEDAMMO");
            long rounds=fixedAmmo>0?fixedAmmo:(long)capacity*Math.Max(1,magazines);
            if(capacity<=0||rounds<=0||rounds>4096){result.Unavailable="탄창 크기를 계산할 수 없음";return result;}
            double reloadSpeed=1+player.GetCustomStatUnsafe("CROSSBOWRELOADSPEED")/100d;
            if(ice&&player.GetCustomStatUnsafe("ICECROSSBOWFROSTRELIC")>0)reloadSpeed+=player.GetCustomStatUnsafe("CHARGINGCHARMBONUS")/100d;
            if(reloadSpeed<=0){result.Unavailable="재장전이 진행되지 않음";return result;}
            double reload=weapon.reloadTime/reloadSpeed;
            // The fire timer continues to advance during reload. Only the longer
            // of the final interval and reload belongs after the last shot.
            result.Seconds=(rounds-1)*interval+Math.Max(interval,reload);
            for(int i=0;i<rounds;i++)
            {
                int remaining=fixedAmmo>0?fixedAmmo-i:capacity-i%capacity;
                result.Begins.Add(i*interval);
                result.Attacks.Add(new Attack{Kind=0,Index=remaining%capacity==1?1:0,Time=i*interval});
            }
            return result;
        }
        internal static Cycle Dash(WeaponSimple weapon,PlayerAvatar player)
        {
            if(weapon is WeaponSimple_SwordAndShield)return Motions(weapon,player,1,"SwordAndShield_New_DashAttack");
            var dagger=weapon as WeaponSimple_Dagger;
            if(dagger)return Motions(weapon,player,1,dagger.enhancedDashAttack?"Dagger_New_DashAttack_Enhanced":"Dagger_New_DashAttack");
            var sword=weapon as WeaponSimple_GreatSword;
            if(sword)return sword.blockDashAttack?new Cycle{Unavailable="돌진 공격 없음"}:Motions(weapon,player,1,weapon.attackMoveSet==4?"DashAttack_Twin_DashAttack":"GreatSword_New_DashAttack");
            if(weapon is WeaponSimple_Katana)return Motions(weapon,player,1,"Katana_DashAttack");
            if(weapon is WeaponSimple_Crossbow)return Motions(weapon,player,1,"Crossbow_DashAttack");
            if(weapon is WeaponSimple_QuartterStaff)return Motions(weapon,player,1,weapon.attackMoveSet==1?"Spear_DashAttack":weapon.attackMoveSet==2?"ThrowingSpear_DashAttack":"Staff_DashAttack");
            return new Cycle{Unavailable="돌진 동작 시간 연결 필요"};
        }
        internal static Cycle Special(WeaponSimple weapon,PlayerAvatar player)
        {
            var cycle=SpecialCore(weapon,player);
            var shield=weapon as WeaponSimple_SwordAndShield;
            var tempest=shield?shield.overrideSweepAddon as WeaponAddonCommon_Tempest:null;
            if(!tempest||cycle.Unavailable!=null)return cycle;
            if(tempest.maxTempestStack<=0||!DpsNumbers.Finite(tempest.tempestStackAddTimer.time)||tempest.tempestStackAddTimer.time<=0)
            {cycle.Unavailable="폭풍 중첩의 자연 회복 시간 데이터 없음";return cycle;}
            if(cycle.Attacks.Count!=1||(cycle.Attacks[0].Kind!=2&&cycle.Attacks[0].Kind!=4))
            {cycle.Unavailable="폭풍 중첩 소비의 다중 공격 연결 필요";return cycle;}
            double interval=tempest.tempestStackAddTimer.time;
            bool full=DamageTooltip.CurrentCapture!=null&&DamageTooltip.CurrentCapture.FullConditions;
            cycle.TempestStacks=full?tempest.maxTempestStack:
                (int)Math.Min(tempest.maxTempestStack,Math.Max(1,Math.Ceiling(cycle.Seconds/interval)));
            // Stack production continues during the attack and is not reset
            // when stacks are spent. Do not add action + refill independently.
            DelayCycle(cycle,Math.Max(0,cycle.TempestStacks*interval-cycle.Seconds));
            return cycle;
        }
        internal static bool DaggerPrimaryAvailable(WeaponSimple_Dagger dagger,PlayerAvatar player)
        {
            return dagger&&!DaggerHas<WeaponAddonDagger_CritFury>(dagger)&&player.GetCustomStatUnsafe("EVASIONFURY")<=0;
        }
        internal static bool DaggerFuryAvailable(WeaponSimple_Dagger dagger,PlayerAvatar player)
        {
            if(!dagger||DaggerHas<WeaponAddonDagger_BasicAttackFinal>(dagger))return false;
            if(dagger.currentFury>0||DaggerHas<WeaponAddonDagger_CritFury>(dagger)||DaggerHas<WeaponAddonDagger_EvadeFury>(dagger)||player.GetCustomStatUnsafe("EVASIONFURY")>0)return true;
            // A successful native parry supplies Fury. Throw Dagger replaces that
            // parry, so it needs one of the alternative Fury sources above.
            return DaggerPrimaryAvailable(dagger,player)&&!dagger.throwDagger;
        }
        internal static Cycle DaggerPrimary(WeaponSimple_Dagger dagger,PlayerAvatar player)
        {
            if(!DaggerPrimaryAvailable(dagger,player))return new Cycle{Unavailable="패리 동작 없음",NoDamage=true};
            return Motions(dagger,player,2,dagger.throwDagger?"Dagger_ThrowDagger":DaggerHas<WeaponAddonDagger_EasyParry>(dagger)?"Dagger_Parry_Easy":DaggerHas<WeaponAddonDagger_ParryHeal>(dagger)?"Dagger_Parry_Heal":DaggerHas<WeaponAddonDagger_WideParry>(dagger)?"Dagger_Parry_Wide":"Dagger_Parry");
        }
        internal static Cycle DaggerFury(WeaponSimple_Dagger dagger,PlayerAvatar player)
        {
            if(!DaggerFuryAvailable(dagger,player))return new Cycle{Unavailable="퓨리 동작 없음",NoDamage=true};
            var fury=Motions(dagger,player,2,dagger.lightningFury?"Dagger_Fury_Lightning":"Dagger_Fury_1");
            fury.Condition=DaggerHas<WeaponAddonDagger_CritFury>(dagger)?"치명타로 충전된 퓨리를 계속 공급하는 상태 기준":
                player.GetCustomStatUnsafe("EVASIONFURY")>0||DaggerHas<WeaponAddonDagger_EvadeFury>(dagger)?"회피로 충전된 퓨리를 계속 공급하는 상태 기준":
                "패리 성공 후 충전된 퓨리를 계속 공급하는 상태 기준";
            return fury;
        }
        private static bool DaggerHas<T>(WeaponSimple_Dagger dagger) where T:WeaponAddon
        {
            if(typeof(T)==typeof(WeaponAddonDagger_BasicAttackFinal)&&dagger.basicAttackFinal)return true;
            if(typeof(T)==typeof(WeaponAddonDagger_CritFury)&&dagger.critFury)return true;
            if(typeof(T)==typeof(WeaponAddonDagger_EvadeFury)&&dagger.evadeFury)return true;
            if(typeof(T)==typeof(WeaponAddonDagger_EasyParry)&&dagger.easyParry)return true;
            if(typeof(T)==typeof(WeaponAddonDagger_ParryHeal)&&dagger.healParry)return true;
            if(typeof(T)==typeof(WeaponAddonDagger_WideParry)&&dagger.wideParry)return true;
            if(dagger.addons!=null)foreach(var addon in dagger.addons)if(addon is T)return true;
            return false;
        }
        private static Cycle SpecialCore(WeaponSimple weapon,PlayerAvatar player)
        {
            var katana=weapon as WeaponSimple_Katana;
            if(katana)
            {
                if(katana.attackMoveSet==1)return new Cycle{Unavailable="자동 납도 공격 · 평타 DPS에 표시",NoDamage=true};
                if(katana.sheathActionType==WeaponSimple_Katana.ESheathActionType.Eclipse)return new Cycle{Unavailable="무기 강화 동작 · 강화 후 평타·돌진 DPS에 반영",NoDamage=true};
                if(katana.sheathActionType==WeaponSimple_Katana.ESheathActionType.CloudSlash)return Motions(weapon,player,2,"Katana_Attack_DarkCloud");
                if(katana.sheathActionType==WeaponSimple_Katana.ESheathActionType.Deflecting)return new Cycle{Unavailable="방어 동작 · 반격은 적의 공격 빈도에 따라 변동",NoDamage=true};
                if(katana.useQuickDraw)
                {
                    // StartQuickDraw fires a fully manual BASIC hit before its
                    // drawing animation. Use basic attack speed/bonuses despite
                    // presenting the input rotation in the special-attack row.
                    var sheath=Motions(weapon,player,0,"Katana_Sheathing");
                    var drawing=Motions(weapon,player,0,"Katana_Drawing");
                    if(sheath.Unavailable!=null)return sheath;
                    if(drawing.Unavailable!=null)return drawing;
                    var result=new Cycle{Seconds=sheath.Seconds+drawing.Seconds,
                        Condition="발도 동작 완료 후 다시 납도 · 피해 보정은 평타 기준 · 현재 게이지 상태 유지"};
                    result.Attacks.Add(new Attack{Kind=3,Index=weapon.finalComboIdx,Time=sheath.Seconds});
                    result.Begins.Add(sheath.Seconds);return result;
                }
                return KatanaSheath(katana,player);
            }
            var sword=weapon as WeaponSimple_GreatSword;
            if(sword)
            {
                if(sword.specialAttackToTransform)return new Cycle{Unavailable="변신 동작 · 변신 후 평타 DPS에 반영",NoDamage=true};
                string[] motions;
                if(weapon.attackMoveSet==4)motions=new[]{"GreatSword_Twin_ChargingAttack"};
                else if(sword.moneyWhirlwind||player.GetCustomStatUnsafe("WOUNDEXPLOSION")>0)motions=new[]{"GreatSword_WoundExplosion"};
                else if(weapon.attackMoveSet==3)motions=new[]{"GreatSword_SweepLaser"};
                else if(sword.outsideBonus&&sword.doubleWhirlwind)motions=new[]{"GreatSword_New_SweepDouble","GreatSword_New_SweepDouble2"};
                else motions=new[]{sword.outsideBonus?"GreatSword_New_SweepOutside":"GreatSword_New_Sweep"};
                var result=Motions(weapon,player,2,motions);
                double rate=sword.longCharge?.6:sword.superQuickSweep?100:sword.quickSweep?1.5:1;
                // All three native charge states send SweepChargeStart at t=0.
                // Their 2-second clips are a charging loop, not a required wait.
                double charge=Math.Max(0,sword.sweepTimer.time)/rate;
                result.Seconds+=charge;
                foreach(var attack in result.Attacks)attack.Time+=charge;
                result.Condition="매회 완전 충전 후 공격 기준";return result;
            }
            var staff=weapon as WeaponSimple_QuartterStaff;
            if(staff)
            {
                int parry=0;
                if(staff.overrideSpecialAttackAddon)
                {
                    var change=staff.overrideSpecialAttackAddon as WeaponAddonCommon_ChangeWeaponAction;
                    var more=staff.overrideSpecialAttackAddon as WeaponAddonCommon_ChangeWeaponAction_More;
                    if(!change&&!more)return new Cycle{Unavailable="변경 효과의 특수 공격 애니메이션 설정 필요"};
                    var controller=player.GetComponent<WeaponControllerSimple>();
                    if(controller&&controller.animator)parry=controller.animator.GetInteger(AnimHashContainer.Instance.ParryNumHash);
                    if(change&&change.animationIntParamName=="ParryNum")parry=change.animationIntParamValue;
                }
                // Native layer-7 entry selectors use ParryNum. Throwing spear
                // enters a different selector from ordinary staff/spear.
                string motion=staff.attackMoveSet==2?(parry==50?"ThrowingSpear_SpecialAttack_Big":"ThrowingSpear_SpecialAttack"):
                    parry==1?"Staff_SpecialAttack_FlameSpear":parry==2?"Staff_SpecialAttack_CrystalExplosion":"Staff_SpecialAttack";
                return Motions(weapon,player,2,motion);
            }
            var dagger=weapon as WeaponSimple_Dagger;
            if(dagger)
            {
                if(dagger.currentFury>0&&!dagger.basicAttackFinal)return DaggerFury(dagger,player);
                return DaggerPrimary(dagger,player);
            }
            var shield=weapon as WeaponSimple_SwordAndShield;
            if(shield)
            {
                if(!shield.isSweepAvailable)return new Cycle{Unavailable="특수 공격 없음",NoDamage=true};
                if(shield.overrideSweepAddon)
                {
                    int? selected=WeaponAdditionalDamageProfiles.ShieldAnimationOverride(shield);
                    // Native parsing uses the last valid ANIMATION value. The
                    // selected clip supplies its own fire kind/index, including
                    // Strike's basic hit despite being a special-attack input.
                    if(selected.HasValue)
                    {
                        string motion=ShieldSweepMotion(selected.Value);
                        return motion==null?new Cycle{Unavailable="특수 공격 애니메이션 선택 값 없음: "+selected.Value}:ShieldRecharge(Motions(weapon,player,2,motion),shield,player);
                    }
                }
                if(player.GetCustomStatUnsafe("DARKCLOUDSWEEP")>0)
                {
                    double luck=Math.Max(0,Math.Min(100,player.GetCustomStatUnsafe("DARKCLOUDLUCK")));
                    var normal=Motions(weapon,player,2,"SwordAndShield_SweepLightning");
                    var enhanced=Motions(weapon,player,2,"SwordAndShield_SweepLightningEx");
                    if(luck<=0)return ShieldRecharge(normal,shield,player);
                    if(luck>=100)return ShieldRecharge(enhanced,shield,player);
                    if(normal.Unavailable!=null)return normal;
                    if(enhanced.Unavailable!=null)return enhanced;
                    // Both native clips have one fire at the same time and the
                    // same duration. Merge damage alternatives, not two casts.
                    if(normal.Seconds!=enhanced.Seconds||normal.Attacks.Count!=1||enhanced.Attacks.Count!=1||
                        normal.Attacks[0].Time!=enhanced.Attacks[0].Time||normal.AttackEnd!=enhanced.AttackEnd)
                        return new Cycle{Unavailable="확률 번개 공격의 동작 시간 데이터가 다름"};
                    normal.Attacks[0].Kind=4;
                    return ShieldRecharge(normal,shield,player);
                }
                if(shield.isFlameEaterHaetaeEnabled)return Motions(weapon,player,2,"SwordAndShield_Strike");
                bool guarded=WeaponAdditionalDamageProfiles.ShieldGuardReady(shield);
                bool charged=shield.chargedSweep&&!shield.shieldThrowing&&DamageTooltip.CurrentCapture!=null&&DamageTooltip.CurrentCapture.FullConditions;
                string sweep=shield.shieldThrowing?"SwordAndShield_Sweep4(Throw)":
                    charged?(guarded?"SwordAndShield_Sweep2(Guard&Charge)":"SwordAndShield_Sweep3(Charge)"):
                    guarded?"SwordAndShield_Sweep1(Guard)":shield.guardSweep?"SwordAndShield_Sweep5(Fast)":"SwordAndShield_Sweep0";
                var sweepCycle=ShieldRecharge(Motions(weapon,player,2,sweep),shield,player);
                if(charged&&sweepCycle.Unavailable==null)
                {
                    if(!shield.isGuardAvailable||!DpsNumbers.Finite(shield.chargeTime)||shield.chargeTime<0)
                        return new Cycle{Unavailable="가드 유지 충전 시간 데이터 없음"};
                    // Old charge resets at attack INPUT, then keeps advancing
                    // while GuardHash is held, including the sweep animation.
                    // StopGuard disables blocking, not the held animator bool.
                    // This is a sustained cycle: the first startup charge is
                    // not added to every cast, nor scaled by attack speed.
                    DelayCycle(sweepCycle,Math.Max(0,shield.chargeTime-sweepCycle.Seconds));
                }
                return sweepCycle;
            }
            var crossbow=weapon as WeaponSimple_Crossbow;
            if(crossbow)
            {
                if(crossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.FireBullet)
                    return Motions(weapon,player,2,crossbow.useMiniDrone?"Crossbow_SpecialAttack_Drone":"Crossbow_SpecialAttack_Magnum");
                if(crossbow.specialAttackType==WeaponSimple_Crossbow.ESpecialAttackType.Minigun)
                {
                    var result=new Cycle{Seconds=crossbow.minigunFireIntervalTimer.time,Condition="예열 완료 후 연속 사격 기준"};
                    result.Attacks.Add(new Attack{Kind=2,Index=0});return result;
                }
                return new Cycle{Unavailable="강화·재장전 동작 · 평타 DPS에 반영",NoDamage=true};
            }
            return new Cycle{Unavailable="충전·특수 동작의 반복 시간 연결 필요"};
        }
        private static Cycle ShieldRecharge(Cycle cycle,WeaponSimple_SwordAndShield shield,PlayerAvatar player)
        {
            if(cycle.Unavailable!=null)return cycle;
            int stacks=WeaponAdditionalDamageProfiles.ShieldChargeStacks(shield);
            if(stacks<=0)return cycle;
            bool spends=false;
            foreach(var attack in cycle.Attacks)if(attack.Kind==2||attack.Kind==4)spends=true;
            if(!spends)return cycle;
            double speed=1+Math.Max(0,player.GetCustomStatUnsafe("ATTACKSPEED"))/100d*
                (KeywordDatabase.GetConstValue("chargedSweepAttackSpeedEfficiency")/100d);
            double recharge=KeywordDatabase.GetConstValue("chargedSweepTime")*stacks/speed;
            if(!DpsNumbers.Finite(recharge)||speed<=0||recharge<0)
            {cycle.Unavailable="충전 강화의 회복 시간 데이터 없음";return cycle;}
            // Native OnSpecialAttackCreated resets the timer and suspends
            // recharge until EndAttackAnimation. Refill before the next input,
            // because GetSpecialAttack selects its projectile from that stack.
            if(cycle.AttackEnd<0){cycle.Unavailable="충전 재개 애니메이션 이벤트 없음";return cycle;}
            double wait=Math.Max(0,cycle.AttackEnd+recharge-cycle.Seconds);
            DelayCycle(cycle,wait);
            return cycle;
        }
        private static void DelayCycle(Cycle cycle,double wait)
        {
            cycle.Seconds+=wait;
            if(cycle.AttackEnd>=0)cycle.AttackEnd+=wait;
            foreach(var attack in cycle.Attacks)attack.Time+=wait;
            for(int i=0;i<cycle.Begins.Count;i++)cycle.Begins[i]+=wait;
        }
        private static string ShieldSweepMotion(int selection)
        {
            // AnimationWrapper layer 1 SweepEntry transitions; zero/negative
            // values match the native Less(1) fallback, not unknown positives.
            if(selection<1)return "SwordAndShield_Sweep0";
            switch(selection)
            {
                case 1:return "SwordAndShield_Sweep1(Guard)";
                case 2:return "SwordAndShield_Sweep2(Guard&Charge)";
                case 3:return "SwordAndShield_Sweep3(Charge)";
                case 4:return "SwordAndShield_Sweep4(Throw)";
                case 5:return "SwordAndShield_Sweep5(Fast)";
                case 10:return "SwordAndShield_SweepLightning";
                case 11:return "SwordAndShield_SweepLightningEx";
                case 12:return "SwordAndShield_ShieldDash";
                case 20:return "SwordAndShield_Strike";
                default:return null;
            }
        }
        private static Cycle KatanaSheath(WeaponSimple_Katana weapon,PlayerAvatar player)
        {
            double interval=weapon.sheathAttackTime;
            if(player.GetCustomStatUnsafe("SPEEDSHEATH")>0)
            {
                double speed=player.GetCustomStatUnsafe("ATTACKSPEED");
                if(speed<=-100)return new Cycle{Unavailable="납도 공격 간격이 진행되지 않음"};
                if(weapon.attackMoveSet==1)speed*=1.8;
                if(1+speed/100<=0)return new Cycle{Unavailable="납도 공격 간격이 진행되지 않음"};
                interval/=1+speed/100;
            }
            bool electric=weapon.electricChargeStackCount>=weapon.electricChargeIssenCost;
            if(electric)
            {
                if(weapon.electricChargeIssenAttackSpeedMultiplier<=0)return new Cycle{Unavailable="전격 납도 공격이 진행되지 않음"};
                interval/=weapon.electricChargeIssenAttackSpeedMultiplier;
            }
            var result=new Cycle{Seconds=2*interval,Condition=electric?"납도 유지 · 전격 중첩을 계속 공급하는 기준":"납도 유지 후 두 방향 공격 반복 기준"};
            result.Attacks.Add(new Attack{Kind=2,Index=0,Time=0});
            result.Attacks.Add(new Attack{Kind=2,Index=1,Time=interval});
            return result;
        }
    }
}
