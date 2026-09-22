using System;
using System.Collections.Generic;

namespace SephiriaDicePreview
{
    // Timers are copied values. Never consumes a live sword or calls AddSword.
    internal sealed class FullBuffSwordRecovery
    {
        internal sealed class Automatic {internal float Age,Interval;}
        internal int Count;
        internal readonly List<Automatic> Sources=new List<Automatic>();
        internal readonly List<float> Pending=new List<float>();
        internal bool Advance(float seconds,int maximum,int pickupBonus)
        {
            int events=0;
            while(seconds>0)
            {
                float step=seconds;
                foreach(float remaining in Pending)step=Math.Min(step,Math.Max(0,remaining));
                bool canRestore=Count<maximum;
                if(canRestore)foreach(var source in Sources)
                {
                    if(source.Interval<=0)return false;
                    step=Math.Min(step,Math.Max(0,source.Interval-source.Age));
                }
                if(++events>4096)return false;
                if(canRestore)foreach(var source in Sources)source.Age+=step;
                int ready=0;
                for(int i=Pending.Count-1;i>=0;i--)
                {
                    Pending[i]-=step;
                    if(Pending[i]<=0){Pending.RemoveAt(i);ready++;}
                }
                // Native recharge batches all due swords into one AddSword call.
                if(ready>0)Count=Math.Min(maximum,Count+ready+pickupBonus);
                foreach(var source in Sources)
                {
                    if(Count>=maximum)break;
                    if(source.Age>=source.Interval)
                    {
                        source.Age=0;
                        Count=Math.Min(maximum,Count+1+pickupBonus);
                    }
                }
                seconds-=step;
            }
            return true;
        }
    }
}
