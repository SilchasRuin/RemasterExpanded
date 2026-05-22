using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MyArchetypes;

public class Pirate
{
    public static IEnumerable<Feat> PirateFeats()
    {
        Feat pirateDedication = ArchetypeFeats.CreateAgnosticArchetypeDedication(MTraits.Pirate, "As a pirate, you sail the seas in search of enemy ships to plunder and great adventures to embark on.", 
            "You gain the Additional Lore skill feat for Maritime Lore.  If you were already trained in Maritime Lore, you also become trained in a lore skill of your choice. You gain a +2 circumstance bonus to Acrobatics checks to Balance and Reflex saves to avoid falling prone due to uneven ground. Additionally, you gain the Boarding Assault action.");
        pirateDedication.WithRulesBlockForCombatAction(cr => new CombatAction(cr, IllustrationName.FleetStep,
                "Boarding Assault", [Trait.Flourish],
                "Stride twice, you may then Strike.", Target.Self()).WithActionCost(2))
            .WithOnSheet(values =>
            {
                if (values.GetProficiency(RemasterLore.MaritimeLore.Trait) >= Proficiency.Trained)
                {
                    values.TrainInThisOrSubstitute(RemasterLore.MaritimeLore, true);
                }
                Lores.GrantAdditionalLore(values, RemasterLore.MaritimeLore);
            })
            .WithPermanentQEffect("You gain a +2 circumstance bonus to Acrobatics checks to Balance and Reflex saves to avoid falling prone due to uneven or narrow ground.",
                qf =>
                {
                    qf.BonusToAttackRolls = (_, action, _) =>
                        action.ActionId == ActionId.Balance ? new Bonus(2, BonusType.Circumstance, "Pirate") : null;
                    qf.BonusToDefenses = (_, action, defense) =>
                    {
                        if (defense != Defense.Reflex || action?.ActionId != ActionId.Balance )
                            return null;
                        return new Bonus(2, BonusType.Circumstance, "Pirate");
                    };
                    qf.ProvideMainAction = qfSelf =>
                    {
                        CombatAction board = new CombatAction(
                                qfSelf.Owner, IllustrationName.FleetStep, "Boarding Assault", [Trait.Flourish, Trait.Move],
                                "Stride twice, you may then Strike.",
                                Target.Self()).WithActionCost(2).WithSoundEffect(SfxName.Footsteps)
                            .WithEffectOnSelf(async (action, self) =>
                            {
                                if (!await self.StrideAsync("Choose where to Stride with Boarding Assault. (1/2)",
                                        allowCancel: true))
                                    action.RevertRequested = true;
                                else if (!await self.StrideAsync(
                                             "Choose where to Stride with Boarding Assault. You should end your movement within range to make a Strike. (2/2)",
                                             allowPass: true))
                                {
                                    self.Battle.Log("Boarding Assault was converted to a simple Stride.");
                                    action.SpentActions = 1;
                                    action.RevertRequested = true;
                                }
                                else
                                    await CommonCombatActions.StrikeAnyCreature(self, _ => true, true);
                            });
                        return new ActionPossibility(board).WithPossibilityGroup("Abilities");
                    };
                })
            .WithPrerequisite(values => values.GetProficiency(Trait.Intimidation) >= Proficiency.Trained, "You must be trained in Intimidation.")
            ;
        yield return pirateDedication;
        if (ModManager.TryParse("Antagonize", out FeatName swash))
        {
            Feat antagonize = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(swash, MTraits.Pirate, 4);
            yield return antagonize;
        }
        Feat youreNext = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.YoureNext, MTraits.Pirate, 4);
        yield return youreNext;
        if (ModManager.TryParse("Underwater Marauder", out FeatName underwaterMarauder))
        {
            List<Trait> pirateWeapons = [Trait.Rapier, Trait.Scimitar, Trait.Whip];
            if (ModManager.TryParse("RL_Hatchet", out Trait hatchet))
                pirateWeapons.Add(hatchet);
            Feat pirateCombat = new TrueFeat(
                    ModManager.RegisterFeatName("RE_PirateCombatTraining", "Pirate Combat Training"), 4, "You're particularly skilled at wielding the weapons used traditionally by pirates.",
                    "You gain the Underwater Marauder skill feat, even if you do not meet its prerequisites. You have familiarity with the following weapons: hatchet, rapier, scimitar, and whip—for the purposes of proficiency, you treat any of these weapons as simple weapons. \n\nAt 5th level, whenever you get a critical hit with one of these weapons, you get its critical specialization effect.",
                    [])
                .WithAvailableAsArchetypeFeat(MTraits.Pirate)
                .WithOnSheet(values =>
                {
                    values.GrantFeat(underwaterMarauder);
                    foreach (Trait weapon in pirateWeapons)
                    {
                        values.Proficiencies.AutoupgradeAlongBestWeaponProficiency(
                            [Trait.Simple, weapon]);
                    }
                    values.Proficiencies.AddProficiencyAdjustment(
                        traits => traits.Any(pirateWeapons.Contains) && traits.Contains(Trait.Martial),
                        Trait.Simple);
                })
                .WithPermanentQEffect("As long as you're at least level 5, Pirate weapons trigger {tooltip:criteffect}critical specialization effects{/}.",
                    qf =>
                    {
                        qf.YouHaveCriticalSpecialization = (qfThis, item,_,_) =>
                            qfThis.Owner.Level >= 5 && item.Traits.Any(pirateWeapons.Contains);
                    });
            yield return pirateCombat;
        }

        Feat walkThePlank = new TrueFeat(ModManager.RegisterFeatName("RE_WalkThePlank", "Walk the Plank"), 8, "You frighten a foe into moving where you want them, traditionally demanding they walk the plank.",
            "Attempt to Demoralize an opponent. On a success, in addition to the normal effects, you can also force the target to Stride up to its Speed immediately. You choose the path the target takes, but you can't force it to move into an obviously harmful space (such as into hazardous terrain or a space where it would fall) unless your check was a critical success. As normal for forced movement, this movement doesn't trigger reactions. The target then becomes temporarily immune to Walk the Plank for the rest of the encounter.", [])
            .WithAvailableAsArchetypeFeat(MTraits.Pirate)
            .WithActionCost(2)
            .WithPermanentQEffect("Demoralize an opponent then if you succeed you can force that enemy to Stride up to its speed. You choose the path, but can't move it into a harmful space unless the check was a critical success.", 
                qf =>
                {
                    qf.ProvideMainAction = effect =>
                    {
                        CombatAction walk = new CombatAction(effect.Owner, MIllustrations.CreateIllustration("Plank"),
                                "Walk the Plank", [Trait.Basic],
                                "Attempt to Demoralize an opponent. On a success, in addition to the normal effects, you can also force the target to Stride up to its Speed immediately. You choose the path the target takes, but you can't force it to move into an obviously harmful space (such as into hazardous terrain or a space where it would fall) unless your check was a critical success. As normal for forced movement, this movement doesn't trigger reactions. The target then becomes temporarily immune to Walk the Plank for the rest of the encounter.", 
                                Target.Ranged(6).WithAdditionalConditionOnTargetCreature((self, enemy) =>
                                {
                                    if (!CommonCombatActions.Demoralize(self).WithActionCost(0).CanBeginToUse(self))
                                        return Usability.NotUsable("Cannot attempt to Demoralize.");
                                    if (enemy.IsImmuneTo(Trait.Fear))
                                        return Usability.NotUsableOnThisCreature("Immune to fear.");
                                    if (enemy.IsImmuneTo(Trait.Emotion))
                                        return Usability.NotUsableOnThisCreature("Immune to emotion.");
                                    if (enemy.IsImmuneTo(Trait.Mental))
                                        return Usability.NotUsableOnThisCreature("Immune to mental.");
                                    if (enemy.IsImmuneTo(Trait.Auditory) &&
                                        !self.HasEffect(QEffectId.IntimidatingGlare))
                                        return Usability.NotUsableOnThisCreature("Immune to auditory effects.");
                                    if (enemy.IsImmuneTo(Trait.Auditory) && enemy.IsImmuneTo(Trait.Visual))
                                        return Usability.NotUsableOnThisCreature("Immune to auditory and visual effects.");
                                    if (enemy.HasEffect(MQEffectIds.PlankWalked))
                                        return Usability.NotUsableOnThisCreature("Immune to Walk the Plank.");
                                    return Usability.Usable;
                                }))
                            .WithActionCost(2)
                            .WithActionId(ActionId.Demoralize)
                            .WithEffectOnEachTarget(async (_, caster, target, result) =>
                            {
                                CombatAction demoralize = CommonCombatActions.Demoralize(caster).WithActionCost(0);
                                await caster.Battle.GameLoop.FullCast(demoralize, ChosenTargets.CreateSingleTarget(target));
                                if (demoralize.CheckResult >= CheckResult.Success)
                                {
                                    Faction original = target.OwningFaction;
                                    target.OwningFaction = caster.OwningFaction;
                                    List<Option> tileOptions =
                                    [
                                        new CancelOption(true)
                                    ];
                                    target.RegeneratePossibilities();
                                    CombatAction? moveAction = Possibilities.Create(target)
                                        .Filter(ap =>
                                        {
                                            if (ap.CombatAction.ActionId != ActionId.Stride)
                                                return false;
                                            ap.CombatAction.ActionCost = 0;
                                            ap.RecalculateUsability();
                                            return true;
                                        }).CreateActions(true).FirstOrDefault(pw =>
                                            pw.Action.ActionId == ActionId.Stride) as CombatAction;
                                    IList<Tile> floodFill = Pathfinding.Floodfill(target, target.Battle,
                                            new PathfindingDescription()
                                            {
                                                Squares = target.Speed,
                                                Style = { MaximumSquares = target.Speed, ForcedMovement = true}
                                            })
                                        .Where(tile =>
                                            tile.LooksFreeTo(target) 
                                            && (demoralize.CheckResult == CheckResult.CriticalSuccess ||
                                                tile is { HazardousTerrainEphemeral: false, InIteration.RequiresTriggeringHazardousTerrain: false }))
                                        .ToList();
                                    floodFill.ForEach(tile =>
                                    {
                                        if (moveAction == null ||
                                            !(bool)moveAction.Target.CanBeginToUse(target)) return;
                                        tileOptions.Add(moveAction.CreateUseOptionOn(tile)
                                            .WithIllustration(moveAction.Illustration));
                                    });
                                    Option chosenTile = (await caster.Battle.SendRequest(
                                        new AdvancedRequest(target,
                                            "Choose where to Stride to or right-click to cancel." +
                                            (demoralize.CheckResult == CheckResult.CriticalSuccess
                                                ? ""
                                                : " You cannot move into hazardous terrain."),
                                            tileOptions)
                                        {
                                            IsMainTurn = false,
                                            IsStandardMovementRequest = true,
                                            TopBarIcon = target.Illustration,
                                            TopBarText =
                                                "Choose where to Stride to or right-click to cancel." +
                                                (demoralize.CheckResult == CheckResult.CriticalSuccess
                                                    ? ""
                                                    : " You cannot move into hazardous terrain."),
                                        })).ChosenOption;
                                    switch (chosenTile)
                                    {
                                        case CancelOption:
                                            break;
                                        case TileOption tOpt:
                                            await tOpt.Action();
                                            break;
                                    }
                                    target.OwningFaction = original;
                                    target.AddQEffect(new QEffect { Id = MQEffectIds.PlankWalked });
                                }
                            });
                        return new ActionPossibility(walk).WithPossibilityGroup("Abilities");
                    };
                });
        yield return walkThePlank;
    }
}