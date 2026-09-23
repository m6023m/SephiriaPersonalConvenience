using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SephiriaDicePreview
{
    // Pure numeric model; no transforms, Unity timers, or active projectiles.
    internal static class DpsMagicNumbers
    {
        internal sealed class BoomerangCycle
        {
            internal double StartSpeed,ReturnSpeed,Acceleration,Stop,SpeedScale,LifeLimit,ContactInterval,Cooldown,Animation;
            internal int Capacity,Casts;
            internal string Signature()
            {
                var text=new StringBuilder().Append(Capacity).Append(',').Append(Casts);
                foreach(double value in new[]{StartSpeed,ReturnSpeed,Acceleration,Stop,SpeedScale,LifeLimit,ContactInterval,Cooldown,Animation})
                    text.Append(',').Append(value.ToString("R",CultureInfo.InvariantCulture));
                return text.ToString();
            }
            internal void Calculate(out double seconds,out double contacts)
            {
                double returning=BoomerangReturn(StartSpeed,ReturnSpeed,Acceleration,Stop,SpeedScale);
                double life=Math.Min(returning,LifeLimit);
                if(!DpsNumbers.Finite(life)||life<=0||!DpsNumbers.Finite(ContactInterval)||ContactInterval<=0||Casts<=0||Casts>8192)
                    throw new InvalidOperationException("Invalid boomerang lifetime/contact data");
                contacts=Math.Max(1,Math.Ceiling(life/ContactInterval));
                double[] delays=null;
                if(returning<LifeLimit)
                {
                    delays=new double[Casts];
                    for(int cast=0;cast<Casts;cast++)delays[cast]=returning+cast*.15;
                }
                seconds=SustainedCastSeconds(Cooldown,Animation,Capacity,.5,delays);
            }
        }
        internal static double BoomerangReturn(double startSpeed,double returnSpeed,double acceleration,double stop,double speedScale)
        {
            if(!DpsNumbers.Finite(startSpeed)||!DpsNumbers.Finite(returnSpeed)||!DpsNumbers.Finite(acceleration)||!DpsNumbers.Finite(stop)||!DpsNumbers.Finite(speedScale)
                ||startSpeed<0||returnSpeed<=0||acceleration<=0||stop<0||speedScale<=0)
                throw new ArgumentOutOfRangeException("Invalid boomerang motion");
            // Stationary caster, straight unobstructed travel, no homing.
            // Native returns inside a radius sqrt(1.5), not at distance zero.
            double outward=startSpeed/acceleration;
            double distance=Math.Max(0,speedScale*startSpeed*startSpeed/(2*acceleration)-Math.Sqrt(1.5));
            double rampDistance=speedScale*returnSpeed*returnSpeed/(2*acceleration);
            double returning=distance<=rampDistance?Math.Sqrt(2*distance/(speedScale*acceleration)):
                returnSpeed/acceleration+(distance-rampDistance)/(speedScale*returnSpeed);
            return outward+stop+returning;
        }
        private sealed class Boundary
        {
            internal int Casts;
            internal double Time,Recovery;
            internal double[] Returns;
        }
        internal static double SustainedCastSeconds(double cooldown,double animation,int capacity,double refundFraction,double[] returnDelays)
        {
            if(!DpsNumbers.Finite(cooldown)||!DpsNumbers.Finite(animation)||!DpsNumbers.Finite(refundFraction)
                ||cooldown<=0||animation<=0||capacity<=0||capacity>256||refundFraction<0||refundFraction>1)
                throw new ArgumentOutOfRangeException("Invalid magic recharge schedule");
            if(returnDelays==null||returnDelays.Length==0||refundFraction==0)return Math.Max(cooldown,animation);
            if(returnDelays.Length>8192)throw new ArgumentOutOfRangeException("Too many magic returns per cast");
            foreach(double delay in returnDelays)if(!DpsNumbers.Finite(delay)||delay<=0)
                throw new ArgumentOutOfRangeException("Invalid magic return delay");
            var pending=new List<double>();
            var seen=new Dictionary<string,Boundary>();
            double time=0,recharge=double.PositiveInfinity,castReady=0;
            int ammo=capacity,casts=0,storedOffsets=0;
            for(int events=0;events<1000000;events++)
            {
                double nextReturn=pending.Count>0?pending[0]:double.PositiveInfinity;
                time=Math.Min(recharge,Math.Min(nextReturn,ammo>0?castReady:double.PositiveInfinity));
                if(!DpsNumbers.Finite(time))throw new InvalidOperationException("Magic recharge cannot advance");
                if(recharge<=time)
                {
                    ammo++;
                    recharge=ammo<capacity?time+cooldown:double.PositiveInfinity;
                }
                // All returns at this instant share the same capped progress;
                // they cannot create several ammo before the recharge update.
                int returned=0;
                while(returned<pending.Count&&pending[returned]<=time)returned++;
                if(returned>0)
                {
                    pending.RemoveRange(0,returned);
                    if(ammo<capacity)recharge=Math.Max(time,recharge-cooldown*refundFraction*returned);
                }
                if(recharge<=time)
                {
                    ammo++;
                    recharge=ammo<capacity?time+cooldown:double.PositiveInfinity;
                }
                if(ammo==0||castReady>time)continue;
                ammo--;casts++;
                if(double.IsPositiveInfinity(recharge))recharge=time+cooldown;
                castReady=time+animation;
                foreach(double delay in returnDelays)
                {
                    double arrival=time+delay;int at=pending.BinarySearch(arrival);
                    pending.Insert(at<0?~at:at,arrival);
                }
                if(pending.Count>8192)throw new InvalidOperationException("Too many overlapping magic returns");
                double recovery=recharge-time;
                var offsets=new double[pending.Count];
                var keyText=new StringBuilder().Append(ammo).Append(':').Append(Math.Round(recovery,8).ToString("R",CultureInfo.InvariantCulture));
                for(int i=0;i<offsets.Length;i++)
                {offsets[i]=pending[i]-time;keyText.Append(',').Append(Math.Round(offsets[i],8).ToString("R",CultureInfo.InvariantCulture));}
                string key=keyText.ToString();
                Boundary previous;
                if(seen.TryGetValue(key,out previous))
                {
                    bool same=Math.Abs(previous.Recovery-recovery)<=1e-9&&previous.Returns.Length==offsets.Length;
                    for(int i=0;same&&i<offsets.Length;i++)same=Math.Abs(previous.Returns[i]-offsets[i])<=1e-9;
                    if(same)return (time-previous.Time)/(casts-previous.Casts);
                }
                storedOffsets+=offsets.Length-(previous!=null?previous.Returns.Length:0);
                if(seen.Count>=10000||storedOffsets>1000000)throw new InvalidOperationException("Magic recharge cycle did not close within budget");
                seen[key]=new Boundary{Casts=casts,Time=time,Recovery=recovery,Returns=offsets};
            }
            throw new InvalidOperationException("Magic recharge event budget exceeded");
        }
    }
}
