using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using static Dawnsbury.Mods.MoreBasicActions.ModData;
using Traits = Dawnsbury.Core.Mechanics.Traits;

namespace RemasterExpanded;

public class AidForCommand
{
    public static string BasicAidReactionDescription =>
        """
        {b}Aid{b} {icon:Reaction}
        {b}Trigger{/b} An ally is about to attempt a check, and you prepared to aid that ally's check.
        {b}Effect{/b} Attempt the same check you prepared to aid with a DC of 
        """ +
        AidDC() + "." + S.FourDegreesOfSuccess(
            "You grant your ally a +2 circumstance bonus to the triggering check. The bonus increases to +3 if you're a master with the check, or +4 if you're legendary.",
            "You grant your ally a +1 circumstance bonus to the triggering check.",
            "No effect.",
            "Your ally takes a -1 circumstance penalty to the triggering check.");
    public static List<ActionPossibility> CreatePrepareToAid(Creature owner)
    {
        List<ActionPossibility> possibilities = [];
        
        foreach (Skill skill in Skills.AllSkills)
        {
            if (!PlayerProfile.Instance.IsBooleanOptionEnabled(BooleanOptions.UntrainedAid)
                && owner.Proficiencies.Get(Skills.SkillToTrait(skill)) == Proficiency.Untrained)
                continue;
            ActionPossibility skillAid = new(CreatePrepareToAidSkill(owner, skill))
            {
                Caption = skill.ToStringOrTechnical()
            };
            possibilities.Add(skillAid);
        }
        CombatAction prepare = CreatePrepareToAidAttack(owner);
        
        ActionPossibility attackAid = new(prepare)
        {
            Caption = "Attack roll"
        };
        possibilities.Add(attackAid);
        return possibilities;
    }

    public static CombatAction CreatePrepareToAidSkill(Creature owner, Skill skill)
    {
        Proficiency rank = owner.Proficiencies.Get(Skills.SkillToTrait(skill));
        
        CombatAction prepare = CreatePrepareToAid(
                owner,
                skill.ToStringOrTechnical(),
                new ActiveRollSpecification(
                    TaggedChecks.SkillCheck(skill),
                    Checks.FlatDC(AidDC())),
                rank,
                [],
                ca => ca.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill == skill);

        return prepare;
    }

    public static CombatAction CreatePrepareToAidAttack(Creature owner)
    {
        Proficiency rank = Proficiency.Untrained;
        CombatAction? mostProficientAttack = null;
        owner.RegeneratePossibilities();
        if (owner.Possibilities.Filter(ap => ap.CombatAction.HasTrait(Trait.Strike))
                     .CreateActions(false) is { Count: > 0 } strikeList)
        {
            Proficiency highestAnyProficiency = strikeList
                .Max(ica => owner.Proficiencies.Get(ica.Action.Item?.Traits ?? []));
            List<CombatAction> mostProficientAttacks = strikeList
                .FindAll(ica =>
                    owner.Proficiencies.Get(ica.Action.Item?.Traits ?? []) == highestAnyProficiency
                    && ica.Action.ActiveRollSpecification != null)
                .Select(ica => ica.Action)
                .ToList();
            mostProficientAttack = mostProficientAttacks.FirstOrDefault();
            if (mostProficientAttack?.Item != null)
                rank = highestAnyProficiency;
        }

        List<Trait> bonusTraits = [];
        if (mostProficientAttack is not null)
        {
            foreach (Trait trait in (List<Trait>)[Trait.Melee, Trait.Ranged, Trait.Finesse, Trait.DoNotAddStrengthToAttack, Trait.Brutal])
                if (mostProficientAttack.HasTrait(trait))
                    bonusTraits.Add(trait);
        }
            
        CombatAction prepare = CreatePrepareToAid(
                owner,
                "Attack",
                new ActiveRollSpecification(
                    Checks.Attack(mostProficientAttack?.Item ?? new Item(IllustrationName.None, "Unarmed", [Trait.Unarmed]), 0),
                    Checks.FlatDC(AidDC())),
                rank,
                bonusTraits,
                ca => // Must be an attack roll without an involved skill
                    ca.HasTrait(Trait.Attack)
                    && ca.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill is null)
            .WithExtraTrait(Trait.ReactiveAttack);
        
        return prepare;
    }

    public static CombatAction CreatePrepareToAid(Creature owner, string? subtitle, ActiveRollSpecification rollSpec, Proficiency rank, List<Trait> bonusTraits, Func<CombatAction,bool> isAidableAction)
    {
        return new CombatAction(
            owner,
            IllustrationName.Action,
            "Prepare to Aid" + (subtitle is not null ? " (" + subtitle + ")" : ""),
            [Trait.Basic],
            CreatePrepareToAidDescription(rank, subtitle),
            Target.RangedCreature(99)
                .WithAdditionalConditionOnTargetCreature((a,d) =>
                    a == d ? Usability.NotUsableOnThisCreature("self") : Usability.Usable))
        .WithActionCost(0)
        .WithActionId(ActionIds.PrepareToAid)
        .WithSoundEffect(SfxName.OpenPage)
        .WithTargetingTooltip((action, target, _) =>
        {
            CombatAction aidReaction = CreateAidReaction(action.Owner, rollSpec, rank, bonusTraits, CombatAction.CreateSimple(action.Owner, ""));
            CheckBreakdown breakdown = CombatActionExecution.BreakdownAttackForTooltip(aidReaction, target);
            return breakdown.TooltipDescription;
        })
        .WithEffectOnEachTarget(async (action, aider, aidee, _) =>
        {
            bool isEnemy = aider.EnemyOf(aidee);

            string checkName = rollSpec.TaggedDetermineBonus.InvolvedSkill is { } skill
                ? skill.ToStringOrTechnical() + " check"
                : "Attack roll";
            
            QEffect canBeAided = AidEffect(
                aider, rollSpec, rank, bonusTraits,
                isEnemy
                    ? checkName + "s against you"
                    : "your " + checkName + "s",
                isAidableAction, isEnemy);
            canBeAided.SourceAction = action;
                
            aidee.AddQEffect(canBeAided);
        });
    }

    public static QEffect AidEffect(
        Creature aider,
        ActiveRollSpecification rollSpec,
        Proficiency rank,
        List<Trait> bonusTraits,
        string aidWhat,
        Func<CombatAction,bool> isAidableAction,
        bool aidActionsAgainstMe)
    {
        QEffect aidEffect = new(
            "Receiving Aid",
            $"{{Blue}}{aider}{{/Blue}} can Aid {{icon:Reaction}} {aidWhat}. They must be next to you when the check is made to do so.",
            ExpirationCondition.ExpiresAtStartOfSourcesTurn,
            aider,
            Illustrations.Aid)
        {
            Id = QEffectIds.PreparedToAid,
            Tag = rollSpec
        };
        
        if (aidActionsAgainstMe)
            aidEffect.YouAreTargeted = async (qfThis, action) =>
            {
                if (action.ActionId == ActionIds.PrepareToAid // Can't be Prepare to Aid
                    || action.ActionId == ActionIds.AidReaction // Can't be Aid
                    || !action.Owner.FriendOfAndNotSelf(qfThis.Source!) // Must be from someone else
                    || action.ActiveRollSpecification is null // Must have an active roll spec
                    || !isAidableAction(action))
                    return;

                string checkName = action.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill is { } skill
                    ? skill.ToStringOrTechnical() + " check"
                    : "Attack roll";

                if (await qfThis.Owner.Battle.AskToUseReaction(
                        qfThis.Source!,
                        $$"""
                          {b}Aid {icon:Reaction}{/b}
                          {{action.Owner.Name.WithColor("Blue")}} is about to make {{AorAn(checkName)}} {{checkName.WithColor("Blue")}} with {{action.Name.WithColor("Blue")}} against {{qfThis.Owner.Name.WithColor("Blue")}}.
                          """,
                        Illustrations.Aid,
                        ["Aid {icon:Reaction}"]) == 0)
                {
                    if (await DoAidReaction(
                            qfThis.Source!,
                            (ActiveRollSpecification)qfThis.Tag!,
                            rank,
                            bonusTraits,
                            action)
                        is not null)
                        qfThis.ExpiresAt = ExpirationCondition.Immediately;
                }
            };
        else
            aidEffect.BeforeYourActiveRoll = async (qfThis, action, defender) =>
            {
                if (action.ActionId == ActionIds.PrepareToAid // Can't be Prepare to Aid
                    || action.ActionId == ActionIds.AidReaction // Can't be Aid
                    || !action.Owner.FriendOfAndNotSelf(qfThis.Source!) // Must be from someone else
                    || !isAidableAction(action))
                    return;

                string checkName = action.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill is { } skill
                    ? skill.ToStringOrTechnical() + " check"
                    : "Attack roll";

                if (await qfThis.Owner.Battle.AskToUseReaction(
                        qfThis.Source!,
                        $$"""
                          {b}Aid {icon:Reaction}{/b}
                          {{qfThis.Owner.Name.WithColor("Blue")}} is about to make {{AorAn(checkName)}} {{checkName.WithColor("Blue")}} with {{action.Name.WithColor("Blue")}} against {{defender.Name.WithColor("Blue")}}.
                          """,
                        Illustrations.Aid,
                        ["Aid {icon:Reaction}"]) == 0)
                {
                    if (await DoAidReaction(
                            qfThis.Source!,
                            (ActiveRollSpecification)qfThis.Tag!,
                            rank,
                            bonusTraits,
                            action)
                        is not null)
                        qfThis.ExpiresAt = ExpirationCondition.Immediately;
                }
            };
        
        return aidEffect;
    }

    public static async Task<CheckResult?> DoAidReaction(Creature aider, ActiveRollSpecification rollSpec, Proficiency rank, List<Trait> bonusTraits, CombatAction aidableAction)
    {
        CombatAction aidReaction = CreateAidReaction(aider, rollSpec, rank, bonusTraits, aidableAction);
        if (!await aider.Battle.GameLoop.FullCast(
                aidReaction,
                ChosenTargets.CreateSingleTarget(aidableAction.Owner)))
            return null;
        return aidReaction.CheckResult;
    }

    public static CombatAction CreateAidReaction(Creature aider, ActiveRollSpecification rollSpec, Proficiency rank, List<Trait> bonusTraits, CombatAction aidableAction)
    {
        CombatAction aidReaction = new CombatAction(
                aider,
                Illustrations.Aid,
                "Aid (" + (rollSpec.TaggedDetermineBonus.InvolvedSkill?.ToStringOrTechnical() ?? "Attack") + ")",
                [Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, ..bonusTraits],
                CreateAidReactionDescription(rank).Replace("{b}Aid{b} {icon:Reaction}\n", ""),
                Target.RangedCreature(99))
            .WithActiveRollSpecification(rollSpec)
            .WithSoundEffect(SfxName.Grapple)
            // Post action log before roll, just like a normal usage of an action.
            .WithPrologueEffectOnChosenTargetsBeforeRolls(async (action, aider2, targs) =>
            {
                aider2.Overhead(
                    "Aid {icon:Reaction}",
                    Color.Black,
                    $"{aider2.Name.WithColor("Blue")} {ProficiencyAdjective(rank)} {{b}}Aids{{/b}} {{icon:Reaction}} {targs.ChosenCreature!.Name.WithColor("Blue")}.",
                    "Aid {icon:Reaction}",
                    action.Description,
                    new Traits([]));
            })
            .WithEffectOnEachTarget(async (action, aider2, aidee, result) =>
            {
                int bonus;
                switch (result)
                {
                    case CheckResult.CriticalSuccess:
                        bonus = CriticalBonusFromProficiency(rank);
                        break;
                    case CheckResult.Success:
                        bonus = 1;
                        break;
                    case CheckResult.CriticalFailure:
                        bonus = -1;
                        break;
                    default: // Failure or other
                        return;
                }
        
                aidee.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    BonusToAttackRolls = (_, aidedAction, _) =>
                        aidedAction == aidableAction
                            ? new Bonus(bonus, BonusType.Circumstance, $"Aid {result.HumanizeLowerCase2()}")
                            : null,
                });
            });
        
        return aidReaction;
    }

    /// <summary>
    /// Generates the full rules string for the Prepare to Aid action card.
    /// </summary>
    /// <param name="rank">The proficiency rank of the skill or attack being used when aiding.</param>
    /// <param name="checkName">The name of the skill, or the string "Attack".</param>
    /// <returns></returns>
    public static string CreatePrepareToAidDescription(Proficiency rank, string? checkName)
    {
        bool isAttack = checkName?.Contains("Attack") ?? false;
        string checkDesc = $"{AorAn(checkName ?? "check")} {{Blue}}{checkName ?? "???"}{{/Blue}} {(isAttack ? "roll" : "check")}";
        string flavorText = "{i}You prepare to help your ally with a task outside your turn.{/i}";
        string rulesText = $"Choose an ally or enemy. When that ally makes {checkDesc} while within your reach, or that enemy is targeted by {checkDesc} while within your reach, you can use the aid {{icon:Reaction}} reaction for that {(isAttack ? "roll" : "check")} as the trigger.";
        return flavorText + "\n\n" + rulesText + "\n\n" + CreateAidReactionDescription(rank);
    }

    /// <summary>
    /// Generates the full rules string for the Aid reaction card.
    /// </summary>
    /// <param name="rank">The proficiency rank of the skill or attack being used when aiding.</param>
    public static string CreateAidReactionDescription(Proficiency rank)
    {
        return BasicAidReactionDescription.Replace("You grant your ally a +2 circumstance bonus to the triggering check. The bonus increases to +3 if you're a master with the check, or +4 if you're legendary.", $"{{Blue}}({rank.ToString()}){{/Blue}} You grant your ally a +{CriticalBonusFromProficiency(rank)} circumstance bonus to the triggering check.");
    }
    
    public static int CriticalBonusFromProficiency(Proficiency proficiency)
    {
        switch (proficiency)
        {
            case Proficiency.Legendary:
                return 4;
            case Proficiency.Master:
                return 3;
            default:
                return 2;
        }
    }

    public static string AorAn(string check)
    {
        //check.WithIndefiniteArticle();
        switch (check.ToUpper()[0])
        {
            case 'A':
                return "an";
            case 'I':
                return "an";
            case 'O':
                return "an";
            default:
                return "a";
        }
    }

    public static string ProficiencyAdjective(Proficiency rank)
    {
        switch (rank)
        {
            case Proficiency.Legendary: return "legendarily";
            case Proficiency.Master: return "masterfully";
            case Proficiency.Expert: return "expertly";
            case Proficiency.Trained: return "professionally";
            case Proficiency.UntrainedWithLevel: return "competently";
            case Proficiency.Untrained: return "clumsily";
        }
        throw new ArgumentException("Invalid proficiency to MoreBasicActions.Aid.ProficiencyAdjective(Proficiency)");
    }

    public static int AidDC()
    {
        return PlayerProfile.Instance.IsBooleanOptionEnabled(BooleanOptions.AidDCIs15) ? 15 : 20;
    }
}