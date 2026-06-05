using System;
using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Mechanics.Zoning;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.Remaster.Spellbook;
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
                    $"This spell deals {NumberToWord(level)} dice of damage to creatures in the area with a Reflex save. This damage has the same type as a weapon you are wielding and uses the same die size. Determine the die size as if you were attacking with the weapon."+
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
        IceStorm = ModManager.RegisterNewSpell("RE_IceStorm", 4, (_, _, level, inCombat, _) =>
        {
            int heighten = level % 2 == 0 ? level - 4 : level - 5;
            return Spells.CreateModern(MIllustrations.CreateIllustration("IceStorm"), "Ice Storm", 
                    [Trait.Cold, Trait.Arcane, Trait.Primal, Trait.SpellWithDuration],
                "You create a gray storm cloud that pelts creatures with an icy deluge.",
                $"When you Cast the Spell, a burst of magical hail deals {S.HeightenedVariable(2 + heighten / 2, 2)}d8 bludgeoning damage and {S.HeightenedVariable(2 + heighten / 2, 2)}d8 cold damage to each creature in the area below the cloud (basic Reflex save). As long as you sustain the spell, snow and sleet continue to rain down in the area, making the area difficult terrain. Any creature that ends its turn in the storm takes {S.HeightenedVariable(2 + heighten / 2, 2)} cold damage." +
                $"\n\nIf you Cast this Spell outdoors, you can create two clouds instead of one. As normal, if a Large or larger creature is in both clouds, it still only takes the initial damage once and the continuing damage once per turn.", 
                Target.Burst(24, 4), level, SpellSavingThrow.Basic(Defense.Reflex))
                .WithSoundEffect(SfxName.RayOfFrost)
                .WithActionCost(3)
                .WithHeighteningNumerical(level,  4, inCombat, 2, "The initial bludgeoning damage and cold damage increase by 1d8 each, and the cold damage creatures take at the end of their turns increases by 1.")
                .WithCastsAsAReaction((effect, action, _) =>
                {
                    effect.ModifyActionPossibility = (_, combatAction) =>
                    {
                        if (action.SpellId != combatAction.SpellId || combatAction.Owner.Battle.Map.IsIndoors)
                            return;
                        combatAction.Target = new MultipleBurstsTarget(24, 4, 2);
                    };
                })
                .WithEffectOnChosenTargets(async (spell, self, targets) =>
                {
                    CheckResult result = spell.CheckResult;
                    foreach (Creature target in targets.ChosenCreatures)
                    {
                        await CommonSpellEffects.DealBasicDamage(spell, self, target, result,
                            new KindedDamage(DiceFormula.FromText($"{2 + heighten / 2}d8", "Ice Storm"),
                                DamageKind.Bludgeoning),
                            new KindedDamage(DiceFormula.FromText($"{2 + heighten / 2}d8", "Ice Storm"),
                                DamageKind.Cold));
                    }
                    Zone iceStorm = Zone.Spawn(self, ZoneAttachment.StableBurst(targets.ChosenTiles));
                    iceStorm.ApplySustainment(spell);
                    iceStorm.TileEffectCreator = _ => new TileQEffect
                    {
                        Illustration = new Illustration[]
                        {
                            IllustrationName.SnowTile1,
                            IllustrationName.SnowTile2,
                            IllustrationName.SnowTile3,
                            IllustrationName.SnowTile4
                        }.GetRandomVisualOnly(),
                        TransformsTileIntoDifficultTerrain = true
                    };
                    iceStorm.AfterCreatureEndsItsTurnHere = async creature =>
                    {
                        await CommonSpellEffects.DealDirectDamage(spell,
                            DiceFormula.FromText($"{2 + heighten / 2}", "Ice Storm"), creature, CheckResult.Failure,
                            DamageKind.Cold);
                    };
                    iceStorm.Apply();
                });
        });
    }
}