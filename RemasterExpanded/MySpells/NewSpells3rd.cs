using System.Collections.Immutable;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Campaign.LongTerm;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
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
using Microsoft.Xna.Framework;
using RemasterExpanded.MyArchetypes;
using RemasterExpanded.Technical;
using SpiritDamage;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public abstract class NewSpells3rd : NewSpells
{
    public static void Load()
    {
        CroakVoice = ModManager.RegisterNewSpell("RE_CroakVoice", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Croak"), "Croak Voice",
                    [Trait.Concentrate, Trait.Curse, Trait.Manipulate, Trait.Morph, Trait.Arcane, Trait.Primal],
                    "You cause the target creature's vocal chords to swell like those of a frog.",
                    "The target must attempt a Fortitude save." +
                    S.FourDegreesOfSuccess("The target is unaffected.",
                        "The target's voice becomes hoarse, and speaking becomes painful. Whenever it uses an action that has the auditory trait or attempts to Cast a Spell that doesn't have the subtle trait, it must succeed at a DC 5 flat check or the action is lost. Once per round, the target can spend an Interact action to massage its throat, attempting a Fortitude save against your spell DC. On a success, the spell ends.",
                        $"As success, but using an action with the auditory trait also deals {S.HeightenedVariable(2 + (level - 3), 2)}d10 mental damage to the target as the sound of its distorted voice grates on its ears.",
                        " As failure, but the damage for using an action with the auditory trait is doubled, and the target can't use an Interact action to attempt a Fortitude save to end the effect early."),
                    Target.Ranged(6), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithActionCost(2).WithSoundEffect(MSoundEffects.Croak)
                .WithHeighteningNumerical(level, 3, inCombat, 1,
                    "The damage for using an action with the auditory trait increases by 1d10.")
                .WithProjectileCone(VfxStyle.NoAnimation())
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result == CheckResult.CriticalSuccess) return;
                    var description =
                        "Whenever you use an action that has the auditory trait or attempt to Cast a Spell that doesn't have the subtle trait, you must succeed at a DC 5 flat check or the action is lost.";
                    switch (result)
                    {
                        case CheckResult.Success:
                            description +=
                                $" Once per round, you can spend an Interact action to massage its throat, attempting a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.";
                            break;
                        case CheckResult.Failure:
                            description +=
                                $" You take {S.HeightenedVariable(2 + (level - 3), 2)}d10 mental damage when using an action with the auditory trait.";
                            description +=
                                $" Once per round, you can spend an Interact action to massage its throat, attempting a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.";
                            break;
                        case CheckResult.CriticalFailure:
                            description +=
                                $" You take {S.HeightenedVariable(2 * (2 + (level - 3)), 2 * 2)}d10 mental damage when using an action with the auditory trait.";
                            break;
                        case CheckResult.CriticalSuccess:
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }

                    QEffect croak = new("Croak Voice", description, ExpirationCondition.Never, caster,
                        spell.Illustration)
                    {
                        FizzleOutgoingActions = async (effect, action, builder) =>
                        {
                            if (!action.HasTrait(Trait.Auditory) &&
                                (!action.HasTrait(Trait.Spell) || action.HasTrait(Trait.SomaticOnly))) return false;
                            (CheckResult, string) tuple = Checks.RollFlatCheck(5);
                            if (action.HasTrait(Trait.Spell))
                                builder.AppendLine("Casting a spell while croaking: " + tuple.Item2);
                            else
                            {
                                builder.AppendLine("Using an auditory action while croaking: " + tuple.Item2);
                            }

                            Sfxs.Play(MSoundEffects.Croak);
                            if (result <= CheckResult.Failure && action.HasTrait(Trait.Auditory))
                            {
                                await CommonSpellEffects.DealBasicDamage(spell, caster, effect.Owner, result,
                                    $"{2 + (level - 3)}d10",
                                    DamageKind.Mental);
                            }

                            return tuple.Item1 < CheckResult.Success;
                        },
                        ProvideContextualAction = effect =>
                        {
                            if (result == CheckResult.CriticalFailure) return null;
                            return new ActionPossibility(new CombatAction(effect.Owner, spell.Illustration,
                                    "Massage Throat", [Trait.Manipulate],
                                    $"You can take an interact action to attempt a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.",
                                    Target.Self().WithAdditionalRestriction(creature =>
                                    {
                                        if (creature.HasEffect(MQEffectIds.Massaged))
                                            return "This action can only be used once per round.";
                                        return creature.HasFreeHand
                                            ? null
                                            : "You must have a free hand to perform an interact action.";
                                    }))
                                .WithGoodness((_, self, _) => self.Possibilities.Filter(ap =>
                                    ap.CombatAction.HasTrait(Trait.Auditory) ||
                                    (ap.CombatAction.HasTrait(Trait.Spell) &&
                                     !ap.CombatAction.HasTrait(Trait.SomaticOnly))).CreateActions(true).Count >= 1
                                    ? float.MaxValue
                                    : float.MinValue)
                                .WithActionCost(1).WithEffectOnChosenTargets(async (_, creature, _) =>
                                {
                                    CheckResult save =
                                        await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell,
                                            Defense.Fortitude);
                                    if (save >= CheckResult.Success)
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                    creature.AddQEffect(new QEffect { Id = MQEffectIds.Massaged }
                                        .WithExpirationAtStartOfOwnerTurn());
                                })
                            );
                        }
                    };
                    target.AddQEffect(croak);
                });
        });
        LongTermEffects.EasyRegister("RE_FeetToFins", LongTermEffectDuration.UntilLongRest, FeetToFinsQf);
        FeetToFins = ModManager.RegisterNewSpell("RE_FeetToFins", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Fish"), "Feet to Fins",
                    [Trait.Morph, Trait.Arcane, Trait.Primal],
                    "The target's feet transform into fins, improving mobility in the water but reducing it on land.",
                    "A willing target gains swimming, but it's speed is reduced to 5 feet unless it is in water or able to fly.",
                    Target.AdjacentCreatureOrSelf().WithAdditionalConditionOnTargetCreature((creature, creature1) =>
                        creature1.FriendOf(creature)
                            ? Usability.Usable
                            : Usability.NotUsableOnThisCreature("Must be willing.")),
                    level, null)
                .WithSoundEffect(SfxName.ScratchFlesh)
                .WithHeightenedAtSpecificLevel(level, 6, inCombat,
                    "You may cast this spell as a free action at the start of combat and it lasts until your next daily preparations.")
                .WithEffectOnEachTarget(async (_, _, target, _) =>
                {
                    target.AddQEffect(FeetToFinsQf());
                    if (level >= 6 && WellKnownLongTermEffects.CreateLongTermEffect("RE_FeetToFins") is { } feetToFins)
                    {
                        target.LongTermEffects?.Add(feetToFins);
                    }
                })
                .WithCastsAsAReaction((effect, thisSpell, canCast) =>
                {
                    if (!canCast() || level < 6)
                        return;
                    effect.StartOfCombatReaction = _ =>
                    {
                        Creature caster = effect.Owner;
                        ReactionOption feet = ReactionOption.CreateFromSpellAsAReaction(thisSpell,
                            thisSpell.Description, async () =>
                            {
                                CombatAction dummy = CombatAction.CreateSimple(caster, "Feet to Fins",
                                    Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName).WithActionCost(0);
                                dummy.Target = new CreatureTarget(RangeKind.Ranged,
                                    new FriendOrSelfCreatureTargetingRequirement(),
                                    (_, _, _) => int.MinValue);
                                dummy.WithDescription(thisSpell.Description);
                                dummy.Illustration = thisSpell.Illustration;
                                await caster.Battle.GameLoop.FullCast(dummy);
                                if (dummy.ChosenTargets.ChosenCreature == null || thisSpell.EffectOnOneTarget == null)
                                {
                                    caster.Spellcasting?.RevertExpendingOfResources(thisSpell);
                                    return;
                                }

                                await thisSpell.EffectOnOneTarget.Invoke(thisSpell, caster,
                                    dummy.ChosenTargets.ChosenCreature,
                                    CheckResult.Success);
                            }).WithIsFreeAction();
                        feet.Caption = "Feet to Fins" + $" (rank {level})";
                        return feet;
                    };
                });
        });
        WardingAggression = ModManager.RegisterNewSpell("WardingAggression", 3, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("WardingAggression"), "Warding Aggression",
                    [
                        Trait.Arcane, Trait.Primal, Trait.Divine, Trait.SpellWithDuration,
                        Trait.DoesNotRequireAttackRollOrSavingThrow, Trait.ProxyAttack
                    ],
                    "You channel a magical ward through your attack and attempt to plant it on a foe. This ward increases your defenses against that foe, as long as you keep attacking that foe to maintain it.",
                    "Make a melee Strike with a weapon or unarmed attack against a foe. In addition to the normal effects of the Strike, it has the effects below. {i}Warding aggression{/i} ends if the foe you attacked dies or at the end of any turn in which you didn't hit that foe with a melee Strike." +
                    S.FourDegreesOfSuccess(
                        "You gain a +3 status bonus to AC against the foe for 1 round and a +2 status bonus to AC against the foe for the remaining duration.",
                        "You gain a +2 status bonus to AC against the foe.",
                        "You gain a +1 status bonus to AC against the foe.", "You gain no additional effect."),
                    Target.ReachWithAnyWeapon()
                        .WithOverriddenFullRangeLine("{b}Range{/b} reach of a weapon"), level, null)
                .WithSoundEffect(SfxName.ShieldSpell)
                .WithProjectileCone(VfxStyle.NoAnimation())
                .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                {
                    QEffect ward = new(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                    {
                        AfterYouTakeAction = async (effect, action) =>
                        {
                            if (!action.HasTrait(Trait.Strike))
                                return;
                            CheckResult result = action.CheckResult;
                            if (result != CheckResult.CriticalFailure)
                                caster.AddQEffect(WardingAggressionEffect(target, result, caster));
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    List<CombatAction> strikes = [];
                    List<Option> options = [];
                    caster.AddQEffect(ward);
                    strikes.AddRange(caster.MeleeWeapons
                        .Where(wp => wp.DetermineReach(caster) >= target.DistanceToWith10FeetException(caster))
                        .Select(weapon => caster.CreateStrike(weapon).WithActionCost(0)));
                    if (strikes.Count > 1)
                    {
                        foreach (CombatAction strike in strikes)
                        {
                            strike.WithFullRename($"Warding Aggression ({strike.Item?.Name})");
                            strike.Illustration = new SideBySideIllustration(strike.Illustration, spell.Illustration);
                            AlternateTaskImplements.AddDirectUsageOnACreatureOptions(target, strike, options);
                        }

                        options.Add(new CancelOption(true));
                        if (!await AlternateTaskImplements.OfferOptions(caster, options, true))
                        {
                            spell.RevertRequested = true;
                            ward.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    }
                    else
                    {
                        CombatAction strike = strikes[0];
                        strike.WithFullRename($"Warding Aggression ({strike.Item?.Name})");
                        strike.Illustration = new SideBySideIllustration(strike.Illustration, spell.Illustration);
                        await caster.Battle.GameLoop.FullCast(strike, ChosenTargets.CreateSingleTarget(target));
                    }

                    ward.ExpiresAt = ExpirationCondition.Immediately;
                });
        });
        WallOfWind = ModManager.TryParse("WallOfWind", out SpellId wallOfWind)
            ? wallOfWind
            : ModManager.RegisterNewSpell("WallOfWind", 3,
                (_, owner, rank, _, _) =>
                {
                    return Spells.CreateModern(
                            MIllustrations.CreateIllustration("WallOfWind"),
                            "Wall of Wind",
                            [
                                Trait.Air, Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Primal,
                                Trait.Evocation
                            ],
                            "You create a barrier of gusting winds that hinders anything moving through it.",
                            "The wall of swirling winds is 5 feet thick, 60 feet long, and 30 feet high. The wall stands vertically, but you can shape its path. "
                            + "Though the wall of wind distorts the air, it does not hamper sight. The wall has the following effects.\n\n"
                            + "- Ammunition from physical ranged attacks—such as arrows, bolts, sling bullets, and other objects of similar size—can't pass through the wall. Attacks with bigger ranged weapons,"
                            + "such as javelins, take a –2 circumstance penalty to their attack rolls if their paths pass through the wall. "
                            + "Massive ranged weapons and spell effects that don't create physical objects pass through the wall with no penalty.\n"
                            + "- The wall is difficult terrain to creatures attempting to move through it. Gases, including creatures in vapor form, can't pass through the wall.\n"
                            + "- A creature that attempts to fly through the wall using a move action must attempt a Fortitude save."
                            + S.FourDegreesOfSuccess(
                                null,
                                null,
                                "The wall stops the movement of the flying creature, and any remaining movement from its current action is wasted.",
                                "As failure, and the creature is pushed 10 feet away from the wall."
                            ),
                            new BendyLineTarget(
                                12,
                                tile =>
                                    owner?.HasLineOfEffectTo(tile) < CoverKind.Blocked
                                    && owner.DistanceTo(tile) <= 24
                                    && !tile.CurrentlyBlocksLineOfEffect
                            ),
                            rank,
                            null
                        )
                        .WithActionCost(3)
                        .WithSoundEffect(SfxName.AirSpell)
                        .WithProjectileCone(VfxStyle.BasicProjectileCone(MIllustrations.CreateIllustration("WallOfWind")))
                        .WithPrologueEffectOnChosenTargetsBeforeRolls(async (action, _, targets) =>
                            {
                                BendyLineTarget.FinalizeLineTargets(targets, action);
                            }
                        )
                        .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                            {
                                Zone.SpawnStaticAndApply(
                                    caster,
                                    targets.ChosenTiles,
                                    zone =>
                                    {
                                        zone.AfterCreatureEntersOrMovesWithin = async (cr) =>
                                        {
                                            if (cr.HasEffect(QEffectId.Flying))
                                            {
                                                CheckResult checkResult = await CommonSpellEffects.RollSpellSavingThrowAsync(
                                                    cr,
                                                    spell,
                                                    Defense.Fortitude
                                                );
                                                if (checkResult <= CheckResult.Failure)
                                                {
                                                    Tile tile = cr.Occupies;
                                                    cr.AddQEffect(
                                                        new QEffect
                                                        {
                                                            Id = QEffectId.InterruptMovement,
                                                            ExpiresAt = ExpirationCondition.ExpiresAtEndOfAnyTurn,
                                                            AfterYouTakeAction = async (q, action) =>
                                                            {
                                                                if (Equals(q.Owner.Occupies, tile) &&
                                                                    action.HasTrait(Trait.Move))
                                                                {
                                                                    q.Owner.RemoveAllQEffects(qf =>
                                                                        qf.Id == QEffectId.InterruptMovement
                                                                    );
                                                                    if (checkResult == CheckResult.Failure)
                                                                    {
                                                                        await q.Owner.MoveTo(
                                                                            q.Owner.Space.PreviousTile ??
                                                                            q.Owner.Occupies,
                                                                            action,
                                                                            new MovementStyle
                                                                            {
                                                                                Shifting = true,
                                                                                ShortestPath = true,
                                                                                MaximumSquares = 100,
                                                                                ForcedMovement = true,
                                                                                IgnoresUnevenTerrain = true,
                                                                            }
                                                                        );
                                                                    }
                                                                    else
                                                                    {
                                                                        Tile? tile1 = q
                                                                            .Owner.Space.PreviousTile?.Neighbours
                                                                            .FirstOrDefault(e =>
                                                                                q.Owner.Space.PreviousTile.DistanceTo(
                                                                                    e.Tile) == 2
                                                                            )
                                                                            ?.Tile;
                                                                        await q.Owner.MoveTo(
                                                                            tile1 ?? q.Owner.Occupies,
                                                                            action,
                                                                            new MovementStyle
                                                                            {
                                                                                Shifting = true,
                                                                                ShortestPath = true,
                                                                                MaximumSquares = 100,
                                                                                ForcedMovement = true,
                                                                                IgnoresUnevenTerrain = true,
                                                                            }
                                                                        );
                                                                    }
                                                                }
                                                            },
                                                        }
                                                    );
                                                }
                                            }
                                        };
                                        zone.TileEffectCreator = wallTile => new TileQEffect(wallTile)
                                        {
                                            VisibleDescription =
                                                $"{{b}}Wall of Wind.{{b}} Flying creatures must attempt a Fortitude save to pass through. Small projectiles cannot pass through. Thrown projectiles take a -2 circumstance penalty to pass through.",
                                            TransformsTileIntoDifficultTerrain = true,
                                            TransformsTileIntoDifficultTerrainForFlyingCreatures = true,
                                            Illustration = MIllustrations.CreateIllustration("WallOfWind"),
                                        };
                                        zone.ControllerQEffect.AddGrantingOfTechnical(
                                            _ => true,
                                            tech =>
                                            {
                                                tech.PreventTargetingBy = action =>
                                                    action.HasTrait(Trait.Ranged)
                                                    && action.HasTrait(Trait.Strike)
                                                    && !action.HasTrait(Trait.Thrown)
                                                    && action.Item?.WeaponProperties?.VfxStyle?.ProjectileKind
                                                    != ProjectileKind.TripleSizeArrow
                                                    && TilesPassedOver(action.Owner, tech.Owner)
                                                        .ContainsOneOf(targets.ChosenTiles)
                                                        ? "wall-of-wind"
                                                        : null;
                                                tech.BonusToAttackRolls = (_, action, target) =>
                                                    action.HasTrait(Trait.Ranged)
                                                    && action.HasTrait(Trait.Strike)
                                                    && action.HasTrait(Trait.Thrown)
                                                    && action.Item?.WeaponProperties?.VfxStyle?.ProjectileKind
                                                    != ProjectileKind.TripleSizeArrow
                                                    && target != null
                                                    && TilesPassedOver(action.Owner, target)
                                                        .ContainsOneOf(targets.ChosenTiles)
                                                        ? new Bonus(-2, BonusType.Circumstance, "Wall of Wind", false)
                                                        : null;
                                            }
                                        );
                                    }
                                );
                            }
                        );
                });
        Earthbind = ModManager.RegisterNewSpell("Earthbind", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Earthbind"), "Earthbind",
                [Trait.Arcane, Trait.Primal, Trait.Rebalanced],
                "Using the weight of earth, you bring the flying back to the ground.",
                "You remove the targets flying with effects based on its Fortitude save. You cannot target a creature flying over a chasm or deep water." +
                S.FourDegreesOfSuccess("The target is unaffected.", "The target cannot fly for 1 round.", "The target is knocked prone and cannot fly for the rest of the encounter.", $"As failure, but the target also takes {S.HeightenedVariable(level, 3)}d8 bludgeoning damage."),
                Target.Ranged(24).WithAdditionalConditionOnTargetCreature(new TargetHasQEffectCreatureTargetingRequirement(QEffectId.Flying, "Can only target flying creatures."))
                    .WithAdditionalConditionOnTargetCreature((_, cr) => cr.Space.Tiles.Any(t => t.Kind is TileKind.Chasm or TileKind.Water) ? Usability.NotUsableOnThisCreature("Over a chasm or deep water.") : Usability.Usable)
                    .WithOverriddenTargetLine("1 flying creature", false), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithHeighteningOfDamageEveryLevel(level, 3, inCombat, "1d8")
                .WithSoundEffect(SfxName.EarthSpell)
                .WithActionCost(2)
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    QEffect flyingRemoval = new("Earthbind", "You cannot fly for the duration.", ExpirationCondition.Never, caster, spell.Illustration)
                    {
                        StateCheck = effect => effect.Owner.RemoveAllQEffects(qf => qf.Id == QEffectId.Flying),
                        WhenExpires = effect => effect.Owner.AddQEffect(QEffect.Flying()),
                        Dispellable = spell
                    };
                    switch (result)
                    {
                        case CheckResult.CriticalSuccess:
                            return;
                        case CheckResult.Success:
                            target.AddQEffect(flyingRemoval
                                .WithExpirationInOneRound(caster));
                            return;
                        case CheckResult.Failure or CheckResult.CriticalFailure:
                            target.AddQEffect(flyingRemoval
                                .WithExpirationNever());
                            await target.FallProne();
                            if (result ==  CheckResult.CriticalFailure)
                                await CommonSpellEffects.DealDirectDamage(spell, target, $"{level}d8",
                                    DamageKind.Bludgeoning);
                            return;
                    }
                });
        });

        ModManager.RegisterNewSpell("SoulCutter", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("SoulCutter"), "Soul Cutter",
                    [Trait.Occult, Trait.Divine, Trait.Attack, MTraits.Sanctified, SpiritTrait.Spirit],
                    "You conjure a blade of spiritual energy and cut at the soul of a foe.",
                    "Make a melee spell attack against your target's AC. On a success, you deal spirit damage. On a critical success, the target takes double damage. The number of actions you spend when Casting this Spell determines the range, damage, and other parameters." +
                    $"\n{{icon:Action}} An adjacent target takes {S.HeightenedVariable(level, 3)}d6 spirit damage." +
                    $"\n{{icon:TwoActions}} An adjacent target takes {S.HeightenedVariable(level * 2, 6)}d6 spirit damage, and on a hit, it becomes enfeebled 1 for 1 round." +
                    $"\n{{icon:ThreeActions}} The blade flies out from you and attacks a target within 30 feet, dealing {S.HeightenedVariable(level * 2, 6)}d6 spirit damage. The spell deals half damage on a failure (but not a critical failure), as the blade's divine influence guides your attack. Unless you critically fail, the target becomes enfeebled 1 for 1 round.",
                    Target.DependsOnActionsSpent(
                        Target.ActuallyAdjacent()
                            .WithAdditionalConditionOnTargetCreature(new EnemyCreatureTargetingRequirement()),
                        Target.ActuallyAdjacent()
                            .WithAdditionalConditionOnTargetCreature(new EnemyCreatureTargetingRequirement()),
                        Target.Ranged(6)),
                    level, null)
                .WithActionCost(-1)
                .WithSpellAttackRoll()
                .WithCreateVariantDescription((i, _) =>
                {
                    return i switch
                    {
                        1 => $"Deal {S.HeightenedVariable(level, 3)}d6 spirit damage to a creature.",
                        2 =>
                            $"Deal {S.HeightenedVariable(level * 2, 6)}d6 spirit damage to a creature and on a hit, it becomes enfeebled 1 for 1 round..",
                        _ =>
                            $"Deal {S.HeightenedVariable(level * 2, 6)}d6 spirit damage to a creature. The spell deals half damage on a failure (but not a critical failure), as the blade's divine influence guides your attack. Unless you critically fail, the target becomes enfeebled 1 for 1 round."
                    };
                })
                .WithSoundEffect(SfxName.PhaseBolt)
                .WithHeighteningNumerical(level, 3, inCombat, 1, "The damage increases by 1d6 for the 1-action version, or by 2d6 for the 2- and 3-action versions.")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (caster.HasTrait(HolyTrait.Holy))
                        spell.Traits.Add(HolyTrait.Holy);
                    if (caster.HasTrait(UnholyTrait.Unholy))
                        spell.Traits.Add(UnholyTrait.Unholy);
                    switch (spell.SpentActions)
                    {
                        case 1:
                            await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, $"{level}d6",
                                DamageKind.Spirit);
                            break;
                        case 2:
                            await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, $"{2 * level}d6",
                                DamageKind.Spirit);
                            if (result >= CheckResult.Success)
                                target.AddQEffect(QEffect.Enfeebled(1).WithExpirationInOneRound(caster));
                            break;
                        case 3:
                            await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, $"{2 * level}d6", DamageKind.Spirit);
                            if (result == CheckResult.Failure)
                                await CommonSpellEffects.DealDirectDamage(new DamageEvent(spell, target, result,
                                [
                                    new KindedDamage(DiceFormula.FromText($"{2 * level}d6", spell.Name),
                                        DamageKind.Spirit)
                                ], false, true));
                            if (result != CheckResult.CriticalFailure)
                                target.AddQEffect(QEffect.Enfeebled(1).WithExpirationInOneRound(caster));
                            break;
                    }
                });
        });
        ModManager.RegisterNewSpell("RendMagic", 3, (_, _, level, inCombat, _) =>
        {
            int heighten = level % 2 == 0 ? level - 1 : level;
            return Spells.CreateModern(MIllustrations.CreateIllustration("RendMagic"), "Rend Magic",
                    [Trait.Arcane, Trait.Force, Trait.Attack],
                    "You spew forth pure magical energy down your arm before releasing it in a devastating attack.",
                    $"Make a melee spell attack against the target's AC. If it hits, the target takes {S.HeightenedVariable(heighten, 3)}d10 force damage, then takes {S.HeightenedVariable((heighten - 1) / 2, 1)}d10 force damage each time it Casts a Spell for the rest of the encounter; on a critical hit, double the initial damage and the damage when the target casts spells. This spell ends immediately if the target ends its turn without Casting a Spell.",
                    Target.Touch(), level, null)
                .WithHeighteningNumerical(level, 3, inCombat, 2, "The initial force damage increases by 2d10, and the force damage taken whenever the target casts a spell increases by 1d10.")
                .WithSoundEffect(SfxName.PhaseBolt)
                .WithActionCost(2)
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, $"{heighten}d10", DamageKind.Force);
                    if (result <= CheckResult.Failure)
                        return;
                    QEffect rend = new("Rend Magic", $"Each time you cast a spell, you will take {(result == CheckResult.CriticalSuccess ? "2(" : "")}{S.HeightenedVariable((heighten - 1) / 2, 1)}d10{(result == CheckResult.CriticalSuccess ? ")" : "")} force damage. This spell ends immediately if you end your turn without Casting a Spell.", ExpirationCondition.Never, caster,
                        MIllustrations.CreateIllustration("RendMagic"))
                    {
                        AfterYouTakeAction = async (effect, action) =>
                        {
                            if (!action.HasTrait(Trait.Spell)) return;
                            effect.CannotExpireThisTurn = true;
                            await CommonSpellEffects.DealDirectDamage(new DamageEvent(spell, target, result,
                            [
                                new KindedDamage(DiceFormula.FromText($"{(heighten - 1) / 2}d10", spell.Name),
                                    DamageKind.Force)
                            ], result == CheckResult.CriticalSuccess));
                        },
                        EndOfYourTurnBeneficialEffect = async (qf, _) =>
                        {
                            if (qf.CannotExpireThisTurn)
                                return;
                            qf.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    target.AddQEffect(rend);
                });
        });
        ModManager.RegisterNewSpell("LightningLasso", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("LightningLasso"), "Lightning Lasso",
                    [Trait.Electricity, Trait.Arcane, Trait.Primal, Trait.SpellWithDuration],
                    "You conjure a crackling lasso of pure electricity, hurling it at a creature to bind them in place.",
                    "The target must attempt a Reflex save."+
                    S.FourDegreesOfSuccess("The target is unaffected.", $"The target takes {S.HeightenedVariable(level -1, 2)}d12 electricity damage.", $"The target takes {S.HeightenedVariable(level -1, 2)}d12 electricity damage and is immobilized until the end of your next turn, but it can Escape. On subsequent turns, if the target is still immobilized by the lasso, you can Sustain the spell to deal {S.HeightenedVariable((level - 2) * 2, 2)} electricity damage and keep the target immobilized until the end of your next turn.", "As failure, but the target is also restrained."),
                    Target.Ranged(6), level, SpellSavingThrow.Standard(Defense.Reflex))
                .WithHeighteningNumerical(level, 3, inCombat, 1, "The initial electricity damage increases by 1d12, and the electricity damage dealt when you Sustain the spell increases by 2.")
                .WithSoundEffect(SfxName.ElectricArc)
                .WithActionCost(2)
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result == CheckResult.CriticalSuccess)
                        return;
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, CheckResult.Failure,
                        $"{level - 1}d12", DamageKind.Electricity);
                    if (result == CheckResult.Success)
                        return;
                    QEffect lasso = new QEffect("Lightning Lasso", $"You are {(result == CheckResult.CriticalFailure ? "restrained" : "immobilized")}.", ExpirationCondition.ExpiresAtEndOfSourcesTurn, caster, spell.Illustration)
                    {
                        StateCheck = qf =>
                        {
                            qf.Owner.AddQEffect(result == CheckResult.CriticalFailure
                                ? QEffect.Restrained(caster).WithExpirationEphemeral()
                                : QEffect.Immobilized().WithExpirationEphemeral());
                        },
                        ProvideContextualAction = qf => new ActionPossibility(Possibilities.CreateEscapeAgainstEffect(qf.Owner, qf, "Lightning Lasso", spell.SpellcastingSource?.GetSpellSaveDC(spell) ?? 10)),
                        CannotExpireThisTurn = true,
                        CountsAsBeneficialToSource = true
                    }.WithSourceAction(spell).WithDispellable(spell).WithSource(caster);
                    target.AddQEffect(lasso);
                    caster.AddQEffect(QEffect.Sustaining(spell, lasso, async qf =>
                    {
                        await CommonSpellEffects.DealDirectDamage(spell, qf.Owner, $"{(level - 2) * 2}", DamageKind.Electricity);
                    }, $" Sustaining the effect deals {S.HeightenedVariable((level - 2) * 2, 2)} electricity damage to {target.Name} and keeps them immobilized until the end of your next turn."));

                });
        });
        return;
        ModManager.RegisterNewSpell("SkyglassPrison", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("SkyglassPrison"), "Skyglass Prison",
                [Trait.Air, Trait.Arcane, Trait.Occult, Trait.Primal],
                "The air around the target solidifies like glass, trapping the creature in a cage that perfectly encases them, making movement nearly impossible.",
                $"The target makes a Fortitude save. The target becomes trapped in a cage that has AC 10, Hardness 5, and {S.HeightenedVariable(level * 10, 30)} Hit Points, and is immune to critical hits and precision damage. When a creature Escapes or the cage is destroyed, shards of hardened air cut the target creature, dealing {S.HeightenedVariable(level, 3)}d4 slashing damage. You can Dismiss this spell to destroy the cage." +
                S.FourDegreesOfSuccess("The target is unaffected.", "The target is immobilized until it Escapes or until the cage is destroyed. The cage is automatically destroyed at the start of your next turn.", "The target is immobilized until it Escapes or until the cage is destroyed.", "The target is restrained until it Escapes or until the cage is destroyed."),
                Target.Ranged(6).WithAdditionalConditionOnTargetCreature((_, enemy) => enemy.HasEffect(QEffectId.Incorporeal) ? Usability.NotUsableOnThisCreature("Cannot capture an incorporeal creature with a corporeal prison.") : Usability.Usable), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithActionCost(2)
                .WithHeighteningNumerical(level, 3, inCombat, 1, "The cage's Hit Points increase by 10, and the slashing damage increases by 1d4.")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result ==  CheckResult.CriticalSuccess)
                        return;
                    QEffect cage = new("Skyglass Prison", $"You are {(result == CheckResult.CriticalFailure ? "restrained" : "immobilized")} until you Escape or until the cage is destroyed.{(result == CheckResult.Success ? $" The cage is automatically destroyed at the start of {caster.Name}'s next turn." : "")}", spell.Illustration)
                    {
                        StateCheck = qf => qf.Owner.AddQEffect(result == CheckResult.CriticalFailure ? QEffect.Restrained(caster).WithExpirationEphemeral() : QEffect.Immobilized().WithExpirationEphemeral()),
                        WhenExpires = _ =>
                        {
                            if (!target.Alive)
                                return;
                            target.AddQEffect(new QEffect
                            {
                                StateCheckWithVisibleChanges = async qf =>
                                {
                                    await CommonSpellEffects.DealDirectDamage(spell,
                                        DiceFormula.FromText($"{level}d4", spell.Name), target, CheckResult.Failure,
                                        DamageKind.Slashing);
                                    qf.ExpiresAt = ExpirationCondition.Immediately;
                                }
                            });
                        },
                        ProvideContextualAction = qf => new ActionPossibility(Possibilities.CreateEscapeAgainstEffect(target, qf, "Skyglass Prison", spell.SpellcastingSource?.GetSpellSaveDC(spell) ?? 10))
                    };
                    cage.AttackableShell = AttackableShellRules.CreateAttackableShell(target, "Skyglass Prison", 10, level * 10)
                        .WithHardness(5)
                        .WithImmunityToCriticalHits()
                        .With(cr =>
                        {
                            cr.AddQEffect(QEffect.ObjectImmunities());
                            cr.AddQEffect(QEffect.TraitImmunity(Trait.PrecisionDamage));
                            cr.AddQEffect(new QEffect
                            {
                                WhenMonsterDies = _ => cage.ExpiresAt = ExpirationCondition.Immediately
                            });
                            cr.OwningFaction = caster.OwningFaction;
                        });
                    cage.WithSourceAction(spell).WithDispellable(spell)
                        .WithExpirationOneRoundOrRestOfTheEncounter(caster, result != CheckResult.Success);
                    target.AddQEffect(cage);
                    caster.AddQEffect(new QEffect
                    {
                        Innate = false,
                        WhenExpires = _ => cage.ExpiresAt = ExpirationCondition.Immediately,
                        StateCheck = qf =>
                        {
                            if (target.HasEffect(cage))
                                return;
                            qf.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    }.WithName("Skyglass Prison").WithDismissable());
                });
        });
    }
    public static List<Tile> TilesPassedOver(Creature origin, Creature target)
    {
        List<Tile> tiles = [];
        Vector2 originPoint = origin.Occupies.ToCenterVector();
        Vector2 targetPoint = target.Occupies.ToCenterVector();
        Vector2 directionVector = targetPoint - originPoint;
        var steps = (int)(directionVector.Length() * 8f);
        Vector2 stepAmount = directionVector / steps;
        for (var step = 0; step < steps; ++step)
        {
            Point point = (originPoint + stepAmount * step).ToPoint();
            if (origin.Battle.Map.GetTile(point.X, point.Y) is { } tile)
            {
                tiles.Add(tile);
            }
        }
        return tiles;
    }

    public static QEffect FeetToFinsQf()
    {
        return new QEffect("Feet to Fins", "You have swimming but your speed is reduced to 5 feet unless you are in water or able to fly.", ExpirationCondition.Never, null, MIllustrations.CreateIllustration("Fish"))
        {
            StateCheck = qf =>
            {
                qf.Owner.AddQEffect(QEffect.Swimming().WithExpirationEphemeral());
                int speed = SetSpeed(qf.Owner.Speed);
                qf.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    BonusToAllSpeeds = effect =>
                        effect.Owner.HasEffect(QEffectId.AquaticCombat) ||
                        effect.Owner.HasEffect(QEffectId.Flying) || effect.Owner.Space.Tiles.All(t => t.Kind is TileKind.Water or TileKind.ShallowWater)
                            ? null
                            : new Bonus(speed + 1, BonusType.Untyped, "Feet to Fins",
                                false)
                });
            }
        };
    }
    public static int SetSpeed(int speed)
    {
        ImmutableList<int> speeds = [speed];
        return -speeds[0];
    }

    public static QEffect WardingAggressionEffect(Creature target, CheckResult result, Creature source)
    {
        bool hit = result != CheckResult.Failure;
        var oneRound = true;
        int value = result == CheckResult.CriticalSuccess ? 3 : result >= CheckResult.Success ? 2 : 1;
        var description = $"You have a +{value} status bonus to AC against attacks made by {target.Name}. This effect expires if {target.Name} dies or at the end of any turn in which you didn't hit that foe with a melee Strike.";
        QEffect aggression = new("Warding Aggression",description,ExpirationCondition.ExpiresAtEndOfYourTurn, source, MIllustrations.CreateIllustration("WardingAggression"))
        {
            BonusToDefenses = (effect, action, defense) => action != null && action.Owner == target && defense == Defense.AC ? new Bonus(effect.Value, BonusType.Status, "Warding Aggression") : null,
            AfterYouTakeAction = async (effect, action) =>
            {
                if (!action.HasTrait(Trait.Strike) || action.ChosenTargets.ChosenCreature != target || action.CheckResult <= CheckResult.Failure)
                    return;
                hit = true;
                effect.CannotExpireThisTurn = true;
            },
            CannotExpireThisTurn = hit,
            StartOfYourPrimaryTurn = async (effect, _) =>
            {
                hit = false;
                if (oneRound && result == CheckResult.CriticalSuccess)
                {
                    effect.Description = effect.Description?.Replace("3", "2");
                    effect.Value = 2;
                }
                oneRound = false;
            },
            WhenYouAcquireThis = effect =>
            {
                target.AddQEffect(new QEffect
                {
                    WhenCreatureDiesAtStateCheckAsync = async _ =>
                    {
                        effect.ExpiresAt = ExpirationCondition.Immediately;
                    }
                });
            },
            Value = value,
            HideValue = true
        };
        return aggression;
    }

    public class BendyLineTarget(int maxLength, Func<Tile, bool>? additionalRequirementsForTarget)
        : GeneratorTarget
    {
        public int MaxLength { get; } = maxLength;
        protected Func<Tile, bool>? AdditionalRequirementsForTarget { get; } = additionalRequirementsForTarget;

        public override GeneratedTargetInSequence? GenerateNextTarget()
        {
            List<Tile> list = OwnerAction.ChosenTargets.ChosenTiles.ToList();
            int count = list.Count;
            var currentLength = 0;
            for (var i = 0; i < list.Count - 1; ++i)
            {
                currentLength += list[i].DistanceTo(list[i + 1]);
            }

            if (currentLength >= MaxLength)
            {
                return null;
            }

            Tile? previousTile = (count > 0 ? list.Last() : null);
            return new GeneratedTargetInSequence(
                Tile(
                    delegate(Creature _, Tile tile3)
                    {
                        bool flag = AdditionalRequirementsForTarget?.Invoke(tile3) ?? true;
                        if (flag && previousTile != null)
                        {
                            flag = previousTile.DistanceTo(tile3) <= MaxLength - currentLength;
                        }

                        return flag;
                    },
                    null
                ),
                $" ({currentLength * 5}/{MaxLength * 5} ft)"
            );
        }

        public static void FinalizeLineTargets(ChosenTargets targets, CombatAction action)
        {
            List<Tile> list = targets.ChosenTiles.ToList();
            for (var i = 0; i < list.Count - 1; i++)
            {
                Tile tile = list[i];
                Tile tile2 = list[i + 1];
                LineAreaTarget lineAreaTarget = Line(tile.DistanceTo(tile2)).WithLesserDistanceIsOkay();
                lineAreaTarget.SetOwnerAction(action);
                AreaSelection targetedTiles = Areas.DetermineTiles(
                    lineAreaTarget,
                    tile,
                    new Vector2(tile2.X + 0.5f, tile2.Y + 0.5f)
                );
                targets.SetFromArea(lineAreaTarget, targetedTiles.TargetedTiles);
            }
        }
    }
}