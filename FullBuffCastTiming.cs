using System;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class FullBuffCastTiming
    {
        private static readonly System.Reflection.FieldInfo GlobalTimer=AccessTools.Field(typeof(SkillController),"globalCooldownTimer");
        internal static float ReadRemainingGlobalCooldown(PlayerAvatar player)
        {
            var skills=player.GetComponent<SkillController>();
            return skills&&skills.IsInGlobalCooldown?Math.Max(0,((Timer)GlobalTimer.GetValue(skills)).GetRemainingTime()):0;
        }
        internal static bool TryReadTransformCharge(PlayerAvatar player,WeaponSimple_GreatSword weapon,out float seconds,out string reason)
        {
            seconds=0;reason=null;
            var controller=player.GetComponent<WeaponControllerSimple>();
            var animator=controller?controller.animator:null;
            if(!animator||!animator.runtimeAnimatorController||animator.runtimeAnimatorController.name!="AnimationWrapper"||weapon.animatorLayer!=2||animator.speed<=0)
            {reason="대검 변신 충전 애니메이션 연결 확인 필요";return false;}
            // GreatSword_New_TransformCharge sends SweepChargeStart at time 0.
            // The server charging timer, not AttackSpeed, controls readiness.
            float speed=weapon.longCharge ? .6f : weapon.superQuickSweep ? 100f : weapon.quickSweep ? 1.5f : 1f;
            seconds=Math.Max(0,weapon.sweepTimer.time)/speed;
            return true;
        }
        internal sealed class MagicSnapshot
        {
            private readonly bool hasControllers,hasAnimation,charged,requiresAnimation;
            private readonly float globalTime,animatorSpeed,attackSpeedAmplify;
            private readonly int layer;
            internal MagicSnapshot(bool hasControllers,bool hasAnimation,bool charged,bool requiresAnimation,
                float globalTime,float animatorSpeed,float attackSpeedAmplify,int layer)
            {
                this.hasControllers=hasControllers;this.hasAnimation=hasAnimation;this.charged=charged;
                this.requiresAnimation=requiresAnimation;this.globalTime=globalTime;
                this.animatorSpeed=animatorSpeed;this.attackSpeedAmplify=attackSpeedAmplify;this.layer=layer;
            }
            internal bool TryRead(int quickCast,int attackSpeed,int fixedAttackSpeed,out float before,out float after,out float global,out string reason)
            {
            before=0;after=0;global=0;reason=null;
            if(!hasControllers){reason="시전 컨트롤러 없음";return false;}
            global=globalTime;
            if(charged){reason="충전형 버프 시전 시간 계산 필요";return false;}
            if(!requiresAnimation||quickCast>0)return true;
            if(!hasAnimation){reason="이 시전 애니메이션의 시간 계산 필요";return false;}
            return TryReadAnimation(layer,animatorSpeed,attackSpeedAmplify,attackSpeed,fixedAttackSpeed,out before,out after,out reason);
            }
        }
        internal static MagicSnapshot Capture(PlayerAvatar player,Charm_Magic magic)
        {
            var skills=player.GetComponent<SkillController>();
            var controller=player.GetComponent<WeaponControllerSimple>();
            var animator=controller?controller.animator:null;
            var weapon=controller?controller.currentWeapon:null;
            bool supported=animator&&animator.runtimeAnimatorController&&animator.runtimeAnimatorController.name=="AnimationWrapper"&&weapon;
            return new MagicSnapshot(skills&&controller,supported,magic.ContainedMagic.castingType==ECastingType.Charge,
                magic.ContainedMagic.requireCastingAnimation,skills?((Timer)GlobalTimer.GetValue(skills)).time:0,
                animator?animator.speed:0,weapon?weapon.attackSpeedAmplify:0,weapon?weapon.animatorLayer:0);
        }
        private static bool TryReadAnimation(int layer,float animatorSpeed,float amplification,int attackSpeed,int fixedAttackSpeed,
            out float before,out float after,out string reason)
        {
            before=0;after=0;reason=null;
            // AnimationWrapper's five magic states, read from native-cast-timing.json.
            // StartCharge/StartFire/StopFire use AttackSpeed. Both loop states use 1.
            // StartCharge and StartFire exit at normalized time 1, LoopFire also
            // waits for its end; LoopCharging exits on its channel-check event.
            float charge,fire,fireLength,stop;
            switch(layer)
            {
                case 1:charge=.208333328f;fire=.1f;fireLength=.349999994f;stop=.085714288f;break;
                case 2:charge=.208333328f;fire=.075f;fireLength=.349999994f;stop=.085714288f;break;
                case 3:case 6:charge=.166666672f;fire=.06666667f;fireLength=.233333334f;stop=.085714288f;break;
                case 4:charge=.166666672f;fire=.06666667f;fireLength=.166666672f;stop=.085714288f;break;
                case 7:charge=.208333328f;fire=.1f;fireLength=.125f;stop=.114285715f;break;
                default:reason="이 무기의 시전 상태 연결 계산 필요";return false;
            }
            float speed=(attackSpeed*(1+amplification)+100)/100f;
            float fixedSpeed=fixedAttackSpeed/100f;
            if(fixedSpeed>0)speed=fixedSpeed;
            if(speed<=0||animatorSpeed<=0){reason="현재 시전 애니메이션이 진행되지 않음";return false;}
            before=((charge+fire)/speed+.016666668f)/animatorSpeed;
            after=((fireLength-fire+stop)/speed+.033333335f)/animatorSpeed;
            return true;
        }
    }
}
