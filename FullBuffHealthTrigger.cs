namespace SephiriaDicePreview
{
    // One captured subscriber's numeric state, private to a full-buff forecast.
    internal sealed class FullBuffHealthTrigger
    {
        internal readonly string Stat;
        private readonly int threshold,bonus;
        private readonly bool atLeast;
        private int applied;
        internal FullBuffHealthTrigger(string stat,int thresholdPercent,int value,bool minimum,int current)
        {Stat=stat;threshold=thresholdPercent;bonus=value;atLeast=minimum;applied=current;}
        internal int Update(float hp,float maximum)
        {
            float ratio=hp/maximum;
            bool enabled=atLeast?ratio>=threshold/100f:ratio<=threshold/100f;
            int next=enabled?bonus:0;
            int delta=next-applied;applied=next;return delta;
        }
    }
}
