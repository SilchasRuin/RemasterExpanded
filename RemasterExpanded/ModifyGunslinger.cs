using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Roller;
using Dawnsbury.Modding;

namespace RemasterExpanded;

public class ModifyGunslinger
{
    public static void Load()
    {
        if (ModManager.TryParse("Singular Expertise", out FeatName singular))
        {
            if (AllFeats.GetFeatByFeatName(singular) is { } singularExpertise)
            {
                singularExpertise.WithCustomName("Slinger's Precision");
                singularExpertise.FlavorText =
                    "You have steady precision with guns and crossbows and can use weapons that incorporate them effectively.";
                singularExpertise.RulesText = "You deal an extra +2 precision damage with Strikes made using crossbows that don't have the repeating trait, and deal an extra 1d4 precision damage on Strikes with firearms that don't have the repeating trait." +
                                              "\n\nIf you have gunslinging legend, you instead deal +3 additional precision damage with Strikes using crossbows that aren't repeating, and you deal an additional 1d6 precision damage with non-repeating firearm Strikes.";
                singularExpertise.OnCreature = null;
                singularExpertise.WithPermanentQEffect("You deal additional precision damage with guns and crossbows.",qf =>
                {
                    qf.YourStrikeMayDealPrecisionDamage = (_, attack, _) =>
                    {
                        if (attack.Item == null ||
                            attack.Item.HasTrait(Trait.Repeating))
                            return null;
                        if (attack.Item.HasTrait(Trait.Firearm))
                            return qf.Owner.Proficiencies.Get(Trait.Firearm) >= Proficiency.Legendary ? DiceFormula.FromText("1d6", "Slinger's Precision") : DiceFormula.FromText("1d4", "Slinger's Precision");
                        if (attack.Item.HasTrait(Trait.Crossbow))
                            return qf.Owner.Proficiencies.Get(Trait.Firearm) >= Proficiency.Legendary ? DiceFormula.FromText("3", "Slinger's Precision") : DiceFormula.FromText("2", "Slinger's Precision");
                        return null;
                    };
                });
            }
        }

        if (ModManager.TryParse("GunslingerClassFeat", out FeatName gunslinger))
        {
            if (AllFeats.GetFeatByFeatName(gunslinger) is { } gunslingerClass)
            {
                gunslingerClass.RulesText = gunslingerClass.RulesText.Replace("Singular Expertise", "Slinger's Precision").Replace("You have particular expertise with guns and crossbows that grants you greater proficiency with them and the ability to deal more damage. You gain a +1 circumstance bonus to damage rolls with firearms and crossbows.", "You have steady precision with guns and crossbows and can use weapons that incorporate them effectively. You deal an extra +2 precision damage with Strikes made using crossbows that don't have the repeating trait, and deal an extra 1d4 precision damage on Strikes with firearms that don't have the repeating trait. If you have gunslinging legend, increase the damage to +3 with crossbows and to an extra 1d6 with firearms.");
            }
        }
        
    }
}