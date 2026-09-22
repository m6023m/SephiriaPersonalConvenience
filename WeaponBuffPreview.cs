using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaDicePreview
{
    internal sealed class WeaponBuffPreview : IDisposable
    {
        [ThreadStatic] private static WeaponBuffPreview current;
        private readonly WeaponSimple weapon;
        private readonly bool eclipse,transformed;
        private readonly IDictionary<int,int> boneBonuses;
        private static readonly FieldInfo BladeStuck=AccessTools.Field(typeof(WeaponSimple_Katana),"isBladeStuck");
        private static readonly PropertyInfo EnhancedAmmo=AccessTools.Property(typeof(WeaponSimple_Crossbow),"IsEnhancedCompressedAmmo");
        private static readonly FieldInfo GreatBasicCost=AccessTools.Field(typeof(WeaponSimple_GreatSword),"mpBasicAttackCost");
        internal static int UsedMp(WeaponSimple source,int kind,UnitAvatar player)
        {
            var great=source as WeaponSimple_GreatSword;
            if(kind==0&&great)
            {
                // Transformation explicitly replaces the animation trigger with 1,
                // independently of the amount spent by the preceding MP basic attack.
                if(Transformed(source))return 1;
                int cost=(int)GreatBasicCost.GetValue(great);
                if(great.useMPBasicAttack&&player.MP>=cost)return Math.Max(0,cost);
                return 0;
            }
            // Basic/dash previews must not inherit the preceding special attack's
            // animation MP marker. MP basic attacks are handled above explicitly.
            if(kind==0||kind==1)return 0;
            return source.owner?Math.Max(0,source.owner.mpConsumedAttackTriggerFromAnimationValue):0;
        }
        internal WeaponBuffPreview(WeaponSimple source,bool useEclipse,bool useTransform,IDictionary<int,int> boneBasicBonuses=null)
        {
            if(current!=null)throw new InvalidOperationException("Nested weapon buff preview");
            weapon=source;eclipse=useEclipse;transformed=useTransform;boneBonuses=boneBasicBonuses;current=this;
        }
        internal static int BoneBonus(WeaponAddonGreatsword_BoneBlood addon,int nativeValue)
        {
            int value;var preview=current;
            return preview!=null&&preview.boneBonuses!=null&&preview.boneBonuses.TryGetValue(addon.GetInstanceID(),out value)?value:nativeValue;
        }
        internal static bool Eclipse(WeaponSimple source)
        {
            var katana=source as WeaponSimple_Katana;
            if(katana&&current!=null&&current.weapon==source)return current.eclipse;
            return katana&&(EclipseBuff(source)||katana.isEclipseAttackActivated);
        }
        internal static bool EclipseBuff(WeaponSimple source)
        {
            var katana=source as WeaponSimple_Katana;
            return katana&&(current!=null&&current.weapon==source?current.eclipse:katana.isEclipseBuffActivated);
        }
        internal static bool Transformed(WeaponSimple source)
        {
            var great=source as WeaponSimple_GreatSword;
            return great&&(current!=null&&current.weapon==source?current.transformed:great.GetWeaponParameter("IsTransformed")==1);
        }
        internal static int FinalCombo(WeaponSimple source)
        {
            var katana=source as WeaponSimple_Katana;
            if(katana&&current!=null&&current.weapon==source&&!current.eclipse&&katana.isEclipseBuffActivated)return 2;
            // Overheat temporarily replaces finalComboIdx even while eclipse is active.
            if(katana&&katana.dashStackCounter>0&&katana.dashStackAttackMoveSet>0)return source.finalComboIdx;
            return Eclipse(source)?4:source.finalComboIdx;
        }
        internal static NewWeaponFireData[] Attacks(WeaponSimple source,NewWeaponFireData[] original,int kind)
        {
            if(!source||original==null||original.Length==0)return original;
            var great=source as WeaponSimple_GreatSword;
            var player=source.owner?source.owner.unitAvatar as PlayerAvatar:null;
            if(kind==0&&great&&player&&great.useMPBasicAttack&&UsedMp(source,kind,player)>0)return great.mpConsumedBasicAttack;
            var crossbow=source as WeaponSimple_Crossbow;
            if(kind==0&&crossbow&&crossbow.owner&&crossbow.iceArrow&&crossbow.owner.unitAvatar.GetCustomStatUnsafe("ICECROSSBOWBUFF")>0)
                return new[]{crossbow.iceArrow};
            if(kind==0&&crossbow&&crossbow.owner&&crossbow.hasCompressedAmmo)
            {
                // The compressed arrays can have a different length from basicComboAttacks.
                // Do not index them using the uncompressed array's length.
                bool enhanced=(bool)EnhancedAmmo.GetValue(crossbow,null);
                int count=enhanced?Math.Max(crossbow.compressedAmmoAttacks.Length,crossbow.enhancedCompressedAmmoAttacks.Length):crossbow.compressedAmmoAttacks.Length;
                var compressed=new NewWeaponFireData[count];
                for(int i=0;i<count;i++)compressed[i]=enhanced&&i<crossbow.enhancedCompressedAmmoAttacks.Length?crossbow.enhancedCompressedAmmoAttacks[i]:crossbow.compressedAmmoAttacks[i];
                return compressed;
            }
            var katana=source as WeaponSimple_Katana;
            var type=source.GetType();
            bool nativeSelector=type==typeof(WeaponSimple_GreatSword)||type==typeof(WeaponSimple_Katana)||type==typeof(WeaponSimple_Crossbow)||type==typeof(WeaponSimple_SwordAndShield)||type==typeof(WeaponSimple_Dagger)||type==typeof(WeaponSimple_QuartterStaff)||type==typeof(WeaponSimple_Staff)||type==typeof(WeaponSimple_Golem)||type==typeof(WeaponSimple_Bow);
            int length=kind==0&&Eclipse(source)?Math.Max(original.Length,FinalCombo(source)+1):original.Length;
            var result=new NewWeaponFireData[length];
            for(int i=0;i<result.Length;i++)
            {
                // These native selectors read current state. Do not call GetBasicAttackParameter:
                // the quarterstaff implementation rolls combat RNG even for a tooltip request.
                string parameter="";
                if(kind==0&&crossbow&&crossbow.hasCompressedAmmo)
                    parameter=(bool)EnhancedAmmo.GetValue(crossbow,null)?"COMPRESSEDAMMO_ENHANCED":"COMPRESSEDAMMO";
                var selected=original[Math.Min(i,original.Length-1)];
                if(source.owner&&nativeSelector&&!(kind==0&&great&&player))
                {
                    var shield=source as WeaponSimple_SwordAndShield;
                    selected=kind==2&&shield?WeaponAdditionalDamageProfiles.ShieldSpecialAttack(shield,i):
                        kind==0?source.GetBasicAttack(i,parameter):kind==1?source.GetDashAttack(i):source.GetSpecialAttack(i);
                }
                if(katana&&Eclipse(source))
                {
                    // A hypothetical eclipse must retain the native higher-priority stuck-blade,
                    // overheat and enhanced dash branches, just as an actual eclipse does.
                    bool blocked=kind==0&&(katana.useSheathHardening&&(bool)BladeStuck.GetValue(katana)&&katana.stuckBladeAttack_FireDatas!=null&&katana.stuckBladeAttack_FireDatas.Length>0);
                    if(kind==0&&katana.dashStackCounter>0&&katana.basicAttack_Overheat_FireDatas!=null&&i<katana.basicAttack_Overheat_FireDatas.Count&&katana.basicAttack_Overheat_FireDatas[i]&&(katana.dashStackAttackMoveSet<=0||i<=katana.dashStackFinalComboIdx))blocked=true;
                    if(kind==1&&katana.isEnhancedDashAttackActive)blocked=true;
                    var replacement=kind==0?katana.eclipseBasicAttackFireDatas:kind==1?katana.eclipseDashAttackFireDatas:null;
                    if(!blocked&&replacement!=null&&replacement.Length>0)selected=replacement[Math.Min(i,replacement.Length-1)];
                }
                result[i]=selected;
            }
            return result;
        }
        internal static bool AlternateBasic(WeaponSimple source,PlayerAvatar player,NewWeaponFireData[] normal,out NewWeaponFireData[] alternate,out float chance,out string label)
        {
            alternate=null;chance=0;label=null;
            if(normal==null||normal.Length==0)return false;
            var staff=source as WeaponSimple_QuartterStaff;
            if(staff&&staff.doubleBasicComboAttacks.Length>0)
            {
                // Native rolls a float in [0,100], despite dividing BOTH source
                // stats by 100 first. Preserve that formula, not an assumed percent.
                chance=Mathf.Clamp(player.GetCustomStatUnsafe("STAFFMULTITHROWINGSPEARDOUBLE")/100f*(player.GetCustomStat(ECustomStat.Evasion)/100f),0,100);
                if(chance<=0)return false;
                alternate=new NewWeaponFireData[normal.Length];bool different=false;
                for(int i=0;i<normal.Length;i++)
                {
                    // Selector preserves rolling/crystal/summon priority and only
                    // indexes the DOUBLE array. It does not perform the RNG roll.
                    alternate[i]=staff.GetBasicAttack(i,"DOUBLE");
                    different|=alternate[i]!=normal[i];
                }
                label="이중 투창";
                return different;
            }
            var crossbow=source as WeaponSimple_Crossbow;
            if(crossbow&&crossbow.lightningArrow&&player.GetCustomStatUnsafe("ICECROSSBOWBUFF")<=0)
            {
                chance=Mathf.Clamp(player.GetCustomStatUnsafe("LIGHTNINGCROSSBOW"),0,100);
                if(chance<=0)return false;
                alternate=new NewWeaponFireData[normal.Length];
                for(int i=0;i<alternate.Length;i++)alternate[i]=crossbow.lightningArrow;
                label="번개 화살";
                return true;
            }
            return false;
        }
        public void Dispose(){if(current==this)current=null;}
    }
}
