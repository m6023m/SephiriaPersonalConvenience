using System;
using System.Linq;

namespace SephiriaDicePreview
{
    internal static class DpsFollowerProfiles
    {
        private static readonly System.Reflection.FieldInfo BallistaInterval=HarmonyLib.AccessTools.Field(typeof(UnitAI_MiniBallista),"attackIntervalTimer");
        internal static bool Capture(DpsSnapshot snapshot,Charm_SummonUnit source,Charm_SummonUnit live,int level,PlayerAvatar player)
        {
            UnitAvatar unit;FollowerDamageProfiles.Hit follower;
            if(!FollowerDamageProfiles.CaptureCharm(source,live,level,player,out unit,out follower))return false;
            var soldier=unit as Unit_Soldier;
            if(soldier&&unit.GetType()==typeof(Unit_Soldier))
            {
                var ai=unit.GetComponent<UnitAI_Soldier>();
                return ai&&Add(snapshot,"동료 기본 공격 DPS",unit,player,follower,soldier.fireData,
                    AttackEnd(unit,"ATTACK","EndAttackAnimation",FollowerSpeed(unit,player)),
                    SoldierRecharge(ai,unit,live,player),1);
            }
            var grenadier=unit as Unit_Grenadier;
            if(grenadier&&unit.GetType()==typeof(Unit_Grenadier))
            {
                var ai=unit.GetComponent<UnitAI_Grenadier>();
                int volleys=Math.Max(1,grenadier.fireCount)*(player.GetCustomStatUnsafe("JELLYFISHDOUBLEATTACK")>0?2:1);
                return ai&&Add(snapshot,"동료 기본 공격 DPS",unit,player,follower,grenadier.fireData,
                    AttackEnd(unit,"ATTACK","EndAttackAnimation",1),
                    .85*ai.tryAttackTimer.time/FollowerSpeed(unit,player),volleys);
            }
            var mole=unit as Unit_MoleChieftain;
            if(mole&&unit.GetType()==typeof(Unit_MoleChieftain))
            {
                AddExternal(snapshot,"첫 휘두르기",unit,player,follower,mole.basicAttackFireData,1);
                AddExternal(snapshot,"두 번째 휘두르기",unit,player,follower,mole.secondBasicAttackFireData,mole.isCompanionBased?1:.6f);
                AddExternal(snapshot,"다이너마이트",unit,player,follower,mole.throwDynamiteFireData,mole.isCompanionBased?1:.5f);
                AddExternal(snapshot,"드릴",unit,player,follower,mole.drillAttackFireData,mole.isCompanionBased?1:.75f);
                AddExternal(snapshot,"회전 공격",unit,player,follower,mole.windmillFireData,1);
                if(mole.handStompPrefab)
                {
                    var cycle=new DpsSnapshot.Cycle{Name="내려찍기 파동 DPS",Seconds=1,ExternalRateLabel="초당 내려찍기 패턴 사용 횟수"};snapshot.Cycles.Add(cycle);
                    var result=DpsProjectiles.Artifact(new DamageTooltip.Hit{Follower=follower},mole.handStompPrefab,player);
                    cycle.Unavailable=result.Unavailable;foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
                }
                if(mole.drumStage)AddExternal(snapshot,"북 무대 낙석",unit,player,follower.Scaled(mole.isCompanionBased?1:.6f),mole.drumStage.stoneFireData,1);
                return true;
            }
            return false;
        }
        internal static bool CaptureLead(DpsSnapshot snapshot,Charm_LeadNPC source,Charm_LeadNPC live,int level,PlayerAvatar player)
        {
            UnitAvatar unit;FollowerDamageProfiles.Hit follower;bool following;
            if(!FollowerDamageProfiles.CaptureLead(source,live,level,player,out unit,out follower,out following))return false;
            var controller=unit.GetComponent<WeaponControllerSimple>();var weapon=controller?controller.currentWeapon:null;
            if(!weapon)
            {
                snapshot.Cycles.Add(new DpsSnapshot.Cycle{Name="동행 동료 DPS",Unavailable="동행 후 무작위 장비가 결정되면 계산 가능"});return true;
            }
            AddLeadAction(snapshot,"동료 평타 연계 DPS","초당 동료 평타 완전 연계 횟수",unit,follower,weapon,weapon.basicComboAttacks,0,player);
            AddLeadAction(snapshot,"동료 돌진 DPS","초당 동료 돌진 사용 횟수",unit,follower,weapon,weapon.dashAttacks,1,player);
            AddLeadAction(snapshot,"동료 특공 DPS","초당 동료 특공 사용 횟수",unit,follower,weapon,weapon.specialAttacks,2,player);
            return true;
        }
        internal static bool CaptureBallista(DpsSnapshot snapshot,Charm_MiniBallista source,Charm_MiniBallista live,int level,PlayerAvatar player)
        {
            Unit_MiniBallista unit;FollowerDamageProfiles.Hit follower;int count;bool actual;
            if(!FollowerDamageProfiles.CaptureBallista(source,live,level,player,out unit,out follower,out count,out actual))return false;
            var ai=unit.GetComponent<UnitAI_MiniBallista>();
            var timer=ai&&BallistaInterval!=null?BallistaInterval.GetValue(ai) as Timer:null;
            var cycle=new DpsSnapshot.Cycle{Name="미니 발리스타 DPS"};snapshot.Cycles.Add(cycle);
            if(timer==null||timer.time<=0){cycle.Unavailable="미니 발리스타 발사 간격 데이터 없음";return true;}
            cycle.Seconds=timer.time/FollowerSpeed(unit,player);
            var result=DpsProjectiles.Artifact(new DamageTooltip.Hit{Follower=follower},unit.bulletPrefab,player,count);
            cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            return true;
        }
        private static double SoldierRecharge(UnitAI_Soldier ai,UnitAvatar unit,Charm_SummonUnit live,PlayerAvatar player)
        {
            // Awake randomizes a newly spawned soldier's timer to [0.5,1.25].
            // A live unit already contains that chosen duration.
            bool spawned=live&&unit.gameObject!=live.unitPrefab;
            double timer=ai.tryAttackTimer.time*(spawned?1:.875);
            return timer/FollowerSpeed(unit,player)+.325;
        }
        private static double FollowerSpeed(UnitAvatar unit,PlayerAvatar player)
        {
            // Catalog prefabs have no Mirror sync-var backing object, so reading
            // NetworkLeader/GetBonusAttackSpeed from them throws. Native follower
            // speed is the unit's own attack-speed stat plus its leader's shared
            // follower bonus; the preview's player is that prospective leader.
            int bonus=unit.GetCustomStat(ECustomStat.AttackSpeed)+player.GetCustomStatUnsafe("FOLLOWERATTACKSPEED");
            return (100+bonus)/100d;
        }
        private static double AttackEnd(UnitAvatar unit,string state,string method,double speed)
        {
            var animator=unit.TopdownActor?unit.TopdownActor.animator:null;
            if(!animator)animator=unit.GetComponentInChildren<Animator2D_Basic>(true);
            var set=animator?animator.currentSet:null;
            if(!set||speed<=0)return double.NaN;
            var info=set.sprites.FirstOrDefault(x=>String.Equals(x.state,state,StringComparison.OrdinalIgnoreCase));
            if(info==null||info.fps<=0)return double.NaN;
            int frame=-1;
            foreach(var group in info.frameEvents)
                if(group.events.Any(x=>x.methodName==method))frame=Math.Max(frame,group.frame);
            return frame>=0?frame/(double)info.fps/speed:double.NaN;
        }
        private static bool Add(DpsSnapshot snapshot,string name,UnitAvatar unit,PlayerAvatar player,FollowerDamageProfiles.Hit source,
            NewWeaponFireData fire,double attack,double recharge,int volleys)
        {
            var cycle=new DpsSnapshot.Cycle{Name=name,Seconds=attack+recharge};snapshot.Cycles.Add(cycle);
            if(!fire||!DpsNumbers.Finite(attack)||!DpsNumbers.Finite(recharge)||attack<0||recharge<0||cycle.Seconds<=0)
            {cycle.Unavailable="동료 공격 주기 데이터 없음";return true;}
            var prepared=source.Scaled(fire.damageMultiplier*fire.CalculateFinalDamageMultiplier(0));
            prepared.DamageElement=fire.damageElementalType;
            var sample=new DamageTooltip.WeaponAttackSample{Fire=fire,Hits=new System.Collections.Generic.List<DamageTooltip.Hit>{new DamageTooltip.Hit{Follower=prepared}}};
            var result=DpsProjectiles.Weapon(sample,player,false);
            if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return true;}
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count*volleys,part.IgnoreDefense);
            return true;
        }
        private static void AddExternal(DpsSnapshot snapshot,string label,UnitAvatar unit,PlayerAvatar player,FollowerDamageProfiles.Hit source,NewWeaponFireData fire,float factor)
        {
            var cycle=new DpsSnapshot.Cycle{Name=label+" DPS",Seconds=1,ExternalRateLabel="초당 "+label+" 패턴 사용 횟수"};snapshot.Cycles.Add(cycle);
            var result=FollowerDamageProfiles.CaptureFollowerFire(source,fire,player,factor);cycle.Unavailable=result.Unavailable;
            foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
        }
        private static void AddLeadAction(DpsSnapshot snapshot,string name,string rate,UnitAvatar unit,FollowerDamageProfiles.Hit source,
            WeaponSimple weapon,NewWeaponFireData[] attacks,int kind,PlayerAvatar player)
        {
            var cycle=new DpsSnapshot.Cycle{Name=name,Seconds=1,ExternalRateLabel=rate};snapshot.Cycles.Add(cycle);
            attacks=WeaponBuffPreview.Attacks(weapon,attacks,kind);
            if(attacks==null||attacks.Length==0){cycle.ExternalRateLabel=null;cycle.Unavailable="해당 동료 공격 없음";return;}
            int limit=kind==0?Math.Min(attacks.Length,WeaponBuffPreview.FinalCombo(weapon)+1):attacks.Length;
            for(int i=0;i<limit;i++)
            {
                var result=FollowerDamageProfiles.CaptureFollowerWeaponFire(unit,source,weapon,attacks[i],kind,player);
                if(result.Unavailable!=null){cycle.Unavailable=result.Unavailable;return;}
                foreach(var part in result.Parts)DpsCapture.Add(cycle,part.Hit,part.Count,part.IgnoreDefense);
            }
        }
    }
}
