using System;

namespace SephiriaDicePreview
{
    // Numeric forecast only. No avatar writes, callbacks, timers or Unity objects.
    // Event times are continuous; frame-boundary differences need runtime validation.
    internal sealed class FullBuffMpRecovery
    {
        internal float Counter,HealFraction,DelayAge,DelayDuration;
        internal bool JustUsed;
        internal bool LimitExceeded {get;private set;}
        private int recoveryEvents;
        internal void Used(){JustUsed=true;DelayAge=0;}
        internal int Heal(int mp,float amount,int maximum,int multiple)
        {
            HealFraction+=amount*(100+multiple)/100f;
            if(HealFraction>1)
            {
                int whole=(int)HealFraction;HealFraction-=whole;
                mp=Math.Min(maximum,mp+whole);
            }
            return mp;
        }
        internal int Advance(int mp,float seconds,int maximum,int reserved,int baseMaximum,int regen,int multiple,int resonance,bool battle)
        {
            if(LimitExceeded)return mp;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)) {LimitExceeded=true;return mp;}
            int cap=Math.Max(0,maximum-reserved);
            if(mp>=cap)return Math.Min(mp,cap);
            if(JustUsed)
            {
                float speed=100f+resonance;
                if(speed<=0){DelayAge+=seconds*speed;return mp;}
                float wait=Math.Max(0,DelayDuration-DelayAge)/speed;
                if(seconds<wait){DelayAge+=seconds*speed;return mp;}
                seconds-=wait;DelayAge=0;JustUsed=false;
            }
            while(seconds>0&&mp<cap)
            {
                float rate=regen;
                if(!battle)
                {
                    float ratio=(float)mp/baseMaximum;
                    rate+=ratio<.33333f?240:ratio<.75f?190:140;
                }
                rate*=.1f;
                if(rate<=0){Counter+=rate*seconds;break;}
                float until=Math.Max(0,1-Counter)/rate;
                if(seconds<=until){Counter+=rate*seconds;break;}
                // Shared across the whole preview, not reset for every cast or buff tick.
                if(++recoveryEvents>4096){LimitExceeded=true;return mp;}
                seconds-=until;Counter=Math.Max(0,Counter-1);
                float gain=(100f+multiple)/100f;
                mp=Heal(mp,1,cap,multiple);
                if(gain<=0)
                {
                    // No MP change can alter the out-of-combat rate in this case.
                    float accumulated=Counter+rate*seconds;
                    int events=Math.Max(0,(int)Math.Ceiling(accumulated)-1);
                    Counter=accumulated-events;HealFraction+=events*gain;break;
                }
            }
            return mp;
        }
    }
}
