using System;
using System.Collections.Generic;

namespace SephiriaDicePreview
{
    // Immutable numeric inputs. No Unity object, status entity, delegate, or timer.
    internal sealed class BuffStatRule
    {
        internal struct Entry
        {
            internal readonly string Key;
            internal readonly int Value;
            internal readonly bool FixedWhileActive;
            internal Entry(string key,int value,bool fixedWhileActive)
            {Key=key.ToUpperInvariant();Value=value;FixedWhileActive=fixedWhileActive;}
        }
        private readonly Entry[] entries;
        internal BuffStatRule(IEnumerable<Entry> values){entries=new List<Entry>(values).ToArray();}
        internal Dictionary<string,int> Evaluate(int stack,float amplified)
        {
            var result=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var entry in entries)
            {
                int value=entry.FixedWhileActive?(stack>0?entry.Value:0):(int)(entry.Value*amplified*stack);
                int previous;result.TryGetValue(entry.Key,out previous);result[entry.Key]=previous+value;
            }
            return result;
        }
    }
}
