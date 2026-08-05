using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Champion;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Specific;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Controls.Listbox;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Phases.Menus.CharacterBuilderPages;
using HarmonyLib;
using Microsoft.Xna.Framework;
using RemasterExpanded.ClassChangesAndFeats;
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
        if (__instance.ActionId != RActionIds.PsychicIgnition || targetedCreature is null)
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
            if ((persistentDamageRecovery != null
                    ? persistentDamageRecovery(qeffect, __instance, damageKind) ? 1 : 0
                    : 0) != 0)
                return false;
            if (qeffect.Id == QEffectId.OrchardsEnduranceEffect)
                flag = true;
        }

        string lower = damageKind.ToStringOrTechnical().ToLower();
        int dc = assisted ? 10 : 15;
        if (__instance.Owner.QEffects.Any(qf =>
            {
                Func<QEffect, QEffect, DamageKind, bool>? damageRecoveryCheckDc =
                    qf.ReducesPersistentDamageRecoveryCheckDc;
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

        string log =
            $"{__instance.Owner} makes a recovery check against persistent {lower} damage vs. DC {dc} ({str1})";
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

[HarmonyPatch]
internal static class PatchInStudiousSpells
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method = typeof(SpellPreparationPage)
            .GetNestedTypes(AccessTools.all)
            .SelectMany(t => t.GetMethods(AccessTools.all))
            .FirstOrDefault(m => m.Name.Contains("g__CreateItems"));
        return method == null ? throw new Exception("Can't find CreateItems!") : method;
    }

    private static FieldInfo? _outerLocalField;
    private static FieldInfo? _sheetField;
    private static FieldInfo? _classOfOriginField;
    private static FieldInfo? _featItemColorBackingField;
    private static void Postfix(object __instance, List<ListboxItem> __result)
    {
        if (_outerLocalField == null)
        {
            _outerLocalField = AccessTools.Field(__instance.GetType(), "CS$<>8__locals1");
            Type outerType = _outerLocalField.FieldType;
            _sheetField = AccessTools.Field(outerType, "sheet");
            _classOfOriginField = AccessTools.Field(outerType, "classOfOrigin");
            _featItemColorBackingField = AccessTools.Field(typeof(SpellListboxItem), "<FeatItemColor>k__BackingField");
        }
        object? outer = _outerLocalField.GetValue(__instance);
        CharacterSheet? sheet = (CharacterSheet?)_sheetField?.GetValue(outer);
        var classOfOrigin = (Trait?)_classOfOriginField?.GetValue(outer);
        if (outer == null ||  sheet == null || classOfOrigin == null)
            return;
        if (classOfOrigin != Trait.Magus)
            return;
        if (!sheet.Calculated.Tags.TryGetValue("StudiousSpells", out object? studious) || studious is not List<SpellId> studiousSpells)
            return;
        foreach (ListboxItem listboxItem in __result)
        {
            if (listboxItem is not SpellListboxItem spellListboxItem || !studiousSpells
                    .Contains(spellListboxItem.Spell.SpellId)) continue;
            spellListboxItem.RightText = "Studious Spell";
            _featItemColorBackingField?.SetValue(spellListboxItem, FeatItemColor.LightGreenUnlocked);
        }
    }
}

[HarmonyPatch]
internal static class PatchSpellStrike
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method = typeof(SpellcastingStrikes)
            .GetNestedTypes(AccessTools.all)
            .SelectMany(t => t.GetMethods(AccessTools.all))
            .FirstOrDefault(m => m.Name.Contains("g__CreateSpellcastingStrike"));
        return method == null ? throw new Exception("Can't find CreateSpellcastingStrike!") : method;
    }
    private static FieldInfo? _spellstrikerField;
    private static FieldInfo? _weaponField;
    private static FieldInfo? _nameField;
    private static FieldInfo? _spellcastingStrikeKindField;
    private static FieldInfo? _postSpellcastField;
    private static FieldInfo? _postActionTextField;
    private static bool Prefix(object __instance, 
        ref CombatAction? __result, 
        CombatAction spell,
        string prologue,
        string aftertext,
        bool spellSwipe,
        bool overwhelmingSpellstrike)
    {
        if (_spellstrikerField == null)
        {
            Type type = __instance.GetType();
            _spellstrikerField = AccessTools.Field(type, "spellstriker");
            _weaponField = AccessTools.Field(type, "weapon");
            _nameField = AccessTools.Field(type, "name");
            _spellcastingStrikeKindField = AccessTools.Field(type, "spellcastingStrikeKind");
            _postSpellcastField = AccessTools.Field(type, "postSpellcast");
            _postActionTextField = AccessTools.Field(type, "postActionText");
        }
        Creature? spellstriker = (Creature?)_spellstrikerField?.GetValue(__instance);
        Item? weapon = (Item?)_weaponField?.GetValue(__instance);
        var name = (string?)_nameField?.GetValue(__instance);
        var spellcastingStrikeKind = (SpellcastingStrikes.SpellcastingStrikeKind?)_spellcastingStrikeKindField?.GetValue(__instance);
        Action? postSpellcast = (Action?)_postSpellcastField?.GetValue(__instance);
        var postActionText = (string?)_postActionTextField?.GetValue(__instance);
        if (spellcastingStrikeKind == null || weapon == null || name == null || postSpellcast == null || spellstriker == null)
            return true;
        __result = MagusRemaster.CreateSpellcastingStrike(spell, spellcastingStrikeKind.Value, weapon, name, postActionText, postSpellcast, spellstriker, prologue, aftertext, spellSwipe, overwhelmingSpellstrike);
        return false;
    }
}

[HarmonyPatch(typeof(RulesBlock), nameof(RulesBlock.GetIconTextFromNumberOfActions))]
internal static class PatchRulesBlock
{
    internal static bool Prefix(int actions, ref string __result)
    {
        if (actions != 7)
            return true;
        __result = "";
        return false;
    }
}

[HarmonyPatch(typeof(Areas))]
[HarmonyPatch(nameof(Areas.DetermineTiles), typeof(CloseAreaTarget), typeof(Tile), typeof(Vector2))]
internal static class PatchAreas
{
    internal static void Postfix(CloseAreaTarget closeAreaTarget,
        Tile originTile,
        Vector2 targetPoint,
        ref AreaSelection __result)
    {
        if (closeAreaTarget is not LineAreaTarget { IsBurningJet: true, BlockedByCreatures: false })
            return;
        HashSet<Tile> tilesExcluded = __result.ExcludedTiles;
        Creature owner = closeAreaTarget.OwnerAction.Owner;
        foreach (Tile tile in tilesExcluded.Where(tile => !tile.AlwaysBlocksMovementOfSingleTile(owner)))
        {
            __result.ExcludedTiles.Remove(tile);
            __result.TargetedTiles.Add(tile);
        }
        if (LineAreaTarget.DetermineFinalTile(originTile, [.. __result.TargetedTiles]) is not { } finalTile)
            return;
        if (!finalTile.CanIStopMyMovementHere(owner) || __result.ExcludedTiles.Count > 0)
        {
            __result.ExcludedTiles.AddRange(__result.TargetedTiles);
            __result.TargetedTiles.Clear();
        }
        if (closeAreaTarget.OwnerAction.Item is {} weapon)
        {
            if (!owner.Battle.AllCreatures.Any(cr =>
                    cr.EnemyOf(owner) && cr.DistanceToWith10FeetException(finalTile) <= weapon.DetermineReach(owner)))
            {
                __result.ExcludedTiles.AddRange(__result.TargetedTiles);
                __result.TargetedTiles.Clear();
            }
        }
        __result = __result.VerifyForOverallLegality(closeAreaTarget.OwnerAction);
    }
}

[HarmonyPatch(typeof(HiddenRules), nameof(HiddenRules.DetermineConcealmentCheckDC))]
internal static class PatchConcealmentCheckDC
{
    internal static void Postfix(CombatAction action, Creature target, ref int? __result)
    {
        if (!action.Owner.HasEffect(MQEffectIds.StarlitEyes) || !action.HasTrait(Trait.Ranged))
        {
            return;
        }
        if (__result is null or 0)
        {
            return;
        }
        DetectionStrength hidden = HiddenRules.DetermineHidden(action.Owner, target);
        switch (hidden)
        {
            case >= DetectionStrength.Hidden when __result < 11:
            case >= DetectionStrength.ConcealedViaBlur when __result < 5:
                return;
            default:
                __result -= action.Owner.HasEffect(QEffectId.ArcaneCascade) ? 2 : 1;
                break;
        }
    }
}

[HarmonyPatch(typeof(CombatActionExecution), "CheckForMissChance")]
internal static class PatchCheckForMissChance
{
    private static FieldInfo? _action;
    private static FieldInfo? _user;
    private static FieldInfo? _isMelee;
    private static FieldInfo? _isRanged;
    private static FieldInfo? _isMultipleRangedWithAnimation;
    private static FieldInfo? _forcedSimpleMiss;
    internal static bool Prefix(Creature target, StringBuilder checkDescription, CombatActionExecution __instance, ref Task<bool> __result)
    {
        if (_action is null)
        {
            Type type = __instance.GetType();
            _action = AccessTools.Field(type, "action");
            _user = AccessTools.Field(type, "user");
            _isMelee = AccessTools.Field(type, "isMelee");
            _isRanged = AccessTools.Field(type, "isRanged");
            _isMultipleRangedWithAnimation = AccessTools.Field(type, "isMultipleRangedWithAnimation");
            _forcedSimpleMiss = AccessTools.Field(type, "forcedSimpleMiss");
        }
        CombatAction? action = _action.GetValue(__instance) as CombatAction;
        Creature? user = _user?.GetValue(__instance) as Creature;
        bool isMelee = _isMelee?.GetValue(__instance) as bool? ?? false;
        bool isRanged = _isRanged?.GetValue(__instance) as bool? ?? false;
        bool isMultiple =  _isMultipleRangedWithAnimation?.GetValue(__instance) as bool? ?? false;
        if (action == null || user == null)
            return true;
        if (!user.HasEffect(MQEffectIds.StarlitEyes))
            return true;
        __result = CheckForMissChance();
        return false;
        async Task<bool> CheckForMissChance()
        {
            if (action.HasTrait(Trait.AlwaysHits))
                return false;
            foreach (QEffect fizzler in target.QEffects)
            {
                if (fizzler.FizzleIncomingActions != null)
                {
                    if (await fizzler.FizzleIncomingActions(fizzler, action, checkDescription))
                    {
                        user.Battle.Log($"Fizzles due to {fizzler.Name}...", action.Name,
                            checkDescription.ToString());
                        return true;
                    }
                }
            }
            if (action.HasTrait(Trait.UnaffectedByConcealment) ||
                action.Owner.HasEffect(QEffectId.TrueTarget) &&
                Level7Spells.TrueTargetApplies(action.Owner, action, target) ||
                !PlayerProfile.Instance.ConcealmentAppliesAlsoToTargetingAllies && target.FriendOf(action.Owner) ||
                !isMelee && !isRanged && !isMultiple)
                return false;
            DetectionStrength hidden = HiddenRules.DetermineHidden(user, target);
            if (hidden >= DetectionStrength.ConcealedViaBlur && action.SpellId != SpellId.MagicMissile)
            {
                if (action.Owner.HasEffect(QEffectId.TrueStrike) ||
                    (action.StrikeModifiers.HuntersAim || target.HasEffect(QEffectId.ShootingStar)) &&
                    hidden < DetectionStrength.Hidden || action.HasTrait(Trait.Bomb) &&
                    action.Owner.HasEffect(QEffectId.UncannyBombs) && action.HasTrait(Trait.Alchemical) &&
                    hidden < DetectionStrength.Hidden)
                    checkDescription.AppendLine(
                        $"Concealment ({hidden.HumanizeLowerCase2()}) ignored because of a True Strike–like effect.");
                else if (action.Owner.HasEffect(QEffectId.ModerateOracleCurse) &&
                         action.Owner.HasEffect(QEffectId.FlamesOracle) && hidden <= DetectionStrength.Concealed &&
                         action.Owner.DistanceTo(target) <= 6 && action.HasTrait(Trait.Fire) &&
                         action.HasTrait(Trait.Spell))
                {
                    checkDescription.AppendLine(
                        $"Concealment ({hidden.HumanizeLowerCase2()}) ignored because of Flames oracle's moderate curse.");
                }
                else
                {
                    int dc = HiddenRules.DetermineConcealmentCheckDC(action, target) ?? 0;
                    if (user.HasEffect(QEffectId.BlindFight))
                    {
                        if (hidden <= DetectionStrength.Concealed)
                            return false;
                        if (hidden == DetectionStrength.Hidden)
                            dc = 5;
                    }

                    (CheckResult, string, int) valueTuple = Checks.RollFlatCheckWithRoll(dc);
                    if (user.HasEffect(QEffectId.EyeOfFortune))
                    {
                        (CheckResult, string, int) tuple = Checks.RollFlatCheckWithRoll(dc);
                        if (tuple.Item3 > valueTuple.Item3)
                            valueTuple = tuple;
                    }

                    checkDescription.AppendLine(
                        $"Concealment ({(user.HasEffect(QEffectId.EyeOfFortune) ? "with reroll) (" : "")}{hidden.HumanizeLowerCase2()}) check: {valueTuple.Item2.Replace("critical ", "")}");
                    if (valueTuple.Item1 < CheckResult.Success)
                    {
                        user.Battle.Log($"Misses because the target is {hidden.HumanizeLowerCase2()}...",
                            action.Name, checkDescription.ToString());
                        if (!action.HasTrait(Trait.Attack))
                            return true;
                        _forcedSimpleMiss?.SetValue(__instance, true);
                    }
                }
            }
            return false;
        }
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

