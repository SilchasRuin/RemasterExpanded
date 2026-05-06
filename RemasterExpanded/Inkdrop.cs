using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Modding;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace RemasterExpanded;

public class Inkdrop
{
    public static Creature CreateInkdrop()
    {
        return new Creature(ModData.MIllustrations.CreateIllustration("Inkdrop"), "Inkdrop", [Trait.Construct, Trait.Small, Trait.Mindless],
            -1, 6, 2, new Defenses(7, 2, 6, 5), 15, new Abilities(0, -3, 2, -5, 0, -5), new Skills(athletics: 2, stealth: 3))
            .AddQEffect(QEffect.TraitImmunity(Trait.Mental))
            .AddQEffect(QEffect.TraitImmunity(Trait.PrecisionDamage))
            .AddQEffect(QEffect.TraitImmunity(Trait.Positive))
            .AddQEffect(QEffect.TraitImmunity(Trait.Negative))
            .AddQEffect(QEffect.ImmunityToCondition(QEffectId.Unconscious))
            .AddQEffect(new QEffect("Blinding Ink", "A creature critically struck by the inkdrop's pseudopod is blinded by the ink until it clears its eyes using an Interact action.")
            {
                AdjustStrikeAction = (_, action) =>
                {
                    action.EffectOnOneTarget += async (_, _, target, result) =>
                    {
                        if (result <= CheckResult.Success) return;
                        target.AddQEffect(QEffect.QuenchableBlinded("Blinding Ink").WithExpirationNever());
                    };
                }
            })
            .AddQEffect(new QEffect("Blotting", "While a creature has the inkdrop grabbed, it can destroy the inkdrop by smudging the inkdrop out with an Interact action.")
            {
                StateCheck = effect =>
                {
                    if (!effect.Owner.HasEffect(QEffectId.Grappled)) return;
                    Creature? grappler = effect.Owner.Battle.AllCreatures.FirstOrDefault(cr => cr.HeldItems.Any(item => item.Grapplee == effect.Owner));
                    if (grappler == null) return;
                    grappler.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        ProvideContextualAction = _ =>
                        {
                            return new ActionPossibility(new CombatAction(grappler, effect.Owner.Illustration,
                                "Blot Out", [Trait.Manipulate],
                                "You can destroy the Inkdrop by blotting it out with an Interact action.",
                                Target.Self()).WithActionCost(1).WithEffectOnChosenTargets(async (_, _, _) =>
                            {
                                effect.Owner.Die();
                            }));
                        }
                    });
                }
            })
            .WithImmunityToCriticalHits()
            .WithCharacteristics(false, false)
            .Builder
            .AddNaturalWeapon(NaturalWeaponKind.Pseudopod, 6,[Trait.Unarmed],"1d4", DamageKind.Bludgeoning).Done();
    }

    public static void AddInkdrop()
    {
        ModManager.RegisterNewCreature("Inkdrop", CreateInkdrop);
    }
}