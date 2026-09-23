using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaDicePreview
{
    // Numeric event model: rates are grouped by stack count so armor, minimum
    // damage and flat additions can be evaluated for EACH tick before summing.
    internal static class DpsDebuffs
    {
        internal static double ResetRate(double period,double[] applications,double duration,int added,int maximum,bool renew)
        {
            if(!DpsNumbers.Finite(period)||!DpsNumbers.Finite(duration)||period<=0||duration<0||added<0||maximum<=0||maximum>256)
                throw new ArgumentOutOfRangeException("Invalid reset debuff data");
            if(added==0||duration==0||applications==null||applications.Length==0)return 0;
            var offsets=new List<double>();
            foreach(double offset in applications)
            {
                if(!DpsNumbers.Finite(offset)||offset<0)throw new ArgumentOutOfRangeException("Invalid reset debuff application");
                offsets.Add(offset%period);
            }
            offsets.Sort();
            var seen=new Dictionary<string,Tuple<int,double,double>>();
            int stacks=0,events=0;double expiry=double.PositiveInfinity,total=0;
            for(int cycle=0;cycle<10000;cycle++)
            {
                double start=cycle*period,end=start+period;
                double life=stacks>0?expiry-start:0;
                string key=stacks+":"+Math.Round(life,9).ToString("R",CultureInfo.InvariantCulture);
                Tuple<int,double,double> previous;
                if(seen.TryGetValue(key,out previous)&&Math.Abs(life-previous.Item3)<1e-10)
                    return (total-previous.Item2)/((cycle-previous.Item1)*period);
                seen[key]=Tuple.Create(cycle,total,life);
                foreach(double offset in offsets)
                {
                    double apply=start+offset;
                    if(++events>1000000)throw new InvalidOperationException("Reset debuff event budget exceeded");
                    if(expiry<=apply){stacks=0;expiry=double.PositiveInfinity;}
                    if(stacks==0)expiry=apply+duration;
                    else if(renew)expiry=apply+duration;
                    int next=stacks+added;
                    if(next>=maximum){total++;stacks=0;expiry=double.PositiveInfinity;}
                    else stacks=next;
                }
            }
            throw new InvalidOperationException("Reset debuff cycle did not close");
        }
        internal static double[] AccumulatedRates(double period,double[] applications,double duration,int added,int maximum,bool renew,Func<int,double,double[]> evaluate)
        {
            if(!DpsNumbers.Finite(period)||!DpsNumbers.Finite(duration)||period<=0||duration<=0||added<0||maximum<0||maximum>256)
                throw new ArgumentOutOfRangeException("Invalid accumulated debuff data");
            var total=new double[2];
            if(added==0||maximum==0||applications==null||applications.Length==0)return total;
            var offsets=new List<double>();
            foreach(double offset in applications)
            {
                if(!DpsNumbers.Finite(offset)||offset<0)throw new ArgumentOutOfRangeException("Invalid accumulated application");
                offsets.Add(offset%period);
            }
            offsets.Sort();
            var seen=new Dictionary<string,Tuple<int,double[],double>>();
            int stacks=0,events=0;double expiry=double.PositiveInfinity;
            for(int cycle=0;cycle<10000;cycle++)
            {
                double start=cycle*period,end=start+period;
                double life=stacks>0?expiry-start:0;
                string key=stacks+":"+Math.Round(life,9).ToString("R",CultureInfo.InvariantCulture);
                Tuple<int,double[],double> previous;
                if(seen.TryGetValue(key,out previous)&&Math.Abs(life-previous.Item3)<1e-10)
                {
                    for(int target=0;target<2;target++)total[target]=(total[target]-previous.Item2[target])/((cycle-previous.Item1)*period);
                    return total;
                }
                seen[key]=Tuple.Create(cycle,(double[])total.Clone(),life);
                int application=0;
                while(true)
                {
                    double apply=application<offsets.Count?start+offsets[application]:double.PositiveInfinity;
                    if(Math.Min(apply,expiry)>=end)break;
                    if(++events>1000000)throw new InvalidOperationException("Accumulated debuff event budget exceeded");
                    double[] damage=null;
                    if(expiry<=apply)
                    {
                        damage=evaluate(stacks,duration);
                        stacks=0;expiry=double.PositiveInfinity;
                    }
                    else
                    {
                        if(stacks==0)expiry=apply+duration;
                        else if(renew)
                        {
                            // Refresh attacks BEFORE adding the new stack and
                            // resets accumulated time even when already capped.
                            damage=evaluate(stacks,Math.Max(0,apply-(expiry-duration)));
                            expiry=apply+duration;
                        }
                        stacks=Math.Min(maximum,stacks+added);application++;
                    }
                    if(damage!=null)for(int target=0;target<2;target++)total[target]+=damage[target];
                }
            }
            throw new InvalidOperationException("Accumulated debuff cycle did not close");
        }
        internal static double[] TickRates(double period,double[] applications,double duration,double interval,int added,int maximum,bool renew,bool resetAtMaximum=false)
        {
            if(!DpsNumbers.Finite(period)||!DpsNumbers.Finite(duration)||!DpsNumbers.Finite(interval)||period<=0||duration<0||interval<=0||added<0||maximum<0||maximum>256)
                throw new ArgumentOutOfRangeException("Invalid periodic debuff data");
            var counts=new double[maximum+1];
            if(added==0||maximum==0||duration==0||applications==null||applications.Length==0)return counts;
            var offsets=new List<double>();
            foreach(double offset in applications)
            {
                if(!DpsNumbers.Finite(offset)||offset<0)throw new ArgumentOutOfRangeException("Invalid debuff application");
                offsets.Add(offset%period);
            }
            offsets.Sort(); // Keep coincident applications: each adds a stack.
            // With uninterrupted renewal the stack eventually stays capped.
            // The tick clock need not have a rational ratio to the attack
            // period, so waiting for identical cycle phases can never close.
            // Its exact long-run rate is one capped tick per interval.
            if(renew&&!resetAtMaximum)
            {
                double longestGap=period-offsets[offsets.Count-1]+offsets[0];
                for(int i=1;i<offsets.Count;i++)longestGap=Math.Max(longestGap,offsets[i]-offsets[i-1]);
                // Equality expires before the application in the native event
                // ordering below; only strictly shorter gaps maintain stacks.
                if(longestGap<duration)
                {counts[maximum]=1/interval;return counts;}
            }
            var seen=new Dictionary<string,Tuple<int,double[],double,double>>();
            int stacks=0,events=0;
            double tick=double.PositiveInfinity,expiry=double.PositiveInfinity;
            for(int cycle=0;cycle<10000;cycle++)
            {
                double start=cycle*period,end=start+period;
                double tickLeft=stacks>0?tick-start:0,lifeLeft=stacks>0?expiry-start:0;
                string key=stacks+":"+Math.Round(tickLeft,9).ToString("R",CultureInfo.InvariantCulture)+":"+Math.Round(lifeLeft,9).ToString("R",CultureInfo.InvariantCulture);
                Tuple<int,double[],double,double> previous;
                if(seen.TryGetValue(key,out previous)&&Math.Abs(tickLeft-previous.Item3)<1e-10&&Math.Abs(lifeLeft-previous.Item4)<1e-10)
                {
                    double elapsed=(cycle-previous.Item1)*period;
                    for(int i=0;i<counts.Length;i++)counts[i]=(counts[i]-previous.Item2[i])/elapsed;
                    return counts;
                }
                seen[key]=Tuple.Create(cycle,(double[])counts.Clone(),tickLeft,lifeLeft);
                int application=0;
                while(true)
                {
                    double apply=application<offsets.Count?start+offsets[application]:double.PositiveInfinity;
                    double next=Math.Min(apply,Math.Min(tick,expiry));
                    if(next>=end)break;
                    if(++events>1000000)throw new InvalidOperationException("Debuff event budget exceeded");
                    // Native burn updates its tick before checking expiration.
                    if(tick<=apply&&tick<=expiry)
                    {counts[stacks]++;tick+=interval;}
                    else if(expiry<=apply)
                    {stacks=0;tick=expiry=double.PositiveInfinity;}
                    else
                    {
                        if(stacks==0){tick=apply+interval;expiry=apply+duration;}
                        else if(renew)expiry=apply+duration;
                        int nextStacks=stacks+added;
                        if(resetAtMaximum&&nextStacks>=maximum)
                        {
                            stacks=0;tick=expiry=double.PositiveInfinity;
                        }
                        else stacks=Math.Min(maximum,nextStacks);
                        application++;
                    }
                }
            }
            throw new InvalidOperationException("Periodic debuff cycle did not close");
        }
    }
}
