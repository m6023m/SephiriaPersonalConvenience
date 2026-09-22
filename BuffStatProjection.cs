using System.Collections.Generic;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class BuffStatProjection
    {
        internal static bool TryRead(CharacterBuff buff,int stack,float amplified,out Dictionary<string,int> stats)
        {
            BuffStatRule rule;stats=null;
            if(!TryCapture(buff,out rule))return false;
            stats=rule.Evaluate(stack,amplified);return true;
        }
        internal static bool TryCapture(CharacterBuff buff,out BuffStatRule rule)
        {
            var values=new List<BuffStatRule.Entry>();rule=null;
            if(!buff)return false;
            var raw=buff as CharacterBuff_StatusUnsafe;
            if(raw && buff.GetType()==typeof(CharacterBuff_StatusUnsafe))
            {
                foreach(var entry in raw.add)Add(values,entry.id,entry.value);
            }
            else if(buff.GetType()==typeof(CharacterBuff_CustomStatusInstance))
            {
                foreach(var entry in ((CharacterBuff_CustomStatusInstance)buff).add)
                    Add(values,entry.id,entry.value);
            }
            // FrostTouch inherits the stat application unchanged; its override only
            // updates visual effects and exposes a separate maximum-stack setter.
            else if(buff.GetType()==typeof(CharacterBuff_StatusInstance)||buff.GetType()==typeof(CharacterBuff_StatusInstance_FrostTouch))
            {
                foreach(var entry in ((CharacterBuff_StatusInstance)buff).add)
                {
                    int value=entry.value;
                    var status=StatusDatabase.CreateStatusEntity(entry.id,value);
                    if(status==null)return false;
                    string[] names;
                    if(status.GetType()==typeof(StatusInstance_MaxMP))names=new[]{DamageStatPreview.MaxMpDeltaKey};
                    else if(status.GetType()==typeof(StatusInstance_MaxHP))names=new[]{DamageStatPreview.MaxHpDeltaKey};
                    else if(status.GetType()==typeof(StatusInstance_FinalHP))names=new[]{DamageStatPreview.FinalHpDeltaKey};
                    else if(status.GetType()==typeof(StatusInstance_Custom))
                    {
                        string name=(string)AccessTools.Field(typeof(StatusInstance_Custom),"customID").GetValue(status);
                        names=string.IsNullOrEmpty(name)?new string[0]:new[]{name};
                    }
                    else names=BuffStatusMappings.Resolve(status.GetType().Name);
                    if(names==null)return false;
                    foreach(string name in names)Add(values,name,value);
                }
            }
            else if(buff.GetType()==typeof(CharacterBuff_Critical))Add(values,"CRITICAL",((CharacterBuff_Critical)buff).critical);
            else if(buff.GetType()==typeof(CharacterBuff_PhysicalDamage))Add(values,"PHYSICALDAMAGE",((CharacterBuff_PhysicalDamage)buff).basicAttackDamage);
            else if(buff.GetType()==typeof(CharacterBuff_AP))Add(values,"AP",((CharacterBuff_AP)buff).abilityPower);
            else if(buff.GetType()==typeof(CharacterBuff_AllAttackDamage_FromHpLossCharm))Add(values,"ALLDAMAGEBONUS",1);
            else if(buff.GetType()==typeof(CharacterBuff_IncreaseCriticalChance_FromMpLossCharm))Add(values,"CRITICAL",100);
            else if(buff.GetType()==typeof(CharacterBuff_WeaponMaxDamageUp))
                Add(values,DamageStatPreview.ReservedMpDeltaKey,((CharacterBuff_WeaponMaxDamageUp)buff).mana,true);
            // Storm is a marker with no overridden stat application. Invincibility
            // changes incoming-hit acceptance, not an outgoing damage stat.
            else if(buff.GetType()==typeof(CharacterBuff_Storm)||buff.GetType()==typeof(CharacterBuff_Invincible)||buff.GetType()==typeof(CharacterBuff_Invulnerable)) { }
            // Periodic recovery is projected on FullBuffPlan's timeline, not as stats.
            else if(buff.GetType()==typeof(CharacterBuff_Heal)||buff.GetType()==typeof(CharacterBuff_MPHeal)) { }
            // Barrier changes shield resources, not additive attack stats.
            else if(buff.GetType()==typeof(CharacterBuff_Shield)) { }
            // Lightning armor deals a separate periodic hit; it adds no attack stat.
            // Its outgoing hit is rendered separately by MagicDamageProfiles.
            else if(buff.GetType()==typeof(CharacterBuff_LightningArmor)) { }
            // These native types contain only an ID, with no stat or attack override.
            else if(buff.GetType()==typeof(CharacterBuff_Sober)||buff.GetType()==typeof(CharacterBuff_Sober_Tier3_B)||
                buff.GetType()==typeof(CharacterBuff_GreatSword_Twin_AttackSpeed)||buff.GetType()==typeof(CharacterBuff_GreatSword_Twin_Tired)) { }
            else return false;
            rule=new BuffStatRule(values);return true;
        }
        private static void Add(List<BuffStatRule.Entry> values,string id,int value,bool fixedWhileActive=false)
        {
            values.Add(new BuffStatRule.Entry(id,value,fixedWhileActive));
        }
    }
}
