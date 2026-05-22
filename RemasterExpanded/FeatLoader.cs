using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Modding;
using RemasterExpanded.MyArchetypes;

namespace RemasterExpanded;

public class FeatLoader
{
    public static IEnumerable<Feat> LoadFeats()
    {
        TrueFeat robustHealth = new(ModManager.RegisterFeatName("RE_RobustHealth", "Robust Health"), 3, "Your physiology responds well to first aid.",
            "You gain a circumstance bonus to the number of Hit Points you regain equal to your level from a successful attempt to Treat your Wounds or use Battle Medicine on you. After you or an ally use Battle Medicine on you, you become temporarily immune to that Battle Medicine for only 1 encounter, instead of 1 day.",
            [Trait.General]);
        RobustHealthLogic(robustHealth);
        yield return robustHealth;
        // TrueFeat spearDancer = new(ModManager.RegisterFeatName("RE_SpearDancer", "Spear Dancer"), 6, "You favor weapons that allow you to lash out viciously while keeping enemies at bay, giving you an opportunity to strike without fear of reprisal.",
        //     "You Step and then Strike with a spear or polearm you are wielding, or Strike with the spear or polearm and then Step.",
        //     [Trait.Fighter]);
        // SpearDancerLogic(spearDancer);
        // yield return spearDancer;
        foreach (Feat feat in Pirate.PirateFeats())
        {
            yield return feat;
        }
        foreach (Feat feat in VikingGuard.VikingGuardFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in BlackJacket.BlackJacketFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in Viking.VikingFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in NewDeities.LoadDeities())
        {
            yield return feat;
        }
    }
    public static void RobustHealthLogic(TrueFeat feat)
    {
        feat.WithPermanentQEffect(
            "You gain a circumstance bonus to the number of Hit Points you regain equal to your level from a successful attempt to use Battle Medicine on you. After you or an ally use Battle Medicine on you, you become temporarily immune to that Battle Medicine for only 1 encounter, instead of 1 day.",
            qf =>
            {
                Creature self = qf.Owner;
                qf.StartOfCombat = _ =>
                {
                    foreach (Creature ally in self.Battle.AllCreatures.Where(creature => creature.PersistentCharacterSheet != null))
                    {
                        self.PersistentUsedUpResources.UsedUpActions.RemoveAll(str => str.Contains("BattleMedicineFrom:" + ally.Name));
                    }
                    return Task.CompletedTask;
                };
                qf.BonusToSelfHealing = (_, action) =>
                {
                    if (action == null || !action.Name.Contains("Battle Medicine") || action.CheckResult <= CheckResult.Failure) return null;
                    return new Bonus(self.Level, BonusType.Circumstance, "Robust Health", true);
                };
            });
    }

    // public static void SpearDancerLogic(TrueFeat feat)
    // {
    //     feat.WithPermanentQEffect("You can Step before or after your Strike with a spear or polearm.", qf =>
    //     {
    //         Creature self = qf.Owner;
    //         qf.Name = "Spear Dancer {icon:Action}";
    //         qf.StateCheck = _ =>
    //         {
    //             self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
    //             {
    //                 ProvideStrikeModifier = weapon =>
    //                 {
    //                     if (!weapon.Traits.Any(trait => trait is Trait.Polearm or Trait.Spear)) return null;
    //                     return new CombatAction(self,
    //                         new SideBySideIllustration(IllustrationName.Walk, weapon.Illustration), "Step, then Strike",
    //                         [Trait.Fighter, Trait.Basic],
    //                         "{i}You favor weapons that allow you to lash out viciously while keeping enemies at bay, giving you an opportunity to strike without fear of reprisal.{/i}\n\nStep, then Strike with a spear or polearm.",
    //                         Target.Self()).WithActionCost(1).WithEffectOnChosenTargets(async (spell, caster, _) =>
    //                     {
    //                         if (!await caster.StepAsync("Make a Step. Then you will Strike.", true))
    //                         {
    //                             spell.RevertRequested = true;
    //                         }
    //                         else
    //                         {
    //                             await caster.Battle.GameLoop.FullCast(caster.CreateStrike(weapon).WithActionCost(0));
    //                         }
    //                     });
    //                 }
    //             });
    //             self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
    //             {
    //                 ProvideStrikeModifier = weapon =>
    //                 {
    //                     CombatAction strike = qf.Owner.CreateStrike(weapon);
    //                     strike.Name = "Strike, then Step";
    //                     strike.Illustration = new SideBySideIllustration(strike.Illustration,
    //                         IllustrationName.Walk);
    //                     strike.Traits.Add(Trait.Basic);
    //                     strike.Description = StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
    //                         additionalAftertext: "Make a Step.");
    //                     strike.EffectOnChosenTargets = Delegates.SmartCombineDelegates(strike.EffectOnChosenTargets,
    //                         async (_, caster, _) =>
    //                             await caster.StepAsync("Make a Step.", allowPass: true));
    //                     return weapon.Traits.Any(trait => trait is Trait.Polearm or Trait.Spear) ? strike : null;;
    //                 }
    //             });
    //         };
    //     });
    // }
}