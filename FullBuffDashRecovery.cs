using System;

namespace SephiriaDicePreview
{
    internal sealed class FullBuffDashRecovery
    {
        private int used,events;
        private float age;
        private readonly float interval;
        private readonly bool supported;
        internal FullBuffDashRecovery(int used,float age,float interval,bool repeating,bool once)
        {this.used=used;this.age=age;this.interval=interval;supported=repeating&&!once&&interval>0;}
        internal bool Advance(float seconds,int recovery)
        {
            if(used<=0||seconds<=0)return true;
            if(!supported)return false;
            float rate=(float)(recovery+100)/100f;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
            if(rate<=0){age+=seconds*rate;return true;}
            while(seconds>0&&used>0)
            {
                float remaining=Math.Max(0,interval-age)/rate;
                if(seconds<remaining){age+=seconds*rate;return true;}
                if(++events>4096)return false;
                seconds-=remaining;age=0;used--;
            }
            return true;
        }
        internal bool TryUse(int fixedCount,int count,int infinity)
        {
            int maximum=fixedCount>0?fixedCount:count;
            if(used<maximum){used++;return true;}
            return infinity>0;
        }
    }
}
