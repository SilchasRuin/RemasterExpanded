using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Mechanics.Zoning;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using SpiritDamage;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public class NewSpells5th : NewSpells
{
    public static void Load()
    {
        HowlingBlizzard = ModManager.RegisterNewSpell("RE_HowlingBlizzard", 5, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Blizzard"), "Howling Blizzard", [Trait.Air, Trait.Cold, Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Primal],
                    "Freezing winds extend from your hands, pushing away from you with great force.",
                    "If you Cast this Spell with 2 actions, it has an area of a 60-foot cone; if you Cast this Spell with 3 actions, it has a range of 500 feet and an area of a 30-foot burst." +
                    $"\n\nEach creature in the area takes {S.HeightenedVariable(10 + (level - 5) * 2, 10)}d6 cold damage with a basic Reflex save. Snowdrifts and icy gales fill the area until the start of your next turn, making the area difficult terrain.",
                    Target.DependsOnActionsSpent(Target.Uncastable("This spell can't be cast as one action."), Target.Cone(12), Target.Burst(100, 6))
                        .WithOverriddenFullTargetLine("{b}Range{/b} 500 feet if burst\n{b}Area{/b} 60-foot cone or 30-foot burst"), level, SpellSavingThrow.Basic(Defense.Reflex))
                .WithCreateVariantDescription((_, _) => $"Each creature in the area takes {S.HeightenedVariable(10 + (level - 5) * 2, 10)}d6 cold damage with a basic Reflex save. Snowdrifts and icy gales fill the area until the start of your next turn, making the area difficult terrain.")
                .WithActionCost(-4).WithSoundEffect(SfxName.RayOfFrost)
                .WithHeighteningOfDamageEveryLevel(level, 5, inCombat, "2d6")
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    foreach (Creature creature in targets.ChosenCreatures)
                    {
                        CheckResult save = targets.GetResultFor(creature);
                        await CommonSpellEffects.DealBasicDamage(spell, caster, creature, save, DiceFormula.FromText($"{10 + (level - 5) * 2}d6", "Howling Blizzard"), DamageKind.Cold);
                    }
                    QEffect control = new QEffect().WithExpirationAtStartOfSourcesTurn(caster, 1);
                    Zone blizzard = Zone.Spawn(control, ZoneAttachment.StableBurst(targets.ChosenTiles));
                    caster.AddQEffect(control);
                    blizzard.TileEffectCreator = _ => new TileQEffect
                    {
                        TransformsTileIntoDifficultTerrain = true,
                        Illustration = new Illustration[]
                        {
                            IllustrationName.SnowTile1,
                            IllustrationName.SnowTile2,
                            IllustrationName.SnowTile3,
                            IllustrationName.SnowTile4
                        }.GetRandomVisualOnly()
                    };
                    blizzard.Apply();
                });
        });
        ShockAndAwe = ModManager.RegisterNewSpell("RE_ShockAndAwe", 5, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("ShockAndAwe"), "Shock and Awe", [Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Fear, Trait.Illusion, Trait.Manipulate, Trait.Mental, Trait.Visual, Trait.Arcane, Trait.Occult, Trait.NoHeightening],
                    "You create the illusion of cannons exploding, bullets and arrows flying, and magical ballistics firing, as an overwhelming torrent of information, both visual and auditory.",
                    "Enemies in the area must attempt a Will save."+
                    S.FourDegreesOfSuccess("The target is unaffected.",  "The target is frightened 1.", "The target is frightened 2 and stunned 1.", "The target is frightened 3 and stunned 2."),
                    Target.Burst(20, 10).WithIncludeOnlyIf((at, creature) => creature.EnemyOf(at.OwnerAction.Owner)), level, SpellSavingThrow.Standard(Defense.Will))
                .WithActionCost(3).WithSoundEffect(SfxName.FieryBurst)
                .WithEffectOnEachTarget(async (_, _, target, result) =>
                {
                    switch (result)
                    {
                        case CheckResult.CriticalSuccess:
                            break;
                        case CheckResult.Success:
                            target.AddQEffect(QEffect.Frightened(1));
                            break;
                        case CheckResult.Failure:
                            target.AddQEffect(QEffect.Frightened(2));
                            target.AddQEffect(QEffect.Stunned(1));
                            break;
                        case CheckResult.CriticalFailure:
                            target.AddQEffect(QEffect.Frightened(3));
                            target.AddQEffect(QEffect.Stunned(2));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }
                });
        });
        InvokeSpirits = ModManager.TryParse("InvokeSpirits", out SpellId invokeSpirits) ? invokeSpirits : ModManager.RegisterNewSpell("RE_InvokeSpirits", 5, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("InvokeSpirits"), "Invoke Spirits", [Trait.Concentrate, Trait.Emotion, Trait.Fear, Trait.Manipulate, Trait.Mental, Trait.Negative, Trait.Arcane, Trait.Divine, Trait.Occult],
                "Ragged apparitions of the dead rise to stalk the living.",
                $"Each living creature in the area takes {S.HeightenedVariable(2 + (level - 5) /2, 2)}d4 mental damage and {S.HeightenedVariable(2 + (level - 5) /2, 2)}d4 void damage, with a basic Will Save. Additionally, creatures that critically fail the save are frightened 2 and are fleeing for 1 round." +
                $"\n\nOn subsequent rounds, the first time you Sustain the spell each round, you can move the area up to 30 feet. Living creatures in the new area must attempt saves with the same effects as above, except that critically failing doesn't make them flee.",
                Target.Burst(24, 2).WithIncludeOnlyIf((_, creature) => creature.IsLivingCreature), level, SpellSavingThrow.Basic(Defense.Will))
                .WithActionCost(2).WithSoundEffect(SfxName.Necromancy).WithProjectileCone(VfxStyle.NoAnimation())
                .WithHeighteningNumerical(level, 5, inCombat, 2, "The mental damage and void damage each increase by 1d4.")
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    Point chosenPoint = targets.ChosenPointOfOrigin;
                    foreach (Creature target in targets.ChosenCreatures)
                    {
                        CheckResult result = targets.GetResultFor(target);
                        await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,
                            new KindedDamage(DiceFormula.FromText($"{2 + (level - 5) / 2}d4", "Invoke Spirits"),
                                DamageKind.Mental),
                            new KindedDamage(DiceFormula.FromText($"{2 + (level - 5) / 2}d4", "Invoke Spirits"),
                                DamageKind.Negative));
                        if (result != CheckResult.CriticalFailure) continue;
                        target.AddQEffect(QEffect.Frightened(2));
                        target.AddQEffect(QEffect.Fleeing(caster).WithExpirationAtStartOfSourcesTurn(caster, 1));
                    }
                    Zone spirits = Zone.Spawn(caster, ZoneAttachment.StableBurst(targets.ChosenTiles));
                    spirits.TileEffectCreator = _ => new TileQEffect
                    {
                        Illustration = new Illustration[]
                        {
                            IllustrationName.Fog1,
                            IllustrationName.Fog2,
                            IllustrationName.Fog3,
                            IllustrationName.Fog4
                        }.GetRandomVisualOnly()
                    };
                    spirits.ApplySustainment(spell, async _ => await OnSustain(), $"When you Sustain the spell each round, you can move the area up to 30 feet. Each living creature in the new area takes {S.HeightenedVariable(2 + (level - 5) /2, 2)}d4 mental damage and {S.HeightenedVariable(2 + (level - 5) /2, 2)}d4 void damage, with a basic Will Save.");
                    spirits.Apply();
                    return;

                    async Task OnSustain()
                    {
                        Target place = Target.Burst(24, 2).WithIncludeOnlyIf((_, creature) => creature.IsLivingCreature);
                        (place as BurstAreaTarget)!.MustBeWithinShortDistanceOfVector2 = chosenPoint.ToVector2();
                        (place as BurstAreaTarget)!.MustBeWithinShortDistanceOf_Distance = 6;
                        CombatAction sustain = new CombatAction(caster, spell.Illustration, "Move Spirits", [Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell, Trait.DoesNotProvoke], "", place)
                            {
                                SpellcastingSource = spell.SpellcastingSource,
                                SpellLevel = spell.SpellLevel,
                                SpellInformation = spell.SpellInformation
                            }
                            .WithActionCost(0).WithSoundEffect(SfxName.Necromancy)
                            .WithSpellSavingThrow(Defense.Will)
                            .WithEffectOnChosenTargets(async (_, _, chosenTargets) =>
                            {
                                foreach (Creature target in chosenTargets.ChosenCreatures)
                                {
                                    CheckResult result = chosenTargets.GetResultFor(target);
                                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,
                                        new KindedDamage(DiceFormula.FromText($"{2 + (level - 5) / 2}d4", "Invoke Spirits"),
                                            DamageKind.Mental),
                                        new KindedDamage(DiceFormula.FromText($"{2 + (level - 5) / 2}d4", "Invoke Spirits"),
                                            DamageKind.Negative));
                                    if (result != CheckResult.CriticalFailure) continue;
                                    target.AddQEffect(QEffect.Frightened(2));
                                }
                            });
                        await caster.Battle.GameLoop.FullCast(sustain);
                        chosenPoint = sustain.ChosenTargets.ChosenPointOfOrigin;
                        IEnumerable<Tile> originalTiles = spirits.AffectedTiles.ToList();
                        foreach (Tile tile in sustain.ChosenTargets.ChosenTiles.Where(tile => !originalTiles.Contains(tile)))
                        {
                            spirits.AddTileAndApplyToIt(tile);
                        }
                        spirits.ZoneAttachment.AffectedTiles.RemoveAll(tile => originalTiles.Contains(tile) && !sustain.ChosenTargets.ChosenTiles.Contains(tile));
                        foreach (Tile tile in originalTiles.Where(tile => !sustain.ChosenTargets.ChosenTiles.Contains(tile)))
                        {
                            tile.RemoveAllQEffects(tqf => tqf.Zone == spirits);
                        }
                    }
                });
        });
        ModManager.RegisterNewSpell("RE_BlindingBottle", 5, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(new ModdedIllustration("PMAssets/BlindingBottle.png"), "Blinding Bottle", [Trait.Concentrate, Trait.Manipulate, Trait.Poison, Trait.Arcane, Trait.Occult],
                "You conjure an exploding glass container filled with a sight stealing poison and hurl it across enemy lines. Upon impact, the bottle bursts and exposes all creatures in the area to the toxin within.",
                "Each creature in the area must attempt a Fortitude save."+
                S.FourDegreesOfSuccess("The creature is unaffected.", "The creature takes 3d6 poison damage.", "The creature is afflicted with blinding poison at stage 1.", "The creature is afflicted with blinding poison at stage 2.")+
                "\n\n{b}Blinding Poison{/b} (incapacitation, poison) {b}Level{/b} 9; {b}Maximum Duration{/b} 4 rounds; {b}Stage 1{/b} 3d6 poison damage and blinded for 1 round (1 round); {b}Stage 2{/b} 4d6 poison damage and blinded for 1 round (1 round); {b}Stage 3{/b} 5d6 poison damage and blinded for 1 round (1 round); {b}Stage 4{/b} 6d6 poison damage and blinded for 1 minute (1 round)",
                Target.Burst(20, 6), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithActionCost(2).WithSoundEffect(SfxName.Mistform)
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    switch (result)
                    {
                        case CheckResult.CriticalSuccess:
                            break;
                        case CheckResult.Success:
                            await CommonSpellEffects.DealDirectDamage(spell, DiceFormula.FromText("3d6", "Blinding Bottle"),
                                target, result, DamageKind.Poison);
                            break;
                        case CheckResult.CriticalFailure:
                        case CheckResult.Failure:
                            await ApplyIncapacitationPoison(CreateBlindingPoison(spell.SpellcastingSource!.GetSpellSaveDC(), caster), caster, target, result, 9);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }
                });
        });
    }
}