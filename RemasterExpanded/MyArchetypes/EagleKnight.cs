using System.Runtime.CompilerServices;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using RemasterExpanded.Technical;
using static RemasterExpanded.FeatLoader;
using static RemasterExpanded.ModData;
using Rogue = Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Rogue;

namespace RemasterExpanded.MyArchetypes;

public class EagleKnight
{
    public static readonly Trait EagleKnightTrait = ModManager.RegisterTrait("RE_EagleKnight", new TraitProperties("Eagle Knight", false));
    public static IEnumerable<Feat> Load()
    {
        yield return ArchetypeFeats.CreateAgnosticArchetypeDedication(EagleKnightTrait, "Eagle Knights are marshals and envoys and are committed to keeping the peace. The order of Eagle Knights exists to spread the principles of equality, justice, and liberty.",
            "On the first round of combat, if you roll Diplomacy for initiative, creatures that haven't acted are off-guard to you. You become an expert in Society.")
            .WithOnSheet(values => values.GrantFeat(FeatName.ExpertSociety))
            .WithPermanentQEffect("On the first round of combat, if you roll Diplomacy for initiative, creatures that haven't acted are off-guard to you.", qf =>
            {
                qf.StartOfCombat = async _ =>
                {
                    if (qf.Owner.QEffects.Any(qff =>
                            qff.OfferAlternateSkillForInitiative?.Invoke(qff) == Skill.Diplomacy))
                    {
                        qf.Owner.AddQEffect(Rogue.SurpriseAttackQEffect());
                    }
                };
            })
            .WithPrerequisite(values => values.GetProficiency(Trait.Society) >= Proficiency.Trained, "You must be trained in Society.")
            .WithPrerequisite(values => values.GetProficiency(Trait.Diplomacy) >= Proficiency.Trained, "You must be trained in Diplomacy.");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_Interpose", "Interpose"), 4,
                "You put yourself between an ally and danger.",
                "You must end your movement adjacent to an ally. You and your ally then swap positions with each other. After changing positions, you can make a melee Strike against an enemy within your reach.",
                [Trait.Flourish])
            .WithActionCost(2)
            .WithAvailableAsArchetypeFeat(EagleKnightTrait)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                qf.ProvideMainAction = effect =>
                {
                    Creature owner = effect.Owner;
                    CombatAction interpose = CombatAction.CreateAction(owner,
                            MIllustrations.CreateIllustration("Interpose"), "Interpose", [Trait.Flourish, Trait.Basic],
                            "Stride up to your Speed. You must end your movement adjacent to an ally. You and your ally then swap positions with each other. After changing positions, you can make a melee Strike against an enemy within your reach.",
                            Target.Self().WithAdditionalRestriction(cr =>
                                cr.Battle.AllCreatures.Any(ally =>
                                    ally.FriendOfAndNotSelf(cr) && ally.DistanceTo(cr) <= cr.Speed)
                                    ? null
                                    : "No ally within range."),
                            2, SfxName.PositivePing, null)
                        .WithEffectOnSelf(async (spell, self) =>
                        {
                            if (!await self.StrideOrStepAdvancedAsync("Stride up to your Speed. You must end your movement adjacent to an ally. You will then swap positions with an adjacent ally.", allowCancel: true, permissibleTarget: tile => owner.Battle.AllCreatures.Any(cr => cr.FriendOfAndNotSelf(owner) && cr.Space.Tiles.Any(t => t.IsAdjacentTo(tile) || (!cr.Space.IsSingleSquare && tile.TilesToTheBottomRight(cr.Space.SizeInSquares).Any(t2 => t2.IsAdjacentTo(t)))))))
                            {
                                spell.RevertRequested = true;
                                return;
                            }

                            IEnumerable<Creature> adjacent =
                                self.Battle.AllCreatures.Where(ally => ally.IsAdjacentTo(self));
                            Creature ally;
                            List<Creature> enumerable = adjacent.ToList();
                            if (enumerable.Count == 1) ally = enumerable[0];
                            else
                            {
                                ally = await self.Battle.AskToChooseACreature(self, enumerable, spell.Illustration, "Select which ally to swap spaces with.",
                                    "", enumerable[0].Name) ?? enumerable[0];
                            }
                            Tile selfStart = self.Occupies;
                            Tile allyStart = ally.Occupies;
                            await self.SingleTileMove(allyStart, spell);
                            await ally.SingleTileMove(selfStart, spell);
                            await self.SingleTileMove(allyStart, spell);
                            await ally.SingleTileMove(selfStart, spell);
                            if (!self.MeleeWeapons.Any(wp => CommonRulesConditions.CouldMakeZeroCostStrike(self, wp)))
                                return;
                            List<CombatAction> possibleStrikes = self.MeleeWeapons
                                .Select(item => self.CreateStrike(item).WithActionCost(0))
                                .Where(atk => atk.CanBeginToUse(self)).ToList();
                            switch (possibleStrikes.Count)
                            {
                                case 1:
                                {
                                    await self.Battle.GameLoop.FullCast(possibleStrikes[0]);
                                    break;
                                }
                                case > 1:
                                {
                                    List<Option> options = [];
                                    foreach (CombatAction possibleStrike in possibleStrikes)
                                        GameLoop.AddDirectUsageOnCreatureOptions(possibleStrike, options);
                                    await AlternateTaskImplements.OfferOptions(self, options, true);
                                    break;
                                }
                            }
                        });
                    return new ActionPossibility(interpose).WithPossibilityGroup("Abilities");
                };
            });
        RulesTooltipRegistrations.Register(QEffect.Frightened(1).Name ?? "", IllustrationName.Frightened, "Condition", QEffect.Frightened(1).Description ?? "");
        RulesTooltipRegistrations.Register(QEffect.Stupefied(1).Name ?? "", IllustrationName.Stupefied, "Condition", QEffect.Stupefied(1).Description ?? "");
        RulesTooltipRegistrations.Register(QEffect.Enfeebled(1).Name ?? "", IllustrationName.Enfeebled, "Condition", QEffect.Enfeebled(1).Description ?? "");
        yield return ArchetypeSkillFeat("Commitment To Equality", 4,
                "You help an ally shake off any impediment that would give an enemy an unfair advantage.",
                "You rally a creature within 30 feet in an attempt to reduce its {r}frightened{/r} or {r}stupefied{/r} condition. If a creature has multiple conditions from this list, choose one. When you're a master in Diplomacy, add {r}clumsy{/r} and {r}enfeebled{/r} to the list of conditions. When you're legendary in Diplomacy, add {r}stunned{/r} to the list of conditions; if the stunned condition has a duration instead of a value, you can't use Commitment to Equality to reduce it." +
                "\n\nAttempt a Diplomacy check against the saving throw DC of the effect that caused the condition; if there was no saving throw DC, use the hard DC for the level of the creature, hazard, or item that caused the effect (or a very hard DC for the creature's level if the source cannot be determined). You can't treat a condition that is part of a curse or disease or is a natural state of the target. Once you attempt to treat a target's condition, that target is immune to further attempts for the rest of the encounter, regardless of the result."+
                S.FourDegreesOfSuccess("Reduce the condition's value by 2.", "Reduce the condition's value by 1.", null, "Increase the condition's value by 1."),
                EagleKnightTrait, MFeatNames.CommitmentToEquality, Trait.Auditory, Trait.Mental)
            .WithActionCost(2) 
            .WithPrerequisite(values => values.Proficiencies.Get(Trait.Diplomacy) >= Proficiency.Expert,
                "You must be an expert in Diplomacy.")
            .WithOnCreature((values, owner) =>
            {
                List<QEffectId> conditions = [QEffectId.Frightened, QEffectId.Stupefied];
                if (values.GetProficiency(Trait.Diplomacy) >= Proficiency.Master) conditions.AddRange([QEffectId.Clumsy, QEffectId.Enfeebled]);
                if (values.GetProficiency(Trait.Diplomacy) == Proficiency.Legendary) conditions.Add(QEffectId.Stunned);
                QEffect qf = new()
                {
                    Description = $"With a Diplomacy check, attempt to reduce the value of a {S.ConstructOrList(conditions.Select(id => id.ToStringFast().ToLower()))} condition.",
                    Name = "Commitment to Equality {icon:TwoActions}",
                    Innate = true,
                    ProvideContextualAction = _ =>
                    {
                        CombatAction equality = CommitmentToEqualityAction(owner, conditions);
                        return owner.Battle.AllCreatures.Any(cr => equality.Target is CreatureTarget creatureTarget && creatureTarget.IsLegalTarget(owner, cr)) ? new ActionPossibility(equality).WithPossibilityGroup("Remove debuff") : null;
                    }
                };
                owner.AddQEffect(qf);
            });
        yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.QuickDraw, EagleKnightTrait, 4);
        yield return new TrueFeat(MFeatNames.CommitmentToJustice, 6,
                "When your allies are harmed, you deliver retribution.",
                "{b}Requirements{/b} You witnessed a creature kill or reduce an ally to 0 Hit Points since your last turn." +
                "\n\nMake a Strike against the required creature. If this Strike hits, you gain a circumstance bonus to damage equal to three times the number of weapon damage dice.",
                [Trait.Flourish])
            .WithAvailableAsArchetypeFeat(EagleKnightTrait)
            .WithActionCost(1)
            .WithPermanentQEffect("When an ally has been reduced to 0 Hit Points you can make a more powerful Strike.",
                qf =>
                {
                    Creature owner = qf.Owner;
                    qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(owner), qfTech =>
                    {
                        qfTech.WhenMonsterDies = _ =>
                        {
                            owner.AddQEffect(CommitToJustice()
                                .WithExpirationAtEndOfSourcesNextTurn(owner, false));
                        };
                        qfTech.YouAreDealtLethalDamage = async (_, _, _, defender) =>
                        {
                            var wait = true;
                            defender.AddQEffect(new QEffect
                            {
                                StateCheck = effect =>
                                {
                                    if (wait)
                                    {
                                        wait = false;
                                        return;
                                    }
                                    if (effect.Owner.HP > 0)
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                    owner.AddQEffect(CommitToJustice()
                                        .WithExpirationAtEndOfSourcesNextTurn(owner, true));
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                }
                            });
                            return null;
                        };
                    });
                    qf.ProvideStrikeModifierAsPossibilities = (_, item) =>
                    {
                        if (item.WeaponProperties == null)
                            return [];
                        List<Possibility> justice = [];
                        StrikeModifiers strikeModifiers = new()
                        {
                            QEffectForStrike = new QEffect
                            {
                                BonusToDamage = (_, action, _) => action.ActionId != RActionIds.CommitmentToJustice || action.Item?.WeaponProperties == null ? null : new Bonus(action.Item.WeaponProperties.DamageDieCount * 3, BonusType.Circumstance, "Commitment to Justice")
                            }
                        };
                        List<CombatAction> strikes = CreateStandardAndThrownStrikes(owner, item, strikeModifiers);
                        foreach (CombatAction strike in strikes)
                        {
                            strike.WithFullRename("Commitment to Justice");
                            strike.WithActionId(RActionIds.CommitmentToJustice)
                                .WithExtraTrait(Trait.Flourish)
                                .WithExtraTrait(Trait.Basic)
                                .WithDescription(StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
                                    additionalSuccessText: $"You gain a +{item.WeaponProperties.DamageDieCount * 3} circumstance bonus to damage."));
                            if (strike.Target is CreatureTarget creatureTarget)
                            {
                                strike.Target = creatureTarget.WithAdditionalConditionOnTargetCreature((self, _) => self.HasEffect(MQEffectIds.CommitmentToJustice) ? Usability.Usable : Usability.NotUsable("No ally has been reduced to 0 HP or died in the last round."));
                            }
                            strike.Illustration = new SideBySideIllustration(MIllustrations.CreateIllustration("Justice"),
                                strike.HasTrait(Trait.Thrown) ? IllustrationName.Throw : item.Illustration);
                            justice.Add(new ActionPossibility(strike));
                        }
                        return justice;
                    };
                });
        RulesTooltipRegistrations.Register(QEffect.Grabbed(Creature.DefaultCreature).Name ?? "", IllustrationName.Grabbed, "Condition", QEffect.Grabbed(Creature.DefaultCreature.With(cr => cr.MainName = "a creature")).Description ?? "");
        RulesTooltipRegistrations.Register(QEffect.Restrained(Creature.DefaultCreature).Name ?? "", IllustrationName.Restrained, "Condition", QEffect.Restrained(Creature.DefaultCreature.With(cr => cr.MainName = "a creature")).Description ?? "");
        yield return ArchetypeFeat("Commitment to Liberty", 6,
                "You can't abide when a foe has one of your allies in its grip.",
                "Make a Strike against a creature that has an ally {r}grabbed{/r} or {r}restrained{/r}. If this Strike hits, the grabbed or restrained ally can immediately attempt to Escape as a free action. If the Strike was a critical hit, that ally gains a +2 circumstance bonus to their Escape attempt.",
                EagleKnightTrait, Trait.Flourish)
            .WithActionCost(2)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideStrikeModifierAsPossibilities = (_, item) =>
                {
                    if (item.WeaponProperties == null)
                        return [];
                    List<CombatAction> strikes = CreateStandardAndThrownStrikes(owner, item);
                    List<ActionPossibility> equality = [];
                    foreach (CombatAction strike in strikes)
                    {
                        strike.WithFullRename("Commitment to Liberty");
                        strike.WithActionCost(2)
                            .WithExtraTrait(Trait.Flourish)
                            .WithExtraTrait(Trait.Basic)
                            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
                                additionalSuccessText:
                                "An ally grabbed or restrained by the target of this attack can attempt to Escape as a free action.",
                                additionalCriticalSuccessText: "The escape attempt gains a +2 circumstance bonus."));
                        strike.Illustration = new SideBySideIllustration(MIllustrations.CreateIllustration("Liberty"),
                            strike.HasTrait(Trait.Thrown) ? IllustrationName.Throw : item.Illustration);
                        if (strike.Target is CreatureTarget creatureTarget)
                        {
                            strike.Target = creatureTarget.WithAdditionalConditionOnTargetCreature((self, enemy) =>
                                enemy.HeldItems.Any(i => i.Grapplee is { } ally && ally.FriendOfAndNotSelf(self))
                                    ? Usability.Usable
                                    : Usability.NotUsableOnThisCreature("This creature is not grappling an ally."));
                        }
                        strike.EffectOnOneTarget += async (spell, _, target, result) =>
                        {
                            if (result < CheckResult.Success || target.DeathScheduledForNextStateCheck || !target.HeldItems.Any(i => i.Grapplee is not null))
                                return;
                            List<Creature?> allies = target.HeldItems.Where(i => i.Grapplee is not null)
                                .Select(i => i.Grapplee).ToList();
                            if (allies.Count == 0)
                                return;
                            foreach (Creature ally in allies.WhereNotNull())
                            {
                                ally.RegeneratePossibilities();
                                if (ally.Possibilities.Filter(ap =>
                                    {
                                        if (ap.CombatAction.ActionId != ActionId.Escape)
                                            return false;
                                        ap.CombatAction.ActionCost = 0;
                                        ap.RecalculateUsability();
                                        return true;
                                    }).CreateActions(true).FirstOrDefault() is not CombatAction escape)
                                    return;
                                QEffect bonus = new()
                                {
                                    BonusToAttackRolls = (_, action, _) =>
                                        action.ActionId == ActionId.Escape
                                            ? new Bonus(2, BonusType.Circumstance, spell.Name)
                                            : null
                                };
                                if (result == CheckResult.CriticalSuccess)
                                    ally.AddQEffect(bonus);
                                await ally.Battle.GameLoop.FullCast(escape);
                                bonus.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        };
                        equality.Add(new ActionPossibility(strike));
                    }
                    return equality;
                };
            });
        yield return ArchetypeFeat("Bolster Ally", 8,
                "You shout encouragement to your embattled ally.",
                "Once per encounter: when one of your allies within 30 feet is targeted by a spell or ability that allows a saving throw, you can use a reaction {icon:Reaction} and that ally can use your saving throw modifier instead of their own against the triggering spell.",
                EagleKnightTrait, Trait.Auditory)
            .WithActionCost(-2)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(self), qfTech =>
                {
                    qfTech.BeforeYourSavingThrow = async (_, action, ally) =>
                    {
                        if (action.SavingThrow is not {} savingThrow || ally.Defenses.GetBaseValue(savingThrow.Defense) >= self.Defenses.GetBaseValue(savingThrow.Defense) || ally.IsImmuneTo(Trait.Auditory))
                            return;
                        if (!await self.Battle.AskToUseReaction(self,
                                $"{ally.Name} is being targeted by {action.Name} from {action.Owner} with a {savingThrow.Defense.ToString().CapitalizeEachWord()} saving throw with DC {savingThrow.DC}. Use a reaction {{icon:Reaction}} to allow your ally to use your saving throw modifier of {self.Defenses.GetBaseValue(savingThrow.Defense)} instead of their modifier of {ally.Defenses.GetBaseValue(savingThrow.Defense)}?",
                                MIllustrations.CreateIllustration("LiberatingCommand")))
                            return;
                        int differenceOfModifier = self.Defenses.GetBaseValue(savingThrow.Defense) - ally.Defenses.GetBaseValue(savingThrow.Defense);
                        QEffect bonus = new()
                        {
                            BonusToDefenses = (_, combatAction, _) => combatAction != action ? null : new Bonus(differenceOfModifier, BonusType.Untyped, "Bolster Ally"),
                            AfterYouMakeSavingThrow = (qEffect, combatAction, _) =>
                            {
                                if (combatAction != action)
                                    return;
                                qEffect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        };
                        ally.AddQEffect(bonus);
                    };
                });
            });
         yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.AttackOfOpportunity, EagleKnightTrait, 8).WithCustomName("Reactive Striker");
         yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.BlindFight, EagleKnightTrait, 10);
         yield return ArchetypeFeat("Stir Allies", 10,
                 "You shout a command for your allies to scramble.",
                 "Allies within 30 feet can use a reaction to Step. If you’re legendary in Diplomacy, they can instead Stride.",
                 EagleKnightTrait, Trait.Auditory, Trait.Flourish)
             .WithActionCost(2)
             .WithPermanentQEffectAndSameRulesText(qf =>
             {
                 Creature owner = qf.Owner;
                 qf.ProvideMainAction = _ =>
                 {
                     bool stride = owner.Proficiencies.Get(Trait.Diplomacy) >= Proficiency.Legendary;
                     CombatAction stir = CombatAction.CreateAction(owner,
                             MIllustrations.CreateIllustration("StirAllies"), "Stir Allies",
                             [Trait.Auditory, Trait.Flourish, Trait.Basic],
                             $"Allies within 30 feet can use a reaction to {(stride ? "Step or Stride" : "Step")}.",
                             Target.AlliesOnlyEmanation(6), 2, SfxName.Drum, null)
                         .WithEffectOnEachTarget(async (_, _, target, _) =>
                         {
                             if (!await target.Battle.AskToUseReaction(target, $"Would you like to use a reaction to {(stride ? "Step or Stride" : "Step")}?", target.Illustration))
                                 return;
                             if (stride)
                                 await target.StrideOrStepAsync("Choose where to stride or step to.", true, allowCancel: true);
                             else
                                 await target.StepAsync("Choose where to step to.", true);
                         });
                     return new ActionPossibility(stir).WithPossibilityGroup("Abilities");
                 };

             });
         yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.CombatReflexes, EagleKnightTrait, 12).WithCustomName("Tactical Reflexes");
         yield return ArchetypeFeat("Aura of Confidence", 12,
                 "Your will and your faith in your cause is unassailable, and the feeling is contagious.",
                 "You gain resistance to mental damage equal to half your level. You and all allies within 15 feet gain a +2 status bonus to saving throws against mental effects.",
                 EagleKnightTrait, Trait.Emotion, Trait.Mental)
             .WithPermanentQEffect("You gain resistance to mental damage equal to half your level. You and all allies within 15 feet gain a +2 status bonus to saving throws against mental effects." ,qf =>
             {
                 qf.Description = qf.Description?.Replace("equal to half your level.",
                     $"equal to {qf.Owner.Level / 2}.");
                 qf.Owner.AddQEffect(QEffect.DamageResistance(DamageKind.Mental, qf.Owner.Level / 2));
                 qf.AddGrantingOfTechnical(cr => cr.FriendOf(qf.Owner) && !cr.IsImmuneTo(Trait.Emotion) && !cr.IsImmuneTo(Trait.Mental), qfTech =>
                 {
                     qfTech.BonusToDefenses = (_, action, defense) =>
                         action != null && action.HasTrait(Trait.Mental) && defense.IsSavingThrow()
                             ? new Bonus(2, BonusType.Status, "Aura of Confidence")
                             : null;
                 });
             });
         yield return ArchetypeFeat("Talmandor's Shout", 12,
                 "After seeing an enemy harm one of your allies, you deliver a righteous shout.",
                 "{b}Frequency{/b} once per day\n" +
                 "{b}Requirements{/b} You witnessed a creature deal damage to an ally within 30 feet since your last turn.\n\n" +
                 "Attempt an Intimidation check to Demoralize, comparing the result to the Will DC of each enemy within a 60-foot emanation; this Demoralize attempt doesn't take any penalty for not sharing a language. It's possible to get a different degree of success for each target.",
                 EagleKnightTrait)
             .WithActionCost(2)
             .WithPermanentQEffect("Attempt an Intimidation check to Demoralize, comparing the result to the Will DC of each enemy within a 60-foot emanation; this Demoralize attempt doesn't take any penalty for not sharing a language.", qf =>
             {
                 Creature owner = qf.Owner;
                 qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(owner) && cr.DistanceTo(owner) <= 6 && owner.CanSee(cr), qfTech =>
                 {
                     qfTech.AfterYouTakeDamage = async (_, amount, _, action, _) =>
                     {
                         if (amount <= 0 || action is not { Owner: var enemy } || !enemy.EnemyOf(owner) || !owner.CanSee(enemy))
                             return;
                         owner.AddQEffect(new QEffect { Id = MQEffectIds.Talmandor }
                             .WithExpirationAtEndOfSourcesNextTurn(owner, false));
                     };
                 });
                 qf.ProvideMainAction = _ =>
                 {
                     CombatAction talmandor = CombatAction.CreateAction(owner,
                             MIllustrations.CreateIllustration("Talmandor"), "Talmandor's Shout", [Trait.Basic],
                             "{b}Frequency{/b} once per day\n" +
                             "{b}Requirements{/b} You witnessed a creature deal damage to an ally within 30 feet since your last turn.\n\n" +
                             "Attempt an Intimidation check to {r}Demoralize{/r}, comparing the result to the Will DC of each enemy within a 60-foot emanation; this Demoralize attempt doesn't take any penalty for not sharing a language. It's possible to get a different degree of success for each target.",
                             Target.EnemiesOnlyEmanation(12).WithAdditionalRestrictions([new Tuple<bool, string>(!owner.HasEffect(MQEffectIds.Talmandor), "You have not witnessed an enemy damage an ally within 30 feet since your last turn.")]), 2,
                             owner.HasTrait(Trait.Male) ? SfxName.RageMale1 : SfxName.RageFemale1, null)
                         .WithEffectOnChosenTargets(async (spell, self, targets) =>
                         {
                             int roll = R.NextD20();
                             CombatAction demoralize = CommonCombatActions.Demoralize(owner);
                             foreach (Creature target in targets.ChosenCreatures)
                             {
                                 CheckBreakdown breakdown = CombatActionExecution.BreakdownAttack(
                                     new CombatAction(self, null!, "Demoralize",
                                             [Trait.Basic, Trait.Mental, Trait.Emotion, Trait.Fear, Trait.Auditory],
                                             "", Target.Self())
                                         .WithActionId(ActionId.Demoralize)
                                         .WithActiveRollSpecification(new ActiveRollSpecification(
                                             TaggedChecks.SkillCheck(Skill.Intimidation),
                                             TaggedChecks.DefenseDC(Defense.Will))), target);
                                 CheckBreakdownResult breakdownResult = new(breakdown, roll);
                                 string str1 = breakdown.DescribeWithFinalRollTotal(breakdownResult);
                                 var str2 = "";
                                 switch (breakdownResult.CheckResult)
                                 {
                                     case CheckResult.CriticalSuccess:
                                         await demoralize.EffectOnOneTarget!.Invoke(demoralize, owner, target,
                                             CheckResult.CriticalSuccess);
                                         str2 = "{b}{Green}Critical Success{/}{/b} vs " + target.Name;
                                         break;
                                     case CheckResult.Success:
                                         await demoralize.EffectOnOneTarget!.Invoke(demoralize, owner, target,
                                             CheckResult.Success);
                                         str2 = "{Green}Success{/} vs " + target.Name;
                                         break;
                                     case CheckResult.Failure:
                                         await demoralize.EffectOnOneTarget!.Invoke(demoralize, owner, target,
                                             CheckResult.Failure);
                                         str2 = "{Red}Failure{/} vs " + target.Name;
                                         break;
                                     case CheckResult.CriticalFailure:
                                         await demoralize.EffectOnOneTarget!.Invoke(demoralize, owner, target,
                                             CheckResult.CriticalFailure);
                                         str2 = "{b}{Red}Critical Failure{/}{/b} vs " + target.Name;
                                         break;
                                 }
                                 var lime = Microsoft.Xna.Framework.Color.Lime;
                                 var red = Microsoft.Xna.Framework.Color.Red;
                                 DefaultInterpolatedStringHandler interpolatedStringHandler = new(10, 3);
                                 interpolatedStringHandler.AppendLiteral(" (");
                                 ref DefaultInterpolatedStringHandler local =
                                     ref interpolatedStringHandler;
                                 var d20Roll = breakdownResult.D20Roll;
                                 string str4 = d20Roll + breakdown.TotalCheckBonus.WithPlus();
                                 local.AppendFormatted(str4);
                                 interpolatedStringHandler.AppendLiteral("=");
                                 interpolatedStringHandler.AppendFormatted(breakdownResult.D20Roll +
                                                                           breakdown.TotalCheckBonus);
                                 interpolatedStringHandler.AppendLiteral(" vs. ");
                                 interpolatedStringHandler.AppendFormatted(breakdown.TotalDC);
                                 interpolatedStringHandler.AppendLiteral(").");
                                 string stringAndClear = interpolatedStringHandler.ToStringAndClear();
                                 string log = $"{str2}{stringAndClear}";
                                 string logDetails = str1;
                                 target.Overhead(breakdownResult.CheckResult.HumanizeTitleCase2(),
                                     breakdownResult.CheckResult >= CheckResult.Success ? lime : red, log, "Talmandor's Shout",
                                     logDetails);
                             }
                             self.PersistentUsedUpResources.UsedUpActions.Add(spell.Name);
                         });
                     return owner.PersistentUsedUpResources.UsedUpActions.Contains(talmandor.Name)
                         ? null
                         : new ActionPossibility(talmandor).WithPossibilityGroup("Abilities");
                 };
             })
             .WithPrerequisite(values => values.GetProficiency(Trait.Intimidation) >= Proficiency.Master,
                 "You must be a master in Intimidation.");
         yield return ArchetypeFeat("Immediate Rebuke", 14,
                 "When enemies attack your allies, you respond in kind.",
                 $"You can use {AllFeats.GetFeatByFeatName(FeatName.AttackOfOpportunity).ToLink("Reactive Strike")} when a creature within your reach Strikes one of your allies.",
                 EagleKnightTrait)
             .WithPermanentQEffectAndSameRulesText(qf =>
             {
                 qf.StartOfCombat = async effect =>
                 {
                     Creature self = effect.Owner;
                     if (self.FindQEffect(QEffectId.AttackOfOpportunity) is not {} reactiveStrike)
                         return;
                     reactiveStrike.AddGrantingOfTechnical(cr => cr.Space.Tiles.Any(self.Threatens),
                         qfTech =>
                         {
                             qfTech.YouBeginAction = async (provokerEffect2, provokingAction) =>
                             {
                                 if (provokingAction.ChosenTargets.ChosenCreature == null || AlreadyRespondedTo(reactiveStrike, provokingAction) || !provokingAction.HasTrait(Trait.Strike) || !provokingAction.ChosenTargets.ChosenCreature.FriendOfAndNotSelf(self))
                                     return;
                                 if (!(await CommonCombatActions.OfferAndMakeReactiveStrike(qf.Owner, provokerEffect2.Owner, $"{{b}}{provokerEffect2.Owner.Name}{{/b}} uses {{b}}{provokingAction.Name}{{/b}} which provokes.\nUse your reaction to make an attack of opportunity?", "*attack of opportunity*", 1,
                                         [Trait.ReactiveAttack, Trait.AttackOfOpportunity])).HasValue)
                                     return;
                                 StoreRespondedTo(reactiveStrike, provokingAction);
                             };
                         });

                 };

             })
             .WithPrerequisite(values => values.HasFeat(FeatName.AttackOfOpportunity) || values.Class is { ClassTrait: Trait.Fighter }, "You must have the Reactive Strike reaction.")
             .WithPrerequisite(MFeatNames.CommitmentToJustice, "Commitment to Justice");
         yield return ArchetypeFeat("Even the Odds", 14,
             "Even when overpowered, Eagle Knights hold out hope.",
             "{b}Frequency{/b} once per day\n\n" +
             $"If your next action is to use {AllFeats.GetFeatByFeatName(MFeatNames.CommitmentToEquality).ToLink("Commitment to Equality")}, you roll the Diplomacy check twice and take the higher result. If you succeed, the target also gains 25 temporary Hit Points.",
             EagleKnightTrait, Trait.Fortune)
             .WithActionCost(0)
             .WithOnCreature((values, owner) =>
             {
                 List<QEffectId> conditions = [QEffectId.Frightened, QEffectId.Stupefied];
                 if (values.GetProficiency(Trait.Diplomacy) >= Proficiency.Master) conditions.AddRange([QEffectId.Clumsy, QEffectId.Enfeebled]);
                 if (values.GetProficiency(Trait.Diplomacy) == Proficiency.Legendary) conditions.Add(QEffectId.Stunned);
                 const string description = "If your next action is to use Commitment to Equality, you roll the Diplomacy check twice and take the higher result. If you succeed, the target also gains 25 temporary Hit Points.";
                 owner.AddQEffect(new QEffect()
                 {
                     Name = "Even the Odds {icon:FreeAction}",
                     Description = description,
                     Innate = true,
                     ProvideContextualAction = _ =>
                     {
                         CombatAction even = CombatAction.CreateAction(owner,
                             MIllustrations.CreateIllustration("EvenTheOdds"), "Even the Odds",
                             [Trait.Fortune, Trait.Basic],
                             "{b}Frequency{/b} once per day\n\n" + description,
                             Target.Self(), 0, SfxName.PositivePing, null)
                             .WithActionId(RActionIds.EvenTheOdds)
                             .WithEffectOnSelf(async (spell, self) =>
                             {
                                 QEffect theOdds = new("Even the Odds", description, ExpirationCondition.ExpiresAtEndOfYourTurn, self, spell.Illustration)
                                 {
                                     AfterYouTakeAction = async (effect, action) =>
                                     {
                                         if (action.ActionId == RActionIds.CommitmentToEquality && action is { CheckResult: >= CheckResult.Success, ChosenTargets.ChosenCreature: {} ally })
                                         {
                                             ally.GainTemporaryHP(25);
                                         }
                                         if (action.ActionId == RActionIds.EvenTheOdds)
                                             return;
                                         effect.ExpiresAt = ExpirationCondition.Immediately;
                                     },
                                     RerollActiveRoll = async (_, _, action, _) => action.ActionId != RActionIds.CommitmentToEquality ? RerollDirection.DoNothing : RerollDirection.RerollAndKeepBest,
                                     DoNotShowUpOverhead = true
                                 };
                                 self.AddQEffect(theOdds);
                                 self.PersistentUsedUpResources.UsedUpActions.Add(spell.Name);
                             });
                         return owner.Battle.AllCreatures.Any(cr =>
                                    CommitmentToEqualityAction(owner, conditions).Target is CreatureTarget target &&
                                    target.IsLegalTarget(owner, cr)) &&
                                !owner.PersistentUsedUpResources.UsedUpActions.Contains(even.Name)
                             ? new ActionPossibility(even).WithPossibilityGroup("Remove debuff")
                             : null;
                     }
                 });
             })
             .WithPrerequisite(MFeatNames.CommitmentToEquality, "Commitment to Equality");
    }

    public static QEffect CommitToJustice()
    {
        return new QEffect
        {
            Id = MQEffectIds.CommitmentToJustice,
            Key = "CommitmentToJustice"
        };
    }

    public static List<CombatAction> CreateStandardAndThrownStrikes(Creature owner, Item item, StrikeModifiers? strikeModifiers = null)
    {
        List<CombatAction> strikes = [];
        if (item.WeaponProperties == null)
            return strikes;
        CombatAction strike1 = StrikeRules.CreateStrike(owner, item,
            item.HasTrait(Trait.Ranged) ? RangeKind.Ranged : item.WeaponProperties.Throwable ? RangeKind.Ranged : RangeKind.Melee, -1,
            item.WeaponProperties.Throwable, strikeModifiers ?? new StrikeModifiers());
        strikes.Add(strike1);
        if (!item.WeaponProperties.Throwable) return strikes;
        CombatAction strike2 = StrikeRules.CreateStrike(owner, item, RangeKind.Melee, -1, false, strikeModifiers ?? new StrikeModifiers());
        strikes.Add(strike2);
        return strikes;
    }
    
    public static void StoreRespondedTo(QEffect qEffect, CombatAction combatAction)
    {
        if (!(qEffect.Tag is List<CombatAction>))
            qEffect.Tag = new List<CombatAction>();
        ((List<CombatAction>) qEffect.Tag).Add(combatAction);
    }

    public static bool AlreadyRespondedTo(QEffect qEffect, CombatAction combatAction)
    {
        return qEffect.Tag is List<CombatAction> tag && tag.Contains(combatAction);
    }

    public static CombatAction CommitmentToEqualityAction(Creature owner, List<QEffectId> conditions)
    {
        return CombatAction.CreateAction(owner,
                MIllustrations.CreateIllustration("Equality"), "Commitment to Equality",
                [Trait.Auditory, Trait.Mental, Trait.Basic],
                $"You rally a creature within 30 feet in an attempt to reduce its {S.ConstructOrList(conditions.Select(id => id.ToStringFast().ToLower()))} condition{(conditions.Contains(QEffectId.Stunned) ? "; if the stunned condition has a duration instead of a value, you can't use Commitment to Equality to reduce it" : "")}. If a creature has multiple conditions from this list, choose one." +
                "\n\nAttempt a Diplomacy check against the saving throw DC of the effect that caused the condition; if there was no saving throw DC, use the hard DC for the level of the creature, hazard, or item that caused the effect (or a very hard DC for the creature's level if the source cannot be determined). You can't treat a condition that is part of a curse or disease or is a natural state of the target. Once you attempt to treat a target's condition, that target is immune to further attempts for 1 hour, regardless of the result." +
                S.FourDegreesOfSuccess("Reduce the condition's value by 2.", "Reduce the condition's value by 1.", null,
                    "Increase the condition's value by 1."),
                Target.RangedFriend(6).WithAdditionalConditionOnTargetCreature((_, ally) =>
                        ally.QEffects.Any(q =>
                            conditions.Contains(q.Id) &&
                            q is { Innate: false, CountsAsADebuff: true, Affliction: null } &&
                            q.ExpiresAt != ExpirationCondition.Ephemeral)
                            ? Usability.Usable
                            : Usability.NotUsableOnThisCreature("No condition to reduce."))
                    .WithAdditionalConditionOnTargetCreature((_, ally) =>
                        ally.HasEffect(MQEffectIds.CommitmentToEquality)
                            ? Usability.NotUsableOnThisCreature(
                                "Commitment to Equality can only be used on a creature once per encounter.")
                            : Usability.Usable),
                2, SfxName.Angelic, null)
            .WithEffectOnEachTarget(async (spell, caster, target, _) =>
            {
                List<ValueTuple<QEffect, int>> debuffs = [];
                foreach (QEffectId id in conditions)
                {
                    if (target.FindQEffect(id) is not { CountsAsADebuff: true, Innate: false } debuff ||
                        debuff.ExpiresAt == ExpirationCondition.Ephemeral ||
                        debuff.Traits.Any(t => t is Trait.Curse or Trait.Disease) || debuff is not
                            { Value: >= 1, Affliction: null }) continue;
                    if (debuff.SourceAction is
                        { SavingThrow: { } savingThrow, Owner: { } debuffOwner })
                    {
                        debuffs.Add(new ValueTuple<QEffect, int>(debuff,
                            savingThrow.DC(debuffOwner)));
                    }
                    else if (debuff.Dispellable is
                             {
                                 SourceSpell:
                                 { SavingThrow: { } sourceSpellSavingThrow, Owner: { } dispelOwner }
                             })
                    {
                        debuffs.Add(new ValueTuple<QEffect, int>(debuff,
                            sourceSpellSavingThrow.DC(dispelOwner)));
                    }
                    else if (debuff.FixedDC != -1)
                    {
                        debuffs.Add(new ValueTuple<QEffect, int>(debuff, debuff.FixedDC));
                    }
                    else if (debuff.Source is { } source)
                    {
                        debuffs.Add(new ValueTuple<QEffect, int>(debuff,
                            Checks.LevelBasedDC(source.Level, SimpleDCAdjustment.Hard)));
                    }
                    else
                    {
                        debuffs.Add(new ValueTuple<QEffect, int>(debuff,
                            Checks.LevelBasedDC(target.Level, SimpleDCAdjustment.VeryHard)));
                    }
                }

                if (debuffs.Count == 0)
                {
                    spell.RevertRequested = true;
                    return;
                }

                QEffect toRemove = debuffs[0].Item1;
                int dc = debuffs[0].Item2;
                if (debuffs.Count > 1)
                {
                    ChoiceButtonOption choice = await caster.AskForChoiceAmongButtons(spell.Illustration,
                        "Choose a condition to reduce.",
                        debuffs.Select(tuple => $"{tuple.Item1.NameWithValue}, DC: {tuple.Item2}").ToArray());
                    toRemove = debuffs[choice.Index].Item1;
                    dc = debuffs[choice.Index].Item2;
                }
                CombatAction resultMaker = CombatAction.CreateSimple(owner, spell.Name,
                        Trait.DoNotShowOverheadOfActionName, Trait.DoNotShowInCombatLog)
                    .WithActionCost(0)
                    .WithActionId(RActionIds.CommitmentToEquality)
                    .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy),
                        Checks.FlatDC(dc)));
                resultMaker.Target = spell.Target;
                await caster.Battle.GameLoop.FullCast(resultMaker, ChosenTargets.CreateSingleTarget(target));
                CheckResult result = resultMaker.CheckResult;
                switch (result)
                {
                    case CheckResult.CriticalSuccess:
                        toRemove.Value -= 2;
                        if (toRemove.Value <= 0)
                            toRemove.ExpiresAt = ExpirationCondition.Immediately;
                        break;
                    case CheckResult.Success:
                        toRemove.Value -= 1;
                        if (toRemove.Value <= 0)
                            toRemove.ExpiresAt = ExpirationCondition.Immediately;
                        break;
                    case CheckResult.Failure:
                        break;
                    case CheckResult.CriticalFailure:
                        toRemove.Value += 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                target.AddQEffect(new QEffect { Id = MQEffectIds.CommitmentToEquality });
            });
    }
}