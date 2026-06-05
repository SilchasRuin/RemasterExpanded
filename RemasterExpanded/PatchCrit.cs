using System.Threading.Tasks;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Tiles;
using Dawnsbury.IO;
using HarmonyLib;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;

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

[HarmonyPatch(typeof(CombatAction), nameof(CombatAction.DoesThisVersatileMeleeActionCountsAsMeleeAgainst))]
internal static class PatchVersatileMelee
{
    internal static bool Prefix(CombatAction __instance, Creature? targetedCreature, ref bool __result)
    {
        if (__instance.ActionId != MActionIds.PsychicIgnition || targetedCreature is null)
            return true;
        __result = __instance.Owner.DistanceToWith10FeetException(targetedCreature) <= __instance.Owner.Space.NaturalReach + 1;
        return false;
    }
}

[HarmonyPatch(typeof(Space), "CalculateActualReach")]
internal static class PatchReach
{
    internal static bool Prefix(Space __instance, ref int __result)
    {
        if (!__instance.Self.HasEffect(MQEffectIds.IncreasedReach) && !__instance.Self.HasEffect(MQEffectIds.LungingReach))
            return true;
        if (__instance.Self.HasEffect(MQEffectIds.IncreasedReach))
            __result = __instance.NaturalReach + 1;
        else if (__instance.Self.HasEffect(MQEffectIds.LungingReach))
        {
            int num = __instance.Self.MeleeWeapons.Max(itm => itm.DetermineReach(__instance.Self));
            __result = num + 1;
        }
        return false;
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