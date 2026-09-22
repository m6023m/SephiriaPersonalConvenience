// Direct, additive stat mappings audited from native ApplyStatusInner bodies.
namespace SephiriaDicePreview
{
    internal static class BuffStatusMappings
    {
        internal static string[] Resolve(string type)
        {
            switch(type)
            {
                case "StatusInstance_AP": return new[]{"AP"};
                case "StatusInstance_AttackSpeed": return new[]{"ATTACKSPEED"};
                case "StatusInstance_BasicAttackDamage": return new[]{"BASICATTACKDAMAGEBONUS"};
                case "StatusInstance_BuffDuration": return new[]{"BUFFDURATION"};
                case "StatusInstance_CooldownRecoverySpeed": return new[]{"COOLDOWNRECOVERYSPEED"};
                case "StatusInstance_Critical": return new[]{"CRITICAL"};
                case "StatusInstance_CriticalDamageRate": return new[]{"CRITICALDAMAGEBONUS"};
                case "StatusInstance_DashAttackDamage": return new[]{"DASHATTACKDAMAGEBONUS"};
                case "StatusInstance_DashCount": return new[]{"DASHCOUNT"};
                case "StatusInstance_DashRecoverySpeed": return new[]{"DASHRECOVERY"};
                case "StatusInstance_DashSpeed": return new[]{"DASHSPEEDBONUSPERCENT"};
                case "StatusInstance_DebuffDuration": return new[]{"DEBUFFDURATION"};
                case "StatusInstance_Defense": return new[]{"DAMAGEREDUCTION"};
                case "StatusInstance_Evasion": return new[]{"EVASION"};
                case "StatusInstance_EXPDrop": return new[]{"EXPDROP"};
                case "StatusInstance_FinalAP": return new[]{"FINALAP"};
                case "StatusInstance_FinalDamage": return new[]{"ALLDAMAGEBONUS"};
                case "StatusInstance_FinalMP": return new[]{"FINALMP"};
                case "StatusInstance_FinalWeaponDamage": return new[]{"FINALWEAPONDAMAGE"};
                case "StatusInstance_FireDamage": return new[]{"FIREDAMAGE"};
                case "StatusInstance_HPPotionBonus": return new[]{"HPPOTIONBONUS"};
                case "StatusInstance_HPRegen": return new[]{"HPREGEN"};
                case "StatusInstance_HPSteal": return new[]{"HPSTEAL"};
                case "StatusInstance_IceDamage": return new[]{"ICEDAMAGE"};
                case "StatusInstance_LeafDrop": return new[]{"MONEYDROP"};
                case "StatusInstance_LightningDamage": return new[]{"LIGHTNINGDAMAGE"};
                case "StatusInstance_Luck": return new[]{"LUCK"};
                case "StatusInstance_MagicCritical": return new[]{"MAGICCRITICAL"};
                case "StatusInstance_MagicCriticalDamageRate": return new[]{"MAGICCRITICALDAMAGEBONUS"};
                case "StatusInstance_MinDarkCloud": return new[]{"MINDARKCLOUD"};
                case "StatusInstance_MPPotionBonus": return new[]{"MPPOTIONBONUS"};
                case "StatusInstance_MPRegen": return new[]{"MPREGEN"};
                case "StatusInstance_MPSteal": return new[]{"MPSTEAL"};
                // This native status updates the movement multiplier, not a custom
                // attack stat. Keep its value distinct while projecting sibling stats.
                case "StatusInstance_MoveSpeed": return new[]{DamageStatPreview.MoveSpeedPercentDeltaKey};
                case "StatusInstance_Negotiation": return new[]{"NEGOTIATION"};
                case "StatusInstance_PhysicalDamage": return new[]{"PHYSICALDAMAGE"};
                case "StatusInstance_SpecialAttackDamage": return new[]{"SPECIALATTACKDAMAGEBONUS"};
                case "StatusInstance_SweepCostReduction": return new[]{"SWEEPCOSTREDUCTION"};
                case "StatusInstance_Thorns": return new[]{"THORNS"};
                case "StatusInstance_TrueDamage": return new[]{"TRUEDAMAGE"};
                default: return null;
            }
        }
    }
}
