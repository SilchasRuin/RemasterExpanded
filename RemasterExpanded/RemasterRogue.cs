using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;

namespace RemasterExpanded;

public class RemasterRogue
{
    public static void AddRemasteredGangUp()
    {
        AllFeats.GetFeatByFeatName(FeatName.GangUp).WithOnCreature(cr =>
        {
            QEffect tech = new()
            {
                StateCheck = effect =>
                {
                    Creature self = effect.Owner;
                    foreach (Creature enemy in self.Battle.AllCreatures.Where(enemy => enemy.DistanceToWith10FeetException(self) <= self.Space.ActualReach))
                    {
                        if (FlankingRules.SimplifiedCanAttack(self))
                            enemy.AddQEffect(FlankedBy(effect.Owner));
                    }
                }
            };
            cr.AddQEffect(tech);
        });
    }

    //new flanked by QEffect that applies to allies of the flanker. This removes the need to foreach twice and to dummy the QF Id.
    public static QEffect FlankedBy(Creature flanker)
    {
        return new QEffect("Flanked", "[this condition has no description]", ExpirationCondition.Ephemeral, flanker)
        {
            IsFlatFootedTo = (qEffect, attacker, combatAction) => attacker == null || !attacker.FriendOf(flanker) || attacker == flanker || combatAction == null || !combatAction.HasTrait(Trait.Melee) && !combatAction.HasTrait(Trait.VersatileMelee) || qEffect.Owner.HasEffect(QEffectId.DenyAdvantage) && attacker.Level <= qEffect.Owner.Level || qEffect.Owner.HasEffect(QEffectId.AllAroundVision) ? null : "flanking",
            
        };
    }

}