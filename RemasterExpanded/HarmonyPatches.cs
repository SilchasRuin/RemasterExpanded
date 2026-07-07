using System.Reflection;
using System.Reflection.Emit;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Champion;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using HarmonyLib;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

[HarmonyPatch(typeof(CommonAbilityEffects), nameof(CommonAbilityEffects.CriticalSpecializationEffect))]
internal static class HarmonyPatches
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

[HarmonyPatch(typeof(QEffect), nameof(QEffect.RollPersistentDamageRecoveryCheck))]
internal static class PatchForDivineHealth
{
    internal static bool Prefix(QEffect __instance, bool assisted)
    {
        if (!__instance.Owner.HasEffect(MQEffectIds.DivineHealth))
            return true;
        DamageKind damageKind = __instance.GetPersistentDamageKind();
        if (damageKind != DamageKind.Poison)
            return true;
        bool flag = false;
        foreach (QEffect qeffect in __instance.Owner.QEffects)
        {
            Func<QEffect, QEffect, DamageKind, bool>? persistentDamageRecovery = qeffect.BansPersistentDamageRecovery;
            if ((persistentDamageRecovery != null ? persistentDamageRecovery(qeffect, __instance, damageKind) ? 1 : 0 : 0) != 0)
                return false;
            if (qeffect.Id == QEffectId.OrchardsEnduranceEffect)
                flag = true;
        }
        string lower = damageKind.ToStringOrTechnical().ToLower();
        int dc = assisted ? 10 : 15;
        if (__instance.Owner.QEffects.Any(qf =>
            {
                Func<QEffect, QEffect, DamageKind, bool>? damageRecoveryCheckDc = qf.ReducesPersistentDamageRecoveryCheckDc;
                return damageRecoveryCheckDc != null && damageRecoveryCheckDc(qf, __instance, damageKind);
            }))
            dc -= 5;
        dc -= DivineHealthDCReduction(__instance.Owner);
        (CheckResult checkResult1, string str1) = Checks.RollFlatCheck(dc);
        if (flag && checkResult1 < CheckResult.Success)
        {
            (CheckResult checkResult2, string str2) = Checks.RollFlatCheck(dc);
            str1 = $"{str1} Rerolling — {str2}";
            checkResult1 = checkResult2;
        }
        string log = $"{__instance.Owner} makes a recovery check against persistent {lower} damage vs. DC {dc} ({str1})";
        if (checkResult1 >= CheckResult.Success)
        {
            __instance.ExpiresAt = ExpirationCondition.Immediately;
            __instance.Owner.Overhead("recovered", Color.Lime, log);
        }
        else
            __instance.Owner.Overhead("not recovered", Color.Black, log);
        return false;
    }
    internal static int DivineHealthDCReduction(Creature owner)
    {
        return owner.FindQEffect(MQEffectIds.DivineHealth)?.Value ?? 0;
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
