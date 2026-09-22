namespace SephiriaDicePreview
{
    internal sealed class FullBuffWeaponCost
    {
        private readonly int basic,bonus;
        private readonly bool truncate;
        internal FullBuffWeaponCost(int basic,int bonus,bool truncate)
        {this.basic=basic;this.bonus=bonus;this.truncate=truncate;}
        internal float Read(int reduction)
        {
            float cost=basic;
            if(truncate)
            {
                cost-=cost*((float)bonus/100f);
                if(cost<0)cost=0;
            }
            cost-=cost*((float)reduction/100f);
            if(cost<0)cost=0;
            return truncate?(int)cost:cost;
        }
    }
}
