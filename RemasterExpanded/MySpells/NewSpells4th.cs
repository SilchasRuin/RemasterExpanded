using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Campaign.LongTerm;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options.Reactive;
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
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using RemasterExpanded.Technical;
using SpiritDamage;
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
        
         ModManager.ReplaceExistingSpell(SpellId.DivineWrath, 4, (_, level, inCombat, _) =>
        {
            return Spells.CreateModern(IllustrationName.DivineWrath, "Divine Wrath",
                    [SpiritTrait.Spirit, MTraits.Sanctified, Trait.Divine, Trait.InflictsSlow],
                    "You channel the fury of divinity against your foes.",
                    $"You deal {S.HeightenedVariable(level, 4)}d10 spirit damage to enemies in the area, depending on their Fortitude save."
                    + S.FourDegreesOfSuccess("The creature is unaffected.", "The creature takes half damage.", "The creature takes full damage and is sickened 1.", "The creature takes full damage and is sickened 2; while it's sickened, it's also slowed 1."),
                    Target.Burst(24, 4).WithIncludeOnlyIf((at, cr) => cr.EnemyOf(at.OwnerAction.Owner)), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithSoundEffect(SfxName.DivineLance)
                .WithHeighteningOfDamageEveryLevel(level, 4, inCombat, "1d10")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (caster.HasTrait(HolyTrait.Holy))
                        spell.Traits.Add(HolyTrait.Holy);
                    if (caster.HasTrait(UnholyTrait.Unholy))
                        spell.Traits.Add(UnholyTrait.Unholy);
                    QEffect sick = QEffect.Sickened(result == CheckResult.CriticalFailure ? 2 : 1,
                        spell.SpellcastingSource?.FixedDC ?? 10);
                    if (result == CheckResult.CriticalFailure)
                    {
                        sick.StateCheck = effect =>
                        {
                            effect.Owner.AddQEffect(QEffect.Slowed(1).WithExpirationEphemeral());
                        };
                    }
                    switch (result)
                    {
                        case CheckResult.CriticalSuccess:
                            return;
                        case CheckResult.Success:
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,
                                DiceFormula.FromText($"{level}d10", "Divine Wrath"), DamageSpirit.Spirit);
                            return;
                        case CheckResult.CriticalFailure:
                        case CheckResult.Failure:
                        default:
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, CheckResult.Failure,
                                DiceFormula.FromText($"{level}d10", "Divine Wrath"), DamageSpirit.Spirit);
                            target.AddQEffect(sick);
                            break;
                    }
                });
        });

        ModManager.RegisterNewSpell("ForceRain", 4, (_, _, level, inCombat, _) =>
        {
            const string flavor = "You conjure a magical cloud that batters creatures with shards of solidified magic.";
            return Spells.CreateModern(MIllustrations.CreateIllustration("ForceRain"), "Force Rain", [Trait.Force, Trait.Arcane, Trait.Occult],
                flavor,
                "Creatures in the spell's area take force damage with a basic Reflex save. The number of actions you spend when Casting this Spell determines the area and other parameters." +
                $"\n\n{{icon:Action}} This spell affects a single 5-foot square and deals {S.HeightenedVariable(level, 4)}d6 force damage." +
                $"\n{{icon:TwoActions}} This spell affects all squares in a 10-foot burst and deals {S.HeightenedVariable(2 * level, 8)}d6 force damage." +
                $"\n{{icon:ThreeActions}} The shards home in on creatures. This spell affects all squares in a 10-foot burst. Creatures in the area don't attempt a saving throw and instead automatically take {S.HeightenedVariable(level * 5, 20)} force damage.",
                Target.DependsOnActionsSpent(Target.Tile((caster, tile) => caster.DistanceTo(tile) <= 12).WithAlsoSelectCreatures().WithOverriddenFullTargetLine("{b}Range{/b} 60 feet\n{b}Area{/b} 5-foot square"), Target.Burst(12, 2), Target.Burst(12, 2)),
                level, SpellSavingThrow.Basic(Defense.Reflex))
                .WithHeighteningNumerical(level, 4, inCombat, 1, "The damage increases by 1d6 for the 1-action version, by 2d6 for the 2-action version, and by 5 for the 3-action version.")
                .WithSoundEffect(SfxName.MagicMissile)
                .WithActionCost(-1)
                .WithNoSaveFor((action, _) => action.SpentActions == 3)
                .With(spell =>
                {
                    spell.CreateVariantDescription = (actionCost, variant) =>
                    {
                        if (actionCost < 0 && variant == null)
                            return spell.Description;
                        Target? target = spell.Target;
                        if (target is DependsOnActionsSpentTarget actionsSpentTarget2 && actionCost > 0)
                            target = actionsSpentTarget2.TargetFromActionCount(actionCost - spell.ActionCostMetaModification);
                        if (target == null)
                            return spell.Description;
                        return Spells.CreateInitialDescriptionBlock(target, actionCost == 3 ? null : SpellSavingThrow.Basic(Defense.Reflex), flavor) + DescriptionCreator(actionCost - spell.ActionCostMetaModification);

                        string DescriptionCreator(int i)
                        {
                            return i switch
                            {
                                1 => $"The spell deals {S.HeightenedVariable(level, 4)}d6 force damage to creatures in the area (basic Reflex save mitigates).",
                                2 => $"The spell deals {S.HeightenedVariable(2 * level, 8)}d6 force damage to creatures in the area (basic Reflex save mitigates).",
                                _ => $"The spell deals {S.HeightenedVariable(level * 5, 20)} force damage to creatures in the area (no saving throw)."
                            };
                        }
                    };
                })
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    switch (spell.SpentActions)
                    {
                        case 3:
                            await CommonSpellEffects.DealDirectDamage(spell, target, $"{level * 5}", DamageKind.Force);
                            break;
                        case 2:
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,$"{level * 2}d8", DamageKind.Force);
                            break;
                        case 1:
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,$"{level}d8", DamageKind.Force);
                            break;
                    }
                });
        });
        LongTermEffects.EasyRegister("RE_VitalBeacon", LongTermEffectDuration.UntilLongRest, VitalBeaconQf);
        VitalBeacon = ModManager.RegisterNewSpell("RE_VitalBeacon", 4, (_, owner, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("VitalBeacon"), "Vital Beacon",
                    [
                        Trait.Divine, Trait.Primal, Trait.Healing, Trait.Positive,
                        Trait.DoesNotCountAsPrimaryCastingOfSpell, Trait.ScrollCastableAtCombatStart, Trait.DoesNotTriggerAnyReactions
                    ],
                    "Vitality radiates outward from you, allowing others to supplicate and receive healing.",
                    $"This spell can only be cast as a free action at the start of an encounter." +
                    $"\n\nOnce per round, either you or an ally can use a single action with the {{tooltip:manipulate}}manipulate{{/}} trait to supplicate and lay hands upon you to regain Hit Points. Each time the beacon heals someone, it decreases in strength. It restores {S.HeightenedVariable(level, 4)}d10 Hit Points to the first creature, {S.HeightenedVariable(level, 4)}d8 Hit Points to the second, {S.HeightenedVariable(level, 4)}d6 Hit Points to the third, and {S.HeightenedVariable(level, 4)}d4 Hit Points to the fourth, after which the spell ends." +
                    $"\n\nYou can have only one {{i}}vital beacon{{/i}} active at a time and it lasts until your next long rest.",
                    Target.Self(), level, null)
                .WithHeighteningNumerical(level, 4, inCombat, 1, "The beacon restores one additional die of HP each time it heals, using the normal die size for that step.")
                .WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    Creature caster = effect.Owner;
                    if (!canCast() || caster.HasEffect(MQEffectIds.VitalBeacon))
                        return;
                    effect.StartOfCombatReaction = _ => ReactionOption.WrapFullcast(spell.WithActionCost(0), "Gain the effects of the {{i}}vital beacon{{/i}} spell.");
                })
                .WithActionCost(7)
                .WithEffectOnSelf(caster => caster.AddQEffect(VitalBeaconQf(level.ToString(), 0)));
        });
    }

    public static QEffect VitalBeaconQf(string diceCount, int useCount)
    {
        QEffect beacon = new("Vital Beacon", "", ExpirationCondition.Never, null,
            MIllustrations.CreateIllustration("VitalBeacon"))
        {
            Value =  useCount,
            HideValue = true,
            Tag = false,
            WhenExpires = qf =>
            {
                if (qf.Value == 4 && WellKnownLongTermEffects.CreateLongTermEffect("RE_VitalBeacon", diceCount, qf.Value) is {} vital)
                    qf.Owner.LongTermEffects?.Effects.RemoveAll(lte => lte.Id == vital.Id);
            },
            EndOfCombat = async (effect, won) =>
            {
                if (!won || effect.Owner.LongTermEffects is not {} lte || WellKnownLongTermEffects.CreateLongTermEffect("RE_VitalBeacon", diceCount, effect.Value) is not {} vital)
                    return;
                if (lte.Effects.FirstOrDefault(lt => lt.Id == vital.Id) is
                    { } vitalBeacon)
                    vitalBeacon.Number = effect.Value;
                else
                    lte.Add(vital);
            },
            StateCheck = qf =>
            {
                int diceSize = qf.Value switch
                {
                    0 => 10,
                    1 => 8,
                    2 => 6,
                    _ => 4
                };
                // if (qf.Description != null && qf.Description.Contains("d" + diceSize))
                //     return;
                qf.Description = $"Once per round, either you or an ally can use a single action with the manipulate trait to regain {diceCount}d{diceSize} Hit Points. {(qf.Value == 3 ? "The next usage will end the spell." : $"The next usage will reduce the future healing to {diceCount}d{diceSize - 2}.")}";
            },
            Id = MQEffectIds.VitalBeacon
        };
        beacon.AddGrantingOfTechnical(cr => cr.FriendOf(beacon.Owner) && cr.DistanceToWith10FeetException(beacon.Owner) <= cr.Space.NaturalReach, qfTech =>
        {
            qfTech.ProvideContextualAction = qf =>
            {
                if (beacon.Tag is true)
                    return null;
                int diceSize = beacon.Value switch
                {
                    0 => 10,
                    1 => 8,
                    2 => 6,
                    _ => 4
                };
                var dice = $"{diceCount}d{diceSize}";
                CombatAction supplicate = new CombatAction(qf.Owner, MIllustrations.CreateIllustration("VitalBeacon"), 
                        "Supplicate", [Trait.Basic, Trait.Manipulate, Trait.Positive, Trait.Healing],
                        $"Regain {dice} Hit Points by using {beacon.Owner}'s {{i}}vital beacon{{/i}} spell effect. {(beacon.Value == 3 ? "This will end the effect." : $"This can only be done once per round and will reduce the future healing to {diceCount}d{diceSize - 2}.")}",
                        Target.Self().WithAdditionalRestriction(cr => cr.HasTrait(Trait.Undead) ? "Cannot be healed by Vitality." : null)
                            .WithAdditionalRestriction(cr => cr.FindQEffect(QEffectId.ImmunityToTargetingByTrait)?.ImmuneToTrait == Trait.Healing ? "Cannot be healed." : null)
                            .WithAdditionalRestriction(cr => cr.HP >= cr.MaxHP ? "Your Hit Points are already full." : null))
                    .WithActionCost(1)
                    .WithSoundEffect(SfxName.MinorHealing)
                    .WithEffectOnSelf(async (action, self) =>
                    {
                        await self.HealAsync(dice, action);
                        beacon.Value += 1;
                        if (beacon.Value == 4)
                            beacon.ExpiresAt = ExpirationCondition.Immediately;
                        beacon.Tag = true;
                        beacon.Owner.AddQEffect(new QEffect
                        {
                            WhenExpires = _ =>
                            {
                                beacon.Tag = false;
                            }
                        }.WithExpirationAtStartOfSourcesTurn(self.Battle.InitiativeOrder[0], 1));
                    });
                return new ActionPossibility(supplicate);
            };
        });
        return beacon;
    }
}