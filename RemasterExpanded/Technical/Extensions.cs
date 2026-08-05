using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Modding;

namespace RemasterExpanded.Technical;

public static class Extensions
{
    extension(Target selfTarget)
    {
        //bool should be false to use action
        public void WithAdditionalRestriction2(bool unuseable, string reasonNotUsable)
        {
            if (!unuseable) return;
            selfTarget.OwnerAction.Target = Target.Uncastable(reasonNotUsable);
        }
        //bool should be false to use action
        public Target WithAdditionalRestrictions(List<Tuple<bool, string>> additionalRestrictions)
        {
            foreach (Tuple<bool, string> additionalRestriction in additionalRestrictions.Where(additionalRestriction => additionalRestriction.Item1))
            {
                return Target.Uncastable(additionalRestriction.Item2);
            }
            return selfTarget;
        }

        public Target With(Action<Target> action)
        {
            action(selfTarget);
            return selfTarget;
        }
        
        public static CreatureTarget ActuallyAdjacent()
        {
            return new CreatureTarget(RangeKind.Melee, [new AdjacencyCreatureTargetingRequirement()],
                (_, _, _) => int.MinValue);
        }
    }

    extension(CombatAction combatAction)
    {
        public CombatAction Duplicate()
        {
            return new CombatAction(combatAction.Owner, combatAction.Illustration, combatAction.Name,
                [.. combatAction.Traits], combatAction.Description, combatAction.Target);
        }
    }

    extension(NineCornerAlignment alignment)
    {
        public bool IsGood()
        {
            return alignment is NineCornerAlignment.ChaoticGood or NineCornerAlignment.LawfulGood
                or NineCornerAlignment.NeutralGood;
        }
    }

    extension(Feat feat)
    {
        // public Feat With(Action<Feat> action)
        // {
        //     action(feat);
        //     return feat;
        // }

        public Feat WithModifiedRulesText(string toReplace, string modifiedRulesText)
        {
            feat.RulesText = feat.RulesText.Replace(toReplace, modifiedRulesText);
            return feat;
        }
    }

    extension(CheckResult result)
    {
        public CheckResult Opposite()
        {
            return result switch
            {
                CheckResult.CriticalFailure => CheckResult.CriticalSuccess,
                CheckResult.CriticalSuccess => CheckResult.CriticalFailure,
                CheckResult.Success => CheckResult.Failure,
                CheckResult.Failure => CheckResult.Success,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
            };
        }
    }
    
    extension(string text)
    {
        public string AsBlue()
        {
            return $"{{Blue}}{text}{{/Blue}}";
        }
    }
}