using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Controls.Statblocks;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using RemasterExpanded.MySpells;
using static ExplorationActivities.ModData.QEffectIds;
using static RemasterExpanded.ModData;
using Ranger = Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Ranger;

namespace RemasterExpanded;

public class RangerFeats
{
    public static IEnumerable<Feat> LoadFeats()
    {
        yield return new TrueFeat(MFeatNames.InitiateWarden, 1, "You've trained with one of the ranger sects known as wardens, who practice a specialized type of primal magic.",
            "You gain your choice of one warden spell from the initial warden spells.", [Trait.Ranger], LoadInitiateWarden().ToList()).WithMultipleSelection();
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AdvancedWarden", "Advanced Warden"), 4,
                "You unlock more powerful primal spells.",
                "You gain your choice of one warden spell from the advanced warden spells.", [Trait.Ranger],
                LoadAdvancedWarden().ToList()).WithMultipleSelection()
            .WithPrerequisite(MFeatNames.InitiateWarden, "Initiate Warden");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_MasterWarden", "Master Warden"), 6,
                "Your mastery of warden magic increases.",
                "You gain your choice of one warden spell from the master warden spells.", [Trait.Ranger],
                LoadMasterWarden().ToList()).WithMultipleSelection()
            .WithPrerequisite(MFeatNames.InitiateWarden, "Initiate Warden");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_PeerlessWarden", "Peerless Warden"), 10,
                "Your mastery of primal magic has given you access to the greatest secrets of warden magic.",
                "You gain your choice of one warden spell from the peerless warden spells.", [Trait.Ranger],
                LoadPeerlessWarden().ToList()).WithMultipleSelection()
            .WithPrerequisite(MFeatNames.InitiateWarden, "Initiate Warden");
        yield return new TrueFeat(MFeatNames.MonsterHunter, 1,
                "You quickly assess your prey and apply what you know.",
                $"As part of the action used to Hunt your Prey, you can attempt a check to {RecallWeakness.GetActionLink("Recall Weakness")} about your prey. When you critically succeed at a Recall Weakness check against your hunted prey, you note an additional weakness in the creature's defenses. You gain a +1 circumstance bonus to your next attack roll against that prey, and any ally gains the same benefit. You can give bonuses from Monster Hunter only once per encounter against a particular creature.",
                [Trait.Ranger])
            .WithPermanentQEffect("You can Recall Weakness when you Hunt Prey. You grant bonuses on a critical success when you Recall Weakness on your prey.", qf =>
            {
                Creature self = qf.Owner;
                qf.YouBeginActionReaction = (_, action) =>
                {
                    if (action.ActionId != ActionId.HuntPrey)
                        return null;
                    self.RegeneratePossibilities();
                    if (self.Possibilities.Filter(ap =>
                        {
                            if (ap.CombatAction.ActionId != RecallWeakness.RWActionId)
                                return false;
                            ap.CombatAction.ActionCost = 0;
                            ap.RecalculateUsability();
                            return true;
                        }).CreateActions(true).FirstOrDefault() is not CombatAction recallWeakness || action.ChosenTargets.ChosenCreature is not {} prey)
                        return null;
                    recallWeakness.Target = action.Target;
                    if (recallWeakness.Target is not CreatureTarget targ || !targ.IsLegalTarget(self, prey))
                    {
                        return null;
                    }
                    ReactionOption recall = ReactionOption.CreateFromCombatActionCustom(recallWeakness, "Recall Weakness on this target as part of Hunt Prey.", async () =>
                    {
                        self.AddQEffect(new QEffect
                        {
                            AfterYouTakeAction = async (effect, cAction) =>
                            {
                                if (cAction.ActionId != ActionId.HuntPrey)
                                    return;
                                self.RegeneratePossibilities();
                                if (self.Possibilities.Filter(ap =>
                                    {
                                        if (ap.CombatAction.ActionId != RecallWeakness.RWActionId)
                                            return false;
                                        ap.CombatAction.ActionCost = 0;
                                        ap.RecalculateUsability();
                                        return true;
                                    }).CreateActions(true).FirstOrDefault() is not CombatAction weakness || cAction.ChosenTargets.ChosenCreature is not {} hunted)
                                    return;
                                weakness.Target = RecallWeakness.RecallWeaknessTarget(100, true);
                                if (weakness.Target is not CreatureTarget creatureTarget || !creatureTarget.IsLegalTarget(self, hunted))
                                {
                                    return;
                                }
                                await self.Battle.GameLoop.FullCast(weakness, ChosenTargets.CreateSingleTarget(hunted));
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        });
                    });
                    recall.MouseOverStatblock = new StringStatblock("Recall Weakness", null, recallWeakness.Traits.ToList(), CombatActionExecution.BreakdownAttackForTooltip(recallWeakness, prey).TooltipDescription, false);
                    return recall;
                };
                qf.AfterYouTakeAction = async (_, action) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId || action.CheckResult <
                        (self.HasEffect(MQEffectIds.MasterMonsterHunter)
                            ? CheckResult.Success
                            : CheckResult.CriticalSuccess) || action.ChosenTargets.ChosenCreature is not { } prey ||
                        !Ranger.HasPrey(self, self, prey) || prey.HasEffect(MQEffectIds.MonsterHunterUsed))
                        return;
                    int amount = self.HasEffect(MQEffectIds.LegendaryMonsterHunter) ? 2 : 1;
                    HashSet<Creature> attacked = [];
                    HashSet<Creature> friends = self.Battle.AllCreatures.Where(cr => cr.FriendOf(self)).ToHashSet();
                    QEffect monster = new("Monster Hunter", "", ExpirationCondition.Never, self, MIllustrations.CreateIllustration("MonsterHunter"))
                    {
                        StateCheck = qff =>
                        {
                            if (friends.All(attacked.Contains))
                                qff.ExpiresAt = ExpirationCondition.Immediately;
                            var description = $"The first attack made by {S.ConstructOrList(friends.Where(cr => !attacked.Contains(cr)).Select(cr => cr.Name), "and")} against this creature gain a +{amount} circumstance bonus to hit.";
                            if (qff.Description != description)
                                qff.Description = description;
                        },
                        YouAreTargetedByARoll = async (_, combatAction, _) =>
                        {
                            if (friends.Contains(combatAction.Owner) && combatAction.HasTrait(Trait.Attack))
                            {
                                attacked.Add(combatAction.Owner);
                            }
                            return false;
                        }
                    };
                    monster.AddGrantingOfTechnical(cr => friends.Contains(cr) && !attacked.Contains(cr), qfTech =>
                    {
                        qfTech.BonusToAttackRolls = (_, combatAction, target) => target == prey && combatAction.HasTrait(Trait.Attack) ? new Bonus(amount, BonusType.Circumstance, "Monster Hunter") : null;
                    });
                    prey.AddQEffect(monster);
                    prey.AddQEffect(new QEffect { Id = MQEffectIds.MonsterHunterUsed });
                    if (!self.HasEffect(MQEffectIds.MonsterWarden))
                        return;
                    HashSet<Creature> warded = [];
                    QEffect warden = new()
                    {
                        StateCheck = qff =>
                        {
                            if (friends.All(warded.Contains))
                                qff.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    warden.AddGrantingOfTechnical(cr => friends.Contains(cr) && !warded.Contains(cr), qfTech =>
                    {
                        qfTech.BonusToDefenses = (_, combatAction, defense) => combatAction?.Owner == prey &&
                            (defense.IsSavingThrow() || defense == Defense.AC) ? new Bonus(2,  BonusType.Circumstance, "Monster Warden") : null;
                        qfTech.AfterYouMakeSavingThrow = (effect, combatAction, _) =>
                        {
                            if (combatAction.Owner == prey)
                                warded.Add(effect.Owner);
                        };
                        qfTech.YouAreTargetedByARoll = async (effect, combatAction, _) =>
                        {
                            if (combatAction.ActiveRollSpecification?.TaggedDetermineDC.InvolvedDefense is Defense.AC)
                            {
                                warded.Add(effect.Owner);
                            }
                            return false;
                        };
                        qfTech.Name = "Monster Warden";
                        qfTech.Description = $"You gain a +2 circumstance bonus to either your AC or saving throw against an action from {prey.Name} (whichever comes first.)";
                        qfTech.Source = self;
                        qfTech.Illustration = MIllustrations.CreateIllustration("MonsterWarden");
                    });
                    prey.AddQEffect(warden);
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_MonsterWarden", "Monster Warden"), 2,
            "You understand how to defend against your prey.",
            "When you grant bonuses from Monster Hunter, each creature who benefits also gains a +2 circumstance bonus either to their AC the next time the creature attacks them or to their next saving throw against an effect from that particular creature (whichever comes first.)",
            [Trait.Ranger])
            .WithPermanentQEffect("When you grant bonuses from Monster Hunter, you grant defensive bonuses as well.", qf => qf.Id = MQEffectIds.MonsterWarden)
            .WithPrerequisite(MFeatNames.MonsterHunter, "Monster Hunter");
        yield return new TrueFeat(MFeatNames.MasterMonsterHunter, 10,
                "You have a nearly encyclopedic knowledge of all creatures of the world.",
                "You can use Nature to Recall Weakness against any creature. In addition, you gain the benefits of Monster Hunter (and Monster Warden, if you have it) on a success as well as a critical success.", [Trait.Ranger])
            .WithPermanentQEffect("You can use Nature to Recall Weakness against any creature. Monster Hunter and Monster Warden grant bonuses on Success as well as Critical Success.", qf =>
            {
                qf.Id = MQEffectIds.MasterMonsterHunter;
                qf.ModifyActionPossibility = (_, action) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId) return;
                    if (action.ActiveRollSpecification is not {} activeRoll || activeRoll.TaggedDetermineBonus.InvolvedSkill == Skill.Nature) return;
                    action.WithActiveRollSpecification(new ActiveRollSpecification(
                        TaggedChecks.BestRoll(TaggedChecks.SkillCheck(Skill.Nature), action.ActiveRollSpecification.TaggedDetermineBonus),
                        action.ActiveRollSpecification.TaggedDetermineDC));
                };
                qf.YouBeginAction = async (_, action) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId) return;
                    if (action.ActiveRollSpecification is not {} activeRoll || activeRoll.TaggedDetermineBonus.InvolvedSkill == Skill.Nature) return;
                    action.WithActiveRollSpecification(new ActiveRollSpecification(
                        TaggedChecks.BestRoll(TaggedChecks.SkillCheck(Skill.Nature), action.ActiveRollSpecification.TaggedDetermineBonus),
                        action.ActiveRollSpecification.TaggedDetermineDC));
                };
            })
            .WithPrerequisite(MFeatNames.MonsterHunter, "Monster Hunter")
            .WithPrerequisite(values => values.GetProficiency(Trait.Nature) >= Proficiency.Master, "You must be a master in Nature.");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_LegendaryMonsterHunter", "Legendary Monster Hunter"), 16,
            "Your knowledge of monsters is so incredible that it reveals glaring flaws in your prey.",
            "Your bonus from Monster Hunter increases from +1 to +2 for you and any allies who benefit.",
            [Trait.Ranger])
            .WithPermanentQEffectAndSameRulesText(qf => qf.Id = MQEffectIds.LegendaryMonsterHunter)
            .WithPrerequisite(MFeatNames.MasterMonsterHunter, "Master Monster Hunter")
            .WithPrerequisite(values => values.GetProficiency(Trait.Nature) >= Proficiency.Legendary, "You must be legendary in Nature.");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AdditionalRecollection", "Additional Recollection"),
                6,
                "You scan the battlefield quickly, remembering critical details about multiple opponents you face.",
                $"{{b}}Trigger{{/b}} You succeed or critically succeed at a check to {RecallWeakness.GetActionLink("Recall Weakness")} on your hunted prey" +
                $"\n\nYou immediately attempt a check to Recall Weakness about a different creature you can perceive.",
                [Trait.Ranger])
            .WithActionCost(0)
            .WithPermanentQEffect("When you successfully Recall Weakness about your prey, you can Recall Weakness against a different creature as well.", qf =>
            {
                Creature self = qf.Owner;
                qf.AfterYouTakeAction = async (_, action) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId || action.CheckResult < CheckResult.Success || action.ChosenTargets.ChosenCreature is not {} prey || !Ranger.HasPrey(self, self, prey))
                        return;
                    self.RegeneratePossibilities();
                    if (self.Possibilities.Filter(ap =>
                        {
                            if (ap.CombatAction.ActionId != RecallWeakness.RWActionId)
                                return false;
                            ap.CombatAction.ActionCost = 0;
                            ap.RecalculateUsability();
                            return true;
                        }).CreateActions(true).FirstOrDefault() is not CombatAction recallWeakness)
                        return;
                    if (recallWeakness.Target is not CreatureTarget target || !recallWeakness.CanBeginToUse(self) || !await self.AskForConfirmation(recallWeakness.Illustration, "Recall Weakness targeting a different creature?", "Yes"))
                        return;
                    target.WithAdditionalConditionOnTargetCreature((_, creature1) =>
                        creature1 == prey
                            ? Usability.NotUsableOnThisCreature("You cannot target the same creature.")
                            : Usability.Usable);
                    recallWeakness.Target = target;
                    await self.Battle.GameLoop.FullCast(recallWeakness);
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AnimalStrength", "Animal Strength"), 6,
            "You tap into the primal strength of the animals you emulate.",
            "When you gain a claw or jaws attack from {i}animal feature{/i}, you apply all runes from the best weapon you are carrying (if they're applicable) to that unarmed attack. This replaces any runes the unarmed attacks would normally have from other sources, like handwraps of mighty blows." +
            "\n\nIn addition, when you critically hit with a claw or jaws attack from animal feature, you deal 1d6 persistent bleed damage.",
            [Trait.Ranger])
            .WithPrerequisite(values => values.HasFeat(MFeatNames.AnimalFeature), "You must have the {i}animal feature{/i} warden spell from the Advanced Warden feat.")
            .WithOnCreature(cr => cr.AddQEffect(RuneHandling.AnimalStrengthRunes()))
            .WithPermanentQEffect("The runes from your weapons apply to your {i}animal feature{/i} natural weapons. Also, you deal persistent bleed damage on a critical hit.", qf =>
            {
                qf.Id = MQEffectIds.AnimalStrength;
                qf.AdjustStrikeAction = (_, action) =>
                {
                    if (action.Item is not {} naturalWeapon || !naturalWeapon.HasTrait(MTraits.AnimalWeapon))
                        return;
                    if (action.StrikeModifiers is { } modifiers)
                    {
                        modifiers.OnEachTarget += async (_, target, result) =>
                        {
                            if (result == CheckResult.CriticalSuccess)
                                target.AddQEffect(QEffect.PersistentDamage("1d6", DamageKind.Bleed));
                        };
                    }
                    else
                    {
                        action.StrikeModifiers = new StrikeModifiers
                        {
                            OnEachTarget = async (_, target, result) =>
                            {
                                if (result == CheckResult.CriticalSuccess)
                                    target.AddQEffect(QEffect.PersistentDamage("1d6", DamageKind.Bleed));
                            }
                        };
                    }
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_NatureProwler", "Nature Prowler"), 6,
                "In the wilds, you move like a ghost.",
                "When you begin your turn hidden or unnoticed by your hunted prey, that creature is off-guard to you until the end of your turn. If you're outdoors, you can Sneak at full speed.",
                [Trait.Ranger])
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                qf.StartOfYourPrimaryTurn = async (_, self) =>
                {
                    if (self.DetectionStatus is { IsHiddenToAnEnemy: false, UndetectedTo.Count: 0 })
                        return;
                    QEffect offGuard = new(ExpirationCondition.ExpiresAtEndOfSourcesTurn)
                    {
                        Source = self,
                        IsFlatFootedTo = (_, creature, _) => creature == self ? "Nature Prowler" : null,
                        Name = "Off-guard",
                        Description = $"You take a –2 circumstance penalty to AC on attacks made by {self.Name}.",
                        Illustration = IllustrationName.Flatfooted
                    };
                    foreach (Creature prey in self.Battle.AllCreatures.Where(cr => Ranger.HasPrey(self, self, cr) &&
                                 (self.DetectionStatus.HiddenTo.Contains(cr) || self.DetectionStatus.IsUndetectedTo(cr))))
                    {
                        prey.AddQEffect(offGuard);
                    }
                };
                qf.StartOfCombat = async qfSelf =>
                {
                    if (!qfSelf.Owner.Battle.Encounter.Map.IsIndoors)
                    {
                        qfSelf.Owner.AddQEffect(new QEffect("Nature Prowler",
                            "You can move at full Speed when you Sneak.")
                        {
                            Id = QEffectId.SwiftSneak
                        });
                        qfSelf.Owner.Overhead("Nature Prowler active", Color.LimeGreen,
                            qfSelf.Owner + " is outdoors, and so can move at full Speed when Sneaking.");
                    }
                    else
                        qfSelf.Owner.Overhead("Nature Prowler inactive", Color.Gainsboro,
                            qfSelf.Owner + " is indoors, and so the Nature Prowler feat doesn't work.");
                };
            });
        yield return new TrueFeat(MFeatNames.ExperiencedTracker, 1,
                "Tracking is second nature to you, and when necessary you can follow a trail without pause.",
                $"When you take the {AllFeats.GetFeatByFeatNameOrStringOptional(null, "Track")?.ToLink("Track")} exploration activity and roll Survival for initiative you gain a +2 circumstance bonus to that roll.",
                [Trait.General, Trait.Skill, Trait.Rebalanced])
            .WithPermanentQEffect("When tracking, you gain a +2 circumstance bonus to initiative.", qf =>
            {
                qf.BonusToInitiative = effect =>
                    effect.Owner.HasEffect(Track)
                        ? new Bonus(2, BonusType.Circumstance, "Experienced Tracker") : null;
            })
            .WithPrerequisite(values => values.GetProficiency(Trait.Survival) >= Proficiency.Trained,
                "You must be trained in Survival.");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_SwiftTracker", "Swift Tracker"), 6,
                "Your keen eyes catch signs of passage even when you're moving.",
                $"If you take the {AllFeats.GetFeatByFeatNameOrStringOptional(null, "Track")?.ToLink("Track")} exploration activity, when you start your first turn of the encounter, you can Stride toward your hunted prey as a free action." +
                $"\n\nIf you have legendary proficiency in Survival, you can use another exploration activity while Tracking.",
                [Trait.Ranger])
            .WithPermanentQEffect("When tracking, at the start of your first turn of the encounter, you may Stride toward your hunted prey as a free action.", qf =>
            {
                var firstTurn = true;
                qf.StartOfYourPrimaryTurn = async (_, self) =>
                {
                    if (!firstTurn)
                        return;
                    firstTurn = false;
                    if (!self.HasEffect(Track) || self.Battle.AllCreatures.All(cr => !Ranger.HasPrey(self, self, cr)))
                        return;
                    if (!await self.AskForConfirmation(IllustrationName.HuntPrey,
                            "Stride towards your hunted prey as a free action?", "Yes"))
                        return;
                    await self.StrideOrStepAdvancedAsync(
                        "Stride towards your hunted prey. You must end closer to your prey than you began.", allowCancel: true, permissibleTarget: tile => self.Battle.AllCreatures.Any(cr => Ranger.HasPrey(self, self, cr) && tile.DistanceTo(cr) < self.DistanceTo(cr)));
                };
                qf.StartOfCombat = async _ => { firstTurn = true; };
            })
            .WithOnSheet(values =>
            {
                if (values.GetProficiency(Trait.Survival) < Proficiency.Legendary)
                    return;
                values.AddSelectionOptionRightNow(new SingleFeatSelectionOption("RE_SwiftTracker", "Swift Tracker", SelectionOption.PRECOMBAT_PREPARATIONS_LEVEL, feat => feat.FeatName == ExplorationActivities.ModData.FeatNames.Track).WithIsOptional());
                
            })
            .WithPrerequisite(values => values.GetProficiency(Trait.Survival) >= Proficiency.Expert,
                "You must be an expert in Survival.")
            .WithPrerequisite(MFeatNames.ExperiencedTracker, "Experienced Tracker");
    }

    public static void HideWardenFeats()
    {
        AllFeats.GetFeatByFeatName(FeatName.GravityWeapon).Traits.Remove(Trait.Ranger);
        AllFeats.GetFeatByFeatName(FeatName.MagicHide).Traits.Remove(Trait.Ranger);
        AllFeats.GetFeatByFeatName(FeatName.HealCompanion).Traits.Remove(Trait.Ranger);
        AllFeats.GetFeatByFeatName(FeatName.EnlargeCompanion).Traits.Remove(Trait.Ranger);
    }

    public static IEnumerable<Feat> LoadInitiateWarden()
    {
        List<SpellId> initiate =
        [
            SpellIds.DistractingDecoy, SpellIds.KeenSmell, SpellIds.SlimeSpit, SpellId.HealCompanion,
            SpellId.GravityWeapon, SpellId.MagicHide
        ];
        foreach (SpellId spellId in initiate)
        {
            Spell template = AllSpells.CreateModernSpellTemplate(spellId, Trait.Ranger);
            yield return new Feat(ModManager.RegisterFeatName($"RE_{template.Name.Replace(" ", "").Replace(",", "").Replace("'", "")}", template.Name), null,
                    $"You gain the {{i}}{template.Name.ToLower()}{{/i}} warden spell and a focus pool of 1 Focus Point.", [],
                    null)
                .WithRulesBlockForSpell(spellId, Trait.Ranger)
                .WithIllustration(template.Illustration)
                .WithOnSheet(sheet =>
                {
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    sheet.AddFocusSpellAndFocusPoint(Trait.Ranger, Ability.Wisdom, spellId);
                });
        }
    }

    public static IEnumerable<Feat> LoadAdvancedWarden()
    {
        List<SpellId> advanced = 
        [
            SpellId.EnlargeCompanion, SpellIds.AnimalFeature, SpellIds.SoothingMist, SpellIds.HuntersLuck
        ];
        foreach (SpellId spellId in advanced)
        {
            Spell template = AllSpells.CreateModernSpellTemplate(spellId, Trait.Ranger);
            yield return new Feat(spellId == SpellIds.AnimalFeature ? MFeatNames.AnimalFeature : ModManager.RegisterFeatName($"RE_{template.Name.Replace(" ", "").Replace(",", "").Replace("'", "")}", template.Name), null,
                    $"You gain the {{i}}{template.Name.ToLower()}{{/i}} warden spell and an additional Focus Point.", [],
                    null)
                .WithRulesBlockForSpell(spellId, Trait.Ranger)
                .WithIllustration(template.Illustration)
                .WithOnSheet(sheet =>
                {
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    sheet.AddFocusSpellAndFocusPoint(Trait.Ranger, Ability.Wisdom, spellId);
                });
        }
    }
    public static IEnumerable<Feat> LoadMasterWarden()
    {
        List<SpellId> master = 
        [
            SpellIds.ThreateningMimicry, SpellIds.WarningStripes, SpellIds.RangersBramble
        ];
        foreach (SpellId spellId in master)
        {
            Spell template = AllSpells.CreateModernSpellTemplate(spellId, Trait.Ranger);
            yield return new Feat(ModManager.RegisterFeatName($"RE_{template.Name.Replace(" ", "").Replace(",", "").Replace("'", "")}", template.Name), null,
                    $"You gain the {{i}}{template.Name.ToLower()}{{/i}} warden spell and an additional Focus Point.", [],
                    null)
                .WithRulesBlockForSpell(spellId, Trait.Ranger)
                .WithIllustration(template.Illustration)
                .WithOnSheet(sheet =>
                {
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    sheet.AddFocusSpellAndFocusPoint(Trait.Ranger, Ability.Wisdom, spellId);
                });
        }
    }
    public static IEnumerable<Feat> LoadPeerlessWarden()
    {
        List<SpellId> peerless = 
        [
            SpellIds.PulverizingWake, SpellIds.GluttonousGrowth, SpellIds.PackBreaker, SpellIds.HuntersVision
        ];
        foreach (SpellId spellId in peerless)
        {
            Spell template = AllSpells.CreateModernSpellTemplate(spellId, Trait.Ranger);
            yield return new Feat(ModManager.RegisterFeatName($"RE_{template.Name.Replace(" ", "").Replace(",", "").Replace("'", "")}", template.Name), null,
                    $"You gain the {{i}}{template.Name.ToLower()}{{/i}} warden spell and an additional Focus Point.", [],
                    null)
                .WithRulesBlockForSpell(spellId, Trait.Ranger)
                .WithIllustration(template.Illustration)
                .WithOnSheet(sheet =>
                {
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    sheet.AddFocusSpellAndFocusPoint(Trait.Ranger, Ability.Wisdom, spellId);
                });
        }
    }
}