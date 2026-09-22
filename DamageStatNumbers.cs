using System;
using System.Collections.Generic;

namespace SephiriaDicePreview
{
    // Captured inputs only. This class never holds or reads a Unity object.
    internal sealed class DamageStatNumbers
    {
        private readonly Dictionary<string,int> values=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> amplification=new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly string[] elements={"PHYSICAL","FIRE","ICE","LIGHTNING"};
        private static readonly string[] damageKeys={"PHYSICALDAMAGE","FIREDAMAGE","ICEDAMAGE","LIGHTNINGDAMAGE"};
        private static readonly string[,] conversionKeys=BuildConversionKeys();
        internal DamageStatNumbers(IEnumerable<KeyValuePair<string,int>> basic,IEnumerable<KeyValuePair<string,int>> calculated,IEnumerable<KeyValuePair<string,int>> amps)
        {
            AddValues(basic);AddValues(calculated);
            foreach(var pair in amps)amplification[pair.Key]=pair.Value;
        }
        private void AddValues(IEnumerable<KeyValuePair<string,int>> source)
        {
            foreach(var pair in source)
            {int old;values.TryGetValue(pair.Key,out old);values[pair.Key]=old+pair.Value;}
        }
        private static string[,] BuildConversionKeys()
        {
            var result=new string[elements.Length,elements.Length];
            for(int source=0;source<elements.Length;source++)
                for(int target=0;target<elements.Length;target++)
                    if(source!=target)result[source,target]=elements[source]+"TO"+elements[target];
            return result;
        }
        internal int Raw(string id,IDictionary<string,int> changes)
        {
            int value,delta=0,amp;values.TryGetValue(id,out value);
            if(changes!=null)changes.TryGetValue(id,out delta);
            value+=delta;
            if(value==0)return 0;
            amplification.TryGetValue(id,out amp);
            return (int)((float)(value*(100+amp))/100f);
        }
        private bool Conversion(int source,IDictionary<string,int> changes,out int target,out int percent)
        {
            target=-1;percent=0;
            for(int candidate=0;candidate<elements.Length;candidate++)
            {
                if(candidate==source)continue;
                int amount=Raw(conversionKeys[source,candidate],changes);
                if(amount>percent){target=candidate;percent=amount;}
            }
            return target>=0;
        }
        internal int Read(string id,IDictionary<string,int> changes)
        {
            int element=Array.IndexOf(damageKeys,id);
            if(element<0)return Raw(id,changes);
            int result=Raw(id,changes),target,percent;
            if(result>20&&Conversion(element,changes,out target,out percent))result=20;
            for(int source=0;source<elements.Length;source++)
            {
                if(source==element||!Conversion(source,changes,out target,out percent)||target!=element)continue;
                int excess=Raw(damageKeys[source],changes)-20;
                if(excess>0)result+=(int)((float)(excess*percent)/100f);
            }
            return result;
        }
    }
}
