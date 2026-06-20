using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics.Targeting;

namespace RemasterExpanded;

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
    }

    extension(CombatAction combatAction)
    {
        public CombatAction Duplicate()
        {
            return new CombatAction(combatAction.Owner, combatAction.Illustration, combatAction.Name, combatAction.Traits.ToArray(), combatAction.Description, combatAction.Target);
        }
    }
}