using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.IO;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace RemasterExpanded;

[HarmonyPatch(typeof(CommonAbilityEffects), nameof(CommonAbilityEffects.CriticalSpecializationEffect))]
internal static class PatchCrit
{
    internal static bool Prefix(CombatAction strike, Creature target, ref Task __result)
    {
        if (strike.Item is null || (!strike.Item.HasTrait(Trait.Flail) && !strike.Item.HasTrait(Trait.Hammer)) || target.HasEffect(QEffectId.Prone) || !PlayerProfile.Instance.IsBooleanOptionEnabled("RE_CritChange")) return true;
        __result = Tasks.AltCrit(strike, target);
        return false;
    }
}

[HarmonyPatch(typeof (DeitySelectionFeat), "ComposeDeityRulesText")]
internal static class PatchDeitySelectionFeat
{
    private static void Postfix(ref string __result)
    {
        __result = __result.Replace("re_", "");
    }
}

internal static class Tasks
{
    internal static async Task AltCrit(CombatAction strike, Creature target)
    {
        int dc = strike.Owner.ClassDC();
        Item weapon = strike.Item!;
        Defense critDefense = weapon.HasTrait(Trait.Hammer) ? Defense.Fortitude : Defense.Reflex;
        CheckResult save = await CommonSpellEffects.RollSavingThrowAsync(target, strike, critDefense, dc);
        if (save >= CheckResult.Success)
        {
            strike.Owner.Overhead( "critical specialization effect", Color.Orange, $"{target} was not knocked prone by {weapon.Name} as a critical specialization effect.".Capitalize());
            return;
        }
        await target.FallProne();
        strike.Owner.Overhead( "critical specialization effect", Color.Orange, $"{weapon.Name} knocked {target} prone as a critical specialization effect.".Capitalize());
    }
}