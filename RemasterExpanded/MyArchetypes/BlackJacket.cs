using Dawnsbury;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MyArchetypes;

public class BlackJacket
{
    public static IEnumerable<Feat> BlackJacketFeats()
    {
        Feat blackJacketDedication = ArchetypeFeats.CreateAgnosticArchetypeDedication(MTraits.BlackJacket,
            "You have entered the ranks of the Mercenary League and now wear the black uniform that's the source of the organization's popular epithet.",
            "You become trained in Intimidation; if you were already trained, you become an expert instead. You gain the Additional Lore skill feat for Warfare Lore; if you were already trained in Warfare Lore, you also become trained in a Lore skill of your choice. While wearing medium or heavy armor, you gain a +1 circumstance bonus to Intimidation checks.");
        DedicationLogic(blackJacketDedication);
        yield return blackJacketDedication;

        Feat mercenaryMotivation = new TrueFeat(MFeatNames.MercenaryMotivation, 4, "You've learned various tricks from working as a mercenary that you can apply in the field.",
            "You gain a +1 circumstance bonus to Perception checks and checks made with two skills of your choice.", []).WithAvailableAsArchetypeFeat(MTraits.BlackJacket)
            .WithOnSheet(values =>
            {
                values.AddSelectionOption(new MultipleFeatSelectionOption("MotivationSkills", "Mercenary Motivation", values.CurrentLevel, feat => feat.Tag is "MercenaryMotivation", 2));
            })
            .WithPermanentQEffect("You have a +1 circumstance bonus to Perception checks.", qf =>
            {
                qf.BonusToPerception = _ => new Bonus(1, BonusType.Circumstance, "Mercenary Motivation", true);
            });
        yield return mercenaryMotivation;
        
        foreach (Skill skill in Skills.AllSkills)
        {
            Feat motivated = new(ModManager.RegisterFeatName("MotivationSkill" + skill,  "Mercenary " + skill), "", $"You gain a +1 circumstance bonus to {skill} checks.",
                [], null);
            MercenaryMotivationLogic(motivated, skill);
            yield return motivated;
        }
        
        yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.IntimidatingStrike, MTraits.BlackJacket, 4);

        Feat belayThat = new TrueFeat(ModManager.RegisterFeatName("RE_BelayThat", "Belay That!"), 4,
                "You can recognize when a strategy isn’t working and advise your ally to change course.",
                "When an ally within 30 feet of you critically fails a Strike, you can use a {icon:Reaction} reaction. The next Strike the triggering ally makes before the end of their turn has the same multiple attack penalty as the critically failed Strike, but it counts toward their multiple attack penalty as normal.",
                [Trait.Auditory])
            .WithActionCost(-2).WithAvailableAsArchetypeFeat(MTraits.BlackJacket);
        BelayThatLogic(belayThat);
        yield return belayThat;
        Feat battlefieldAgility = new TrueFeat(ModManager.RegisterFeatName("RE_BattlefieldAgility", "Battlefield Agility"), 6,
            "Your enemies might think they have you surrounded, but you know just how to extricate yourself.",
            "Make a melee Strike against an enemy and Step, in any order. You must be flanked in order to use this ability.",
            [Trait.Flourish]).WithActionCost(1).WithAvailableAsArchetypeFeat(MTraits.BlackJacket);
        BattlefieldAgilityLogic(battlefieldAgility);
        yield return battlefieldAgility;
        Feat reactiveStrike = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.AttackOfOpportunity, MTraits.BlackJacket, 6).WithCustomName("Reactive Striker");
        yield return reactiveStrike;
        Feat shatterDefenses = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.ShatterDefenses, MTraits.BlackJacket, 8);
        yield return shatterDefenses;
        Feat leadByExample = new TrueFeat(ModManager.RegisterFeatName("RE_LeadByExample", "Lead by Example"), 8,
            "You telegraph your next attack to let your allies in on your strategy.",
            "Make a melee or ranged Strike with a –2 circumstance penalty. On a hit, the next ally to target the same creature with a Strike gains a +2 circumstance bonus to their attack roll. On a critical hit, the bonus applies to any allies who act before the start of your next turn.",
            [Trait.Flourish]).WithActionCost(1).WithAvailableAsArchetypeFeat(MTraits.BlackJacket);
        LeadByExampleLogic(leadByExample);
        yield return leadByExample;
        Feat nothingPersonal = new TrueFeat(ModManager.RegisterFeatName("RE_NothingPersonal", "Nothing Personal"), 8,
            "When you’re on a job, you can’t allow anyone to stop you.",
            "Designate a single creature you can see as being an impediment to your active course of action. The first time you Strike your impediment in a round, you deal an extra die of weapon damage. At 14th level, this increases to two extra dice, and at 20th level, this increases to three extra dice." +
            "\n\nYou can have only one creature designated as an impediment at a time. If you use Nothing Personal against a creature when you already have a creature designated, the prior creature loses the designation, and the new impediment gains the designation. Otherwise, your designation lasts until the end of the encounter.",
            [Trait.Concentrate]).WithActionCost(1).WithAvailableAsArchetypeFeat(MTraits.BlackJacket)
            .WithPrerequisite(MFeatNames.MercenaryMotivation, "Mercenary Motivation");
        NothingPersonalLogic(nothingPersonal);
        yield return nothingPersonal;
        
    }

    internal static void DedicationLogic(Feat dedication)
    {
        dedication.WithPrerequisite(values => values.GetProficiency(Trait.MediumArmor) >= Proficiency.Trained && values.GetProficiency(Trait.Martial) >= Proficiency.Trained, "You must be trained in medium armor and martial weapons.")
            .WithOnSheet(values =>
            {
                values.GrantFeat(values.GetProficiency(Trait.Intimidation) < Proficiency.Trained
                    ? FeatName.Intimidation
                    : FeatName.ExpertIntimidation);
                if (Lores.AllPublicLores.FirstOrDefault(lore => lore.Name == "Warfare Lore") is not { } wLore) return;
                if (values.GetProficiency(wLore.Trait) >= Proficiency.Trained)
                {
                    values.TrainInThisOrSubstitute(wLore);
                }
                Lores.GrantAdditionalLore(values, wLore);
            })
            .WithPermanentQEffect(null, qf =>
            {
                qf.BonusToSkillChecks = (skill, _, _) => qf.Owner.BaseArmor != null &&
                                                         qf.Owner.BaseArmor.Traits.Any(t => t is Trait.MediumArmor or Trait.HeavyArmor) &&
                                                         skill == Skill.Intimidation
                    ? new Bonus(1, BonusType.Circumstance, "Black Jacket", true)
                    : null;
            });
    }

    internal static void MercenaryMotivationLogic(Feat mercenaryMotivation, Skill skill)
    {
        mercenaryMotivation.WithPermanentQEffect($"You have a +1 circumstance bonus to {skill} checks.", qf =>
        {
            qf.BonusToSkillChecks = (skill1, _, _) => skill1 == skill ? new Bonus(1, BonusType.Circumstance, "Mercenary Motivation", true) : null;
        })
        .WithTag("MercenaryMotivation");
    }

    internal static void BelayThatLogic(Feat belayThat)
    {
        belayThat.WithPermanentQEffect("Use a reaction to allow an ally to reduce their multiple attack penalty after critically missing a strike.", qf =>
        {
            Creature self = qf.Owner;
            qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(self) && cr.DistanceTo(self) <= 6, qfTech =>
            {
                int map = 0;
                qfTech.StartOfYourEveryTurn = (_, _) =>
                {
                    map = 0;
                    return Task.CompletedTask;
                };
                qfTech.YouBeginAction = (_, action) =>
                {
                    if (!action.HasTrait(Trait.Strike)) return Task.CompletedTask;
                    map = action.Owner.Actions.AttackedThisManyTimesThisTurn;
                    return Task.CompletedTask;
                };
                qfTech.AfterYouTakeAction = async (_, action) =>
                {
                    Creature ally = qfTech.Owner; 
                    if (!self.Actions.CanTakeReaction() || action.CheckResult != CheckResult.CriticalFailure || !action.HasTrait(Trait.Strike)) return;
                    if (!await self.AskToUseReaction($"{ally} has critically missed with a Strike, would you like to use a {{icon:Reaction}} reaction to use Belay That!? ", self.Illustration)) return;
                    ally.AddQEffect(new QEffect("Belay That!", "The next attack", ExpirationCondition.ExpiresAtEndOfAnyTurn, self)
                    {
                        YouBeginAction = (_, strike) =>
                        {
                            if (!strike.HasTrait(Trait.Strike))
                                return Task.CompletedTask;
                            strike.Owner.Actions.AttackedThisManyTimesThisTurn = map;
                            return Task.CompletedTask;
                        },
                        AfterYouTakeAction = (qf2, strike) =>
                        {
                            if (!strike.HasTrait(Trait.Strike))
                                return Task.CompletedTask;
                            strike.Owner.Actions.AttackedThisManyTimesThisTurn += 1;
                            qf2.ExpiresAt = ExpirationCondition.Immediately;
                            return Task.CompletedTask;
                        },
                        Illustration = IllustrationName.Swords
                    });
                };
            });
        });
    }

    internal static void BattlefieldAgilityLogic(Feat battlefieldAgility)
    {
        battlefieldAgility.WithPermanentQEffect("When you're flanked, you can step then strike or strike then step.", qf =>
        {
            Creature self = qf.Owner;
            qf.StateCheck = _ =>
            {
                self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    ProvideStrikeModifier = weapon =>
                    {
                        if (weapon.HasTrait(Trait.Ranged))
                            return null;
                        return new CombatAction(self,
                                new SideBySideIllustration(IllustrationName.Walk, weapon.Illustration),
                                "Step, then Strike",
                                [Trait.Flourish, Trait.Basic],
                                "{i}Your enemies might think they have you surrounded, but you know just how to extricate yourself.{/i}\n\nStep, then make a melee Strike.",
                                Target.Self().WithAdditionalRestriction(creature =>
                                    creature.HasEffect(QEffectId.FlankedBy) ? null : "You must be flanked."))
                            .WithActionCost(1).WithEffectOnChosenTargets(async (spell, caster, _) =>
                            {
                                if (!await caster.StepAsync("Make a Step. Then you will Strike.", true))
                                {
                                    spell.RevertRequested = true;
                                }
                                else
                                {
                                    await caster.Battle.GameLoop.FullCast(caster.CreateStrike(weapon)
                                        .WithActionCost(0));
                                }
                            });
                    }
                });
                self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    ProvideStrikeModifier = weapon =>
                    {
                        CombatAction strike = qf.Owner.CreateStrike(weapon);
                        strike.Name = "Strike, then Step";
                        strike.Illustration = new SideBySideIllustration(strike.Illustration,
                            IllustrationName.Walk);
                        strike.Traits.Add(Trait.Basic);
                        strike.Traits.Add(Trait.Flourish);
                        strike.Description = StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
                            additionalAftertext: "Make a Step.");
                        strike.EffectOnChosenTargets = Delegates.SmartCombineDelegates(strike.EffectOnChosenTargets,
                            async (_, caster, _) =>
                                await caster.StepAsync("Make a Step.", allowPass: true));

                        return weapon.HasTrait(Trait.Melee) && self.HasEffect(QEffectId.FlankedBy) ? strike : null;
                    }
                });
            };
        });
    }

    internal static void LeadByExampleLogic(Feat leadByExample)
    {
        leadByExample.WithPermanentQEffect("Take a penalty to a Strike to grant a bonus to Strikes made by allies.", qf =>
        {
            qf.ProvideStrikeModifier = weapon =>
            {
                CombatAction strike = qf.Owner.CreateStrike(weapon);
                strike.Name = "Lead by Example";
                StrikeModifiers modify = strike.StrikeModifiers;
                if (modify.AdditionalBonusesToAttackRoll == null)
                    modify.AdditionalBonusesToAttackRoll = [new Bonus(-2, BonusType.Circumstance, "Lead by Example", false)];
                else
                {
                    modify.AdditionalBonusesToAttackRoll.Add(new Bonus(-2, BonusType.Circumstance, "Lead by Example", false));
                }
                strike.StrikeModifiers = modify;
                strike.Illustration = new SideBySideIllustration(weapon.Illustration, IllustrationName.TrueStrike);
                strike.Traits.Add(Trait.Basic);
                strike.Traits.Add(Trait.Flourish);
                strike.Description = StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers, additionalSuccessText: "The next ally to target the same creature with a Strike gains a +2 circumstance bonus to their attack roll.",
                    additionalCriticalSuccessText: "The bonus applies to any allies who act before the start of your next turn");
                strike.EffectOnChosenTargets = Delegates.SmartCombineDelegates(strike.EffectOnChosenTargets, (action, caster, target) =>
                    {
                        CheckResult result = action.CheckResult;
                        if (result < CheckResult.Success) return Task.CompletedTask;
                        List<Creature> benefitted = [];
                        QEffect lead = new("Lead by Example",
                            (result == CheckResult.CriticalSuccess ? "Each ally " : "The next ally ") +
                            $"of {caster} who targets this creature with a Strike gains a +2 circumstance bonus to their attack roll.",
                            ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, IllustrationName.TrueStrike)
                        {
                            AfterYouAreTargeted = (effect, combatAction) =>
                            {
                                if (!combatAction.HasTrait(Trait.Strike) || combatAction == action || combatAction.Owner == caster)
                                    return Task.CompletedTask;
                                if (result != CheckResult.CriticalSuccess)
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                benefitted.Add(combatAction.Owner);
                                return Task.CompletedTask;
                            }
                        };
                        lead.AddGrantingOfTechnical(cr => cr != caster && cr.FriendOf(caster), qfTech =>
                        {
                            qfTech.BonusToAttackRolls = (_, combatAction, creature) =>
                            {
                                if (!combatAction.HasTrait(Trait.Strike) || creature != target.ChosenCreature || benefitted.Contains(combatAction.Owner)) return null;
                                return new Bonus(2, BonusType.Circumstance, "Lead by Example", true);
                            };
                        });
                        target.ChosenCreatures.FirstOrDefault()?.AddQEffect(lead);
                        return Task.CompletedTask;
                    });
                return strike;
            };
        });
    }

    internal static void NothingPersonalLogic(Feat nothingPersonal)
    {
        nothingPersonal.WithPermanentQEffect("Designate a target. The first time you Strike that target in a round you deal additional weapon damage.", qf =>
        {
            qf.ProvideMainAction = _ =>
            {
                CombatAction personal = new(qf.Owner, MIllustrations.CreateIllustration("NothingPersonal"),
                    "Nothing Personal",
                    [Trait.Concentrate, Trait.Basic], 
                    "{i}When you’re on a job, you can’t allow anyone to stop you.{/i}" +
                    $"\n\nDesignate a single creature you can see as being an impediment to your active course of action. The first time you Strike your impediment in a round, you deal {(qf.Owner.Level == 20 ? "three" : qf.Owner.Level >= 14 ? "two" : "an")} extra die of weapon damage." +
                    "\n\nYou can have only one creature designated as an impediment at a time. If you use Nothing Personal against a creature when you already have a creature designated, the prior creature loses the designation, and the new impediment gains the designation. Otherwise, your designation lasts until the end of the encounter.",
                    Target.Ranged(120))
                {
                    ActionCost = 1,
                    EffectOnOneTarget = (_, caster, target, _) =>
                    {
                        var struck = false;
                        QEffect nothing = new("Nothing Personal", $"You deal {(qf.Owner.Level == 20 ? "three" : qf.Owner.Level >= 14 ? "two" : "an")} extra die of weapon damage on the first Strike made in a round against {target}.")
                        {
                            YouDealDamageWithStrike = (_, action, formula, creature) => 
                            {
                                if (creature != target || struck)
                                    return formula;
                                string dice = action.Item!.WeaponProperties!.Damage;
                                if (action.CheckResult == CheckResult.CriticalSuccess)
                                {
                                    if (action.HasTrait(Trait.FatalD8))
                                        dice = "1d8";
                                    if (action.HasTrait(Trait.FatalD10))
                                        dice = "1d10";
                                    if (action.HasTrait(Trait.FatalD12))
                                        dice = "1d12";
                                }
                                dice = dice.Remove(0, 1);
                                dice = action.Owner.Level switch
                                {
                                    20 => dice.Insert(0, "3"),
                                    >= 14 => dice.Insert(0, "2"),
                                    < 14 => dice.Insert(0, "1")
                                };

                                DiceFormula form = new ComplexDiceFormula
                                {
                                    List = [formula, DiceFormula.FromText(dice, "Nothing Personal")]
                                };
                                return form;
                            },
                            Id = MQEffectIds.NothingPersonal,
                            StartOfYourPrimaryTurn = (_, _) =>
                            {
                                struck = false;
                                return Task.CompletedTask;
                            },
                            AfterYouTakeAction = (_, strike) =>
                            {
                                if (strike.HasTrait(Trait.Strike) && strike.ChosenTargets.ChosenCreatures.Contains(target))
                                    struck = true;
                                return Task.CompletedTask;
                            },
                            Illustration = MIllustrations.CreateIllustration("NothingPersonal"),
                            StateCheck = _ =>
                            {
                                target.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                {
                                    Illustration = MIllustrations.CreateIllustration("NothingPersonal"),
                                    Name = "Nothing Personal - Target",
                                    Description = $"The first Strike made by {caster.Name} against this creature in a round deals {(caster.Level == 20 ? "3" : caster.Level >= 14 ? "2" : "an")} extra die of weapon damage."
                                });
                            }
                        };
                        caster.RemoveAllQEffects(qff => qff.Id == MQEffectIds.NothingPersonal);
                        caster.AddQEffect(nothing);
                        return Task.CompletedTask;
                    }
                };
                return new ActionPossibility(personal).WithPossibilityGroup("Abilities");
            };
        });
    }
}