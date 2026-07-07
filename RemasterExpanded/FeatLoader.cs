using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Modding;
using RemasterExpanded.ClassChangesAndFeats;
using RemasterExpanded.MyArchetypes;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

public class FeatLoader
{
    public static IEnumerable<Feat> LoadFeats()
    {
        TrueFeat robustHealth = new(ModManager.RegisterFeatName("RE_RobustHealth", "Robust Health"), 3, "Your physiology responds well to first aid.",
            "You gain a circumstance bonus to the number of Hit Points you regain equal to your level from a successful attempt to Treat your Wounds or use Battle Medicine on you. After you or an ally use Battle Medicine on you, you become temporarily immune to that Battle Medicine for only 1 encounter, instead of 1 day.",
            [Trait.General]);
        RobustHealthLogic(robustHealth);
        yield return robustHealth;
        
        foreach (Feat feat in Pirate.PirateFeats())
        {
            yield return feat;
        }
        
        foreach (Feat feat in VikingGuard.VikingGuardFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in BlackJacket.BlackJacketFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in Viking.VikingFeats())
        {
            yield return feat;
        }
        
        foreach (Feat feat in NewDeities.LoadDeities())
        {
            yield return feat;
        }

        foreach (Feat feat in CampfireChronicler.CampfireFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in Sanctification.SanctifyFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in RangerFeats.LoadFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in EagleKnight.Load())
        {
            yield return feat;
        }

        foreach (Feat feat in SkillFeats.Load())
        {
            yield return feat;
        }

        foreach (Feat feat in SisterOfTheGoldenErinys.Load())
        {
            yield return feat;
        }

        foreach (Feat feat in ChampionRemaster.LoadFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in FighterFeats.LoadFeats())
        {
            yield return feat;
        }
    }

    public static void ModifyFeats()
    {
        HashSet<Feat> armorTraining =
        [
            AllFeats.GetFeatByFeatName(FeatName.ArmorProficiencyLight),
            AllFeats.GetFeatByFeatName(FeatName.ArmorProficiencyMedium),
            AllFeats.GetFeatByFeatName(FeatName.ArmorProficiencyHeavy)
        ];
        foreach (Feat feat in armorTraining)
        {
            switch (feat.FeatName)
            {
                case FeatName.ArmorProficiencyLight:
                    feat.WithOnSheet(values => values.AddAtLevel(13,
                        sheetValues => sheetValues.SetProficiency(Trait.LightArmor, Proficiency.Expert)));
                    break;
                case FeatName.ArmorProficiencyMedium:
                    feat.WithOnSheet(values => values.AddAtLevel(13,
                        sheetValues => sheetValues.SetProficiency(Trait.MediumArmor, Proficiency.Expert)));
                    break;
                case FeatName.ArmorProficiencyHeavy:
                    feat.WithOnSheet(values => values.AddAtLevel(13,
                        sheetValues => sheetValues.SetProficiency(Trait.HeavyArmor, Proficiency.Expert)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            feat.RulesText += " If you are at least 13th level, you become an expert in this armor type.";
        }
    }
    
    public static void RobustHealthLogic(TrueFeat feat)
    {
        feat.WithPermanentQEffect(
            "You gain a circumstance bonus to the number of Hit Points you regain equal to your level from a successful attempt to use Battle Medicine on you. After you or an ally use Battle Medicine on you, you become temporarily immune to that Battle Medicine for only 1 encounter, instead of 1 day.",
            qf =>
            {
                Creature self = qf.Owner;
                qf.StartOfCombat = async _ =>
                {
                    foreach (Creature ally in self.Battle.AllCreatures.Where(creature => creature.PersistentCharacterSheet != null))
                    {
                        self.PersistentUsedUpResources.UsedUpActions.RemoveAll(str => str.Contains("BattleMedicineFrom:" + ally.Name));
                    }
                };
                qf.BonusToSelfHealing = (_, action) =>
                {
                    if (action == null || !action.Name.Contains("Battle Medicine") || action.CheckResult <= CheckResult.Failure) return null;
                    return new Bonus(self.Level, BonusType.Circumstance, "Robust Health", true);
                };
            });
    }

    public static void LungeLogic(TrueFeat feat)
    {
        feat.WithActionCost(1)
            .WithPermanentQEffect("You are able to make attacks from a greater distance.", qf =>
        {
            qf.ProvideStrikeModifierAsPossibilities = (effect, item) =>
            {
                if (!item.HasTrait(Trait.Melee) || item.HasTrait(Trait.Unarmed))
                    return [];
                Creature self = effect.Owner;
                List<Possibility> lunges = [];
                StrikeModifiers lungeModifiers = new()
                {
                    QEffectForStrike = new QEffect
                    {
                        Id = MQEffectIds.LungingReach,
                        WhenYouAcquireThis = qff => qff.Owner.Battle.GameLoop.RecalculateFlankingFor(qff.Owner)
                    }
                };
                CombatAction strike = StrikeRules.CreateStrike(self, item, RangeKind.Melee, -1, false, lungeModifiers);
                strike.Name = strike.Name.Replace("Strike", "Lunge");
                strike.ContextMenuName = strike.ContextMenuName?.Replace("Strike", "Lunge") ?? strike.ContextMenuName;
                strike.ShortName = strike.ShortName.Replace("Strike", "Lunge");
                strike.Target = ReachPlusFive(item, self);
                strike.Description = strike.Description.Replace($"{{b}}Reach{{/b}} {item.DetermineReach(self)*5}", $"{{b}}Reach{{/b}} {(item.DetermineReach(self)+1)*5}");
                strike.WithTargetingTooltip((action, flankee, _) =>
                {
                    CheckBreakdown breakdownAttack = CombatActionExecution.BreakdownAttackForTooltip(action, flankee);
                    string breakdownTooltip = breakdownAttack.TooltipDescription;
                    Creature flanker = action.Owner;
                    if (breakdownTooltip.Contains("flat-footed") || flankee.HasEffect(QEffectId.AllAroundVision) || (flankee.HasEffect(QEffectId.DenyAdvantage) && flanker.Level < flankee.Level)) return breakdownTooltip;
                    if (!flanker.Battle.AllCreatures.Any(allCreature =>
                            allCreature != flanker &&
                            !allCreature.HasEffect(QEffectId.CannotFlank) &&
                            allCreature.DistanceToWith10FeetException(flankee) <= allCreature.Space.ActualReach &&
                            FlankingRules.SimplifiedCanAttack(allCreature) &&
                            (FlankingRules.IsFlanking(flanker, flankee, allCreature) ||
                             flanker.HasEffect(QEffectId.GangUp) ||
                             flanker.HasEffect(QEffectId.SideBySide) &&
                             Ranger.GetAnimalCompanion(flanker) == allCreature))) return breakdownTooltip;
                    int breakdownDC = breakdownAttack.TotalDC;
                    return breakdownTooltip.Replace($"{breakdownDC} DC breakdown",
                               $"{breakdownDC - 2} DC breakdown") +
                           "\n{Red}{b}-2{/b}{/Red} Circumstance (flat-footed (flanking))";

                });
                lunges.Add(new ActionPossibility(strike));
                return lunges;
            };
            qf.ProvideActionsIntoPossibilitySection = (effect, section) =>
            {
                if (section.PossibilitySectionId != PossibilitySectionId.AttackManeuvers)
                    return [];
                Creature self = effect.Owner;
                List<Possibility> lunges = [];
                foreach (Item item in self.MeleeWeapons.Where(it =>
                             (it.HasTrait(Trait.Trip) || it.HasTrait(Trait.Disarm) || it.HasTrait(Trait.Shove)) && !it.HasTrait(Trait.Unarmed)))
                {
                    if (item.HasTrait(Trait.Trip))
                    {
                        CombatAction trip = CombatManeuverPossibilities.CreateTripAction(self, item);
                        trip.Name = "Lunge - " + trip.Name;
                        trip.ShortName = "Lunge - Trip";
                        trip.ContextMenuName = $"Lunge - Trip ({item.BaseHumanName})";
                        trip.Target = ReachPlusFive(item, self)
                            .WithAdditionalConditionOnTargetCreature(
                                new TargetMustNotBeTwoSizesAboveYouCreatureTargetingRequirement())
                            .WithAdditionalConditionOnTargetCreature((a, _) =>
                                !a.HasFreeHand && !a.WieldsItem(Trait.Trip)
                                    ? Usability.CommonReasons.NoFreeHandForManeuver
                                    : Usability.Usable).WithAdditionalConditionOnTargetCreature((_, d) =>
                                d.HasEffect(QEffectId.Prone)
                                    ? Usability.CommonReasons.TargetIsAlreadyProne
                                    : Usability.Usable);
                        lunges.Add(new ActionPossibility(trip));
                    }
                    if (item.HasTrait(Trait.Shove))
                    {
                        CombatAction shove = CombatManeuverPossibilities.CreateShoveAction(self, item);
                        shove.Name = "Lunge - " + shove.Name;
                        shove.ShortName = "Lunge - Shove";
                        shove.ContextMenuName = $"Lunge - Shove ({item.BaseHumanName})";
                        shove.Target = ReachPlusFive(item, self)
                            .WithAdditionalConditionOnTargetCreature(
                                new TargetMustNotBeTwoSizesAboveYouCreatureTargetingRequirement())
                            .WithAdditionalConditionOnTargetCreature((a, _) =>
                                !a.HasFreeHand && !a.WieldsItem(Trait.Shove)
                                    ? Usability.CommonReasons.NoFreeHandForManeuver
                                    : Usability.Usable);
                        lunges.Add(new ActionPossibility(shove));
                    }
                    // ReSharper disable once InvertIf
                    if (item.HasTrait(Trait.Disarm))
                    {
                        CombatAction disarm = CombatManeuverPossibilities.CreateDisarmAction(self, item);
                        disarm.Name = "Lunge - " + disarm.Name;
                        disarm.ShortName = "Lunge - Disarm";
                        disarm.ContextMenuName = $"Lunge - Disarm ({item.BaseHumanName})";
                        disarm.Target = ReachPlusFive(item, self)
                            .WithAdditionalConditionOnTargetCreature((a, _) =>
                                !a.HasFreeHand && !a.WieldsItem(Trait.Disarm)
                                    ? Usability.CommonReasons.NoFreeHandForManeuver
                                    : Usability.Usable)
                            .WithAdditionalConditionOnTargetCreature(
                                new TargetWieldsAnItemCreatureTargetingRequirement());
                        lunges.Add(new ActionPossibility(disarm));
                    }
                }
                return lunges;
            };
        });
    }
    
    public static CreatureTarget ReachPlusFive(Item meleeWeapon, Creature owner)
    {
        return meleeWeapon.HasTrait(Trait.TwoHanded) ? new CreatureTarget(RangeKind.Melee, [
            new EnemyCreatureTargetingRequirement(),
            new MaximumRangeCreatureTargetingRequirement(meleeWeapon.DetermineReach(owner)+1),
            new TwoHandedRequirement(meleeWeapon)
        ],  null) : new CreatureTarget(RangeKind.Melee, [
            new EnemyCreatureTargetingRequirement(),
            new MaximumRangeCreatureTargetingRequirement(meleeWeapon.DetermineReach(owner)+1)
        ],  null);
    }

    public static TrueFeat ArchetypeFeat(string featName, int level, string flavorText, string rulesText, Trait archetypeTrait, FeatName? registeredFeatName = null, params Trait[] traits)
    {
        return new TrueFeat(registeredFeatName ?? ModManager.RegisterFeatName("RE_"+featName.Replace(" ", "").Replace("'", "").Replace(",", ""), featName), level, flavorText, rulesText, traits)
            .WithAvailableAsArchetypeFeat(archetypeTrait);
    }
    public static TrueFeat ArchetypeFeat(string featName, int level, string flavorText, string rulesText, Trait archetypeTrait, params Trait[] traits)
    {
        return ArchetypeFeat(featName, level, flavorText, rulesText, archetypeTrait, null, traits);
    }
    ///<summary>
    /// Does not need Skill or General trait
    /// </summary>
    public static TrueFeat ArchetypeSkillFeat(string featName, int level, string flavorText, string rulesText,
        Trait archetypeTrait, FeatName? registeredFeatName = null, params Trait[] traits)
    {
        List<Trait> add = traits.ToList();
        add.AddRange([Trait.Skill, Trait.General]);
        TrueFeat skillFeat = ArchetypeFeat(featName, level, flavorText, rulesText, archetypeTrait, registeredFeatName,add.ToArray());
        skillFeat.BelongsToArchetype = Trait.None;
        skillFeat.FeatGroup = FeatGroup.SkillFeats;
        return skillFeat;
    }

    public static TrueFeat ArchetypeSkillFeat(string featName, int level, string flavorText, string rulesText,
        Trait archetypeTrait, params Trait[] traits)
    {
        return ArchetypeSkillFeat(featName, level, flavorText, rulesText, archetypeTrait, null, traits);
    }
    

    // public static void SpearDancerLogic(TrueFeat feat)
    // {
    //     feat.WithPermanentQEffect("You can Step before or after your Strike with a spear or polearm.", qf =>
    //     {
    //         Creature self = qf.Owner;
    //         qf.Name = "Spear Dancer {icon:Action}";
    //         qf.StateCheck = _ =>
    //         {
    //             self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
    //             {
    //                 ProvideStrikeModifier = weapon =>
    //                 {
    //                     if (!weapon.Traits.Any(trait => trait is Trait.Polearm or Trait.Spear)) return null;
    //                     return new CombatAction(self,
    //                         new SideBySideIllustration(IllustrationName.Walk, weapon.Illustration), "Step, then Strike",
    //                         [Trait.Fighter, Trait.Basic],
    //                         "{i}You favor weapons that allow you to lash out viciously while keeping enemies at bay, giving you an opportunity to strike without fear of reprisal.{/i}\n\nStep, then Strike with a spear or polearm.",
    //                         Target.Self()).WithActionCost(1).WithEffectOnChosenTargets(async (spell, caster, _) =>
    //                     {
    //                         if (!await caster.StepAsync("Make a Step. Then you will Strike.", true))
    //                         {
    //                             spell.RevertRequested = true;
    //                         }
    //                         else
    //                         {
    //                             await caster.Battle.GameLoop.FullCast(caster.CreateStrike(weapon).WithActionCost(0));
    //                         }
    //                     });
    //                 }
    //             });
    //             self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
    //             {
    //                 ProvideStrikeModifier = weapon =>
    //                 {
    //                     CombatAction strike = qf.Owner.CreateStrike(weapon);
    //                     strike.Name = "Strike, then Step";
    //                     strike.Illustration = new SideBySideIllustration(strike.Illustration,
    //                         IllustrationName.Walk);
    //                     strike.Traits.Add(Trait.Basic);
    //                     strike.Description = StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
    //                         additionalAftertext: "Make a Step.");
    //                     strike.EffectOnChosenTargets = Delegates.SmartCombineDelegates(strike.EffectOnChosenTargets,
    //                         async (_, caster, _) =>
    //                             await caster.StepAsync("Make a Step.", allowPass: true));
    //                     return weapon.Traits.Any(trait => trait is Trait.Polearm or Trait.Spear) ? strike : null;;
    //                 }
    //             });
    //         };
    //     });
    // }
}