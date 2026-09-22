using System;

namespace SephiriaDicePreview
{
    internal sealed class DamageResourceNumbers
    {
        internal readonly float InitialHp,BaseHp,FinalHp;
        internal readonly int BaseMp,ReservedMp,HpCurse,CursedMaxHp,InfinityMaxMp;
        internal DamageResourceNumbers(float hp,float baseHp,float finalHp,int baseMp,int reservedMp,int curse,int cursedMaxHp,int infinityMaxMp)
        {
            InitialHp=hp;BaseHp=baseHp;FinalHp=finalHp;BaseMp=baseMp;ReservedMp=reservedMp;
            HpCurse=curse;CursedMaxHp=cursedMaxHp;InfinityMaxMp=infinityMaxMp;
        }
        internal float MaximumHp(int baseDelta,int percentDelta,int? curseOverride=null,int cursedMaximum=0)
        {
            if(curseOverride.HasValue?curseOverride.Value>0:HpCurse>0)return curseOverride.HasValue?cursedMaximum:CursedMaxHp;
            float value=BaseHp+baseDelta;
            return value+(FinalHp+percentDelta)*value/100f;
        }
        internal int MaximumMp(int baseDelta,int finalPercent,bool infinity)
        {
            if(infinity)return InfinityMaxMp;
            int value=BaseMp+baseDelta;
            return value+(int)((float)(finalPercent*value)/100f);
        }
        internal float HpWithMaximum(float maximum)
        {
            if(HpCurse>0)return InitialHp;
            float original=MaximumHp(0,0);
            return original!=0?maximum*Math.Min(1f,InitialHp/original):InitialHp;
        }
    }
}
