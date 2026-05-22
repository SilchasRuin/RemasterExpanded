using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public abstract class NewSpells4th : NewSpells
{
    public static void Load()
    {
        WeaponStorm = ModManager.TryParse("WeaponStorm", out SpellId storm) ? storm : ModManager.RegisterNewSpell("RE_WeaponStorm", 4, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("WeaponStorm"), "Weapon Storm", [Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Primal],
                    "You swing a weapon you're holding, and the weapon magically multiplies into duplicates that swipe at all creatures in either a cone or an emanation.",
                    $"This spell deals {IntToString(level)} dice of damage to creatures in the area with a Reflex save. This damage has the same type as a weapon you are wielding and uses the same die size. Determine the die size as if you were attacking with the weapon."+
                    S.FourDegreesOfSuccess("The creature is unaffected.", "The target takes half damage.", "The target takes full damage.", "The target takes double damage and is subject to the weapon's critical specialization effect."),
                    Target.DependsOnSpellVariant(variant => variant.Id == "EmanationWS" ? Target.SelfExcludingEmanation(2).WithAdditionalRequirementOnCaster(creature => creature.HeldItems.Any(item => item.WeaponProperties != null) ? Usability.Usable : Usability.NotUsable("You must be wielding a weapon.")) : 
                            Target.Cone(6).WithAdditionalRequirementOnCaster(creature => creature.HeldItems.Any(item => item.WeaponProperties != null) ? Usability.Usable : Usability.NotUsable("You must be wielding a weapon.")))
                        .WithOverriddenFullTargetLine("{b}Area{/b} 10-foot emanation or 30-foot cone"), level, SpellSavingThrow.Standard(Defense.Reflex))
                .WithVariants([
                    new SpellVariant("EmanationWS", "Weapon Storm - Emanation", new SideBySideIllustration(MIllustrations.CreateIllustration("WeaponStorm"), IllustrationName.SeekBurst)),
                    new SpellVariant("ConeWS", "Weapon Storm - Cone", new SideBySideIllustration(MIllustrations.CreateIllustration("WeaponStorm"), IllustrationName.SeekCone))
                ])
                .WithActionCost(2).WithSoundEffect(SfxName.SwordStrike)
                .WithHeighteningNumerical(level, 4, inCombat, 1, "Add another damage die.")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    Item? weapon = caster.HeldItems.MaxBy(item => item.WeaponProperties?.DamageDieSize);
                    if (weapon?.WeaponProperties == null)
                    {
                        spell.RevertRequested = true;
                        return;
                    }
                    int dieSize = weapon.WeaponProperties.DamageDieSize;
                    List<DamageKind> kinds = [weapon.WeaponProperties.DamageKind];
                    if (weapon.HasTrait(Trait.VersatileB))
                        kinds.Add(DamageKind.Bludgeoning);
                    if (weapon.HasTrait(Trait.VersatileP))
                        kinds.Add(DamageKind.Piercing);
                    if (weapon.HasTrait(Trait.VersatileS))
                        kinds.Add(DamageKind.Slashing);
                    DamageKind kind = target.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(kinds);
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText($"{level}d{dieSize}", "Weapon Storm"), kind);
                    if (result == CheckResult.CriticalFailure)
                        await CommonAbilityEffects.CriticalSpecializationEffect(caster.CreateStrike(weapon), target);
                });
        });
        ModManager.RegisterActionOnEachSpell(spell =>
        {
            if (ModManager.TryParse("WeaponStorm", out SpellId weapon) && spell.SpellId == weapon)
                spell.Illustration = MIllustrations.CreateIllustration("WeaponStorm");
        });
        VisionOfDeath = ModManager.RegisterNewSpell("RE_VisionOfDeath", 4, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Vision"), "Vision of Death", [Trait.Concentrate, Trait.Death, Trait.Emotion, Trait.Fear, Trait.Manipulate, Trait.Mental,
                        Trait.Arcane, Trait.Occult], "You force the target to see a vision of its own death.",
                    $"It takes {S.HeightenedVariable(2 * level, 8)}d6 mental damage with a Will save. If the target is reduced to 0 HP by this spell, its vision becomes reality and kills it instantly." +
                    S.FourDegreesOfSuccess("The target is unaffected.", "The target takes half damage and is frightened 1.",
                        "The target takes full damage and is frightened 2.",
                        "The target takes double damage, is frightened 4, and is fleeing for as long as it's frightened."),
                    Target.Ranged(24).WithAdditionalConditionOnTargetCreature(new LivingCreatureTargetingRequirement()),
                    level, SpellSavingThrow.Standard(Defense.Will))
                .WithActionCost(2).WithSoundEffect(SfxName.Fear)
                .WithHeighteningOfDamageEveryLevel(level, 4, inCombat, "2d6")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result == CheckResult.CriticalSuccess) return;
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText($"{2*level}d6", "Vision of Death"), DamageKind.Mental);
                    if (target.HP == 0)
                    {
                        target.Overhead("killed outright", Color.Black);
                        target.Die();
                        return;
                    }
                    switch (result)
                    {
                        case CheckResult.Success:
                            target.AddQEffect(QEffect.Frightened(1));
                            break;
                        case CheckResult.Failure:
                            target.AddQEffect(QEffect.Frightened(2));
                            break;
                        case CheckResult.CriticalFailure:
                            QEffect critFear = QEffect.Fleeing(caster).WithExpirationNever();
                            critFear.StateCheck = qf =>
                            {
                                if (!qf.Owner.HasEffect(QEffectId.Frightened))
                                    qf.ExpiresAt = ExpirationCondition.Immediately;
                            };
                            target.AddQEffect(QEffect.Frightened(4));
                            target.AddQEffect(critFear);
                            break;
                        case CheckResult.CriticalSuccess:
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }
                });
        });
    }
}