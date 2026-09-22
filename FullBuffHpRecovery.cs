using System;

namespace SephiriaDicePreview
{
    // Numeric event forecast. The native Update loop quantizes these events to frames;
    // runtime comparisons must allow for the frame containing a threshold crossing.
    internal sealed class FullBuffHpRecovery
    {
        internal float Counter,RestoreAge,RestoreRemaining,RestoreRate;
        private void ClearRestore(){RestoreAge=0;RestoreRemaining=0;RestoreRate=0;}
        internal static float Heal(float hp,float maximum,float amount,int penalty,int hardPenalty)
        {
            if(hp>=maximum)return hp;
            amount-=amount*Math.Max(0,penalty)/100f;
            amount-=amount*Math.Max(0,hardPenalty)/100f;
            return Math.Min(maximum,hp+Math.Max(0,amount));
        }
        internal bool Advance(ref float hp,float maximum,float seconds,int regen,int penalty,int hardPenalty,bool restoreEnabled)
        {
            float rate=regen*.1f;
            int events=0;
            while(seconds>0)
            {
                if(hp>=maximum||!restoreEnabled)ClearRestore();
                if(hp>=maximum)return true;
                // Native natural recovery uses >1; damage restoration uses >=1.
                float natural=rate>0?Math.Max(0,1-Counter)/rate:float.PositiveInfinity;
                float restore=RestoreRemaining>0?Math.Max(0,1-RestoreAge):float.PositiveInfinity;
                bool naturalDue=natural<seconds;
                bool restoreDue=restore<=seconds;
                if(!naturalDue&&!restoreDue)
                {
                    Counter+=rate*seconds;
                    if(RestoreRemaining>0)RestoreAge+=seconds;
                    return true;
                }
                // Do not allow an unusual stat value to stall snapshot capture.
                if(++events>4096)return false;
                float step=Math.Min(naturalDue?natural:float.PositiveInfinity,restoreDue?restore:float.PositiveInfinity);
                Counter+=rate*step;
                if(RestoreRemaining>0)RestoreAge+=step;
                seconds-=step;
                if(naturalDue&&natural<=step)
                {
                    Counter=Math.Max(0,Counter-1);
                    hp=Heal(hp,maximum,1,penalty,hardPenalty);
                }
                if(hp>=maximum){ClearRestore();return true;}
                if(restoreDue&&restore<=step)
                {
                    RestoreAge=Math.Max(0,RestoreAge-1);
                    float amount=Math.Min(RestoreRemaining,RestoreRate);
                    float next=Heal(hp,maximum,amount,penalty,hardPenalty);
                    float actual=next-hp;
                    hp=next;
                    RestoreRemaining-=actual;
                    if(actual<=0||RestoreRemaining<=.001f||hp>=maximum)ClearRestore();
                }
            }
            return true;
        }
    }
}
