using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using RemasterExpanded.Technical;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

public class SkillFeats
{
    public static readonly Lore? WarfareLore = Lores.GetRegisteredLore("Warfare Lore", null);
    public static readonly Skill WarfareLoreSkill = WarfareLore?.Skill ?? Skill.Society;
    public static readonly Trait Bravado = ModManager.TryParse("Bravado", out Trait bravado) ? bravado : Trait.None;
    private static readonly string FourSuccess = S.FourDegreesOfSuccess(
        "The target gains one of the following conditions of your choice until the end of your next turn: {r}clumsy{/r} 1, {r}dazzled{/r}, {r:flat-footed}off-guard{/r}, or {r}stupefied{/r} 1. The target can end the effect by using either a single action with the concentrate trait to regain their bearings (if your strategy had the auditory trait) or an Interact action to rub their eyes (if your strategy had the visual trait). In addition, the target isn't temporarily immune to further uses of Improvise Strategy.",
        " As critical success, but the target is temporarily immune to further uses of Improvise Strategy.",
        "The target is unaffected.",
        "The target sees through the gambit, leaving you off-guard until the beginning of your next turn.");
    public static IEnumerable<Feat> Load()
    {
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_ImproviseStrategy", "Improvise Strategy"), 7,
                "You're a wild card on the battlefield, inventing unpredictable tactics and surprising foes.",
                "Choose to add either the auditory or visual trait to this action. Attempt a Warfare Lore check against the Perception DC of a creature within 30 feet, after which the target becomes temporarily immune to further uses of Improvise Strategy for the rest of the encounter." +
                FourSuccess,
                [Trait.Concentrate, Trait.General, Trait.Mental, Trait.Skill, Bravado])
            .WithActionCost(1)
            .WithPrerequisite(
                values => values.GetProficiency(WarfareLore?.Trait ?? Trait.None) >=
                          Proficiency.Master, "You must be a master in Warfare Lore.")
            .WithPermanentQEffect("You can use Warfare Lore to inflict conditions on an enemy.", qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideActionIntoPossibilitySection = (_, section) =>
                {
                    if (section.PossibilitySectionId != PossibilitySectionId.NonAttackManeuvers)
                        return null;
                    return new SubmenuPossibility(MIllustrations.CreateIllustration("Strategy"), "Improvise Strategy")
                    {
                        Subsections = 
                        [
                            new PossibilitySection("Improvise Strategy")
                            {
                                Possibilities = [new ActionPossibility(Strategies(owner, Trait.Auditory)), new ActionPossibility(Strategies(owner, Trait.Visual))]
                            }
                        ],
                        SpellIfAny = new CombatAction(owner, MIllustrations.CreateIllustration("Strategy"),"Improvise Strategy", [Trait.Concentrate, Trait.General, Trait.Mental, Trait.Skill, Bravado],
                            "Choose to add either the auditory or visual trait to this action. Attempt a Warfare Lore check against the Perception DC of a creature within 30 feet, after which the target becomes temporarily immune to further uses of Improvise Strategy for the rest of the encounter."+
                            FourSuccess,
                            Target.Ranged(6))
                    };
                };
            });
        if (!ModManager.TryParse("AidReaction", out ActionId aidReaction))
            yield break;
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_SeasonedCommand", "Seasoned Command"), 7,
                "Allies intuitively follow your orders.",
                "Choose an ally within 30 feet and then attempt a Diplomacy or Intimidation check against a hard DC for that ally’s level. This counts as a preparatory action to Aid that ally. Regardless of your result, the ally is then temporarily immune to your further uses of Seasoned Command for the rest of the encounter."+
                S.FourDegreesOfSuccess("You immediately gain an additional reaction that can be used only to Aid the chosen ally, and you gain a +1 circumstance bonus to your skill check or attack roll to Aid.", "As critical success, but you don't gain the circumstance bonus.", "You don't gain an additional reaction.", null),
                [Trait.Auditory, Trait.Concentrate, Trait.Linguistic, Trait.General, Trait.Mental, Trait.Skill])
            .WithActionCost(1)
            .WithPrerequisite(
                values => values.GetProficiency(Trait.Diplomacy) >= Proficiency.Master ||
                          values.GetProficiency(Trait.Intimidation) >= Proficiency.Master,
                "You must be a master in Diplomacy or Intimidation.")
            .WithPermanentQEffect("With a Diplomacy or Intimidation check, you can prepare to Aid and potentially gain an additional reaction for Aid.", qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideActionIntoPossibilitySection = (_, section) =>
                {
                    if (section.PossibilitySectionId != PossibilitySectionId.NonAttackManeuvers)
                        return null;
                    CombatAction command = new CombatAction(owner, MIllustrations.CreateIllustration("Command"),
                            "Seasoned Command",
                            [
                                Trait.Auditory, Trait.Concentrate, Trait.General, Trait.Linguistic, Trait.Mental,
                                Trait.Skill
                            ],
                            "You prepare to Aid that ally."+
                            S.FourDegreesOfSuccess("You immediately gain an additional reaction that can be used only to Aid the chosen ally, and you gain a +1 circumstance bonus to your skill check or attack roll to Aid.", "As critical success, but you don't gain the circumstance bonus.", "You don't gain an additional reaction.", null),
                            Target.RangedFriend(6))
                        .WithActionCost(1)
                        .WithTargetingTooltip((action, creature, _) => CombatActionExecution.BreakdownAttackForTooltip(
                            CombatAction.CreateSimple(action.Owner, "Seasoned Command").WithActiveRollSpecification(
                                new ActiveRollSpecification(
                                    TaggedChecks.BestRoll(TaggedChecks.SkillCheck(Skill.Diplomacy),
                                        TaggedChecks.SkillCheck(Skill.Intimidation)),
                                    Checks.FlatDC(Checks.LevelBasedDC(
                                        (action.ChosenTargets.ChosenCreature ?? creature).Level,
                                        SimpleDCAdjustment.Hard)))),
                            action.ChosenTargets.ChosenCreature ?? creature).TooltipDescription)
                        .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                        {
                            CombatAction resultMaker = CombatAction.CreateSimple(caster, "Seasoned Command", Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName)
                                .WithActionCost(0)
                                .WithActiveRollSpecification(
                                    new ActiveRollSpecification(
                                        TaggedChecks.BestRoll(TaggedChecks.SkillCheck(Skill.Diplomacy),
                                            TaggedChecks.SkillCheck(Skill.Intimidation)),
                                        Checks.FlatDC(Checks.LevelBasedDC(
                                            target.Level,
                                            SimpleDCAdjustment.Hard))));
                            await caster.Battle.GameLoop.FullCast(resultMaker, ChosenTargets.CreateSingleTarget(target));
                            CheckResult result = resultMaker.CheckResult;
                            QEffect additionalReaction = new("Seasoned Command", $"You have an additional reaction that you can use to Aid.{(result == CheckResult.CriticalSuccess ? " You also gain a +1 circumstance bonus to your skill check or attack roll to Aid." : "")}", ExpirationCondition.ExpiresAtStartOfYourTurn, caster, spell.Illustration)
                            {
                                OfferExtraReaction = (_, s, _) => s.Contains("Aid {icon:Reaction}") && s.Contains(target.Name) ? "Seasoned Command" : null
                            };
                            if (result == CheckResult.CriticalSuccess)
                                additionalReaction.BonusToAttackRolls = (_, action, _) =>
                                    action.ActionId != aidReaction
                                        ? null
                                        : new Bonus(1, BonusType.Circumstance, spell.Name);
                            if (result >= CheckResult.Success)
                                caster.AddQEffect(additionalReaction);
                            List<ActionPossibility> possibles = AidForCommand.CreatePrepareToAid(owner);
                            List<Option> options = [];
                            foreach (ActionPossibility possible in possibles)
                            {
                                AlternateTaskImplements.AddDirectUsageOnACreatureOptions(target, possible.CombatAction, options);
                            }
                            await AlternateTaskImplements.OfferOptions(caster, options, true);
                            target.AddQEffect(QEffect.ImmunityToTargeting(RActionIds.SeasonedCommand));
                        });
                    return new ActionPossibility(command);
                };
            });
    }

    public static CombatAction Strategies(Creature owner, Trait auditoryVisual)
    {
        bool auditory = auditoryVisual == Trait.Auditory;
        return CombatAction.CreateAction(owner,
                auditory
                    ? MIllustrations.CreateIllustration("LiberatingCommand")
                    : MIllustrations.CreateIllustration("Visual"), auditory ? "Auditory" : "Visual",
                [Bravado, auditoryVisual, Trait.Concentrate, Trait.General, Trait.Mental, Trait.Skill],
                "Attempt a Warfare Lore check against the Perception DC of a creature within 30 feet, after which the target becomes temporarily immune to further uses of Improvise Strategy for the rest of the encounter." + 
                FourSuccess.Replace("either a single action with the concentrate trait to regain their bearings (if your strategy had the auditory trait) or an Interact action to rub their eyes (if your strategy had the visual trait).", auditory ? "a single action with the concentrate trait to regain their bearings." : "an Interact action to rub their eyes."),
                Target.Ranged(6), 1, SfxName.Victory, null)
            .WithActionCost(1)
            .With(action => action.ContextMenuName = "Improvise Strategy - " +
                                                     (auditory ? "Auditory" : "Visual"))
            .WithActionId(RActionIds.ImproviseStrategy)
            .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(WarfareLoreSkill),
                TaggedChecks.DefenseDC(Defense.Perception)))
            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
            {
                switch (result)
                {
                    case CheckResult.CriticalSuccess:
                    case CheckResult.Success:
                        List<QEffect> conditions = [QEffect.Clumsy(1), QEffect.Dazzled(), QEffect.FlatFooted("Improvise Strategy"), QEffect.Stupefied(1)];
                        ChoiceButtonOption choice = await caster.AskForChoiceAmongButtons(spell.Illustration, "Select a condition to inflict.", conditions.Select(qf => qf.NameWithValue).ToArray());
                        target.AddQEffect(StrategyEffect(conditions[choice.Index], spell, caster, auditory));
                        if (result == CheckResult.CriticalSuccess)
                            return;
                        break;
                    case CheckResult.Failure:
                        break;
                    case CheckResult.CriticalFailure:
                        caster.AddQEffect(QEffect.FlatFooted("Improvise Strategy (Critical Failure)"));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
                }
                target.AddQEffect(QEffect.ImmunityToTargeting(RActionIds.ImproviseStrategy));
            });
    }

    public static QEffect StrategyEffect(QEffect effect, CombatAction combatAction, Creature source, bool auditory)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfSourcesTurn)
        {
            CannotExpireThisTurn = true,
            SourceAction = combatAction,
            Source = source,
            StateCheck = qf =>
            {
                qf.Owner.AddQEffect(effect.WithExpirationEphemeral());
            },
            ProvideContextualAction = qf =>
            {
                CombatAction endEffect = new CombatAction(qf.Owner,
                        auditory ? IllustrationName.BrainDrain : IllustrationName.RubEyes,
                        $"{(auditory ? "Regain bearings" : "Rub eyes")}",
                        [auditory ? Trait.Concentrate : Trait.Manipulate],
                        $"This action will end the {effect.Name} condition from Improvise Strategy.", 
                        Target.Self((creature, ai) =>
                        {
                            if (creature.HasEffect(QEffectId.Slowed) || creature.HasEffect(QEffectId.Stunned))
                                return float.MinValue;
                            if (creature.Battle.AllCreatures.Any(cr => cr.HasEffect(QEffectId.AttackOfOpportunity) && creature.Space.Tiles.Any(cr.Threatens)))
                            {
                                return auditory ? ai.AlwaysIfSmartAndTakingCareOfSelf : float.MinValue;
                            }
                            if (effect.IsFlatFootedTo is not null)
                            {
                                return creature.QEffects.Any(q => q.IsFlatFootedTo is not null && q != effect) ? float.MinValue : ai.AlwaysIfSmartAndTakingCareOfSelf;
                            }
                            if (effect.Id == QEffectId.Stupefied)
                            {
                                return creature.Spellcasting != null ? ai.AlwaysIfSmartAndTakingCareOfSelf : float.MinValue;
                            }
                            return ai.AlwaysIfSmartAndTakingCareOfSelf;
                        }))
                    .WithActionCost(1)
                    .WithEffectOnSelf(_ => qf.ExpiresAt = ExpirationCondition.Immediately);
                return new ActionPossibility(endEffect).WithPossibilityGroup("Remove debuff");
            }
            
        };
    }
}