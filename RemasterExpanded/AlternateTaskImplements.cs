using Dawnsbury.Core;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;

namespace RemasterExpanded;

public class AlternateTaskImplements
{
    public static void AddDirectUsageOnACreatureOptions(Creature targetedCreature,
        CombatAction combatAction,
        List<Option> options,
        bool noConfirmation = false)
    {
        TBattle battle = combatAction.Owner.Battle;
        if (!(bool)combatAction.Target.CanBeginToUse(combatAction.Owner) ||
            combatAction.Target is not CreatureTarget target)
            return;
        Creature targetCreature = targetedCreature;
        if (!(bool)target.IsLegalTarget(combatAction.Owner, targetCreature)) return;
        Option option = Option.ChooseCreature(combatAction.Name, targetCreature,
            async () =>
                _ = await battle.GameLoop.FullCast(combatAction, ChosenTargets.CreateSingleTarget(targetCreature))
                    ? 1
                    : 0, int.MinValue).WithIllustration(combatAction.Illustration);
        option.ContextMenuText = combatAction.ContextMenuName ?? combatAction.Name;
        option.SuppressFromContextMenu = combatAction.HasTrait(Trait.DoNotShowInContextMenu);
        Func<CombatAction, Creature, int, string>? tooltipCreator = combatAction.TooltipCreator;
        string? tooltip = tooltipCreator?.Invoke(combatAction, targetCreature, 0);
        if (tooltip != null)
            option.WithTooltip(tooltip);
        else if (combatAction.ActiveRollSpecification != null &&
                 (combatAction.ExcludeTargetFromSavingThrow == null ||
                  !combatAction.ExcludeTargetFromSavingThrow(combatAction, targetCreature)))
            option.WithTooltip(CombatActionExecution.BreakdownAttackForTooltip(combatAction, targetCreature)
                .TooltipDescription);
        else if (combatAction.SavingThrow != null && (combatAction.ExcludeTargetFromSavingThrow == null ||
                                                      !combatAction.ExcludeTargetFromSavingThrow(combatAction,
                                                          targetCreature)))
            option.WithTooltip(CombatActionExecution
                .BreakdownSavingThrowForTooltip(combatAction, targetCreature, combatAction.SavingThrow)
                .TooltipDescription);
        else
            option.WithTooltip(combatAction.Description);
        if (noConfirmation)
            option.NoConfirmation = true;
        option.PossibilityChain = combatAction.PossibilityChain;
        options.Add(option);
    }

    public static async Task<bool> OfferOptions(Creature selectedCreature, List<Option> options, bool midSpell)
    {
        while (true)
        {
            if (midSpell)
            {
                if (options.Count == 0) return false;
                if (options.Count == 1 || options is [_, PassOption])
                {
                    int num = await options[0].Action() ? 1 : 0;
                    selectedCreature.Actions.WishesToEndTurn = false;
                    return num == 1;
                }
            }

            selectedCreature.Battle.MovementConfirmer = null;
            if (await (await selectedCreature.Battle.SendRequest(
                    new AdvancedRequest(selectedCreature,
                            midSpell ? "Choose what action to take." : selectedCreature + "'s turn.", options)
                        { IsMainTurn = !midSpell, IsStandardMovementRequest = !midSpell })).ChosenOption
                .Action()) return true;
        }
    }
}