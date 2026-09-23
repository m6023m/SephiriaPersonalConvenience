using System;
using System.Collections.Generic;
using System.Text;

namespace SephiriaDicePreview
{
    // The single boundary between equipped artifacts and weapon damage previews.
    // Only effects that create one separate damage instance for every successful
    // DirectAttack contact belong here. Cooldown, charge and chance based effects
    // keep their own artifact DPS cycles so they are not multiplied per contact.
    internal static class AttackLinkedArtifactDamage
    {
        internal sealed class Addition
        {
            internal DamageTooltip.Hit Hit;
        }

        internal static List<Addition> PerDirectHit(PlayerAvatar player)
        {
            var result=new List<Addition>();
            if(!player||!player.Inventory)return result;
            foreach(var item in player.Inventory.inventoryMatrix.Values)
            {
                if(item==null||!item.Charm||!item.Charm.IsEffectEnabled)continue;
                AddForCharm(result,item.Charm,player,true,null);
            }
            var preview=RewardDpsPrediction.PreviewCharm;
            if(preview)AddForCharm(result,preview,player,true,RewardDpsPrediction.PreviewLevel);
            return result;
        }

        // Kept separate from inventory traversal so every new per-hit artifact
        // is registered and formula-tested at this one point.
        internal static List<Addition> ForCharm(Charm_Basic charm,PlayerAvatar player)
        {
            var result=new List<Addition>();
            AddForCharm(result,charm,player,false,null);
            return result;
        }

        // Adds the modeled instances to the attack sample consumed by both the
        // tooltip total and DpsProjectiles. DpsProjectiles multiplies additions
        // by DirectContacts without applying the weapon projectile ratio again.
        internal static void AppendToWeaponAttack(PlayerAvatar player,List<DamageTooltip.Hit> hits)
        {
            foreach(var addition in PerDirectHit(player))
                hits.Add(addition.Hit);
        }

        internal static string DpsCondition(PlayerAvatar player)
        {
            var additions=PerDirectHit(player);
            if(additions.Count==0)return null;
            return "장착 효과 피해 포함 · 직접 공격 적중마다 별도 판정";
        }

        private static void AddForCharm(List<Addition> result,Charm_Basic source,PlayerAvatar player,bool connected,int? previewLevel)
        {
            AddTyphoon(result,source as Charm_TheTyphoonSheetmusic,player,connected,previewLevel);
        }

        private static void AddTyphoon(List<Addition> result,Charm_TheTyphoonSheetmusic charm,PlayerAvatar player,bool connected,int? previewLevel)
        {
            if(!charm||charm.damageByLevel==null||charm.damageByLevel.Length==0)return;
            int index=Math.Max(0,Math.Min(charm.damageByLevel.Length-1,previewLevel.HasValue?charm.LevelToIdx(previewLevel.Value):charm.CurrentLevelToIdx()));
            result.Add(new Addition{
                Hit=new DamageTooltip.Hit{
                    Raw=charm.damageByLevel[index],
                    Element=EDamageElementalType.Lightning,
                    Factors=new[]{ArtifactDamageProfiles.RootBonus(connected?charm:null,player)}
                }
            });
        }
    }
}
