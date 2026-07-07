using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;

namespace RemasterExpanded;

public class SorcerousPotency
{
    public static void Load()
    {
        Feat dangerousSorcery =  AllFeats.GetFeatByFeatName(FeatName.DangerousSorcery);
        dangerousSorcery.WithCustomName("Sorcerous Potency");
        dangerousSorcery.FlavorText = "Because of the magical power inherent in your blood, your spells that hurt or cure are stronger than those of other spellcasters.";
        dangerousSorcery.RulesText = "When you Cast a Spell from your spell slots that either deals damage or restores Hit Points, you gain a status bonus to that spell's damage or healing equal to the spell's rank. This applies only to the initial damage or healing the spell deals when cast.";
        dangerousSorcery.OnCreature = null;
        dangerousSorcery.WithPermanentQEffect("Your damage spells deal extra damage and your healing spells heal for more.", effect =>
        {
            effect.BonusToDamage = (_, spell, _) =>
                spell.HasTrait(Trait.Spell) && !spell.HasTrait(Trait.Cantrip) && !spell.HasTrait(Trait.Focus) &&
                spell.CastFromScroll == null && spell.SpellcastingSource?.Kind != SpellcastingKind.Innate
                    ? new Bonus(spell.SpellLevel, BonusType.Status, "Sorcerous Potency")
                    : null;
            effect.AddGrantingOfTechnical(_ => true, qfTech =>
            {
                qfTech.BonusToSelfHealing = (_, spell) => spell != null && spell.Owner == effect.Owner &&
                                                          spell.HasTrait(Trait.Spell) && !spell.HasTrait(Trait.Cantrip) && !spell.HasTrait(Trait.Focus) &&
                                                          spell.CastFromScroll == null && spell.SpellcastingSource?.Kind != SpellcastingKind.Innate
                    ? new Bonus(spell.SpellLevel, BonusType.Status, "Sorcerous Potency")
                    : null;
            });
        });
    }
}