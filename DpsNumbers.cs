using System;

namespace SephiriaDicePreview
{
    // Pure numeric functions. No Unity objects, clocks, RNG, or network state.
    internal static class DpsNumbers
    {
        internal static double CriticalExpected(double normal,double critical,double execution,double chancePercent,bool executionEnabled)
        {
            double crit=Math.Max(0,Math.Min(1,chancePercent/100));
            double execute=executionEnabled?Math.Max(0,Math.Min(1,(chancePercent-100)/100)):0;
            return execute*execution+(1-execute)*(crit*critical+(1-crit)*normal);
        }
        internal static double DashReducedCooldown(double cooldown,double dashPeriod,double reduction)
        {
            if(!Finite(cooldown)||!Finite(dashPeriod)||!Finite(reduction)||cooldown<=0||dashPeriod<=0||reduction<0)
                throw new ArgumentOutOfRangeException("Invalid dash cooldown timing");
            if(reduction==0)return cooldown;
            double elapsed=0,casts=0;
            // Regeneration point: cast immediately after a paid dash, timer 0.
            // Count natural recoveries between dashes; excess dash reduction is
            // discarded by native Timer.AddTimer and cannot fund multiple casts.
            for(int steps=1;steps<=100000;steps++)
            {
                double natural=Math.Floor((elapsed+dashPeriod)/cooldown);
                casts+=natural;elapsed=elapsed+dashPeriod-natural*cooldown;
                if(elapsed+reduction>=cooldown)
                    return steps*dashPeriod/(casts+1);
                elapsed+=reduction;
            }
            throw new InvalidOperationException("Dash cooldown cycle did not close");
        }
        internal sealed class RechargeSchedule
        {
            internal double Period;
            internal double[] Offsets;
            internal double MeanSeconds {get{return Period/Offsets.Length;}}
        }
        internal static double AttackReducedCooldown(double cooldown,double search,double period,double[] attacks,double reduction)
        {return AttackRechargeSchedule(cooldown,search,period,attacks,reduction).MeanSeconds;}
        internal static RechargeSchedule AttackRechargeSchedule(double cooldown,double search,double period,double[] attacks,double reduction)
        {
            if(!Finite(cooldown)||!Finite(search)||!Finite(period)||!Finite(reduction)||cooldown<0||search<=0||period<=0||reduction<0)
                throw new ArgumentOutOfRangeException("Invalid attack-reduced cooldown");
            if(reduction==0||attacks==null||attacks.Length==0)return new RechargeSchedule{Period=cooldown+search,Offsets=new[]{0d}};
            var sorted=new System.Collections.Generic.List<double>();
            foreach(double attack in attacks)
            {
                if(!Finite(attack)||attack<0)throw new ArgumentOutOfRangeException("Invalid recharge event");
                sorted.Add(attack%period);
            }
            sorted.Sort();
            for(int i=sorted.Count-1;i>0;i--)if(sorted[i]==sorted[i-1])sorted.RemoveAt(i);
            // Store a state only at activation boundaries: timers are reset,
            // leaving attack phase and one overwritten pending reduction.
            var seen=new System.Collections.Generic.Dictionary<string,Tuple<double,int,double>>();
            var activations=new System.Collections.Generic.List<double>();
            double now=0;bool pending=false;int events=0;
            for(int casts=0;casts<100000;casts++)
            {
                double phase=now%period;
                string key=Math.Round(phase,9).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+(pending?"+":"-");
                Tuple<double,int,double> previous;
                if(seen.TryGetValue(key,out previous)&&Math.Abs(phase-previous.Item3)<1e-10)
                {
                    var offsets=new double[casts-previous.Item2];
                    for(int i=0;i<offsets.Length;i++)offsets[i]=activations[previous.Item2+i]-previous.Item1;
                    return new RechargeSchedule{Period=now-previous.Item1,Offsets=offsets};
                }
                seen[key]=Tuple.Create(now,casts,phase);
                activations.Add(now);
                double remaining=Math.Max(0,cooldown-(pending?reduction:0));
                pending=false;
                double next=NextRechargeEvent(now,period,sorted);
                while(next<=now+remaining)
                {
                    if(++events>1000000)throw new InvalidOperationException("Recharge event budget exceeded");
                    remaining=Math.Max(0,remaining-(next-now)-reduction);
                    now=next;
                    if(remaining==0)break;
                    next=NextRechargeEvent(now,period,sorted);
                }
                now+=remaining;
                double ready=now;
                now+=search;
                // During search, native addCooldown is overwritten. One or a
                // hundred attack starts therefore carry ONE reduction forward.
                pending=NextRechargeEvent(ready,period,sorted)<=now;
            }
            throw new InvalidOperationException("Attack-reduced cooldown cycle did not close");
        }
        // Some native timers add every attack's progress even while the effect
        // is firing. This differs from callbacks that keep one pending refund.
        internal static RechargeSchedule AdditiveRechargeSchedule(double cooldown,double search,double period,double[] attacks,double reduction)
        {
            if(!Finite(cooldown)||!Finite(search)||!Finite(period)||!Finite(reduction)||cooldown<0||search<=0||period<=0||reduction<0)
                throw new ArgumentOutOfRangeException("Invalid additive recharge");
            if(reduction==0||attacks==null||attacks.Length==0)return new RechargeSchedule{Period=cooldown+search,Offsets=new[]{0d}};
            var sorted=new System.Collections.Generic.List<double>();
            foreach(double attack in attacks)
            {
                if(!Finite(attack)||attack<0)throw new ArgumentOutOfRangeException("Invalid additive recharge event");
                sorted.Add(attack%period);
            }
            sorted.Sort();
            for(int i=sorted.Count-1;i>0;i--)if(sorted[i]==sorted[i-1])sorted.RemoveAt(i);
            var seen=new System.Collections.Generic.Dictionary<string,Tuple<double,int,double>>();
            var activations=new System.Collections.Generic.List<double>();
            double now=0;int events=0;
            for(int casts=0;casts<100000;casts++)
            {
                double phase=now%period;
                string key=Math.Round(phase,9).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                Tuple<double,int,double> previous;
                if(seen.TryGetValue(key,out previous)&&Math.Abs(phase-previous.Item3)<1e-10)
                {
                    var offsets=new double[casts-previous.Item2];
                    for(int i=0;i<offsets.Length;i++)offsets[i]=activations[previous.Item2+i]-previous.Item1;
                    return new RechargeSchedule{Period=now-previous.Item1,Offsets=offsets};
                }
                seen[key]=Tuple.Create(now,casts,phase);activations.Add(now);
                double progress=0,end=now+search,next=NextRechargeEvent(now,period,sorted);
                while(next<=end)
                {
                    if(++events>1000000)throw new InvalidOperationException("Additive recharge event budget exceeded");
                    progress=Math.Min(cooldown,progress+reduction);next=NextRechargeEvent(next,period,sorted);
                }
                now=end;
                double remaining=Math.Max(0,cooldown-progress);next=NextRechargeEvent(now,period,sorted);
                while(next<=now+remaining)
                {
                    if(++events>1000000)throw new InvalidOperationException("Additive recharge event budget exceeded");
                    remaining=Math.Max(0,remaining-(next-now)-reduction);now=next;
                    if(remaining==0)break;
                    next=NextRechargeEvent(now,period,sorted);
                }
                now+=remaining;
            }
            throw new InvalidOperationException("Additive recharge cycle did not close");
        }
        private static double NextRechargeEvent(double after,double period,System.Collections.Generic.List<double> offsets)
        {
            double start=Math.Floor(after/period)*period;
            double phase=after-start;
            int low=0,high=offsets.Count;
            while(low<high)
            {
                int middle=(low+high)/2;
                if(offsets[middle]<=phase+1e-10)low=middle+1;else high=middle;
            }
            return low<offsets.Count?start+offsets[low]:start+period+offsets[0];
        }
        // Native planetary timers begin at a uniform ratio in [0,.75].
        // First fire is uniform in [.25*interval,interval], not at time zero.
        internal static double PlanetaryShots(int planets,double lifetime,double interval,bool shared)
        {
            if(planets<0||!Finite(lifetime)||!Finite(interval)||interval<=0)
                throw new ArgumentOutOfRangeException("Invalid planetary volley timing");
            if(planets==0||lifetime<=0)return 0;
            double cycles=lifetime/interval;
            if(shared)
            {
                double firstChance=Math.Max(0,Math.Min(1,(cycles-.25)/.75));
                return 1-Math.Pow(1-firstChance,planets);
            }
            double whole=Math.Floor(cycles);
            return planets*(whole+Math.Max(0,Math.Min(1,(cycles-whole-.25)/.75)));
        }
        internal static float FinishDamage(float outgoing,float defenseBonus,int flat,int defense,int ignoreDefense)
        {
            outgoing+=outgoing*defenseBonus;
            if(outgoing<=0)return 0;
            float reduction=defense>0?Math.Min(outgoing,outgoing*(float)Math.Log(defense/40f+1f)*.445f):outgoing*defense/100f;
            if(ignoreDefense>0)reduction*=1-ignoreDefense/100f;
            return Math.Max(1,outgoing-reduction+flat);
        }

        // Cooldown may run concurrently with the action. Its starting offset must
        // come from the native trigger, not an assumption that all waits add up.
        internal static double CycleSeconds(double action,double cooldown,double cooldownStartsAt,double reload)
        {
            if(!Finite(action)||!Finite(cooldown)||!Finite(cooldownStartsAt)||!Finite(reload)||action<0||cooldown<0||cooldownStartsAt<0||reload<0)
                throw new ArgumentOutOfRangeException("Invalid DPS timing");
            return Math.Max(action,cooldownStartsAt+cooldown)+reload;
        }

        internal static bool Finite(double number){return !double.IsNaN(number)&&!double.IsInfinity(number);}

        // Full-magazine charged bow rotation. Native reload pauses during the
        // original volley, resumes after .1 seconds, and the optional retrigger
        // decrements ammo while reloading. A full rotation waits for that volley
        // too; input/network/frame delays are outside this numeric estimate.
        internal static double ChargedBowSeconds(int arrows,double fireInterval,double reload,double charge,bool repeat)
        {
            if(arrows<=0||arrows>4096||!Finite(fireInterval)||fireInterval<0||!Finite(reload)||reload<=0||!Finite(charge)||charge<=0)
                throw new ArgumentOutOfRangeException("Invalid charged bow timing");
            double last=(arrows-1)*fireInterval,release=last+.1;
            bool alreadyCharged=arrows>1&&charge<=fireInterval;
            double ready=alreadyCharged?0:last+charge;
            if(!repeat)return Math.Max(release+arrows*reload,ready);
            double time=release,remainder=0;int ammo=0;
            double repeatStart=release+.22;
            // Becoming available is latched; ResetTimer does not clear it.
            if(charge<=repeatStart-last)alreadyCharged=true;
            for(int arrow=0;arrow<arrows;arrow++)
            {
                double at=repeatStart+arrow*fireInterval;
                double accumulated=remainder+at-time;
                double ticks=Math.Floor((accumulated+1e-10)/reload);
                if(ticks>=arrows-ammo){ammo=arrows;remainder=0;}
                else {ammo+=(int)ticks;remainder=accumulated-ticks*reload;}
                ammo--;time=at;
            }
            ready=alreadyCharged?0:time+charge;
            return Math.Max(time+(arrows-ammo)*reload-remainder,ready);
        }

        // Time covered by periodically repeated intervals. Overlapping flame
        // patches share one victim hit per global tick, so sum their UNION.
        // This is also the exact phase-averaged tick coverage for integer tick
        // lifetimes when the global tick phase relative to the combo is unknown.
        internal static double CoveredSeconds(double period,double[] starts,double[] lengths)
        {
            if(!Finite(period)||period<=0||starts==null||lengths==null||starts.Length!=lengths.Length)
                throw new ArgumentOutOfRangeException("Invalid periodic coverage");
            var intervals=new System.Collections.Generic.List<double[]>();
            for(int i=0;i<starts.Length;i++)
            {
                double length=lengths[i];
                if(!Finite(starts[i])||!Finite(length)||length<0)throw new ArgumentOutOfRangeException("Invalid coverage interval");
                if(length>=period)return period;
                if(length==0)continue;
                double start=starts[i]%period;if(start<0)start+=period;
                double end=start+length;
                intervals.Add(new[]{start,Math.Min(period,end)});
                if(end>period)intervals.Add(new[]{0d,end-period});
            }
            intervals.Sort(delegate(double[] a,double[] b){return a[0].CompareTo(b[0]);});
            double total=0,left=0,right=0;
            foreach(var interval in intervals)
            {
                if(interval[0]>right){total+=right-left;left=interval[0];right=interval[1];}
                else right=Math.Max(right,interval[1]);
            }
            return Math.Min(period,total+right-left);
        }

        // Long-run rate for a chance proc triggered by a repeated, non-uniform
        // combo. Successful procs start the cooldown; failed attempts do not.
        // This avoids simply multiplying attacks/sec by chance past the cooldown.
        internal static double ProcRate(double[] offsets,double period,double cooldown,double probability,int[] simultaneousAttempts=null)
        {
            if(offsets==null||offsets.Length==0||period<=0||!Finite(period)||cooldown<0||!Finite(cooldown)||!Finite(probability))
                throw new ArgumentOutOfRangeException("Invalid proc cycle");
            int n=offsets.Length;
            if(simultaneousAttempts!=null&&simultaneousAttempts.Length!=n)throw new ArgumentException("Invalid proc groups");
            for(int i=0;i<n;i++)if(!Finite(offsets[i])||offsets[i]<0||offsets[i]>=period||(i>0&&offsets[i]<offsets[i-1]))
                throw new ArgumentOutOfRangeException("Invalid attack offsets");
            double attempts=0;bool sameCount=true;int firstCount=simultaneousAttempts==null?1:simultaneousAttempts[0];
            for(int i=0;i<n;i++)
            {
                int count=simultaneousAttempts==null?1:simultaneousAttempts[i];
                if(count<=0)throw new ArgumentOutOfRangeException("Invalid proc group size");
                attempts+=count;if(count!=firstCount)sameCount=false;
            }
            probability=Math.Max(0,Math.Min(1,probability));
            if(probability==0)return 0;
            double spacing=period/n,minimumGap=period+offsets[0]-offsets[n-1];
            bool uniform=Math.Abs(minimumGap-spacing)<=1e-9;
            for(int i=1;i<n;i++)
            {
                double gap=offsets[i]-offsets[i-1];minimumGap=Math.Min(minimumGap,gap);
                if(Math.Abs(gap-spacing)>1e-9)uniform=false;
            }
            // No cooldown can suppress an event in this case, irrespective of
            // uneven spacing. Uniform events have a geometric waiting time and
            // need no phase matrix either (including large infinite magazines).
            if(cooldown<=minimumGap+1e-9)return probability*attempts/period;
            if(uniform&&sameCount)
            {
                double skipped=Math.Max(1,Math.Ceiling((cooldown-1e-9)/spacing));
                double groupChance=AnySuccess(probability,firstCount);
                return firstCount*probability/(spacing*(1+groupChance*(skipped-1)));
            }
            var next=new int[n];var firstDelay=new double[n];
            for(int i=0;i<n;i++)
            {
                double threshold=offsets[i]+cooldown;
                double laps=Math.Floor(threshold/period);
                double within=threshold-laps*period;
                int low=0,high=n;
                while(low<high){int middle=low+(high-low)/2;if(offsets[middle]+1e-9<within)low=middle+1;else high=middle;}
                int j=low;
                if(j==n){j=0;laps++;}
                // One attack-start event cannot proc twice even at zero cooldown.
                if(laps==0&&j<=i){j=i+1;if(j==n){j=0;laps++;}}
                next[i]=j;firstDelay[i]=laps*period+offsets[j]-offsets[i];
            }
            if(probability==1)
            {
                var visited=new int[n];for(int i=0;i<n;i++)visited[i]=-1;
                var times=new double[n+1];var rewards=new double[n+1];int at=0,steps=0;
                while(visited[at]<0)
                {
                    visited[at]=steps;times[steps+1]=times[steps]+firstDelay[at];
                    rewards[steps+1]=rewards[steps]+(simultaneousAttempts==null?1:simultaneousAttempts[at]);
                    steps++;at=next[at];
                }
                int start=visited[at];double duration=times[steps]-times[start];
                return duration>0?(rewards[steps]-rewards[start])/duration:0;
            }
            // A deferred cooldown starts after the whole same-frame group. Each
            // contact can succeed independently; entering cooldown depends on at
            // least one success, while its reward is the number of successes.
            double denominator=AnySuccess(probability,attempts);
            if(denominator<=0)return 0;
            var chances=new double[n];var rewardsByPhase=new double[n];
            for(int i=0;i<n;i++)
            {
                int count=simultaneousAttempts==null?1:simultaneousAttempts[i];
                chances[i]=AnySuccess(probability,count);rewardsByPhase[i]=count*probability/chances[i];
            }
            if(n>64)return LargeProcRate(offsets,period,next,firstDelay,chances,rewardsByPhase,denominator);
            var transition=new double[n,n];var durationByPhase=new double[n];
            for(int i=0;i<n;i++)
            {
                int first=next[i];double wait=0,failure=1;
                for(int d=0;d<n;d++)
                {
                    int phase=(first+d)%n;
                    transition[i,phase]=failure*chances[phase]/denominator;
                    failure*=1-chances[phase];
                    int following=(phase+1)%n;
                    double gap=following==0?period+offsets[0]-offsets[phase]:offsets[following]-offsets[phase];
                    wait+=failure*gap/denominator;
                }
                durationByPhase[i]=firstDelay[i]+wait;
            }
            // Stationary distribution of successful proc phases.
            var matrix=new double[n,n+1];
            for(int row=0;row<n-1;row++)for(int col=0;col<n;col++)matrix[row,col]=transition[col,row]-(col==row?1:0);
            for(int col=0;col<n;col++)matrix[n-1,col]=1;matrix[n-1,n]=1;
            for(int col=0;col<n;col++)
            {
                int pivot=col;for(int row=col+1;row<n;row++)if(Math.Abs(matrix[row,col])>Math.Abs(matrix[pivot,col]))pivot=row;
                if(Math.Abs(matrix[pivot,col])<1e-14)throw new InvalidOperationException("Proc phase system is singular");
                for(int j=col;j<=n;j++){double value=matrix[col,j];matrix[col,j]=matrix[pivot,j];matrix[pivot,j]=value;}
                double scale=matrix[col,col];for(int j=col;j<=n;j++)matrix[col,j]/=scale;
                for(int row=0;row<n;row++)if(row!=col){double value=matrix[row,col];for(int j=col;j<=n;j++)matrix[row,j]-=value*matrix[col,j];}
            }
            double average=0,reward=0;
            for(int i=0;i<n;i++){average+=matrix[i,n]*durationByPhase[i];reward+=matrix[i,n]*rewardsByPhase[i];}
            return average>0?reward/average:0;
        }

        private static double LargeProcRate(double[] offsets,double period,int[] next,double[] firstDelay,double[] chance,double[] reward,double denominator)
        {
            int n=offsets.Length;
            var gaps=new double[n];var wait=new double[n];
            for(int i=0;i<n;i++)gaps[i]=i+1<n?offsets[i+1]-offsets[i]:period+offsets[0]-offsets[i];
            double failure=1;
            for(int i=0;i<n;i++){failure*=1-chance[i];wait[0]+=failure*gaps[i]/denominator;}
            for(int i=n-1;i>0;i--)wait[i]=(1-chance[i])*(gaps[i]+wait[(i+1)%n]);
            var distribution=new double[n];var arrivals=new double[n];var updated=new double[n];
            for(int i=0;i<n;i++)distribution[i]=1d/n;
            // Transition application is a cyclic failure-flow recurrence, not
            // an n-by-n matrix. Laziness removes periodic oscillation. The
            // iteration bound prevents pathological proc inputs from consuming
            // unbounded worker time; only converged estimates are returned.
            for(int iteration=0;iteration<20000;iteration++)
            {
                Array.Clear(arrivals,0,n);
                for(int i=0;i<n;i++)arrivals[next[i]]+=distribution[i];
                double flow=0;
                for(int i=0;i<n;i++)flow=arrivals[i]+(1-chance[(i+n-1)%n])*flow;
                flow/=denominator;
                double residual=0,total=0;
                for(int i=0;i<n;i++)
                {
                    flow=arrivals[i]+(1-chance[(i+n-1)%n])*flow;
                    double value=flow*chance[i];
                    residual+=Math.Abs(value-distribution[i]);
                    updated[i]=(value+distribution[i])*.5;total+=updated[i];
                }
                if(residual<1e-10)
                {
                    double duration=0,averageReward=0;
                    for(int i=0;i<n;i++)
                    {
                        duration+=distribution[i]*(firstDelay[i]+wait[next[i]]);
                        averageReward+=distribution[i]*reward[i];
                    }
                    return duration>0?averageReward/duration:0;
                }
                if(!Finite(total)||total<=0)throw new InvalidOperationException("Invalid proc phase distribution");
                for(int i=0;i<n;i++)distribution[i]=updated[i]/total;
            }
            throw new InvalidOperationException("Proc phase estimate did not converge");
        }

        private static double AnySuccess(double probability,double attempts)
        {
            if(probability>=1)return 1;
            // Avoid cancellation for very small proc probabilities.
            double log=probability<1e-5?-probability*(1+probability*(.5+probability/3)):Math.Log(1-probability);
            double exponent=attempts*log;
            return Math.Abs(exponent)<1e-5?-exponent*(1+exponent*(.5+exponent/6)):1-Math.Exp(exponent);
        }
    }
}
