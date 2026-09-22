using System;

namespace SephiriaDicePreview
{
    internal sealed class FullBuffMagicNumbers
    {
        private readonly float baseCost;
        private readonly int additionalCost,ammo;
        private readonly bool enabled;
        internal FullBuffMagicNumbers(float baseCost,int additionalCost,int ammo,bool enabled)
        {this.baseCost=baseCost;this.additionalCost=additionalCost;this.ammo=ammo;this.enabled=enabled;}
        internal int Cost(int noMagicCost,int partyBuff,int costReduction)
        {
            if(noMagicCost>0)return 0;
            float cost=baseCost;
            if(partyBuff>0)cost+=cost*.5f;
            int adjustment=additionalCost-costReduction;
            if(adjustment<=-100)return 0;
            return (int)Math.Round(cost+cost*((float)adjustment/100f),MidpointRounding.ToEven);
        }
        internal ECanUseSkillResult Availability(int remainingMp,int cost)
        {
            if(!enabled)return ECanUseSkillResult.Failed_Common;
            if(ammo<=0)return ECanUseSkillResult.Failed_NotYet;
            if(cost>0&&remainingMp<cost)return ECanUseSkillResult.Failed_NotEnoughMana;
            return ECanUseSkillResult.Succeeded;
        }
    }
}
