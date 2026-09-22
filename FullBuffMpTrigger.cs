using System;

namespace SephiriaDicePreview
{
    // Numeric state shared by duplicate subscriptions of one equipment instance.
    internal sealed class FullBuffMpTrigger
    {
        private int counter;
        private float readyAt;
        private readonly int maximum,required;
        internal readonly float Cooldown;
        internal readonly bool Water;
        internal FullBuffMpTrigger(int stack,int maximum,float readyAt,float cooldown)
        {counter=stack;this.maximum=maximum;this.readyAt=readyAt;Cooldown=cooldown;Water=true;}
        internal FullBuffMpTrigger(int counter,int required)
        {this.counter=counter;this.required=required;}
        internal int OnUse(int requested,float elapsed)
        {
            if(Water)
            {
                if(requested<=0||!(elapsed>=readyAt)||counter>=maximum)return 0;
                int previous=Clamp(counter,0,maximum);
                counter++;readyAt=elapsed+Cooldown;
                return Clamp(counter,0,maximum)-previous;
            }
            if(required<=0)throw new InvalidOperationException("MP 소모 버프의 발동 기준 확인 필요");
            if(requested>0)counter+=requested;
            if(counter<required)return 0;
            int stacks=counter/required;counter%=required;return stacks;
        }
        private static int Clamp(int value,int minimum,int maximum)
        {return value<minimum?minimum:value>maximum?maximum:value;}
    }
}
