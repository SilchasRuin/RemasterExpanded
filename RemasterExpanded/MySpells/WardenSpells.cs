using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Squeezing;
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
using HarmonyLib;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.NewSpells;

namespace RemasterExpanded.MySpells;

public static class WardenSpells
{
    public static void Load()
    {
        #region Initiate
        SpellIds.DistractingDecoy = FastCreateFocusSpell("RE_DistractingDecoy", (level, _) =>
        {
            return Spells.CreateModern(IllustrationName.ShadowProjectile, "Distracting Decoy",
                [Trait.Uncommon, Trait.VerbalOnly, Trait.Focus, Trait.Ranger, Trait.Visual, Trait.NoHeightening],
                "You conjure a colorful, fast-moving shape, such as a small bird or other animal that draws your target’s eye.",
                "The target makes a Will save."+
                S.FourDegreesOfSuccess(null, "The creature is unaffected.", "The creature is off-guard until the start of your next turn.", "As failure, but the creature also takes a –2 circumstance penalty to attacks while it’s off-guard."),
                Target.Ranged(2), level, SpellSavingThrow.Standard(Defense.Will))
                .WithSoundEffect(SfxName.Feint)
                .WithActionCost(1)
                .WithEffectOnEachTarget(async (_, caster, target, result) =>
                {
                    if (result >= CheckResult.Success)
                        return;
                    QEffect offGuard = QEffect.FlatFooted("Distracting Decoy")
                        .WithExpirationAtStartOfSourcesTurn(caster, 1);
                    if (result == CheckResult.CriticalFailure)
                    {
                        offGuard.BonusToAttackRolls = (_, action, _) => action.HasTrait(Trait.Attack) ? new Bonus(-2, BonusType.Circumstance, "Distracting Decoy", false) : null;
                        offGuard.Description += " You also take a -2 circumstance penalty to attacks.";
                    }
                    offGuard.Name = "Off-guard";
                    target.AddQEffect(offGuard);
                });
        });
        SpellIds.SlimeSpit = FastCreateFocusSpell("RE_SlimeSpit", (level, inCombat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("SlimeSpit"), "Slime Spit",
                    [Trait.Uncommon, Trait.Focus, Trait.Poison, Trait.Ranger],
                    "You spit toxic goo that coats your target’s face and eyes.",
                    $"You deal {S.HeightenedVariable(level*2, 2)}d6 poison damage with a Reflex save."+
                    S.FourDegreesOfSuccess("The target takes no damage.", "The target takes half damage and is dazzled for 1 round, though it can Interact to wipe its eyes and remove the condition.", "The target takes full damage and is dazzled until the end of your next turn.", "The target takes double damage, is blinded for 1 round, and is dazzled until it uses an Interact action to wipe its eyes."),
                    Target.Ranged(6), level, SpellSavingThrow.Standard(Defense.Reflex))
                .WithSoundEffect(SfxName.AcidSplash)
                .WithHeighteningOfDamageEveryLevel(level, 1, inCombat, "2d6")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result >= CheckResult.CriticalSuccess)
                        return;
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText($"{level*2}d6", "Slime Spit"), DamageKind.Poison);
                    QEffect slimed = new()
                    {
                        StateCheck = qf =>
                        {
                            qf.Owner.AddQEffect(QEffect.Dazzled());
                        }
                    };
                    if (result != CheckResult.Failure)
                    {
                        slimed.ProvideContextualAction = qfDazzled =>
                            new ActionPossibility(new CombatAction(qfDazzled.Owner, IllustrationName.RubEyes,
                                        "Wipe eyes", [Trait.Manipulate],
                                        $"End the dazzled condition affecting you because of {{i}}{spell.Name.ToLower()}{{/i}}.",
                                        Target.Self((self, ai) =>
                                            !self.HasEffect(QEffectId.Blinded) && result == CheckResult.CriticalFailure
                                                ? ai.AlwaysIfSmartAndTakingCareOfSelf
                                                : int.MinValue)).WithActionCost(1)
                                    .WithEffectOnSelf(_ => qfDazzled.ExpiresAt = ExpirationCondition.Immediately))
                                .WithPossibilityGroup("Remove debuff");
                    }
                    switch (result)
                    {
                        case CheckResult.Success:
                            target.AddQEffect(slimed.WithExpirationInOneRound(caster));
                            break;
                        case CheckResult.Failure:
                            target.AddQEffect(slimed.WithExpirationAtEndOfSourcesNextTurn(caster, true));
                            break;
                        case CheckResult.CriticalFailure:
                            target.AddQEffect(slimed);
                            target.AddQEffect(QEffect.Blinded().WithExpirationInOneRound(caster));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }
                });
        });
        SpellIds.KeenSmell = FastCreateFocusSpell("RE_KeenSmell", (level, inCombat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("KeenSmell"), "Keen Smell",
                [Trait.Uncommon, Trait.Focus, Trait.Morph, Trait.Ranger, Trait.SpellWithDuration],
                "Your nose becomes more sensitive to the scents of the wild, the better for you to track your quarry.",
                $"For the spell’s duration, you gain scent as an imprecise sense with a range of {S.HeightenedVariable(level >= 3 ? 60 : 30, 30)} feet. Creatures can't be undetected by you as long as they are within your scent range.",
                Target.Self(), level, null)
                .WithSoundEffect(SfxName.ScratchFlesh)
                .WithHeightenedAtSpecificLevel(level, 3, inCombat, "Your scent range increases to 60 feet.")
                .WithEffectOnSelf(async self =>
                {
                    int scentRange = level >= 3 ? 12 : 6;
                    QEffect smell = new()
                    {
                        StateCheck = qfThis =>
                        {
                            qfThis.Owner.Battle.AllCreatures.Where(cr => cr.DistanceTo(qfThis.Owner) <= scentRange)
                                .ForEach(cr => cr.DetectionStatus.Undetected = false);
                        }
                    };
                    self.AddQEffect(smell);
                });
        });
        #endregion
        #region Advanced
        ModManager.ReplaceExistingSpell(SpellId.EnlargeCompanion, 0, (_, level, inCombat) =>
        {
            return Spells.CreateModern(IllustrationName.EnlargeCompanion, "Enlarge Companion", [Trait.Transmutation, Trait.Polymorph, Trait.Ranger, Trait.Focus, Trait.NoHeightening], 
                "Your animal companion grows much larger, towering over its foes in battle.", 
                $"For the rest of the encounter, your animal companion becomes {(level >= 4 ? "{b}Huge{/b}" : "Large")}. It becomes clumsy 1, its natural reach increases to {S.HeightenedVariable(level >= 4 ? 15 : 10, 10)} feet, and it gains a +{S.HeightenedVariable(level >= 4 ? 4 : 2, 2)} status bonus to melee damage.", 
                Target.RangedFriend(6).WithAdditionalConditionOnTargetCreature((a, d) => Ranger.GetAnimalCompanion(a) != d ? Usability.NotUsableOnThisCreature("not your companion") : Usability.Usable), level, null)
                .WithActionCost(2)
                .WithSoundEffect(SfxName.MagicWeapon)
                .WithHeightenedAtSpecificLevel(level, 4, inCombat, "Your animal companion instead becomes Huge, its reach increases to 15 feet, and the status bonus to melee damage increases to +4.")
                .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                {
                    int enlargeStatusBonus = level >= 4 ? 4 : 2;
                    QEffect? qeffect = await SizeChangeRules.EnlargeCreature(caster, target, level >= 4 ? Size.Huge : Size.Large, IllustrationName.Enlarge, "Enlarge Companion", $"You're clumsy 1, you have greater reach and a +{enlargeStatusBonus} status bonus to melee damage.");
                    if (qeffect != null)
                    {
                        qeffect.BonusToDamage = (_, action, _) => !action.HasTrait(Trait.Melee) && !action.HasTrait(Trait.VersatileMelee) ? null : new Bonus(enlargeStatusBonus, BonusType.Status, "Enlarge");
                        qeffect.StateCheck = sc => sc.Owner.AddQEffect(QEffect.Clumsy(1).WithExpirationEphemeral());
                    }
                    else
                        spell.RevertRequested = true;
                });
        });
        SpellIds.AnimalFeature = FastCreateFocusSpell("RE_AnimalFeature", (level, inCombat) =>
        {
            List<SpellVariant> variants =
            [
                new("CLAWS", "Claws", IllustrationName.DragonClaws),
                new("JAWS", "Jaws", IllustrationName.Jaws)
            ];
            if (level >= 4)
            {
                variants.AddRange([
                    new SpellVariant("FISH", "Fish Tail", MIllustrations.CreateIllustration("Fish")),
                    new SpellVariant("WINGS", "Wings", IllustrationName.AngelicWings)
                ]);
            }

            return Spells.CreateModern(MIllustrations.CreateIllustration("AnimalFeature"), "Animal Feature",
                    [Trait.Uncommon, Trait.Focus, Trait.Morph, Trait.Ranger, Trait.SpellWithDuration],
                    "You have learned to take on the adaptations of animals without fully transforming your body.",
                    "You gain one of the following animalistic features for the rest of the encounter:" +
                    "\n\n{b}• Claws{/b} You gain a claw attack that deals 1d6 slashing damage and has the agile, finesse, and unarmed traits." +
                    "\n{b}• Jaws{/b} You gain a jaws attack that deals 1d8 piercing damage and has the unarmed trait."+
                    (level >= 4 && inCombat ? "\n{b}• Fish Tail{/b} You gain {r}swimming{/r}." +
                                              "\n{b}• Wings{/b} You gain {r}flying{/r}." : ""),
                    Target.Self(), level, null)
                .WithActionCost(1)
                .WithHeightenedAtSpecificLevel(level, 4, inCombat, "Add the following options to the list." +
                                                                   "\n{b}• Fish Tail{/b} You gain {r}swimming{/r}." +
                                                                   "\n{b}• Wings{/b} You gain {r}flying{/r}.")
                .WithSoundEffect(SfxName.ScratchFlesh)
                .WithVariants(variants.ToArray())
                .WithCreateVariantDescription((_, variant) =>
                {
                    if (variant == null)
                        return "No variant";
                    return variant.Id switch
                    {
                        "CLAWS" =>
                            "You gain a claw attack that deals 1d6 slashing damage and has the agile, finesse, and unarmed traits.",
                        "JAWS" => "You gain a jaws attack that deals 1d8 piercing damage and has the unarmed trait.",
                        "WINGS" => "You gain {r}flying{/r}.",
                        "FISH" => "You gain {r}swimming{/r}.",
                        _ => "No variant"
                    };
                })
                .WithEffectOnSelf(async (spell, self) =>
                {
                    if (spell.ChosenVariant == null)
                        return;
                    QEffect claw = new()
                    {
                        DoNotShowUpOverhead = true,
                        AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.DragonClaws, "claw", "1d6",
                            DamageKind.Slashing, Trait.Agile, Trait.Finesse, Trait.Morph, MTraits.AnimalWeapon),
                        Id = MQEffectIds.AnimalFeatureClaws
                    };
                    QEffect jaws = new()
                    {
                        DoNotShowUpOverhead = true,
                        AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.Jaws, "jaws", "1d8",
                            DamageKind.Piercing, Trait.Morph, MTraits.AnimalWeapon),
                        Id = MQEffectIds.AnimalFeatureClaws
                    };
                    switch (spell.ChosenVariant.Id)
                    {
                        case "CLAWS":
                            self.AddQEffect(claw);
                            break;
                        case "JAWS":
                            self.AddQEffect(jaws);
                            break;
                        case "WINGS":
                            self.AddQEffect(QEffect.Flying());
                            break;
                        case "FISH":
                            self.AddQEffect(QEffect.Swimming());
                            break;
                    }
                });
        });
        SpellIds.HuntersLuck = FastCreateFocusSpell("RE_HuntersLuck", (level, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("HuntersLuck"), "Hunter's Luck",
                [Trait.Uncommon, Trait.Focus, Trait.Fortune, Trait.Ranger, Trait.VerbalOnly, Trait.NoHeightening],
                "You have a preternatural ability to remember details about your foes.",
                $"{{b}}Trigger{{/b}} You attempt a check to {RecallWeakness.GetActionLink("Recall Weakness")} about a creature, but you haven't rolled yet" +
                "\n\nRoll the triggering check twice and use the better result.",
                Target.Uncastable(), level, null)
                .WithActionCost(0)
                .WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    if (!canCast())
                        return;
                    effect.YouBeginActionReaction = (_, action) =>
                    {
                        if (action.ActionId != RecallWeakness.RWActionId)
                            return null;
                        ReactionOption luck = ReactionOption.CreateFromCombatActionCustom(spell, "Roll the triggering check twice and use the better result.", async () =>
                        {
                            CombatAction hunt = spell.Duplicate();
                            hunt.Target = Target.Self();
                            hunt.WithSpellcastingSource(spell.SpellcastingSource);
                            hunt.WithActionCost(0)
                                .WithEffectOnSelf(async (_, self) =>
                                {
                                    self.AddQEffect(new QEffect
                                    {
                                        RerollActiveRoll = async (_, _, cAction, _) => cAction.ActionId != RecallWeakness.RWActionId ? RerollDirection.DoNothing : RerollDirection.RerollAndKeepBest,
                                        AfterYouTakeAction = async (qEffect, cAction) =>
                                        {
                                            if (cAction.ActionId != RecallWeakness.RWActionId)
                                                return;
                                            qEffect.ExpiresAt = ExpirationCondition.Immediately;
                                        }
                                    });
                                });
                            await effect.Owner.Battle.GameLoop.FullCast(hunt);
                        });
                        return luck;
                    };
                });
        });
        SpellIds.SoothingMist = FastCreateFocusSpell("RE_SoothingMist", (level, combat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Restoration"), "Soothing Mist",
                [Trait.Uncommon, Trait.Focus, Trait.Healing, Trait.Ranger, Trait.Positive],
                "You call forth a magical mist that envelops a creature.",
                $"The mist restores {S.HeightenedVariable(level, 2)}d8 Hit Points to a target living creature and ends one source of persistent acid, bleed, fire, poison, or void damage affecting it. If the creature is taking persistent damage from multiple sources, you select which one is removed. Against an undead target, you deal {S.HeightenedVariable(level, 2)}d8 vitality damage (basic Fortitude save mitigates); if it fails the save, it also takes {S.HeightenedVariable(level, 2)} persistent vitality damage.",
                Target.RangedCreature(6).WithAdditionalConditionOnTargetCreature(
                    (self, target) => target.FriendOf(self) || target.HasTrait(Trait.Undead)
                        ? Usability.Usable
                        : Usability.NotUsableOnThisCreature("You can only target allies or undead creatures.")
                        ).WithAdditionalConditionOnTargetCreature((_, target) => target.HasEffect(QEffectId.PersistentDamage) || target.HP != target.MaxHPMinusDrained || target.HasTrait(Trait.Undead) ? Usability.Usable : Usability.NotUsableOnThisCreature("Would do nothing."))
                    .WithOverriddenFullTargetLine("{b}Targets{/b} 1 willing living creature or 1 undead creature"), level,
                    null)
                .WithSoundEffect(SfxName.Mistform)
                .WithHeighteningNumerical(level, 2, combat, 1, "The amount of healing (or damage to an undead target) increases by 1d8, and the persistent vitality damage to an undead creature increases by 1.")
                .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                {
                    if (target.HasTrait(Trait.Undead))
                    {
                        CheckResult result = await CommonSpellEffects.RollSpellSavingThrowAsync(target, spell, Defense.Fortitude);
                        await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText($"{level}d8", "Soothing Mist"), DamageKind.Positive);
                        if (result <= CheckResult.Failure)
                            await CommonSpellEffects.DealBasicPersistentDamage(target, result, $"{level}", DamageKind.Positive);
                    }
                    else
                    {
                        await target.HealAsync(DiceFormula.FromText($"{level}d8", "Soothing Mist"), spell);
                        List<QEffect> persistentDamages = target.QEffects.Where(qf => qf.Id == QEffectId.PersistentDamage && CorrectKind(qf.GetPersistentDamageKind())).ToList();
                        switch (persistentDamages.Count)
                        {
                            case 0:
                                return;
                            case 1:
                                target.RemoveAllQEffects(qf => qf.Id == QEffectId.PersistentDamage);
                                break;
                            case > 1:
                                List<QEffect> effects = [];
                                List<string> kindNames = [];
                                foreach (QEffect effect in persistentDamages)
                                {
                                    if (effect.Name == null) continue;
                                    kindNames.Add(effect.Name);
                                    effects.Add(effect);
                                }
                                ChoiceButtonOption choice = await caster.AskForChoiceAmongButtons(spell.Illustration,
                                    "Which persistent damage to remove?", kindNames.ToArray());
                                target.RemoveAllQEffects(qf => qf == effects[choice.Index]);
                                break;
                        }
                    }
                });

        });
        #endregion
        #region Master
        SpellIds.ThreateningMimicry = FastCreateFocusSpell("RE_ThreateningMimicry", (level, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("ThreateningMimicry"), "Threatening Mimicry",
                    [
                        Trait.Uncommon, Trait.Emotion, Trait.Fear, Trait.Focus, Trait.Illusion, Trait.Mental,
                        Trait.Ranger, Trait.NoHeightening
                    ],
                    "You appear larger and stronger to nearby creatures, appearing to possess threatening features like antlers, claws, or poison to their senses.",
                    "Each non-allied creature in the area must attempt a Will save."+
                    S.FourDegreesOfSuccess("The creature is unaffected.", "The creature is frightened 1. This condition doesn't decrease at the end of its turn if it damaged you during that turn.", "As success, but frightened 2.", "As success, but frightened 3."),
                    Target.EnemiesOnlyEmanation(2), level, SpellSavingThrow.Standard(Defense.Will))
                .WithSoundEffect(SfxName.SnakeHiss)
                .WithEffectOnEachTarget(async (_, caster, target, result) =>
                {
                    QEffect threaten = QEffect.Frightened(result switch
                    {
                        CheckResult.CriticalFailure => 3,
                        CheckResult.Failure => 2,
                        _ => 1
                    });
                    threaten.AfterYouDealDamageAgainstPrimaryTarget = async (_, self, me, _, _) =>
                    {
                        if (me != caster)
                            return;
                        if (self.FindQEffect(QEffectId.Frightened) is {} frightened)
                        {
                            int value = frightened.Value;
                            self.AddQEffect(new QEffect
                            {
                                WhenExpires = _ =>
                                {
                                    if (frightened.Value != value)
                                        frightened.Value += 1;
                                }
                            }.WithExpirationAtEndOfOwnerTurn());
                            frightened.CannotExpireThisTurn = true;
                        }
                    };
                    target.AddQEffect(threaten);
                });
        });
        SpellIds.WarningStripes = FastCreateFocusSpell("RE_WarningStripes", (level, combat) =>
        {
            int heighten = level % 2 == 0 ? level - 1 : level;
            return Spells.CreateModern(IllustrationName.CloakOfPoison, "Warning Stripes", [Trait.Uncommon, Trait.Focus, Trait.Morph, Trait.Poison, Trait.Ranger, Trait.SpellWithDuration,
                Trait.DoesNotRequireAttackRollOrSavingThrow, Trait.VerbalOnly],
                "Your skin becomes painted with vivid stripes or bright aposematic swirls, warning other creatures of its toxic qualities.",
                $"Any creature that touches you or hits you with an unarmed melee attack takes {S.HeightenedVariable((heighten - 1)/2 + 1, 2)}d8 poison damage. If you have an animal companion, you can cast this spell on them instead, with a range of 30 feet.",
                Target.Self(), level, null)
                .WithSoundEffect(SfxName.ScratchFlesh)
                .WithActionCost(1)
                .WithHeighteningOfDamageEveryTwoLevels(level, combat, "1d8")
                .WithCastsAsAReaction((effect, spell, _) =>
                {
                    if (effect.Owner.PersistentUsedUpResources.AnimalCompanionIsDead || Ranger.GetAnimalCompanion(effect.Owner) == null)
                        return;
                    effect.ModifyActionPossibility = (_, action) =>
                    {
                        if (action != spell)
                            return;
                        action.Target = Target.RangedFriend(6)
                            .WithAdditionalConditionOnTargetCreature((self, companion) =>
                                self == companion || companion == Ranger.GetAnimalCompanion(self)
                                    ? Usability.Usable
                                    : Usability.NotUsableOnThisCreature(
                                        "You can target only yourself or your animal companion."));
                    };
                })
                .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                {
                    QEffect stripes = new("Warning Stripes", $"Any creature that touches you or hits you with an unarmed melee attack takes {S.HeightenedVariable((heighten - 1)/2 + 1, 2)}d8 poison damage.", ExpirationCondition.Never, caster,
                        spell.Illustration)
                    {
                        DoNotShowUpOverhead = target == caster
                    };
                    stripes.AddGrantingOfTechnical(cr => cr != caster, qfTech =>
                    {
                        qfTech.AfterYouTakeActionAgainstTarget = async (qf, action, touchy, result) =>
                        {
                            if (touchy != target)
                                return;
                            if (action.HasTrait(Trait.Attack) && result <= CheckResult.Failure)
                                return;
                            if ((action.HasTrait(Trait.Melee) &&
                                 action.HasTrait(Trait.Attack) && action.HasTrait(Trait.Unarmed)) ||
                                ((action.Item?.HasTrait(Trait.Unarmed) ?? false) && action.ActionId != ActionId.Disarm) ||
                                action.Description.Contains("{b}Range{/b} touch"))
                            {
                                touchy.Overhead("Warning Stripes", Color.DarkOliveGreen,
                                    $"{qf.Owner.Name} triggered {{i}}warning stripes{{/i}}!", spell.Name,
                                    spell.Description, spell.Traits);
                                await CommonSpellEffects.DealDirectDamage(spell, qf.Owner,
                                    $"{(heighten - 1) / 2 + 1}d8", DamageKind.Poison);
                            }
                        };
                    });
                    target.AddQEffect(stripes);
                });
        });
        SpellIds.RangersBramble = FastCreateFocusSpell("RE_RangersBramble", (level, combat) =>
        {
            return Spells.CreateModern(IllustrationName.FlourishingFlora, "Ranger's Bramble",
                    [
                        Trait.Uncommon, Trait.Focus, Trait.Plant, Trait.Ranger, Trait.SpellWithDuration,
                        Trait.DoesNotRequireAttackRollOrSavingThrow
                    ],
                    "Plants transform into brambles and quickly grow, entangling creatures.",
                    $"All tiles with grass in the area are difficult terrain. Each round that a creature starts its turn in the area, it must attempt a Reflex save. On a failure, it takes a –10-foot circumstance penalty to its Speeds until it leaves the area, and on a critical failure, it's also immobilized for 1 round and takes {S.HeightenedVariable(level-1, 2)}d4 persistent bleed damage. Creatures can attempt to Escape to remove these effects (escaping does not end the bleed damage).",
                    Target.Burst(20, 1)
                        .WithAdditionalRequirementOnCaster(cr =>
                        cr.Battle.Map.AllTiles.Any(t => t.IsGrass)
                            ? Usability.Usable
                            : Usability.NotUsable("There are no plants here."))
                        .WithOverriddenFullTargetLine("{b}Range{/b} 100 feet" +
                                                      "\n{b}Area{/b} all squares that contain plants in a 5-foot burst")
                    , level, null)
                .WithSoundEffect(SfxName.Boneshaker)
                .WithHeighteningNumerical(level, 3, combat, 1, "The bleed damage on a critical failure increases by 1d4.")
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    List<Tile> plants = targets.ChosenTiles.Where(t => t.IsGrass).ToList();
                    if (plants.Count == 0)
                    {
                        spell.RevertRequested = true;
                        caster.Battle.Log("No plants in target area.");
                    }
                    Zone bramble = Zone.Spawn(caster, ZoneAttachment.StableBurst(plants));
                    bramble.AfterCreatureBeginsItsTurnHere = async creature =>
                    {
                        CheckResult checkResult = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Reflex);
                        if (checkResult <= CheckResult.Failure)
                        {
                            QEffect qEffect = QEffect.PenaltyToSpeed(2, BonusType.Circumstance).WithExpirationNever();
                            qEffect.Key = "Base Entangled";
                            qEffect.StateCheck = self =>
                            {
                                if (self.Owner.Space.AnyTile(tile => tile.TileQEffects.Any(tqf => tqf.Zone == bramble)))
                                    return;
                                self.ExpiresAt = ExpirationCondition.Immediately;
                            };
                            qEffect.ProvideContextualAction = (Func<QEffect, Possibility>) (self =>
                            {
                                Creature owner2 = self.Owner;
                                SpellcastingSource? spellcastingSource = spell.SpellcastingSource;
                                int flatDc = spellcastingSource?.GetSpellSaveDC(spell) ?? 10;
                                return new ActionPossibility(Possibilities.CreateEscapeAgainstEffect(owner2, self, "entangled", flatDc)).WithPossibilityGroup("Remove debuff");
                            });
                            creature.AddQEffect(qEffect);
                        }
                        if (checkResult != CheckResult.CriticalFailure)
                            return;
                        QEffect qEffect2 = QEffect.Immobilized().WithExpirationAtStartOfOwnerTurn();
                        qEffect2.StateCheck = self =>
                        {
                            if (self.Owner.QEffects.Any(qf => qf.Key == "Base Entangled"))
                                return;
                            self.ExpiresAt = ExpirationCondition.Immediately;
                        };
                        qEffect2.Key = "Entangle Immobilized";
                        creature.AddQEffect(qEffect2);
                        creature.AddQEffect(QEffect.PersistentDamage(
                            DiceFormula.FromText($"{level - 1}d4", "Ranger's Bramble"), DamageKind.Bleed));
                    };
                    bramble.TileEffectCreator = _ => new TileQEffect
                    {
                        Illustration = IllustrationName.LeshyCorpse,
                        ExpiresAt = ExpirationCondition.Never,
                        Name = "Ranger's Bramble",
                        TransformsTileIntoDifficultTerrain = true,
                        VisibleDescription = $"{{b}}Ranger's Bramble.{{/b}} Each round that a creature starts its turn in the area, it must attempt a Reflex save. On a failure, it takes a –10-foot circumstance penalty to its Speeds until it leaves the area, and on a critical failure, it's also immobilized for 1 round and takes {S.HeightenedVariable(level-1, 2)}d4 persistent bleed damage. Creatures can attempt to Escape to remove these effects (escaping does not end the bleed damage)."
                    };
                    bramble.Apply();
                });
        });
        #endregion
        #region Peerless
        SpellIds.PulverizingWake = FastCreateFocusSpell("RE_PulverizingWake", (level, combat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("PulverizingWake"), "Pulverizing Wake",
                    [
                        Trait.Uncommon, Trait.Attack, Trait.Focus, Trait.Ranger, Trait.Sonic,
                        Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Trait.DoesNotRequireAttackRollOrSavingThrow
                    ],
                    "Imitating raptorial creatures, you attack with such speed it drives a sonic shockwave.",
                    $"Make a melee Strike against a creature in your reach; if you hit, you deal an additional 3d8 sonic damage. Regardless of the result of your Strike, that creature becomes the point of origin of a 15-foot cone, aimed directly away from you. All creatures in that cone take {S.HeightenedVariable(level + 1, 6)}d8 sonic damage (basic Fortitude save mitigates).",
                    ReachWithWeapon().WithOverriddenFullTargetLine("{b}Area{/b} 15-foot cone" +
                                                                             "\n{b}Targets{/b} 1 creature within reach" +
                                                                             "\n{b}Saving throw{/b} basic Fortitude (see text)"), 
                    level, null)
                .WithHeighteningNumerical(level, 5, combat, 1, "The damage of the cone increases by 1d8.")
                .WithProjectileCone(VfxStyle.NoAnimation())
                .WithTargetingTooltip((action, target, _) =>
                {
                    CombatAction? bestStrike = DetermineBestMeleeStrike(action.Owner);
                    return bestStrike == null ? action.Description : CombatActionExecution.BreakdownAttackForTooltip(bestStrike, target).TooltipDescription;
                })
                .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                {
                    QEffect bonus = new()
                    {
                        AddExtraKindedDamageOnStrike = (_, _) =>
                            new KindedDamage(DiceFormula.FromText("3d8", "Pulverizing Wake"),
                                DamageKind.Sonic)
                    };
                    List<CombatAction> strikes = [];
                    List<Option> options = [];
                    caster.AddQEffect(bonus);
                    strikes.AddRange(caster.MeleeWeapons.Where(wp => wp.DetermineReach(caster) >= target.DistanceToWith10FeetException(caster)).Select(weapon => caster.CreateStrike(weapon).WithActionCost(0)));
                    if (strikes.Count > 1)
                    {
                        foreach (CombatAction strike in strikes)
                        {
                            strike.WithFullRename($"Pulverizing Wake ({strike.Item?.Name})");
                            strike.Illustration = new SideBySideIllustration(strike.Illustration, spell.Illustration);
                            AlternateTaskImplements.AddDirectUsageOnACreatureOptions(target, strike, options);
                        }
                        options.Add(new CancelOption(true));
                        if (!await AlternateTaskImplements.OfferOptions(caster, options, true))
                        {
                            spell.RevertRequested = true;
                            bonus.ExpiresAt = ExpirationCondition.Immediately;
                            return;
                        }
                    }
                    else
                    {
                        CombatAction strike = strikes[0];
                        strike.WithFullRename($"Pulverizing Wake ({strike.Item?.Name})");
                        strike.Illustration = new SideBySideIllustration(strike.Illustration, spell.Illustration);
                        await caster.Battle.GameLoop.FullCast(strike, ChosenTargets.CreateSingleTarget(target));
                    }
                    bonus.ExpiresAt = ExpirationCondition.Immediately;
                    CombatAction wake = new(caster, spell.Illustration, spell.Name,
                        [Trait.Focus, Trait.Ranger, Trait.Sonic, Trait.Spell, Trait.DoNotShowOverheadOfActionName, Trait.DoNotShowInCombatLog], spell.Description,
                        Target.FifteenFootCone())
                    {
                        SpellInformation = spell.SpellInformation,
                        SpellcastingSource = spell.SpellcastingSource,
                        ActionCost = 0
                    };
                    wake.WithSoundEffect(SfxName.SoundBurst)
                        .WithProjectileCone(IllustrationName.SoundBurst, 6, ProjectileKind.Cone)
                        .WithSpellSavingThrow(Defense.Fortitude)
                        .WithEffectOnEachTarget(async (action, _, target1, result) =>
                        {
                            await CommonSpellEffects.DealBasicDamage(action, caster, target1, result,
                                DiceFormula.FromText($"{level + 1}d8", "Pulverizing Wake"), DamageKind.Sonic);
                        });
                    if (wake.Target is not CloseAreaTarget coneTarget)
                        return;
                    coneTarget.AlternateCreatureOfOrigin = target;
                    coneTarget.AlternateOriginOfAnimation = target.Occupies.ToCenterVector();
                    await caster.Battle.GameLoop.FullCast(wake,
                        CreateCloseBurst(coneTarget, GetDirectionToTarget(caster, target)));
                });
        });
        SpellIds.GluttonousGrowth = FastCreateFocusSpell("RE_GluttonousGrowth", (level, combat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("GluttonousGrowth"), "Gluttonous Growth",
                    [
                        Trait.Uncommon, Trait.Focus, Trait.Plant, Trait.Ranger, Trait.SpellWithDuration,
                        Trait.DoesNotRequireAttackRollOrSavingThrow, Trait.Rebalanced
                    ],
                    "Plants in the area grow rapidly, taking on carnivorous characteristics as they seek to consume prey.",
                    "All tiles with grass in the area are difficult terrain, both on the ground and for flying creatures. Each creature that begins its turn in the area must attempt a Reflex save unless it's already grabbed or restrained." +
                    S.FourDegreesOfSuccess("The creature is unaffected.", "The creature is grabbed until the beginning of its next turn or it Escapes.", "The creature is grabbed until the spell ends or it Escapes.", "The creature is restrained until the spell ends or it Escapes."),
                    Target.Burst(24, 4).WithAdditionalRequirementOnCaster(cr =>
                            cr.Battle.Map.AllTiles.Any(t => t.IsGrass)
                                ? Usability.Usable
                                : Usability.NotUsable("There are no plants here."))
                        .WithOverriddenFullTargetLine("{b}Range{/b} 120 feet" +
                                                      "\n{b}Area{/b} all squares that contain plants in a 20-foot burst"),
                    level, null)
                .WithSoundEffect(SfxName.Boneshaker)
                .WithHeighteningNumerical(level, 5, combat, 1,
                    "The damage dealt by the plants when you {r}Sustain{/r} the spell increases by 2d6.")
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    List<Tile> plants = targets.ChosenTiles.Where(t =>
                        t.IsGrass).ToList();
                    if (plants.Count == 0)
                    {
                        spell.RevertRequested = true; 
                        caster.Battle.Log("No plants in target area.");
                    }
                    Zone gGrowth = Zone.Spawn(caster, ZoneAttachment.StableBurst(plants));
                    gGrowth.AfterCreatureBeginsItsTurnHere = async creature =>
                    {
                        if (creature.HasEffect(QEffectId.Grabbed) || creature.HasEffect(QEffectId.Restrained)) return;
                        CheckResult save = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Fortitude);
                        if (save >= CheckResult.CriticalSuccess) return;
                        creature.AddQEffect(GluttonousGrab(spell, caster, gGrowth, save).WithExpirationOneRoundOrRestOfTheEncounter(caster, save <= CheckResult.Failure));
                    };
                    gGrowth.TileEffectCreator = _ => new TileQEffect
                    {
                        Illustration = IllustrationName.LeshyCorpse,
                        Name = "Gluttonous Growth",
                        TransformsTileIntoDifficultTerrain = true,
                        TransformsTileIntoDifficultTerrainForFlyingCreatures = true,
                        VisibleDescription = "{b}Gluttonous Growth{/b} A creature that begins its turn here may be grabbed by the carnivorous plants."
                    };
                    gGrowth.ApplySustainment(spell, async _ => await OnSustain(), $"Each time you sustain {{i}}gluttonous growth{{/i}}, creatures grabbed or restrained by the plants takes {S.HeightenedVariable((level-3)*2, 4)}d6 piercing damage.");
                    gGrowth.Apply();
                    return;
                    async Task OnSustain()
                    {
                        await caster.Battle.GameLoop.StateCheck();
                        foreach (Creature creature in caster.Battle.AllCreatures.Where(creature =>
                                     creature.FindQEffect(QEffectId.Grappled) is { } qEffect &&
                                     qEffect.Source == caster && qEffect.ReferencedSpell == spell))
                        {
                            if (creature.HasEffect(QEffectId.Grabbed) || creature.HasEffect(QEffectId.Restrained))
                                await CommonSpellEffects.DealDirectDamage(spell, DiceFormula.FromText($"{(level-3)*2}d6", "Gluttonous Growth"), creature, CheckResult.Failure, DamageKind.Piercing);
                        }
                    }
                });
        });
        SpellIds.PackBreaker = FastCreateFocusSpell("RE_PackBreaker", (level, _) =>
        {
            return Spells.CreateModern(IllustrationName.Paranoia, "Pack Breaker",
                    [Trait.Uncommon, Trait.Focus, Trait.Mental, Trait.Ranger, Trait.SpellWithDuration, Trait.Rebalanced, Trait.NoHeightening],
                    "You can deceive creatures by changing their perception of their allies' behavior.",
                    "Each affected creature suspects their allies have changed allegiance, depending on the outcome of their Will save. Regardless of the effects of the saving throw, the creature is then temporarily immune to {i}pack breaker{/i} for the rest of the encounter."+
                    S.FourDegreesOfSuccess("The creature is unaffected.", "The creature becomes unfriendly to all creatures to which it wasn't already hostile, even those that were previously allies. It treats no one as an ally for the rest of the encounter. Each of its former allies within its reach must attempt a save against {i}pack breaker{/i}.", "As success, but the creature is also confused for 1 round.", "As success, but the creature is confused until the end of the encounter."),
                    Target.Ranged(6), level, SpellSavingThrow.Standard(Defense.Will))
                .WithSoundEffect(SfxName.Mental)
                .WithActionId(RActionIds.PackBreaker)
                .WithEffectOnEachTarget(async (spell, caster, target, result) => await SpreadPackBreaker(spell, caster, target, result,
                    []));
        });
        SpellIds.HuntersVision = FastCreateFocusSpell("RE_HuntersVision", (level, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("HuntersVision"), "Hunter's Vision",
                    [
                        Trait.Uncommon, Trait.Focus, Trait.Ranger, Trait.DoesNotRequireAttackRollOrSavingThrow,
                        Trait.NoHeightening, Trait.SpellWithDuration, Trait.VerbalOnly, Trait.UnaffectedByConcealment
                    ],
                    "Your target glows with a magical aura visible only to you and those who follow your lead.",
                    "Your target is visible to you and others sharing your Hunt Prey benefits even if it wouldn't normally be due to the concealed or invisible conditions, though cover from opaque objects still blocks your sight. You ignore the flat check against the target due to the concealed condition, and the target isn't automatically hidden from you due to being invisible.",
                    Target.Ranged(6).WithAdditionalConditionOnTargetCreature((self, target) =>
                        Ranger.HasPrey(self, self, target)
                            ? Usability.Usable
                            : Usability.CommonReasons.TargetIsNotHuntedPrey), level, null)
                .WithSoundEffect(SfxName.MinorAbjuration)
                .WithActionCost(1)
                .WithEffectOnEachTarget(async (_, caster, target, _) =>
                {
                    QEffect vision = new()
                    {
                        StateCheck = qf =>
                        {
                            if (!qf.Owner.HasEffect(QEffectId.Invisible) ||
                                (!qf.Owner.DetectionStatus.UndetectedTo.Any(cr =>
                                     Ranger.HasPrey(cr, caster, qf.Owner)) &&
                                 !qf.Owner.DetectionStatus.HiddenTo.Any(cr => Ranger.HasPrey(cr, caster, qf.Owner)))) 
                                return;
                            foreach (Creature hunter in caster.Battle.AllCreatures.Where(cr => Ranger.HasPrey(cr, caster, qf.Owner)))
                            {
                                qf.Owner.DetectionStatus.UndetectedTo.Remove(hunter);
                                qf.Owner.DetectionStatus.HiddenTo.Remove(hunter);
                            }
                        }
                    };
                    vision.AddGrantingOfTechnical(cr => Ranger.HasPrey(cr, caster, target), qfTech =>
                    {
                        qfTech.YouBeginAction = async (_, action) =>
                        {
                            if (!action.ChosenTargets.Targets(target))
                                return;
                            if (target.HasEffect(QEffectId.Invisible))
                                action.WithExtraTrait(Trait.UnaffectedByConcealment);
                            if (action.StrikeModifiers is { } modifiers)
                                modifiers.HuntersAim = true;
                            else
                            {
                                action.StrikeModifiers = new StrikeModifiers()
                                {
                                    HuntersAim = true
                                };
                            }
                        };
                    });
                    target.AddQEffect(vision);
                });

        });
        #endregion
    }
    internal static QEffect GluttonousGrab(CombatAction spell, Creature caster, Zone zone, CheckResult result)
    {
        return new QEffect("Gluttonous Growth", $"You've been grabbed by carnivorous plants conjured by {caster.Name}.",
            ExpirationCondition.Never, caster, spell.Illustration)
        {
            Id = QEffectId.Grappled,
            Source = caster,
            ReferencedSpell = spell,
            Key = nameof(spell),
            Value = result == CheckResult.CriticalFailure ? 2 : 1,
            StateCheck = sc =>
            {
                if (caster.QEffects.All(qf => qf != zone.ControllerQEffect))
                {
                    sc.ExpiresAt = ExpirationCondition.Immediately;
                }
                else
                {
                    QEffect qEffect = QEffect.Grabbed(caster).WithExpirationEphemeral();
                    if (result == CheckResult.CriticalFailure)
                    {
                        qEffect = QEffect.Restrained(caster).WithExpirationEphemeral();
                    }
                    qEffect.Key = "Grabbed by Gluttonous Growth";
                    sc.Owner.AddQEffect(qEffect);
                }
            },
            ProvideContextualAction = sourceEffect =>
            {
                Creature owner5 = sourceEffect.Owner;
                SpellcastingSource? spellcastingSource = spell.SpellcastingSource;
                int flatDC = spellcastingSource?.GetSpellSaveDC(spell) ?? 10;
                return new ActionPossibility(Possibilities.CreateEscapeAgainstEffect(owner5, sourceEffect, "Grabbed", flatDC)).WithPossibilityGroup("Remove debuff");
            }
        };
    }
    private static bool CorrectKind(DamageKind kind)
    {
        return kind is DamageKind.Acid or DamageKind.Bleed or DamageKind.Fire or DamageKind.Poison
            or DamageKind.Negative;
    }

    private static Direction GetDirectionToTarget(Creature original, Creature target)
    {
        Vector2 vector21 = new(original.Occupies.X + 0.5f, original.Occupies.Y + 0.5f);
        Vector2 vector22 = new(target.Occupies.X + 0.5f, target.Occupies.Y + 0.5f);
        Vector2 vector23 = vector22 - vector21;
        vector23.Normalize();
        Direction directionFromBracket = GetDirectionFromBracket(Math.Atan2(-(double) vector23.Y, vector23.X) / 0.7853981852531433);
        return directionFromBracket;
    }
    
    private static Direction GetDirectionFromBracket(double bracket)
    {
        return bracket switch
        {
            double.NaN or >= -0.5 and <= 0.5 => Direction.East,
            >= 0.5 and <= 1.5 => Direction.Northeast,
            >= 1.5 and <= 2.5 => Direction.North,
            >= 2.5 and <= 3.5 => Direction.Northwest,
            >= 3.5 => Direction.West,
            >= -1.5 and <= -0.5 => Direction.Southeast,
            >= -2.5 and <= -1.5 => Direction.South,
            >= -3.5 and <= -2.5 => Direction.Southwest,
            <= -3.5 => Direction.West
        };
    }
    public static CreatureTarget ReachWithWeapon()
    {
        return new CreatureTarget(RangeKind.Ranged, [
            new EnemyCreatureTargetingRequirement(),
            MeleeReachCreatureTargetingRequirement.WithWeaponOfTrait(Trait.Weapon)
        ], (_, _, _) => int.MinValue);
    }
    public static ChosenTargets CreateCloseBurst(CloseAreaTarget coneAreaTarget, Direction direction)
    {
        ChosenTargets closeBurst = new();
        Vector2 vector = Areas.DirectionToVector(direction);
        HashSet<Tile> targetedTiles = Areas.DetermineTiles(coneAreaTarget, coneAreaTarget.AlternateCreatureOfOrigin?.Space.CenterTile ?? coneAreaTarget.OwnerAction.Owner.Space.CenterTile,
            (coneAreaTarget.AlternateCreatureOfOrigin?.Space.CenterTile.ToCenterVector() ??
             coneAreaTarget.OwnerAction.Owner.Space.CenterTile.ToCenterVector()) + vector).TargetedTiles;
        closeBurst.SetFromArea(coneAreaTarget, targetedTiles);
        return closeBurst;
    }
    private static CombatAction? DetermineBestMeleeStrike(Creature target)
    {
        List<CombatAction> possibleStrikes = target.MeleeWeapons
            .Select(item => CreateFreeAttackFromWeapon(item, target))
            .Where(atk => atk.CanBeginToUse(target)).ToList();
        CombatAction? bestStrike = possibleStrikes.MaxBy(combatAction =>
        {
            if (combatAction.Item?.WeaponProperties != null)
                return combatAction.Item != null ? combatAction.Item.WeaponProperties.ItemBonus : 0;
            return 0;
        });
        CombatAction? maxByStriking = possibleStrikes.MaxBy(combatAction =>
        {
            if (combatAction.Item?.WeaponProperties != null)
                return combatAction.Item != null ? combatAction.Item.WeaponProperties.DamageDieSize : 0;
            return 0;
        });
        if (maxByStriking != bestStrike && maxByStriking != null && bestStrike != null && maxByStriking.Item != null && bestStrike.Item != null && maxByStriking.Item.WeaponProperties != null && bestStrike.Item.WeaponProperties != null && maxByStriking.Item.WeaponProperties.ItemBonus == bestStrike.Item.WeaponProperties.ItemBonus)
        {
            bestStrike = maxByStriking;
        }
        return bestStrike;
    }
    private static CombatAction CreateFreeAttackFromWeapon(Item weapon, Creature target)
    {
        CombatAction attackFromWeapon = target.CreateStrike(weapon, 0).WithActionCost(0);
        return attackFromWeapon;
    }
    
    private static async Task SpreadPackBreaker(CombatAction spell, Creature caster, Creature target, CheckResult result, HashSet<Creature> visited)
    {
        {
            if (!visited.Add(target))
                return;
            QEffect immunity = QEffect.ImmunityToTargeting(RActionIds.PackBreaker);
            target.AddQEffect(immunity);
            QEffect packBreaker = new()
            {
                Name = "Pack Breaker (allies cannot help you)",
                Key = "Paranoid",
                Illustration = IllustrationName.Paranoia,
                Description =
                    "You don't trust any incoming beneficial effects and can't receive them. {i}(Your allies can't target you as though you were their ally.){/i}",
                Id = QEffectId.ParanoiaFailure,
                SubsumedBy =
                [
                    QEffectId.ParanoiaCriticalFailure
                ]
            };
            switch (result)
            {
                case CheckResult.CriticalSuccess:
                    break;
                case CheckResult.Success:
                case CheckResult.Failure:
                case CheckResult.CriticalFailure:
                    target.AddQEffect(packBreaker);
                    if (result <= CheckResult.Failure)
                        target.AddQEffect(QEffect.Confused(false, spell)
                        .WithExpirationOneRoundOrRestOfTheEncounter(caster, result == CheckResult.CriticalFailure));
                    foreach (Creature enemyAlly in target.Battle.AllCreatures.Where(cr =>
                                 cr.EnemyOf(caster) && cr != target && cr.DistanceToWith10FeetException(target) <=
                                 target.Space.ActualReach))
                    {
                        if (enemyAlly.QEffects.Any(qf => qf is { Id: QEffectId.ImmunityToTargeting, Tag: ActionId tag } && tag == RActionIds.PackBreaker))
                            continue;
                        CheckResult result2 = await CommonSpellEffects.RollSpellSavingThrowAsync(enemyAlly, spell, Defense.Will);
                        await SpreadPackBreaker(spell, caster, enemyAlly, result2, visited);
                    }
                    break;
            }
        };
    }
    
}