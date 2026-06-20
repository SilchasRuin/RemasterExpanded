using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Modding;
using static RemasterExpanded.ModData;
using Lores = Dawnsbury.Mods.LoresAndWeaknesses.Lores;

namespace RemasterExpanded.MyArchetypes;

public class VikingGuard
{
    public static IEnumerable<Feat> VikingGuardFeats()
    {
        Feat vikingGuardDedication = ArchetypeFeats.CreateAgnosticArchetypeDedication(MTraits.VikingGuard, "The elite bodyguards of the High Crown, the Viking Guards are masters of defending their charges.",
            "You gain the Additional Lore skill feat for Warfare Lore. If you were already trained in Warfare Lore, you also become trained in a lore skill of your choice. You gain the Designate Ally action and can take the Protect Ally exploration activity to use it as a free action at the beginning of an encounter.");
        vikingGuardDedication.WithRulesBlockForCombatAction(cr => new CombatAction(cr, IllustrationName.Shield, "Designate Ally", [],
                "Choose an ally you can see, who becomes your designated ally. Until the end of the encounter, whenever your designated ally is adjacent to you and you're conscious, they gain a +2 circumstance bonus to AC and Reflex saving throws. You can have only one designated ally at a time, and if you designate a new ally, the previous ally loses any benefits.", Target.RangedFriend(200)))
            .WithOnSheet(values =>
            {
                if (Lores.GetRegisteredLore("Warfare Lore", null) is not { } wLore) return;
                if (values.GetProficiency(wLore.Trait) >= Proficiency.Trained)
                {
                    Lores.TrainInThisOrSubstitute(values, wLore, true);
                }
                Lores.GrantAdditionalLore(values, wLore);
            })
            .WithPermanentQEffect("Your designated ally gains a +2 circumstance bonus to AC and Reflex saves while you are adjacent to them.", qf =>
            {
                qf.ProvideMainAction = _ =>
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Shield, "Designate Ally", [Trait.Basic],
                        "Choose an ally you can see, who becomes your designated ally. Until the end of the encounter, whenever your designated ally is adjacent to you and you're conscious, they gain a +2 circumstance bonus to AC and Reflex saving throws. You can have only one designated ally at a time, and if you designate a new ally, the previous ally loses any benefits.",
                        Target.RangedFriend(200).WithAdditionalConditionOnTargetCreature((self, friend) => friend == self ? Usability.NotUsableOnThisCreature("You cannot target yourself") : Usability.Usable))
                        .WithActionId(RActionIds.DesignateAlly)
                        .WithEffectOnEachTarget(async (_, caster, target, _) =>
                    {
                        QEffect ally = new("Designated Ally", $"As long as you are adjacent to {caster.Name} you gain a +2 circumstance bonus to AC and Reflex saves.", ExpirationCondition.Never, qf.Owner,
                            IllustrationName.Shield)
                        {
                            BonusToDefenses = (effect, _, defense) =>
                            {
                                if (!effect.Owner.IsAdjacentTo(caster) || caster.HasEffect(QEffectId.Unconscious) ||
                                    (defense != Defense.AC && defense != Defense.Reflex))
                                    return null;
                                return new Bonus(2, BonusType.Circumstance, "Designated Ally");
                            },
                            Id = MQEffectIds.DesignatedAlly
                        };
                        foreach (Creature friend in caster.Battle.AllCreatures.Where(cr => cr.HasEffect(MQEffectIds.DesignatedAlly)))
                        {
                            friend.RemoveAllQEffects(effect => effect.Id == MQEffectIds.DesignatedAlly && effect.Source == caster);
                        }
                        target.AddQEffect(ally);
                    })).WithPossibilityGroup("Abilities"); 
                };
                qf.Name = "Designate Ally {icon: Action}";
            })
            .WithPrerequisite(values => values.GetProficiency(Trait.Athletics) >=  Proficiency.Trained && values.GetProficiency(Trait.Intimidation) >= Proficiency.Trained, "You must be trained in Athletics and Intimidation.");
        yield return vikingGuardDedication;
        Feat reactiveStrike = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.AttackOfOpportunity, MTraits.VikingGuard, 4).WithCustomName("Reactive Striker");
        yield return reactiveStrike;
        Feat protectAllyExploration = new Feat(ModManager.RegisterFeatName("RE_ProtectAlly", "Protect Ally"), "", "You may use Designate Ally as a free action at the start of an encounter.", [ExplorationActivities.ModData.Traits.ExplorationActivity], null)
            .WithPermanentQEffect("You may use Designate Ally as a free action at the start of an encounter.",qf =>
            {
                qf.StartOfCombatReaction = effect =>
                {
                    Creature caster = effect.Owner;
                    ReactionOption designate = ReactionOption.CreateCustom("Designate Ally",
                        "Use designate ally as a free action.", IllustrationName.Shield, caster,
                        async () =>
                        {
                            Creature? original = caster.Battle.ActiveCreature;
                            caster.Battle.ActiveCreature = caster;
                            caster.RegeneratePossibilities();
                            CombatAction? designate = effect.Owner.Possibilities
                                .Filter(ap => ap.CombatAction.ActionId == RActionIds.DesignateAlly).CreateActions(true).FirstOrDefault()
                                ?.Action;
                            if (designate != null)
                                await qf.Owner.Battle.GameLoop.FullCast(designate.WithActionCost(0));
                            caster.Battle.ActiveCreature = original;
                        });
                    return designate.WithIsFreeAction();
                };
            })
            .WithPrerequisite(values => values.HasFeat(vikingGuardDedication), "You must have the Viking Guard dedication to take this exploration activity.");
        yield return protectAllyExploration;
        Feat defendersGrit = new TrueFeat(ModManager.RegisterFeatName("RE_DefendersGrit", "Defender's Grit"), 4,
                "You can't protect anyone if you're dead.",
                "You gain the Diehard general feat. If you start your turn adjacent to your designated ally, you gain a number of temporary Hit Points equal to half your level.",
                [])
            .WithAvailableAsArchetypeFeat(MTraits.VikingGuard)
            .WithPermanentQEffect("If you start your turn adjacent to your designated ally, you gain a number of temporary Hit Points equal to half your level.", qf =>
            {
                qf.StartOfYourPrimaryTurn = (_, self) =>
                {
                    if (self.Battle.AllCreatures.Any(cr =>
                            cr.IsAdjacentTo(self) && cr.FindQEffect(MQEffectIds.DesignatedAlly)?.Source == self))
                        self.GainTemporaryHP(self.Level / 2);
                    return Task.CompletedTask;
                };
            })
            .WithOnSheet(values => values.GrantFeat(FeatName.Diehard));
        yield return defendersGrit;
        Feat guardsFury = new TrueFeat(MFeatNames.GuardsFury, 4,
                "Some Viking Guards tap into a well of fury to protect their charges.",
                "You can use the Rage action. While raging, you take a -1 penalty to AC. If you’re adjacent to your designated ally while raging, increase the additional damage from Rage from 2 to 4.",
                [])
            .WithAvailableAsArchetypeFeat(MTraits.VikingGuard)
            .WithOnCreature(self =>
            {
                self.AddQEffect(BarbarianFeatsDb.CreateRageProvider());
                self.AddQEffect(new QEffect("Guard's Fury", "If you’re adjacent to your designated ally while raging, increase the additional damage from Rage from 2 to 4.")
                {
                    YouDealDamageWithStrike = (_,attack, dice, _) =>
                    {
                        if (!self.HasEffect(QEffectId.Rage) || !self.Battle.AllCreatures.Any(cr =>
                                cr.IsAdjacentTo(self) &&
                                cr.FindQEffect(MQEffectIds.DesignatedAlly)?.Source == self)
                            || !BarbarianFeatsDb.DoesRageApplyToAction(attack) || attack.Item == null)
                            return dice;
                        DiceFormula newDice = new ComplexDiceFormula
                        {
                            List =
                            [
                                dice,
                                DiceFormula.FromText(attack.HasTrait(Trait.Agile) ? "1" : "2", "Guard's Fury")
                            ]
                        };
                        return newDice;
                    }
                });
            })
            .WithPrerequisite(values => !values.AdditionalClassTraits.Contains(Trait.Barbarian) && values.Class?.ClassTrait != Trait.Barbarian, "You cannot select this feat if you can already Rage.");
        yield return guardsFury;
        Feat guardedMind = new TrueFeat(ModManager.RegisterFeatName("RE_GuardedMind", "Guarded Mind"), 6, "When your enemies try to turn your mind against you, thoughts of your anathema bolster you.",
            "Once per encounter, when you fail a saving throw against an effect that has the mental trait, you can reroll the triggering saving throw with a +2 circumstance bonus, but you must use the new result, even if it’s worse.", [Trait.Fortune])
            .WithAvailableAsArchetypeFeat(MTraits.VikingGuard)
            .WithActionCost(0)
            .WithPermanentQEffect("Once per encounter, when you fail a saving throw against an effect that has the mental trait, you can reroll the triggering saving throw with a +2 circumstance bonus, but you must use the new result, even if it's worse.",
                qf =>
                {
                    qf.RerollSavingThrow = async (effect, result, action) =>
                    {
                        Creature self = effect.Owner;
                        if (!action.HasTrait(Trait.Mental) || self.HasEffect(MQEffectIds.GuardedMindUsed) ||
                            result.CheckResult >= CheckResult.Success || !await self.Battle.AskForConfirmation(self, self.Illustration,
                                $"You rolled a {result.CheckResult.ToString()} against {action}, would you like to use Guarded Mind to reroll this save with a +2 circumstance bonus?",
                                "Yes", "No"))
                            return RerollDirection.DoNothing; 
                        QEffect bonus = new()
                        {
                            BonusToDefenses = (_, combatAction, _) =>
                                combatAction != null && combatAction.HasTrait(Trait.Mental)
                                    ? new Bonus(2, BonusType.Circumstance, "Guarded Mind")
                                    : null,
                            ExpiresAt = ExpirationCondition.Ephemeral
                        };
                        self.AddQEffect(bonus);
                        self.AddQEffect(new QEffect
                        {
                            Id = MQEffectIds.GuardedMindUsed
                        });
                        return RerollDirection.RerollAndKeepSecond;
                    };
                });
        yield return guardedMind;
        Feat woundedParty = new TrueFeat(ModManager.RegisterFeatName("RE_WoundedParty", "Wounded Party"), 6, "Harm to either you or your allies awakens your fury.",
                "When you or your designated ally takes damage, you can use a reaction {icon: Reaction} to Rage.", [])
            .WithAvailableAsArchetypeFeat(MTraits.VikingGuard)
            .WithActionCost(-2)
            .WithPermanentQEffect("When you or your designated ally takes damage, you can use a reaction {icon: Reaction} to Rage.",
                qf =>
                {
                    qf.AddGrantingOfTechnical(cr => cr == qf.Owner || cr.HasEffect(MQEffectIds.DesignatedAlly),
                        qfTech =>
                        {
                            qfTech.AfterYouTakeDamage = async (effect, amount, _, _, _) =>
                            {
                                Creature guard = qf.Owner;
                                if (amount == 0 || guard.HasEffect(QEffectId.Rage) || guard.Possibilities.Filter(ap => ap.CombatAction.Name == "Rage").CreateActions(true).FirstOrDefault() is not CombatAction rage)
                                    return;
                                Creature tookDamage = effect.Owner;
                                if (!await guard.Battle.AskToUseReaction(guard ,$"{tookDamage} just took damage, would you like to use a reaction {{icon: Reaction}} to Rage?", guard.Illustration))
                                    return;
                                rage.ActionCost = 0;
                                await guard.Battle.GameLoop.FullCast(rage);
                            };
                        });
                })
            .WithPrerequisite(values => values.HasFeat(MFeatNames.GuardsFury) || values.Class?.ClassTrait == Trait.Barbarian || values.AdditionalClassTraits.Contains(Trait.Barbarian), "You must be able to Rage.");
        yield return woundedParty;
        Feat shieldWarden = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.ShieldWarden, MTraits.VikingGuard, 8);
        shieldWarden.Prerequisites.RemoveAll(prerequisite => prerequisite.Description.Contains("Fighter"));
        yield return shieldWarden;
        if (ModManager.TryParse("Guardian's Deflection", out FeatName guardDeflection))
        {
            Feat guardiansDeflection = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(guardDeflection, MTraits.VikingGuard, 8);
            yield return guardiansDeflection;
        }

        Feat tightFollower = new TrueFeat(ModManager.RegisterFeatName("RE_TightFollower", "Tight Follower"), 8, "You keep up as a protector.",
                "When your designated ally moves and ends that movement more than 10 feet from you, you can use a reaction {icon: Reaction} to stride up to your Speed closer to your designated ally. Your movement must end within 10 feet of your designated ally.", [])
            .WithAvailableAsArchetypeFeat(MTraits.VikingGuard)
            .WithActionCost(-2)
            .WithPermanentQEffect("When your designated ally moves and ends that movement away from you, you can Stride closer to them.",
                qf =>
                {
                    qf.AddGrantingOfTechnical(cr => cr.FindQEffect(MQEffectIds.DesignatedAlly)?.Source == qf.Owner,
                        qfTech =>
                        {
                            qfTech.AfterYouTakeAction = async (effect, action) =>
                            {
                                if (!action.HasTrait(Trait.Move) || effect.Owner.DistanceTo(qf.Owner) <= 2)
                                    return;
                                Creature guard = qf.Owner;
                                List<Option> tileOptions =
                                [
                                    new CancelOption(true)
                                ];
                                CombatAction? moveAction = Possibilities.Create(guard)
                                    .Filter(ap =>
                                    {
                                        if (ap.CombatAction.ActionId != ActionId.Stride)
                                            return false;
                                        ap.CombatAction.ActionCost = 0;
                                        ap.RecalculateUsability();
                                        return true;
                                    }).CreateActions(true).FirstOrDefault(pw =>
                                        pw.Action.ActionId == ActionId.Stride) as CombatAction;
                                IList<Tile> floodFill = Pathfinding.Floodfill(guard, guard.Battle,
                                        new PathfindingDescription()
                                        {
                                            Squares = guard.Speed,
                                            Style = { MaximumSquares = guard.Speed }
                                        })
                                    .Where(tile =>
                                        tile.LooksFreeTo(guard) 
                                        && tile.DistanceTo(effect.Owner) <= 2)
                                    .ToList();
                                floodFill.ForEach(tile =>
                                {
                                    if (moveAction == null ||
                                        !(bool)moveAction.Target.CanBeginToUse(guard)) return;
                                    tileOptions.Add(moveAction.CreateUseOptionOn(tile)
                                        .WithIllustration(moveAction.Illustration));
                                });
                                if (tileOptions.Count <= 1)
                                    return;
                                if (!await guard.Battle.AskToUseReaction(guard, $"Your designated ally, {effect.Owner}, has ended a movement more than 10 feet from you, would you like to use a reaction {{icon: Reaction}} to Stride up to your Speed closer to your designated ally? Your movement must end within 10 feet of your designated ally.", guard.Illustration))
                                    return;
                                Option chosenTile = (await guard.Battle.SendRequest(
                                    new AdvancedRequest(guard,
                                        "Choose where to Stride to or right-click to cancel. You must end your movement within 10 feet of your designated ally.",
                                        tileOptions)
                                    {
                                        IsMainTurn = false,
                                        IsStandardMovementRequest = true,
                                        TopBarIcon = guard.Illustration,
                                        TopBarText =
                                            "Choose where to Stride to or right-click to cancel. You must end your movement within 10 feet of your designated ally.",
                                    })).ChosenOption;
                                switch (chosenTile)
                                {
                                    case CancelOption:
                                        action.RevertRequested = true;
                                        guard.Actions.RefundReaction();
                                        break;
                                    case TileOption tOpt:
                                        await tOpt.Action();
                                        break;
                                }

                            };
                        });
                });
        yield return tightFollower;
    }
}