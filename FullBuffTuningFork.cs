namespace SephiriaDicePreview
{
    internal sealed class FullBuffTuningFork
    {
        internal readonly int Id;
        private readonly int maximum;
        private readonly float cooldown;
        private float readyAt;
        private int stack;
        private bool enhanced;
        internal int Stack {get{return enhanced?stack:0;}}
        internal FullBuffTuningFork(int id,int current,bool active,int limit,float wait,float interval)
        {Id=id;stack=current;enhanced=active;maximum=limit;readyAt=wait;cooldown=interval;}
        internal void OnMagic(float elapsed)
        {
            if(!(elapsed>=readyAt))return;
            readyAt=elapsed+cooldown;
            if(!enhanced){stack=1;enhanced=true;}
            else
            {
                stack++;
                if(stack<0)stack=0;
                else if(stack>maximum)stack=maximum;
            }
        }
    }
}
