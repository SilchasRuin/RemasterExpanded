using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.Remaster.FeatsDb;

namespace RemasterExpanded.ClassChangesAndFeats;

public static class FighterFeats
{
    public static void Load()
    {
        TrueFeat lunge = (TrueFeat)AllFeats.GetFeatByFeatName(FeatName.Lunge);
        lunge.Traits.Add(ModManager.ModBeingLoadedTrait ?? Trait.None);
        lunge.OnCreature = null;
        lunge.RulesText =
            "Make a Strike with a melee weapon, increasing your reach by 5 feet for that Strike." +
            "\n\nIf the weapon has the disarm, shove, or trip trait, you can use the corresponding action instead of a Strike.";
        FeatLoader.LungeLogic(lunge);
    }

    public static IEnumerable<Feat> LoadFeats()
    {
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_CertainStrike", "Certain Strike"), 10,
            "Even when you don't hit squarely, you can still score a glancing blow.",
            "Make a melee Strike. It gains the following failure effect.\n\n" +
            "{b}Failure{/b} Your attack deals any damage it would have dealt on a hit, excluding all damage dice. (This removes damage dice from weapon runes, spells, and special abilities, in addition to weapon damage dice.)",
            [Trait.Fighter, Trait.Press])
            .WithActionCost(1)
            .WithPermanentQEffect("You can make a Strike that deals some damage even on a miss.", 
                qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideStrikeModifier = weapon =>
                {
                    if (!weapon.HasTrait(Trait.Melee))
                        return null;
                    StrikeModifiers strikeModifiers = new()
                    {
                        OnEachTarget = async (self, target, result) =>
                        {
                            if (result != CheckResult.Failure)
                                return;
                            QEffect removeDice = new()
                            {
                                YouDealDamageEvent = async (_, damage) =>
                                {
                                    foreach (KindedDamage kindedDamage in damage.KindedDamages)
                                    {
                                        DiceFormula? dice = RangerFeats.NoDice(kindedDamage.DiceFormula);
                                        kindedDamage.DiceFormula = dice;
                                    }

                                    damage.KindedDamages.RemoveAll(kd => kd.DiceFormula == null);
                                }
                            };
                            CombatAction strike = StrikeRules.CreateStrike(self, weapon, RangeKind.Melee, -1);
                            if (strike.EffectOnOneTarget == null)
                                return;
                            self.AddQEffect(removeDice);
                            await strike.EffectOnOneTarget.Invoke(strike, self, target, CheckResult.Success);
                            removeDice.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    CombatAction certainStrike = StrikeRules.CreateStrike(owner, weapon, RangeKind.Melee, -1, false, strikeModifiers);
                    certainStrike.WithFullRename("Certain Strike");
                    certainStrike.WithExtraTrait(Trait.Press).WithExtraTrait(Trait.Basic)
                        .WithDescription(StrikeRules.CreateBasicStrikeDescription2(strikeModifiers, additionalFailureText: "Your attack deals any damage it would have dealt on a hit, excluding all damage dice. (This removes damage dice from weapon runes, spells, and special abilities, in addition to weapon damage dice.)"));
                    certainStrike.Illustration = new SideBySideIllustration(weapon.Illustration, IllustrationName.WinningStreak);
                    return certainStrike;
                };
            });
    }
}