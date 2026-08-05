using System.Runtime.CompilerServices;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Feats.Features;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Kineticist;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
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
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Dawnsbury.ThirdParty.SteamApi;
using RemasterExpanded.MySpells;
using RemasterExpanded.Technical;
using SpiritDamage;
using static Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Specific.SpellcastingStrikes;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.ClassChangesAndFeats;

public class MagusRemaster
{
    internal static void Load()
    {
        if (AllFeats.GetFeatByFeatName(FeatName.Magus) is not ClassSelectionFeat magusClass)
            return;
        magusClass.WithModifiedRulesText("As you make a melee Strike, you can spend an extra action to imbue that Strike with an attack spell you know. If you hit, you deal both normal damage and the effect of that spell. After you use Spellstrike, you must recharge it before using it again. Using Spellstrike counts as two attacks for your multiple attack penalty.", 
            "As you make a melee Strike, you can spend an extra action to imbue that Strike with a spell that requires an attack roll or saving throw that you know. If you hit, you deal both normal damage and the effect of that spell (if the spell was an attack roll spell, use the result of your Strike for the effect of the spell, if it was a saving throw spell, unless the Strike was a critical miss, the target must make a saving throw against the spell). After you use Spellstrike, you must recharge it before using it again. Using Spellstrike counts as two attacks for your multiple attack penalty.")
            .WithModifiedRulesText("After you use Spellstrike or cast a spell, you can spend an action to enter the Arcane cascade stance which causes your melee Strikes to deal 1 extra damage.", 
                "You can enter the Arcane Cascade stance with an action. While you're in the stance, your melee Strikes gain the arcane trait (making them magical) and deal 1 extra force damage, and you gain resistance 1 to damage from spells. In addition, if you Cast a Spell and then enter Arcane Cascade on the same turn, you can change the damage from the stance to any one damage type that spell could deal instead of force.");
        magusClass.WithOnSheet(sheet =>
        {
            for (var i = 1; i <= 20; i += 1)
            {
                int thisLevel = i;
                sheet.AddAtLevel(thisLevel, values =>
                {
                    values.PreparedSpells[Trait.Magus].Slots.RemoveAll(pss => pss.Key.Contains("Magus:Spell") || pss.Key.Contains("StudiousSpell"));
                });
            }
            sheet.PreparedSpells[Trait.Magus].Slots.Add(new FreePreparedSpellSlot(1, "MagusR:Spell1-1"));
            for (var i = 2; i <= 18; ++i)
            {
                int thisLevel = i;
                if (thisLevel % 2 == 1)
                    sheet.AddAtLevel(thisLevel, values =>
                    {
                        int level = (thisLevel + 1) / 2;
                        values.PreparedSpells[Trait.Magus].Slots.Add(new FreePreparedSpellSlot(level, $"MagusR:Spell{level}-1"));
                    });
                else
                    sheet.AddAtLevel(thisLevel, values =>
                    {
                        int level = thisLevel / 2;
                        values.PreparedSpells[Trait.Magus].Slots.Add(new FreePreparedSpellSlot(level, $"MagusR:Spell{level}-2"));
                    });
            }
            sheet.AddAtLevel(7, values =>
            {
                values.Tags.TryAdd("StudiousSpells", new List<SpellId>());
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.WaterWalk);
            });
            sheet.AddAtLevel(11, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.Haste);
            });
            sheet.AddAtLevel(13, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.Fly);
            });
        })
        .WithOnCreature((values, creature) =>
        {
            creature.AddQEffect(new QEffect
            {
                ModifyActionPossibility = (effect, cascade) =>
                {
                    if (cascade.Name != "Arcane cascade")
                        return;
                    cascade.Target = Target.Self();
                    Creature magus = effect.Owner;
                    CombatAction? triggeringSpell =
                        magus.Actions.ActionHistoryThisTurn.LastOrDefault(sp => sp.HasTrait(Trait.Spell));
                    ModifyCascade(cascade, triggeringSpell);
                },
                AfterYouTakeAction = async (effect, triggeringSpell) =>
                {
                    Creature magus = effect.Owner;
                    if (!values.Tags.TryGetValue("StudiousSpells", out object? studious) || studious is not List<SpellId> studiousSpells)
                        return;
                    if (studiousSpells.Contains(triggeringSpell.SpellId))
                    {
                        magus.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                        {
                            ModifyActionPossibility = (_, cascade) =>
                            {
                                if (cascade.Name != "Arcane cascade")
                                    return;
                                cascade.ActionCost = 0;
                            },
                            AfterYouTakeAction = async (qf, spell) =>
                            {
                                if (spell == triggeringSpell)
                                    return;
                                qf.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        });
                    }
                },
                AdjustEachPossibility = (_, possibility) =>
                {
                    if (possibility is not ActionPossibility ap || ap.CombatAction.Name != "Recharge Spellstrike")
                        return;
                    possibility.WithPossibilityGroup("Recharge Spellstrike");
                }
            });
        });
        if (magusClass.ClassFeatures is not null)
        {
            for (var level = 2; level <= 20; level += 1)
            {
                magusClass.ClassFeatures.ClassFeaturesByLevel[level]
                    .RemoveAll(cf => cf.Caption.ContainsIgnoreCase("spell slot"));
            }
            magusClass.ClassFeatures.ClassFeaturesByLevel[7].RemoveAll(cf => cf.Caption.Contains("Studious"));
            magusClass.ClassFeatures.AddPreparedSpellcasting("one spell slot")
                .AddFeature(7, "Studious spells (when you cast a studious spell, you can use Arcane Cascade as a free action as your next action this turn. Your studious spells are {i}water walk{/i} and an additional spell depending on your hybrid study)")
                .AddFeature(11, "Studious spells (add {i}haste{/i} and an additional spell depending on your hybrid study to your studious spells)")
                .AddFeature(13, "Studious spells (add {i}fly{/i} and an additional spell depending on your hybrid study to your studious spells)");
            magusClass.ClassFeatures.ClassFeaturesByLevel[19].RemoveAll(cf => cf.Caption.ContainsIgnoreCase("level 10"));
            int index = magusClass.ClassFeatures.ClassFeaturesByLevel[7].IndexOf(magusClass.ClassFeatures.ClassFeaturesByLevel[7].FirstOrDefault(cf => cf.Caption.Contains("cascade")) ?? magusClass.ClassFeatures.ClassFeaturesByLevel[7][0]);
            magusClass.ClassFeatures.ClassFeaturesByLevel[7].RemoveAll(ft => ft.Caption.Contains("cascade"));
            magusClass.ClassFeatures.ClassFeaturesByLevel[7].Insert(index, new ClassFeature("Arcane cascade's additional damage and resistance increases to 2 from 1"));
            int index2 = magusClass.ClassFeatures.ClassFeaturesByLevel[15].IndexOf(magusClass.ClassFeatures.ClassFeaturesByLevel[15].FirstOrDefault(cf => cf.Caption.Contains("cascade")) ?? magusClass.ClassFeatures.ClassFeaturesByLevel[15][0]);
            magusClass.ClassFeatures.ClassFeaturesByLevel[15].RemoveAll(ft => ft.Caption.Contains("cascade"));
            magusClass.ClassFeatures.ClassFeaturesByLevel[15].Insert(index2, new ClassFeature("Arcane cascade's additional damage and resistance increases to 3 from 2"));
        }
        AllFeats.GetFeatByFeatName(FeatName.LaughingShadow).WithOnSheet(sheet =>
        {
            sheet.AddAtLevel(7, values =>
            {
                values.Tags.TryAdd("StudiousSpells", new List<SpellId>());
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.Blur);
            });
            sheet.AddAtLevel(11, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.GhostlyWeapon);
            });
            sheet.AddAtLevel(13, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.DimensionDoor);
            });
        })
        .With(ft =>
        {
            ft.RulesText += $"\nAt level 7, add {AllSpells.CreateSpellLink(SpellId.Blur, Trait.Magus, 2)} to your list of studious spells. At level 11, add {AllSpells.CreateSpellLink(SpellId.GhostlyWeapon, Trait.Magus, 3)}. At level 13, add {AllSpells.CreateSpellLink(SpellId.DimensionDoor, Trait.Magus, 4)}.";
        });
        AllFeats.GetFeatByFeatName(FeatName.InexorableIron).WithOnSheet(sheet =>
        {
            sheet.AddAtLevel(7, values =>
            {
                values.Tags.TryAdd("StudiousSpells", new List<SpellId>());
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.Enlarge);
            });
            sheet.AddAtLevel(11, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellIds.Earthbind);
            });
            sheet.AddAtLevel(13, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.DimensionalAnchor);
            });
        }).With(ft =>
        {
            ft.RulesText += $"\nAt level 7, add {AllSpells.CreateSpellLink(SpellId.Enlarge, Trait.Magus, 2)} to your list of studious spells. At level 11, add {AllSpells.CreateSpellLink(SpellIds.Earthbind, Trait.Magus, 3)}. At level 13, add {AllSpells.CreateSpellLink(SpellId.DimensionalAnchor, Trait.Magus, 4)}.";
        });
        AllFeats.GetFeatByFeatName(FeatName.SparklingTarge).WithOnSheet(sheet =>
        {
            sheet.AddAtLevel(7, values =>
            {
                values.Tags.TryAdd("StudiousSpells", new List<SpellId>());
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.ResistEnergy);
            });
            sheet.AddAtLevel(11, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellIds.WardingAggression);
            });
            sheet.AddAtLevel(13, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.Stoneskin);
            });
        }).With(ft =>
        {
            ft.RulesText += $"\nAt level 7, add {AllSpells.CreateSpellLink(SpellId.ResistEnergy, Trait.Magus, 2)} to your list of studious spells. At level 11, add {AllSpells.CreateSpellLink(SpellIds.WardingAggression, Trait.Magus, 3)}. At level 13, add {AllSpells.CreateSpellLink(SpellId.Stoneskin, Trait.Magus, 4)}.";
        });
        AllFeats.GetFeatByFeatName(FeatName.StarlitSpan).WithOnSheet(sheet =>
        {
            sheet.AddAtLevel(7, values =>
            {
                values.Tags.TryAdd("StudiousSpells", new List<SpellId>());
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.TelekineticManeuver);
            });
            sheet.AddAtLevel(11, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellIds.WallOfWind);
            });
            sheet.AddAtLevel(13, values =>
            {
                if (values.Tags["StudiousSpells"] is List<SpellId> studiousSpells)
                    studiousSpells.Add(SpellId.FreedomOfMovement);
            });
        }).With(ft =>
        {
            ft.RulesText += $"\nAt level 7, add {AllSpells.CreateSpellLink(SpellId.TelekineticManeuver, Trait.Magus, 2)} to your list of studious spells. At level 11, add {AllSpells.CreateSpellLink(SpellIds.WallOfWind, Trait.Magus, 3)}. At level 13, add {AllSpells.CreateSpellLink(SpellId.FreedomOfMovement, Trait.Magus, 4)}.";
        });
        ConfluxSpells.Load();
        AllFeats.GetFeatByFeatName(FeatName.ExpansiveSpellstrike).With(ft =>
        {
            ft.OnCreature = null;
            ft.WithPermanentQEffect("Spellstrikes made with area spells affect an area, instead of only the target of your Spellstrike.", qf =>
            {
                qf.Id = QEffectId.ExpansiveSpellstrike;
            });
            ft.WithOnCreature(cr =>
            {
                QEffect effect = new()
                {
                    ProvideActionIntoPossibilitySection = (_, section) =>
                    {
                        return section.PossibilitySectionId != PossibilitySectionId.OtherManeuvers ? null : new ActionPossibility(Toggle(cr.HasEffect(QEffectId.ExpansiveSpellstrike)));
                        CombatAction Toggle(bool hasExpansive)
                        {
                            string description = hasExpansive
                                ? "Disable the effects of Expansive Spellstrike. Your Spellstrikes made with area of effect spells will only affect the target of your Spellstrike."
                                : "Enable the effects of Expansive Spellstrike. Your Spellstrikes made with area of effect spells will affect an area, instead of only the target of your Spellstrike.";
                            return new CombatAction(cr, IllustrationName.EnergyEmanation, (hasExpansive ? "Disable" : "Enable") + " Expansive Spellstrike",
                                    [Trait.DoesNotBreakStealth, Trait.DoesNotPreventDelay, Trait.DoesNotRequireAttackRollOrSavingThrow, Trait.Basic],
                                    description,
                                    Target.Self())
                                .WithActionCost(0)
                                .WithEffectOnSelf(async (_, self) =>
                                {
                                    switch (hasExpansive)
                                    {
                                        case true:
                                            self.RemoveAllQEffects(qff => qff.Id == QEffectId.ExpansiveSpellstrike);
                                            break;
                                        case false:
                                            self.AddQEffect(new QEffect
                                            {
                                                Id = QEffectId.ExpansiveSpellstrike,
                                                DoNotShowUpOverhead = true
                                            });
                                            break;
                                    }
                                });
                        }
                    }
                };
                return effect;
            });
            ft.WithRulesTextCreator(_ => "When you Spellstrike with spells with an area of effect of cone, line, or burst, instead of only applying to the target of your spellstrike, it functions as follows:" +
                                         "\r\n• If your Strike critically fails, the spell is lost with no effect." +
                                         "\r\n• You must place the area such that it includes the target. Burst areas must be centered on the target. Cone and line areas emanate from you and must include the target. If you're not adjacent to the target (such as when you're using a reach weapon or you're a Starlit Span magus), the cone or line emanates from any square you choose that's adjacent to the target. The spell affects all creatures in the area as normal, not only the target of the Spellstrike."+
                                         "\n\n{b}Special{/b} You can enable or disable this feature as a free action from the other actions section.");
            ft.FlavorText = "You can blast the energy of a spell out around the target of your attack.";
        });
        
        AllFeats.GetFeatByFeatName(FeatName.DistractingSpellstrike).WithPermanentQEffect(null, qf =>
        {
            Creature owner = qf.Owner;
            qf.ModifyActionPossibility = (_, action) =>
            {
                if (action.ActionId != RActionIds.DistractingSpellstrike)
                    return;
                if (action.Target is not CreatureTarget target) return;
                target.CreatureTargetingRequirements.RemoveAll(crt =>
                    crt is YouHaveQEffectCreatureTargetingRequirement { QEffectId: QEffectId.ArcaneCascade });
                action.Target = target;
                action.EffectOnChosenTargetsBeforeRolls = async (_, caster, targets) =>
                {
                    if (targets.ChosenCreature == null)
                        return;
                    CombatAction feint = CombatManeuverPossibilities.CreateFeintAction(caster)
                        .WithExtraTrait(Trait.Visual).WithExtraTrait(Trait.Arcane).WithActionCost(0);
                    int amount = caster.HasEffect(QEffectId.GreaterWeaponSpecialization) ? 3 :
                        caster.HasEffect(QEffectId.WeaponSpecialization) ? 2 : 1;
                    QEffect bonus = new()
                    {
                        BonusToSkillChecks = (skill, combatAction, _) => combatAction == feint && skill == Skill.Deception ? new Bonus(amount, BonusType.Status, "Arcane Cascade") : null
                    };
                    if (caster.HasEffect(QEffectId.ArcaneCascade))
                    {
                        caster.AddQEffect(bonus);
                    }
                    await caster.Battle.GameLoop.FullCast(feint, ChosenTargets.CreateSingleTarget(targets.ChosenCreature));
                    bonus.ExpiresAt = ExpirationCondition.Immediately;
                };
                action.WithTargetingTooltip((_, creature, _) =>
                {
                    string str1 = CombatActionExecution.BreakdownAttackForTooltip(action, creature).TooltipDescription;
                    CombatAction feint = CombatManeuverPossibilities.CreateFeintAction(owner);
                    if (feint.ActiveRollSpecification is not { } ar)
                        return str1;
                    if (owner.HasEffect(QEffectId.ArcaneCascade))
                    {
                        int amount = owner.HasEffect(QEffectId.GreaterWeaponSpecialization) ? 3 :
                            owner.HasEffect(QEffectId.WeaponSpecialization) ? 2 : 1;
                        TaggedCalculatedNumberProducer determined = ar.TaggedDetermineBonus;
                        feint.WithActiveRollSpecification(new ActiveRollSpecification(determined.WithExtraBonus((_, _, _) => new Bonus(amount, BonusType.Status, "Arcane Cascade")), ar.TaggedDetermineDC));
                    }
                    string str2 = CombatActionExecution.BreakdownAttackForTooltip(feint, creature).TooltipDescription;
                    return "{b}Feint{/b}\n" + str2 + "\n{b}Strike{/b}\n" + str1;
                });
            };
        })
        .WithRulesTextCreator(_ => "{b}Requirements{/b} You have a hand free, and your Spellstrike is charged." +
                                   "\n\nMake a Spellstrike and Feint against the target of your Strike. Execute your Feint immediately before making the Strike, but after choosing your target. The Feint gains the arcane and visual traits." +
                                   "\n\nIf you're in the Arcane Cascade stance, you gain a status bonus to your Deception check equal to your extra damage from Arcane Cascade (changes to this damage due to abilities other than Arcane Cascade do not apply).");
        AllFeats.GetFeatByFeatName(FeatName.DevastatingSpellstrike).WithPermanentQEffect(null, qf =>
        {
            qf.ModifyActionPossibility = (_, action) =>
            {
                if (action.ActionId != RActionIds.DevastatingSpellstrike)
                    return;
                if (action.Target is not CreatureTarget target)
                    return;
                target.CreatureTargetingRequirements.RemoveAll(crt =>
                    crt is YouHaveQEffectCreatureTargetingRequirement { QEffectId: QEffectId.ArcaneCascade });
                if (action.Tag is not Tuple<CombatAction, Func<Creature, Creature, CheckResult, Task>> tuple)
                    return;
                action.StrikeModifiers.OnEachTarget = Delegates.SmartCombineDelegates(tuple.Item2,
                    async (caster, enemy, _) =>
                    {
                        List<DamageKind> damageKinds2 = [.. DetermineDamageKindFromSpell(tuple.Item1, false)];
                        int amount = caster.HasEffect(QEffectId.GreaterWeaponSpecialization) ? 3 :
                            caster.HasEffect(QEffectId.WeaponSpecialization) ? 2 : 1;
                        int baseD = caster.Level >= 19 ? 5 : caster.Level >= 14 ? 4 : caster.Level >= 9 ? 3 : 2;
                        CombatAction dupe = action.Duplicate().With(a => a.Traits.Remove(Trait.Strike));
                        QEffect bonus = new()
                        {
                            BonusToDamage = (_, combatAction, targ) => targ != enemy && combatAction == dupe ? new Bonus(amount, BonusType.Status, "Arcane Cascade") : null
                        };
                        if (caster.HasEffect(QEffectId.ArcaneCascade))
                        {
                            caster.AddQEffect(bonus);
                        }
                        foreach (Creature creature in enemy.Neighbours.Creatures.Where(cr => cr.EnemyOf(caster)))
                        {
                            DamageKind damageKind = creature.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(damageKinds2);
                            await CommonSpellEffects.DealDirectDamage(dupe, DiceFormula.FromText(baseD.ToString(), "Devastating Spellstrike emanation damage"), creature, CheckResult.Failure, damageKind);
                        }
                        bonus.ExpiresAt = ExpirationCondition.Immediately;
                    });
            };
        }).WithRulesTextCreator(_ => "{b}Requirements{/b} Your Spellstrike is charged." +
                                     "\n\nMake a Spellstrike with a spell that can deal damage. Enemies in a 5-foot emanation around the target (not including the target) take 2 damage of the same damage type your spell could deal. This damage increases to 3 at 9th level, 4 at 14th level, and 5 at 19th level." +
                                     "\n\nIf you're in the Arcane Cascade stance, the emanation's damage gains a status bonus to damage equal to the extra damage from your Arcane Cascade.");

        AllFeats.GetFeatByFeatName(FeatName.SpellSwipe).WithModifiedRulesText("If you have Expansive Spellstrike and use a ", "If you use a ");
        AllFeats.GetFeatByFeatName(FeatName.CascadeCountermeasure).WithModifiedRulesText("cascade countermeasure", "conjurer's countermeasure")
            .With(ft => ft.FlavorText = "Using your knowledge of how to battle enemy mages, you create a barrier to protect yourself from spells.")
            .WithCustomName("Conjurer's Countermeasure")
            .WithIllustration(IllustrationName.SpellImmunity);
        ModManager.RegisterBooleanSettingsOption("RE_RemoveHomebrewMagus", "Remaster Expanded: Remove Homebrew Magus Feats", "If this option is enabled, homebrew feats for the Magus class added by Dawnsbury Days are removed. {b}NOTE:{/b} You must restart the game for this to take place.", false);
        if (!PlayerProfile.Instance.IsBooleanOptionEnabled("RE_RemoveHomebrewMagus")) return;
        {
            foreach (Feat vFeat in AllFeats.All.Where(ft => ft.HasTrait(Trait.Homebrew) && ft.HasTrait(Trait.Magus)))
            {
                vFeat.Traits.Clear();
            }
        }
    }

    internal static IEnumerable<Feat> LoadFeats()
    {
        yield return new TrueFeat(ModManager.RegisterFeatName("MagusAnalysis", "Magus's Analysis"), 1,
            "You make an assessment informed by your knowledge of how a creature fights.",
            $"Attempt a check to {RecallWeakness.GetActionLink("Recall Weakness")} about a creature, then recharge your SpellStrike. You gain a +1 circumstance bonus to your check if you previously hit the creature with a Strike this turn. The subject of your check is immune to Magus's Analysis for the rest of the encounter.",
            [Trait.Magus])
            .WithActionCost(1)
            .WithPermanentQEffect("You can Recall Weakness about a creature and recharge your Spellstrike.", qf =>
            {
                Creature owner = qf.Owner;
                qf.AfterYouTakeActionAgainstTarget = async (_, action, target, result) =>
                {
                    if (!action.HasTrait(Trait.Strike) || result <= CheckResult.Failure)
                        return;
                    target.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                        { Id = MQEffectIds.StruckForAnalysis, Source = owner});
                };
                qf.ProvideMainAction = _ =>
                {
                    if (!owner.HasEffect(QEffectId.SpellstrikeDischarged))
                        return null;
                    CombatAction recallWeakness = RecallWeakness.CreateRecallWeaknessAction(owner).WithActionCost(0);
                    if (recallWeakness.Target is not CreatureTarget recallTarget)
                        return null;
                    CombatAction analysis = new CombatAction(owner,
                        new SideBySideIllustration(IllustrationName.NarratorBook, IllustrationName.Good),
                        "Magus's Analysis", [Trait.Basic, Trait.Concentrate],
                         "Attempt a check to Recall Weakness about a creature, then recharge your SpellStrike. You gain a +1 circumstance bonus to your check if you previously hit the creature with a Strike this turn. The subject of your check is immune to Magus's Analysis for the rest of the encounter.",
                        recallTarget.WithAdditionalConditionOnTargetCreature((_, enemy) => enemy.HasEffect(MQEffectIds.Analyzed) ? Usability.NotUsableOnThisCreature("Immune to Magus's Analysis.") : Usability.Usable)
                            .WithAdditionalConditionOnTargetCreature((self, _) => RecallWeakness.CreateRecallWeaknessAction(self).WithActionCost(0).CanBeginToUse(self) ? Usability.Usable : Usability.NotUsable("You cannot use the Recall Weakness action.")))
                        .WithActionCost(1)
                        .WithEffectOnEachTarget(async (_, self, target, _) =>
                        {
                            QEffect bonus = new()
                            {
                                BonusToAttackRolls = (_, action, cr) => cr == target && action.ActionId == RecallWeakness.RWActionId ? new Bonus(1, BonusType.Circumstance, "Magus's Analysis") : null
                            };
                            if (target.FindQEffect(MQEffectIds.StruckForAnalysis)?.Source == self)
                                self.AddQEffect(bonus);
                            await self.Battle.GameLoop.FullCast(recallWeakness,
                                ChosenTargets.CreateSingleTarget(target));
                            bonus.ExpiresAt = ExpirationCondition.Immediately;
                            Magus.RechargeSpellstrike(self);
                            target.AddQEffect(new QEffect {Id = MQEffectIds.Analyzed});
                        });
                    return new ActionPossibility(analysis).WithPossibilityGroup("Recharge Spellstrike");
                };
            }).WithPrerequisite(new TrueClassPrerequisite(Trait.Magus));
        
        yield return new TrueFeat(ModManager.RegisterFeatName("RunningRecharge", "Running Recharge"), 4,
            "Preparing yourself for your next assault, you both relocate yourself and ready your Spellstrike.",
            "You recharge your Spellstrike and either Stride up to half your speed or Step.",
            [Trait.Magus, Trait.Concentrate])
            .WithActionCost(1)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    if (!owner.HasEffect(QEffectId.SpellstrikeDischarged))
                        return null;
                    CombatAction runningRecharge = new CombatAction(owner,
                            new SideBySideIllustration(IllustrationName.FleetStep, IllustrationName.Good),
                            "Running Recharge", [Trait.Concentrate, Trait.Basic],
                            "You recharge your Spellstrike and either Stride up to half your speed or Step.",
                            Target.Self())
                        .WithActionCost(1)
                        .WithEffectOnSelf(async (_, self) =>
                        {
                            Magus.RechargeSpellstrike(self);
                            await self.StrideOrStepAdvancedAsync("Choose where to Stride to or where to Step.",
                                maximumSpeed: Math.Max(1, self.Speed / 2), allowCancel: true, allowStep: true);
                        });
                    return new ActionPossibility(runningRecharge).WithPossibilityGroup("Recharge Spellstrike");
                };
            }).WithPrerequisite(new TrueClassPrerequisite(Trait.Magus));
        yield return new TrueFeat(ModManager.RegisterFeatName("SpellCrash", "Spell Crash"), 6,
                "Instead of blending a spell into an attack, you can funnel the spell's magic into a pure blast that expands out from you by smashing your weapon or fist into the ground.",
                SpellCrashDescription(),
                [Trait.Arcane, Trait.Force, Trait.Magus])
            .WithActionCost(2)
            .WithPermanentQEffect("You can expend a spell slot and discharge your Spellstrike to deal damage in an area.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    if (self.HasEffect(QEffectId.SpellstrikeDischarged) ||
                        !self.QEffects.Any(qff => qff.Name is "Spellstrike {icon:TwoActions}") ||
                        self.Spellcasting == null || !self.Spellcasting.Sources.Any(src =>
                            src is { Kind: SpellcastingKind.Prepared, Spells.Count: > 0 }))
                        return null;
                    List<PossibilitySection> spellLevels = [];
                    PossibilitySection level1 = new("Level 1");
                    PossibilitySection level2 = new("Level 2");
                    PossibilitySection level3 = new("Level 3");
                    PossibilitySection level4 = new("Level 4");
                    PossibilitySection level5 = new("Level 5");
                    PossibilitySection level6 = new("Level 6");
                    PossibilitySection level7 = new("Level 7");
                    PossibilitySection level8 = new("Level 8");
                    PossibilitySection level9 = new("Level 9");
                    PossibilitySection level10 = new("Level 10");
                    foreach (SpellcastingSource source in self.Spellcasting.Sources.Where(src =>
                                 src is { Kind: SpellcastingKind.Prepared, Spells.Count: > 0 }))
                    {
                        foreach (CombatAction spell in source.Spells)
                        {
                            switch (spell.SpellLevel)
                            {
                                case 1:
                                    level1.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 2:
                                    level2.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 3:
                                    level3.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 4:
                                    level4.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 5:
                                    level5.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 6:
                                    level6.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 7:
                                    level7.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 8:
                                    level8.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 9:
                                    level9.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                                case 10:
                                    level10.Possibilities.Add(Possibilities.CreateSpellPossibility(SpellCrashActions(self, spell)));
                                    break;
                            }
                        }
                    }
                    spellLevels.AddRange([level1, level2, level3, level4, level5, level6, level7, level8, level9, level10]);
                    return new SubmenuPossibility(MIllustrations.CreateIllustration("SpellCrash"), "Spell Crash")
                    {
                        Subsections = spellLevels,
                        SpellIfAny = new CombatAction(self, MIllustrations.CreateIllustration("SpellCrash"), "Spell Crash",
                            [Trait.Arcane, Trait.Force], SpellCrashDescription(), Target.Self())
                    };
                };
                return;
                CombatAction SpellCrashActions(Creature owner, CombatAction spell)
                {
                    DamageKind damageKind = DamageKind.Force;
                    bool toTrip = owner.Weapons.Any(wp => wp.HasTrait(Trait.Trip));
                    bool toShove =  owner.Weapons.Any(wp => wp.HasTrait(Trait.Shove));
                    int spellLevel = spell.SpellLevel;
                    List<DamageKind> kinds = [.. DetermineDamageKindFromSpell(spell)];
                    return CombatAction.CreateAction(owner,
                            spell.Illustration,
                            $"Spell Crash ({spell.Name})", [Trait.Arcane, Trait.Force, Trait.Basic],
                            SpellCrashDescription(false, false, toTrip, toShove, spellLevel, kinds),
                            Target.DependsOnSpellVariant(variant =>
                            {
                                return variant.Id switch
                                {
                                    "EMANATION" => Target.SelfExcludingEmanation(4),
                                    "LINE" => Target.Line(12),
                                    "CONE" => Target.Cone(6),
                                    _ => throw new ArgumentOutOfRangeException(nameof(variant), spell, null)
                                };
                            }), 2, SfxName.PureEnergyRelease, new SavingThrow(Defense.Reflex, creature =>
                            {
                                if (creature is { Spellcasting: not null })
                                    return creature.Spellcasting.Sources
                                               .MaxBy(src => src.GetSpellSaveDC())
                                               ?.GetSpellSaveDC() ??
                                           10;
                                return 10;
                            }))
                        .WithProjectileCone(VfxStyle.BasicProjectileCone(MIllustrations.CreateIllustration("SpellCrash")))
                        .WithVariants(
                        [
                            new SpellVariant("EMANATION", "Emanation Spell Crash",
                                new SideBySideIllustration(MIllustrations.CreateIllustration("SpellCrash"),
                                    IllustrationName.VariantBurst15)),
                            new SpellVariant("CONE", "Cone Spell Crash",
                                new SideBySideIllustration(MIllustrations.CreateIllustration("SpellCrash"),
                                    IllustrationName.VariantCone30)),
                            new SpellVariant("LINE", "Line Spell Crash",
                                new SideBySideIllustration(MIllustrations.CreateIllustration("SpellCrash"),
                                    IllustrationName.VariantLine60))
                        ])
                        .WithCreateVariantDescription((_, _) => SpellCrashDescription(false, true, toTrip, toShove,  spellLevel, kinds))
                        .WithPrologueEffectOnChosenTargetsBeforeRolls(async (action, caster, _) =>
                        {
                            if (kinds.Count <= 1)
                                return;
                            List<string> list = [.. kinds.Select(kind => kind.ToString())];
                            ChoiceButtonOption choice = await caster.AskForChoiceAmongButtons(
                                MIllustrations.CreateIllustration("SpellCrash"),
                                "Which damage type should Spell Crash deal?",
                                [.. list]);
                            damageKind = kinds[choice.Index];
                            if (damageKind == DamageKind.Force)
                                return;
                            action.Traits.Remove(Trait.Force);
                            if (damageKind == DamageKind.Spirit)
                            {
                                action.Traits.Add(SpiritTrait.Spirit);
                                return;
                            }
                            action.WithExtraTrait(DamageKindExtensions.DamageKindToTrait(damageKind));
                        })
                        .WithEffectOnSelf(caster =>
                        {
                            QEffect spellStrike = self.QEffects.FirstOrDefault(qff => qff.Name is "Spellstrike {icon:TwoActions}")!;
                            DischargeSpellstrike(caster, spellStrike);
                            caster.Spellcasting?.UseUpSpellcastingResources(spell);
                        })
                        .WithEffectOnEachTarget(async (action, caster, creature, result) =>
                        {
                            await CommonSpellEffects.DealBasicDamage(action, caster, creature, result,
                                DiceFormula.FromText($"{spellLevel}d10", "Spell Crash"), damageKind);
                            if (result >= CheckResult.Success)
                                return;
                            if (toTrip)
                                await creature.FallProne();
                            if (toShove)
                                await caster.PushCreature(creature, 2);
                        });
                }

                void DischargeSpellstrike(Creature spellStriker, QEffect qfSpellstrike)
                {
                    if (spellStriker.PersistentCharacterSheet?.Class?.ClassTrait != Trait.Magus)
                    {
                        qfSpellstrike.ExpiresAt = ExpirationCondition.Immediately;
                    }
                    else
                    {
                        spellStriker.AddQEffect(new QEffect
                        {
                            Id = QEffectId.SpellstrikeDischarged,
                            AfterYouTakeAction = async (qfDischarge, action) =>
                            {
                                if (!action.HasTrait(Trait.Focus) || !action.HasTrait(Trait.Magus))
                                    return;
                                qfDischarge.ExpiresAt = ExpirationCondition.Immediately;
                            },
                            ProvideMainAction = qfDischarge =>
                                (ActionPossibility)new CombatAction(qfDischarge.Owner,
                                        IllustrationName.Good, "Recharge Spellstrike", 
                                        [
                                            Trait.Concentrate,
                                            Trait.Basic
                                        ],
                                        "Recharge your Spellstrike so that you can use it again." +
                                        (qfDischarge.Owner.HasEffect(QEffectId.MagussConcentration)
                                            ? " {Blue}You gain a +1 circumstance bonus to your next attack until the end of your next turn.{/Blue}"
                                            : ""), Target.Self()).WithActionCost(1)
                                    .WithSoundEffect(SfxName.AuraExpansion)
                                    .WithEffectOnSelf(
                                        async self2 => Magus.RechargeSpellstrike(self2))
                        });
                    }
                }
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_StarlitEyes", "Starlit Eyes"), 4, 
            "Starlight shines in your sight, enhancing your perception and range.",
            "When you make a ranged Strike, you lower the DC of your flat check to target a creature that's concealed or hidden from you. The DC is reduced to 4 instead of 5 against a concealed creature and to 10 instead of 11 against a hidden one. If you're in Arcane Cascade stance, you instead reduce the DC to 3 against a concealed creature or 9 against a hidden one." +
            "\n\nIn addition, when you cast shooting star and target a hidden creature, you don't have to attempt the flat check for targeting a hidden creature with a ranged Strike.",
            [Trait.Magus])
            .WithPrerequisite(FeatName.StarlitSpan, "Starlit Span")
            .WithPermanentQEffect("The flat check for targeting concealed creatures is reduced. You ignore the flat check for targeting a hidden creature when casting {i}shooting star/i}.",qf =>
            {
                qf.Id = MQEffectIds.StarlitEyes;
                qf.ModifyActionPossibility = (_, action) =>
                {
                    if (action.SpellId != SpellId.ShootingStar)
                        return;
                    QEffect star = new()
                    {
                        StateCheck = inner => inner.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                            { Id = QEffectId.TrueStrike })
                    };
                    action.WithPrologueEffectOnChosenTargetsBeforeRolls(async (_, cr, _) => cr.AddQEffect(star));
                    action.WithEffectOnEachTarget(async (_, _, _, _) => star.ExpiresAt = ExpirationCondition.Immediately);
                };
            });
        
        yield return new TrueFeat(ModManager.RegisterFeatName("KnowledgeIsPower", "Knowledge is Power"), 6,
                "You know knowledge is a blade as sharp as any sword.",
                $"When you critically succeed at a {RecallWeakness.GetActionLink("Recall Weakness")} check about a creature, you gain a +1 circumstance bonus to your next attack roll against the creature, to your AC against its next attack roll, and to your save against its next effect requiring a save." +
                "\n\nAs long as you can speak, your allies gain these benefits as well.",
                [Trait.Magus])
            .WithPermanentQEffect("When you critically succeed at a Recall Weakness check on a creature, you and allies gain additional benefits.", qf =>
            {
                Creature self = qf.Owner;
                qf.AfterYouTakeAction = async (_, action) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId || action.CheckResult != CheckResult.CriticalSuccess)
                        return;
                    Creature? enemy = action.ChosenTargets.ChosenCreature;
                    if (enemy ==  null)
                        return;
                    self.AddQEffect(KnowledgeBonuses(enemy));
                    CombatAction speak = CombatAction.CreateSimple(self, "speak", Trait.Auditory, Trait.Linguistic, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName)
                        .WithActionCost(0);
                    if (!speak.CanBeginToUse(self) || !await self.Battle.GameLoop.FullCast(speak))
                        return;
                    foreach (Creature ally in self.Battle.AllCreatures.Where(cr => cr.FriendOfAndNotSelf(self)))
                    {
                        ally.AddQEffect(KnowledgeBonuses(enemy));
                    }

                    return;

                    static QEffect KnowledgeBonuses(Creature enemy)
                    {
                        return new QEffect("Knowledge is Power", "", IllustrationName.NarratorBook)
                        {
                            Tag = new HashSet<string>(),
                            BonusToAttackRolls = (effect, combatAction, target) =>
                            {
                                if (effect.Tag is not HashSet<string> use || use.Contains("Attack"))
                                    return null;
                                if (target != enemy || !combatAction.HasTrait(Trait.Attack))
                                    return null;
                                return new Bonus(1, BonusType.Circumstance, "Knowledge is Power", true);
                            },
                            AfterYouTakeActionAgainstTarget = async (effect, combatAction, target, _) =>
                            {
                                if (effect.Tag is not HashSet<string> use)
                                    return;
                                if (target != enemy || !combatAction.HasTrait(Trait.Attack))
                                    return;
                                use.Add("Attack");
                            },
                            BonusToDefenses = (effect, combatAction, defense) =>
                            {
                                if (effect.Tag is not HashSet<string> use)
                                    return null;
                                if (combatAction?.Owner != enemy)
                                    return null;
                                if (defense == Defense.AC && !use.Contains("AC"))
                                    return new Bonus(1, BonusType.Circumstance, "Knowledge is Power");
                                if (!defense.IsSavingThrow() || combatAction.SavingThrow == null || use.Contains("Save"))
                                    return null;
                                return new Bonus(1, BonusType.Circumstance, "Knowledge is Power");
                            },
                            AfterYouAreTargeted = async (effect, combatAction) =>
                            {
                                if (effect.Tag is not HashSet<string> use)
                                    return;
                                if (combatAction.Owner != enemy)
                                    return;
                                Defense? defense = combatAction.ActiveRollSpecification?.TaggedDetermineDC
                                    .InvolvedDefense ?? combatAction.SavingThrow?.Defense;
                                // ReSharper disable once ConvertIfStatementToSwitchStatement
                                if (defense is null) return;
                                if (defense == Defense.AC)
                                {
                                    use.Add("AC");
                                    return;
                                }
                                if (!defense.Value.IsSavingThrow() || combatAction.SavingThrow == null)
                                {
                                    return;
                                }
                                use.Add("Save");
                            },
                            StateCheck = effect =>
                            {
                                if (effect.Tag is not HashSet<string> use)
                                    return;
                                if (use.Count >= 3 || !enemy.Alive)
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                effect.Description = "";
                                bool value = use.Contains("Attack");
                                bool value2 = use.Contains("Save");
                                if (!value || !value2)
                                    effect.Description +=
                                        $"You have a +1 circumstance bonus to the next {(!value && !value2 ? "attack roll and saving throw" : !value && value2 ? "attack roll" : "saving throw")} made against {enemy.Name}.";
                                bool value3 = use.Contains("AC");
                                if ((!value || !value2) && !value3)
                                    effect.Description += " ";
                                if (!value3)
                                    effect.Description +=
                                        $"You have a +1 circumstance bonus to AC against the next attack made by {enemy.Name}.";
                            }
                        };
                    }
                };
            }); 
        yield return new TrueFeat(ModManager.RegisterFeatName("FusedStaff", "Fused Staff"), 8,
                "You can use your arcane prowess to fuse a magical staff into a weapon.",
                "The item you wield in your left hand is automatically fused with a magical staff you have prepared at the start of an encounter. The fused item has the staff's activations and other benefits, and it can be used to cast spells." +
                "\n\nThe staff and the weapon share their fundamental runes, using whichever weapon potency and whichever striking rune is higher level. Any property runes from the staff are not applied." +
                "\n\nOnly you can wield the fused item.",
                [Trait.Magus])
            .WithPermanentQEffect("You fuse a staff with a weapon, enabling you to use the staff's abilities while wielding the weapon.", qf =>
            {
                Creature self = qf.Owner;
                qf.StartOfCombat = async _ =>
                {
                    Item? staff = self.CarriedItems.FirstOrDefault(it =>
                        it.MagicalStaffProperties is not null &&
                        it.ItemModifications.Any(mod => mod.Kind == MagicalStaves.Charges));
                    if (staff is null || self.HeldItems.Count == 0)
                        return;
                    Item weapon = self.HeldItems[0];
                    weapon.With(wp =>
                    {
                        wp.MagicalStaffProperties = staff.MagicalStaffProperties;
                        wp.StateCheckWhenWielded += staff.StateCheckWhenWielded;
                        wp.Traits.Add(Trait.MagicalStaff);
                        wp.WithModification(staff.ItemModifications.FirstOrDefault(mod => mod.Kind == MagicalStaves.Charges)!);
                        Item? staffRuneP = staff.ActiveRunes.FirstOrDefault(rune =>
                            rune.RuneProperties?.RuneKind == RuneKind.WeaponPotency);
                        Item? weaponRuneP = wp.ActiveRunes.FirstOrDefault(rune =>
                            rune.RuneProperties?.RuneKind == RuneKind.WeaponPotency);
                        if (staffRuneP is { RuneProperties: { } runeP } && runeP.FundamentalLevel >
                            (weaponRuneP?.RuneProperties?.FundamentalLevel ?? 0))
                        {
                            wp.Runes.RemoveAll(rn => rn.RuneProperties?.RuneKind == RuneKind.WeaponPotency);
                            RuneHandling.HandleRune(staff, wp, RuneKind.WeaponPotency);
                            int? plus = weaponRuneP?.RuneProperties?.FundamentalLevel;
                            wp.ProsaicName = wp.ProsaicName.Contains($"+{plus}") ? wp.ProsaicName.Replace($"+{plus}", $"+{runeP.FundamentalLevel}".AsBlue()) : $"{{Blue}}+{runeP.FundamentalLevel}{{/Blue}} {wp.ProsaicName}";
                        }
                        Item? staffRuneS = staff.ActiveRunes.FirstOrDefault(rune =>
                            rune.RuneProperties?.RuneKind == RuneKind.WeaponStriking);
                        Item? weaponRuneS = wp.ActiveRunes.FirstOrDefault(rune =>
                            rune.RuneProperties?.RuneKind == RuneKind.WeaponStriking);
                        if (staffRuneS is { RuneProperties: { } runeS } && runeS.FundamentalLevel >
                            (weaponRuneS?.RuneProperties?.FundamentalLevel ?? 0))
                        {
                            wp.Runes.RemoveAll(rn => rn.RuneProperties?.RuneKind == RuneKind.WeaponStriking);
                            RuneHandling.HandleRune(staff, wp, RuneKind.WeaponStriking);
                            string striking = weaponRuneP?.RuneProperties?.Prefix ?? "";
                            string striking2 = runeS.Prefix;
                            if (wp.ProsaicName.Contains("striking"))
                                wp.ProsaicName = wp.ProsaicName.Replace(striking, striking2.AsBlue());
                            else if (wp.ProsaicName.Contains('+'))
                                wp.ProsaicName = wp.ProsaicName.Insert(wp.ProsaicName.IndexOf('+')+2, $" {striking2.AsBlue()}");
                            else
                                wp.ProsaicName = $"{striking2.AsBlue()} {wp.ProsaicName}";
                        }

                        wp.ProsaicName += $" {staff.ProsaicName.Remove(0, staff.ProsaicName.IndexOf("of", StringComparison.Ordinal))}".AsBlue();
                        if (staff.Illustration is SuperimposedIllustration { Bottom: { } illustration })
                            wp.Illustration = new SuperimposedIllustration(illustration, wp.Illustration);
                        wp.WithCanUse((values, _) => values == self.PersistentCharacterSheet?.Calculated);
                    });
                    self.CarriedItems.Remove(staff);
                    self.AddQEffect(new QEffect
                    {
                        EndOfCombat = async (_, _) =>
                        {
                            self.CarriedItems.Add(staff);
                        }
                    });
                };

            });
        yield return new TrueFeat(ModManager.RegisterFeatName("SprintingSpellstrike", "Sprinting Spellstrike"), 12,
                "Magic propels you across the battlefield as you speed toward your target.",
                "{b}Requirements{/b} Your Spellstrike is charged." +
                "\n\nMake a Spellstrike, casting a spell that has a line area. Before making your Strike, you Stride in a straight line to a distance up to the line's length; you can move through enemies during this movement, and your movement doesn't trigger reactions." +
                "\n\n{b}Special{/b} If you have the Expansive Spellstrike feat, the spell's area uses the line of your movement instead of the normal method of determining the line.",
                [Trait.Magus])
            .WithActionCost(2)
            .WithPermanentQEffect("You can make a Spellstrike with a line spell and Stride in a straight line up to the line's length before making the Strike.", qf =>
            {
                Creature spellstriker = qf.Owner;
                const SpellcastingStrikeKind strikeKind = SpellcastingStrikeKind.Spellstrike;
                const Trait classOfOrigin = Trait.Magus;
                qf.ProvideStrikeModifierAsPossibilities = (_, weapon) =>
                {
                    if (spellstriker.Spellcasting == null || (weapon.HasTrait(Trait.Ranged) && !spellstriker.HasEffect(QEffectId.StarlitSpan)))
                        return [];
                    List<SubmenuPossibility> possibilities =
                    [
                        .. spellstriker.Spellcasting.Sources
                            .Where(src => src.Spells.Any(sp => sp.Target is LineAreaTarget)).Select(source =>
                                CreateSpellstrikeMenu(source, "Sprinting Spellstrike",
                                    spell => CreateSpellcastingStrike(spell, strikeKind, weapon,
                                        "Sprinting Spellstrike", null, () => DischargeSpellstrike(spellstriker),
                                        spellstriker, null, null, false, false, true)))
                    ];
                    return possibilities;

                    SubmenuPossibility CreateSpellstrikeMenu(
                        SpellcastingSource source,
                        string caption,
                        Func<CombatAction, CombatAction?> spellTransformation)
                    {
                        string str = source.ClassOfOrigin != classOfOrigin || source.Self.Spellcasting != null && source.Self.Spellcasting.Sources.Count != 1 ? $" ({source.ClassOfOrigin.HumanizeTitleCase2()})" : "";
                        SubmenuPossibility castASpell = new(new SideBySideIllustration(weapon.Illustration, IllustrationName.CastASpell), caption + str)
                        {
                            Subsections = []
                        };
                        if (spellstriker.Spellcasting is { FocusPoints: > 0 } && source.FocusSpells.Count > 0)
                            AddSpellSubmenu("Focus spells " + string.Join("", Enumerable.Repeat<string>("{icon:SpontaneousSpellSlot}", spellstriker.Spellcasting.FocusPoints)), source.FocusSpells);
                        if (source.PsiCantrips.Count > 0) 
                            AddSpellSubmenu("Psi cantrips " + string.Join("", Enumerable.Repeat<string>("{icon:SpontaneousSpellSlot}", spellstriker.Spellcasting!.FocusPoints)), source.PsiCantrips);
                        if (source.Cantrips.Count > 0)
                            AddSpellSubmenu("Cantrips", source.Cantrips);
                        if (source.Kind is SpellcastingKind.Prepared or SpellcastingKind.Innate &&
                            source.Spells.Count > 0)
                        {
                            for (var index = 1; index <= 10; ++index)
                            {
                                int index1 = index;
                                AddSpellSubmenu($"Level {index}", source.Spells.Where(ca => ca.SpellLevel == index1));
                            }
                        }

                        if (source is { Kind: SpellcastingKind.Spontaneous, Spells.Count: > 0 })
                        {
                            for (int index = 1; index <= 10; ++index)
                            {
                                int index1 = index;
                                if (source.SpontaneousSpellSlots[index] > 0)
                                {
                                    AddSpellSubmenu($"Level {index.ToString()} {string.Join("", Enumerable.Repeat<string>("{icon:SpontaneousSpellSlot}", source.SpontaneousSpellSlots[index]))}", source.Spells.Where(ca => ca.SpellLevel == index1));
                                }
                            }
                        }
                        return castASpell;

                        void AddSpellSubmenu(string miniSectionCaption, IEnumerable<CombatAction> spells)
                        {
                            PossibilitySection possibilitySection1 = new(miniSectionCaption);
                            foreach (CombatAction spell in spells)
                            {
                                CombatAction? combatAction1 = spellTransformation(spell);
                                if (combatAction1 == null) continue;
                                string name = combatAction1.Name;
                                DefaultInterpolatedStringHandler interpolatedStringHandler = new(3, 2);
                                interpolatedStringHandler.AppendFormatted(caption);
                                interpolatedStringHandler.AppendLiteral(" (");
                                interpolatedStringHandler.AppendFormatted(combatAction1);
                                interpolatedStringHandler.AppendLiteral(")");
                                string stringAndClear = interpolatedStringHandler.ToStringAndClear();
                                combatAction1.Name = stringAndClear;
                                if (spell.HasTrait(Trait.Psi))
                                {
                                    var spellId = (int)spell.SpellId;
                                    Creature owner = spell.Owner;
                                    int spellLevel = spell.SpellLevel;
                                    SpellInformation spellInformation = new();
                                    PsychicAmpInformation psychicAmpInformation = new()
                                    {
                                        Amped = true
                                    };
                                    spellInformation.PsychicAmpInformation = psychicAmpInformation;
                                    spellInformation.ClassOfOrigin = Trait.Psychic;
                                    CombatAction combatActionSpell = AllSpells
                                        .CreateModernSpell((SpellId)spellId, owner, spellLevel, true,
                                            spellInformation)
                                        .CombatActionSpell;
                                    combatActionSpell.SpellcastingSource = spell.SpellcastingSource;
                                    combatActionSpell.Name = "Amped " + combatActionSpell.Name;
                                    CombatAction? combatAction3 = spellTransformation(combatActionSpell);
                                    if (combatAction3 != null)
                                    {
                                        combatAction3.Name = caption + " (" + combatActionSpell.Name + ")";
                                        List<Possibility> possibilities1 = possibilitySection1.Possibilities;
                                        SubmenuPossibility submenuPossibility =
                                            new(spell.Illustration, spell.Name,
                                                PossibilitySize.Half)
                                            {
                                                SpellIfAny = combatAction1
                                            };
                                        List<PossibilitySection> subsections = submenuPossibility.Subsections;
                                        PossibilitySection possibilitySection2 = new(spell.Name);
                                        List<Possibility> possibilities2 = possibilitySection2.Possibilities;
                                        ActionPossibility actionPossibility1 =
                                            new(combatAction1)
                                            {
                                                Caption = name
                                            };
                                        possibilities2.Add(actionPossibility1);
                                        List<Possibility> possibilities3 = possibilitySection2.Possibilities;
                                        ActionPossibility actionPossibility2 =
                                            new(combatAction3)
                                            {
                                                Caption = combatActionSpell.Name
                                            };
                                        possibilities3.Add(actionPossibility2);
                                        subsections.Add(possibilitySection2);
                                        possibilities1.Add(submenuPossibility);
                                    }
                                    else
                                    {
                                        List<Possibility> list = possibilitySection1.Possibilities;
                                        ActionPossibility actionPossibility =
                                            new(combatAction1, PossibilitySize.Half)
                                            {
                                                Caption = name
                                            };
                                        list.Add(actionPossibility);
                                    }
                                }
                                else
                                {
                                    List<Possibility> list = possibilitySection1.Possibilities;
                                    ActionPossibility actionPossibility = new(combatAction1, PossibilitySize.Half)
                                        {
                                            Caption = name
                                        };
                                    list.Add(actionPossibility);
                                }
                            }

                            if (possibilitySection1.Possibilities.Count <= 0)
                                return;
                            castASpell.Subsections.Add(possibilitySection1);
                        }
                    }
                    void DischargeSpellstrike(Creature a)
                    {
                        a.AddQEffect(new QEffect
                        {
                            Id = QEffectId.SpellstrikeDischarged,
                            AfterYouTakeAction =  async (qfDischarge, action) =>
                            {
                                if (!action.HasTrait(Trait.Focus))
                                    return;
                                qfDischarge.ExpiresAt = ExpirationCondition.Immediately;
                            },
                            ProvideMainAction = qfDischarge => (ActionPossibility) new CombatAction(qfDischarge.Owner, IllustrationName.Good, "Recharge Spellstrike",
                            [
                                Trait.Concentrate,
                                Trait.Basic
                            ], "Recharge your Spellstrike so that you can use it again." + (qfDischarge.Owner.HasEffect(QEffectId.MagussConcentration) ? " {Blue}You gain a +1 circumstance bonus to your next attack until the end of your next turn.{/Blue}" : ""), (Target) Target.Self()).WithActionCost(1).WithSoundEffect(SfxName.AuraExpansion).WithEffectOnSelf((Func<Creature, Task>) (async self2 => Magus.RechargeSpellstrike(self2)))
                        });
                        if (!a.HasEffect(QEffectId.EndlessSpellstrike))
                            return;
                        Magus.RechargeSpellstrike(a);
                    }
                };
            });
    }

    internal static void ModifyCascade(CombatAction cascade, CombatAction? triggeringSpell)
    {
        cascade.EffectOnChosenTargets = null;
        cascade.EffectOnOneTarget = null;
        Creature magus = cascade.Owner;
        List<DamageKind> damageKinds = triggeringSpell != null
            ? DetermineDamageKindForArcaneCascade(triggeringSpell)
            : [DamageKind.Force];
        int bonus = magus.Level >= 15 ? 3 : magus.Level >= 7 ? 2 : 1;
        var str1 = $"{S.HeightenedVariable(bonus, 1)} extra {{Blue}}{S.ConstructOrList(damageKinds.Select(dk => dk.HumanizeTitleCase2().ToLower()))}{{/Blue}} damage. You also gain resistance {S.HeightenedVariable(bonus, 1)} to damage from spells";
        var str2 = "";
        var additionalQEffectText = "";
        var thp = 0;
        if (magus.HasEffect(QEffectId.InexorableIron))
        {
            thp = magus.MaximumSpellRank;
            str2 =
                $"\n\n{{Blue}}You also gain {thp.ToString()} temporary HP now and at the start of each of your turns while in this stance.{{/Blue}}";
            additionalQEffectText = $"You gain {thp} temporary HP at the start of each turn.";
        }

        if (magus.HasEffect(QEffectId.LaughingShadow))
        {
            str2 =
                $"\n\n{{Blue}}While in this stance, you also gain a +5-foot status bonus to your Speed, or a +10-foot bonus if you're unarmored. If you have a free hand and are attacking an off-guard creature, you also deal {S.HeightenedVariable(bonus + 2, 3)} extra damage instead of {S.HeightenedVariable(bonus, 1)} extra damage.{{/Blue}}";
            additionalQEffectText =
                $"You gain a +5-foot status bonus to your Speed, or a +10-foot bonus if you're unarmored. If you have a free hand and are attacking an off-guard creature, you also deal {S.HeightenedVariable(bonus + 2, 3)} extra damage instead of {S.HeightenedVariable(bonus, 1)} extra damage.";
        }

        if (magus.HasEffect(QEffectId.StarlitSpan))
        {
            str2 =
                "\n\n{Blue} While in this stance, you ignore any lesser cover your allies would grant to your target.{/Blue}";
            additionalQEffectText =
                "You ignore any lesser cover your allies would grant to your target.";
        }
        
        if (magus.HasEffect(QEffectId.SparklingTarge))
        {
            str2 =
                "\n\n{Blue}While in this stance with your shield raised, your circumstance bonus to AC from your shield also applies to your saves against spells and magical effects. In addition, damage you take as a result of a spell or magical effect while you're in Arcane Cascade can trigger your Shield Block reaction, even if the damage isn't physical. When blocking damage in this way, increase your shield's Hardness by an amount equal to the extra damage from Arcane Cascade.{/Blue}";
            additionalQEffectText =
                $"{additionalQEffectText}Your circumstance bonus to AC from any raise shield actions you take also applies to spells and magical effects, and you can use shield block {{icon:Reaction}} against damage from them. When you do so, reduce the damage by {S.HeightenedVariable(bonus, 1)}.";
        }

        if (magus.HasEffect(QEffectId.CascadingConflux))
            str2 += "\n\n{Blue}You also immediately recharge your spellstrike when you enter this stance.{/Blue}";
        
        cascade.Description = $"{{i}}You cycle arcane power through your body and weapon using specialized forms, breathing, or footwork.{{/i}}\n\nEnter the Arcane Cascade stance. While you're in this stance, your melee Strikes gain the arcane trait (making them magical) and deal {str1}.{str2}";
        cascade.WithEffectOnSelf(async (_, self) =>
        {
            DamageKind damageKind = damageKinds[0];
            if (damageKinds.Count > 1)
            {
                List<string> names = [.. damageKinds.Select(k => k.HumanizeTitleCase2())];
                ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(IllustrationName.ArcaneCascade, "Choose a damage type for arcane cascade's bonus damage.", [.. names]);
                damageKind = damageKinds[choice.Index];
            }
            QEffect effect = KineticistCommonEffects.EnterStance(self, IllustrationName.ArcaneCascade, "Arcane Cascade", $"Your melee Strikes gain the arcane trait and deal {S.HeightenedVariable(bonus, 1)} extra {damageKind.HumanizeLowerCase2()} damage. You also gain resistance {S.HeightenedVariable(bonus, 1)} to damage from spells.{(additionalQEffectText != "" ? " " + additionalQEffectText : "")}", QEffectId.ArcaneCascade);
            effect.Tag = new List<DamageKind> {damageKind};
            effect.AddExtraStrikeDamage = (strike, defender) =>
            {
                if (!strike.HasTrait(Trait.Melee))
                    return null;
                var diceFormula = bonus.ToString();
                if (magus.HasEffect(QEffectId.LaughingShadow) && strike.Owner.HasFreeHand && defender.IsFlatFootedTo(strike.Owner, strike))
                    diceFormula = (bonus * 2 + 1).ToString();
                return (DiceFormula.FromText(diceFormula, "Arcane Cascade"), damageKind);
            };
            effect.AdjustStrikeAction = (_, action) =>
            {
                if (action.HasTrait(Trait.Melee))
                    action.WithExtraTrait(Trait.Arcane);
            };
            effect.StateCheck = _ =>
            {
                magus.WeaknessAndResistance.AddSpecialResistance("Damage from spells", (action, _) => action != null && action.HasTrait(Trait.Spell), bonus, null);
            };
            if (magus.HasEffect(QEffectId.LaughingShadow))
                effect.BonusToAllSpeeds = qfArcaneCascade => qfArcaneCascade.Owner.Armor.WearsArmor ? new Bonus(1, BonusType.Status, "Laughing Shadow") : new Bonus(2, BonusType.Status, "Laughing Shadow");
            if (self.HasEffect(QEffectId.InexorableIron))
            {
                self.GainTemporaryHP(thp);
                effect.StartOfYourPrimaryTurn = async (_, self2) => self2.GainTemporaryHP(thp);
            }
            HashSet<Creature> alreadyIgnoreCover = [];
            if (magus.HasEffect(QEffectId.StarlitSpan))
            {
                effect.StartOfYourEveryTurn = async (_, _) =>
                {
                    foreach (Creature friend in magus.Battle.AllCreatures.Where(cr => cr.FriendOfAndNotSelf(magus)))
                    {
                        if (!friend.DoesNotProvideSoftCoverToEnemies)
                            friend.DoesNotProvideSoftCoverToEnemies = true;
                        else
                        {
                            alreadyIgnoreCover.Add(friend);
                        }
                    }
                };
                effect.EndOfYourTurn = async (_, _) =>
                {
                    foreach (Creature friend in magus.Battle.AllCreatures.Where(cr => cr.FriendOfAndNotSelf(magus) && !alreadyIgnoreCover.Contains(cr)))
                    {
                        friend.DoesNotProvideSoftCoverToEnemies = false;
                    }
                };
            }
            if (!self.HasEffect(QEffectId.CascadingConflux))
                return;
            Magus.RechargeSpellstrike(self);
        });
        return;

        static List<DamageKind> DetermineDamageKindForArcaneCascade(CombatAction spell)
        {
            List<DamageKind> list = [.. DetermineDamageKindFromSpell(spell)];
            return list;
        }
    }

    public static HashSet<DamageKind> DetermineDamageKindFromSpell(CombatAction spell, bool arcaneCascade = true)
    {
        string s = spell.Description;
        HashSet<DamageKind> set = [];
        if (arcaneCascade)
            set.Add(DamageKind.Force);
        if (!SpellDealsDamage(spell))
            return set;
        foreach (DamageKind kind in DamageKind.GetValues())
        {
            if (s.ContainsIgnoreCase(kind + " damage"))
                set.Add(kind);
        }
        return set;
    }
    
    internal static bool SpellDealsDamage(CombatAction action)
    {
        if ((!action.Description.ContainsIgnoreCase("deal") &&
             !action.Description.ContainsIgnoreCase("attack") &&
             !action.Description.ContainsIgnoreCase("take") &&
             !action.Description.ContainsIgnoreCase("damage")) ||
            action.Description.ContainsIgnoreCase("battleform"))
            return false;
        return action.Description.Contains("d4") || action.Description.Contains("d6") ||
               action.Description.Contains("d8") || action.Description.Contains("d10") ||
               action.Description.Contains("d12") ||
               action.Description.Contains("Deal 40");
    }
    
    internal static CombatAction? CreateSpellcastingStrike(
      CombatAction spell,
      SpellcastingStrikeKind spellcastingStrikeKind,
      Item weapon,
      string name,
      string? postActionText,
      Action postSpellcast,
      Creature spellStriker,
      string? prologue,
      string? aftertext,
      bool spellSwipe,
      bool overwhelmingSpellstrike,
      bool sprintingSpellstrike = false)
    {
      if (spell.Variants != null)
        return null;
      if (spell.VariantsCreator != null)
        return null;
      if (spell.SubspellVariants != null)
        return null;
      if (spell.ActionCost != 1 && spell.ActionCost != 2 && spell.ActionCost != -3 && spell.ActionCost != -1 && spell.ActionCost != -4 && spell.ActionCost != -5)
        return null;
      if (spell.HasTrait(Trait.BannedFromSpellstrike))
        return null;
      bool hasExpansiveSpellstrike = spellcastingStrikeKind == SpellcastingStrikeKind.Spellstrike && spellStriker.HasEffect(QEffectId.ExpansiveSpellstrike);
      Target target = spell.Target is DependsOnActionsSpentTarget target1 ? target1.IfTwoActions : spell.Target;
      CreatureTarget? finalSpellTarget = target switch
      {
          CreatureTarget creatureTarget => creatureTarget,
          MultipleCreatureTargetsTarget creatureTargetsTarget => creatureTargetsTarget.Targets[0],
          _ => null
      };
      bool isSavingThrowSpellStrike;
      bool usesExpansiveSpellStrike;
      if (spell.HasTrait(Trait.Attack))
      {
        isSavingThrowSpellStrike = false;
        usesExpansiveSpellStrike = false;
      }
      else if (spell.SavingThrow == null)
      {
          return null;
      }
      else if (target.IsAreaTarget)
      {
          isSavingThrowSpellStrike = true;
          usesExpansiveSpellStrike = hasExpansiveSpellstrike;
          if (target is EmanationTarget)
              usesExpansiveSpellStrike = false;
      }
      else
      {
          if ((finalSpellTarget != null
                  ? finalSpellTarget.CreatureTargetingRequirements.All(ctr =>
                      ctr.GetType() != typeof(FriendOrSelfCreatureTargetingRequirement) &&
                      ctr.GetType() != typeof(FriendCreatureTargetingRequirement))
                      ? 1
                      : 0
                  : 0) == 0) return null;
          isSavingThrowSpellStrike = true;
          usesExpansiveSpellStrike = false;
      }
      if (spellSwipe)
      {
        List<Trait> list =
        [
            Trait.Magus, Trait.Basic, Trait.AlwaysHits, Trait.IsHostile, Trait.Attack,
            Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Trait.Spellstrike,
            .. spell.Traits.Except(new List<Trait>([
                Trait.Ranged,
                Trait.Prepared,
                Trait.Spontaneous,
                Trait.Focus,
                Trait.Spell
            ]))
        ];
        return new CombatAction(spellStriker, new SideBySideIllustration(IllustrationName.Swipe, spell.Illustration), spell.Name, [.. list], 
                "{b}Requirements{/b} Your Spellstrike is charged.\r\n\r\nMake a Spellstrike against two enemy creatures using your current multiple attack penalty, each of whom must be within your melee reach, and they must also be adjacent to each other. If you're using a weapon with the sweep trait, its +1 circumstance bonus applies to both your Swipe attacks.\r\n\r\nThe spell you cast affects only the first creature you hit, not both, unless it allows for multiple targets, in which case it affects all targets you hit.\r\n\r\nIf you have Expansive Spellstrike and use a non-attack targeted spell, it affects the first creature who you don't critically miss (unless it allows for multiple targets, in which case it affects all targets you don't critically miss).\r\n\r\nIf you have Expansive Spellstrike and use an area spell, you designate the area as normal as long as you didn't critically miss both targets, and the area must include at least the first target you didn't critically miss.", 
                Target.MultipleCreatureTargets(AddAdditionalRestrictions(Target.Reach(weapon)), 
                        AddAdditionalRestrictions(Target.Reach(weapon)))
                .WithAdditionalRestrictionsOnEachTarget((_, earlierCreatures, newCreature) => earlierCreatures.All(acr => acr != newCreature && acr.IsAdjacentTo(newCreature))))
            .WithTargetingTooltip((_, _, n) => n == 0 ? "Strike this creature as the first target (1/2). If you hit both targets, the spell will prioritize affecting this target." : "Strike this creature as the second target (2/2).")
            .WithActionCost(3)
            .WithEffectOnChosenTargets(async (fighter, targets) =>
            {
                int map = fighter.Actions.AttackedThisManyTimesThisTurn;
                QEffect qPenalty = new("", "[this condition has no description]", ExpirationCondition.Never, fighter, (Illustration) IllustrationName.None)
                {
                    BonusToAttackRolls = (_, _, _) => new Bonus(1, BonusType.Circumstance, "Swipe sweep")
                };
                if (weapon.HasTrait(Trait.Sweep))
                    fighter.AddQEffect(qPenalty);
                CheckResult result1 = await fighter.MakeStrike(targets.ChosenCreatures[0], weapon, map);
                CheckResult checkResult = await fighter.MakeStrike(targets.ChosenCreatures[1], weapon, map);
                fighter.RemoveAllQEffects(qfr => qfr == qPenalty);
                if ((result1 != CheckResult.CriticalFailure || checkResult != CheckResult.CriticalFailure) && (isSavingThrowSpellStrike || result1 >= CheckResult.Success || checkResult >= CheckResult.Success))
                {
                    if ((result1 >= CheckResult.Success || isSavingThrowSpellStrike && result1 >= CheckResult.Failure) && (targets.ChosenCreatures[0].Alive || !targets.ChosenCreatures[1].Alive))
                        await ApplySpellToTargets(fighter, targets.ChosenCreatures[0], result1, checkResult >= CheckResult.Success || isSavingThrowSpellStrike && checkResult >= CheckResult.Failure ? targets.ChosenCreatures[1] : null, checkResult);
                    else
                        await ApplySpellToTargets(fighter, targets.ChosenCreatures[1], checkResult, null, CheckResult.Failure);
                }
                postSpellcast();
            });
      }

      switch (sprintingSpellstrike)
      {
          case true when target is LineAreaTarget lineAreaTarget:
          {
              List<Trait> traits =
              [
                  Trait.Magus, Trait.Basic, Trait.IsHostile, Trait.Attack,
                  Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Trait.Spellstrike,
                  .. spell.Traits.Except(new List<Trait>([
                      Trait.Ranged,
                      Trait.Prepared,
                      Trait.Spontaneous,
                      Trait.Focus,
                      Trait.Spell
                  ]))
              ];
              return new CombatAction(spellStriker,
                      new SideBySideIllustration(IllustrationName.WarpStep, spell.Illustration), spell.Name,
                      [.. traits],
                      "{b}Requirements{/b} Your Spellstrike is charged." +
                      "\n\nMake a Spellstrike, casting a spell that has a line area. Before making your Strike, you Stride in a straight line to a distance up to the line's length; you can move through enemies during this movement, and your movement doesn't trigger reactions." +
                      "\n\n{b}Special{/b} If you have the Expansive Spellstrike feat, the spell's area uses the line of your movement instead of the normal method of determining the line.",
                      (lineAreaTarget.Duplicate() as LineAreaTarget)!.WithLesserDistanceIsOkay()
                          .WithAdditionalRequirementOnCaster(a=>
                              a.HasEffect(QEffectId.SpellstrikeDischarged)
                                  ? Usability.NotUsable(
                                      "You must first recharge your Spellstrike by spending an action or casting a conflux spell.")
                                  : Usability.Usable)
                          .With(t =>
                          {
                              if (t is not LineAreaTarget t2)
                                  return;
                              t2.IsBurningJet = true;
                              t2.BlockedByCreatures = false;
                              if (lineAreaTarget.IncludeOnlyIf != null)
                                  t2.WithIncludeOnlyIf(lineAreaTarget.IncludeOnlyIf);
                              if (lineAreaTarget.AdditionalRequirementOnAreaCaster != null)
                                  t2.WithAdditionalRequirementOnCaster(lineAreaTarget.AdditionalRequirementOnAreaCaster);
                          })
                      )
                  .WithActionCost(2)
                  .WithItem(weapon)
                  .WithSoundEffect(spell.SoundEffectName)
                  .WithProjectileCone(spell.Illustration, spell.ProjectileCount, spell.ProjectileKind)
                  .WithEffectOnChosenTargets(async (action, caster, targets) =>
                  {
                      if (LineAreaTarget.DetermineFinalTile(caster.Space.TopLeftTile, [.. targets.ChosenTiles]) is not
                              { } finalTile)
                      {
                          caster.Battle.Log("Final tile is not valid!");
                          action.RevertRequested = true;
                          return;
                      }
                      if (!caster.Battle.AllCreatures.Any(cr =>
                              cr.EnemyOf(caster) && cr.DistanceToWith10FeetException(finalTile) <= weapon.DetermineReach(caster)))
                      {
                          caster.Battle.Log("Sprinting Spellstrike reverted! You must choose a final tile where you could make a Strike.");
                          action.RevertRequested = true;
                          return;
                      }
                      QEffect ghost = new() { Id = QEffectId.Incorporeal };
                      caster.AddQEffect(ghost);
                      await caster.MoveTo(finalTile, action, new MovementStyle
                      {
                          Shifting = true,
                          IgnoresUnevenTerrain = true,
                          Insubstantial = true,
                          ShortestPath = true,
                          MaximumSquares = 100
                      });
                      ghost.ExpiresAt = ExpirationCondition.Immediately;
                      CombatAction strike = spellStriker.CreateStrike(weapon).WithActionCost(0);
                      strike.Name = spell.Name;
                      strike.ContextMenuName = $"{name} ({spell.Name})";
                      strike.ShortName = name;
                      strike.SpellcastingSource = spell.SpellcastingSource;
                      strike.Traits.Add(Trait.Spellstrike);
                      strike.StrikeModifiers.OnEachTarget = async (a, d, result) =>
                      {
                          await ApplySpellToTargets(a, d, result, null, CheckResult.CriticalFailure,
                              [.. targets.ChosenCreatures]);
                          postSpellcast();
                          ++a.Actions.AttackedThisManyTimesThisTurn;
                      };
                      StrikeModifiers strikeModifiers = strike.StrikeModifiers;
                      var str1 = $"{prologue}{(prologue != null ? " " : "")}Using {name} increases your multiple attack penalty twice.";
                      string? str2 = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : null;
                      string additionalSuccessText = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : $"The success effect of {spell.Name} is inflicted upon the target.";
                      string additionalCriticalSuccessText = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : "Critical spell effect.";
                      string? additionalAftertext = postActionText != null ? postActionText + (aftertext != null ? " " + aftertext : "") :  null;
                      string strikeDescription3 = StrikeRules.CreateBasicStrikeDescription3(strikeModifiers, additionalSuccessText: additionalSuccessText, additionalCriticalSuccessText: additionalCriticalSuccessText, additionalFailureText: str2, additionalAftertext: additionalAftertext, prologueText: str1);
                      strike.Description = strikeDescription3;
                      await caster.Battle.GameLoop.FullCast(strike);
                  });
          }
          case true when target is not LineAreaTarget:
              return null;
      }
      CombatAction strike = spellStriker.CreateStrike(weapon);
      strike.Name = spell.Name;
      strike.ContextMenuName = $"{name} ({spell.Name})";
      if (prologue == "First, make a free Feint against the target.")
      {
          strike.WithActionId(RActionIds.DistractingSpellstrike);
          strike.ContextMenuName = $"Distracting Spellstrike ({spell.Name})";
      }
      strike.ShortName = "Spellstrike";
      strike.Illustration = new SideBySideIllustration(strike.Illustration, spell.Illustration);
      strike.Traits.AddRange(spell.Traits.Except(new List<Trait>([
          Trait.Ranged,
          Trait.Melee,
          Trait.Prepared,
          Trait.Spontaneous,
          Trait.Spell,
          Trait.Focus
      ])));
      strike.SpellcastingSource = spell.SpellcastingSource;
      strike.Traits.Add(Trait.Spellstrike);
      strike.Traits.Add(Trait.Basic);
      strike.ActionCost = spellcastingStrikeKind == SpellcastingStrikeKind.EldritchShot ? 3 : 2;
      AddAdditionalRestrictions((CreatureTarget) strike.Target);
      strike.StrikeModifiers.OnEachTarget = (Func<Creature, Creature, CheckResult, Task>) (async (a, d, result) =>
      {
        await ApplySpellToTargets(a, d, result, null, CheckResult.CriticalFailure);
        postSpellcast();
        ++a.Actions.AttackedThisManyTimesThisTurn;
      });
      StrikeModifiers strikeModifiers = strike.StrikeModifiers;
      if (aftertext == "Enemies adjacent to the target take splash damage equal to 2 plus the extra damage from Arcane Cascade. The damage type is the same as Arcane Cascade.")
      {
          if (!SpellDealsDamage(spell))
              return null;
          int baseD = spellStriker.Level >= 19 ? 5 : spellStriker.Level >= 14 ? 4 : spellStriker.Level >= 9 ? 3 : 2;
          aftertext = $"Enemies adjacent to the target take {baseD} {S.ConstructOrList(DetermineDamageKindFromSpell(spell, false).Select(dk => dk.ToString()))} damage. If you're in Arcane Cascade stance, this damage gains a status bonus equal to the extra damage from your Arcane Cascade.";
          strike.WithActionId(RActionIds.DevastatingSpellstrike);
          var tuple = new Tuple<CombatAction, Func<Creature, Creature, CheckResult, Task>>(spell,
              strikeModifiers.OnEachTarget);
          strike.WithTag(tuple);
          strike.ContextMenuName = $"Devastating Spellstrike ({spell.Name})";
      }
      var str1 = $"{prologue}{(prologue != null ? " " : "")}Using {name} increases your multiple attack penalty twice.";
      string? str2 = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : null;
      string additionalSuccessText = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : $"The success effect of {spell.Name} is inflicted upon the target.";
      string additionalCriticalSuccessText = isSavingThrowSpellStrike ? $"You cast {spell.Name} at the target." : "Critical spell effect.";
      string? additionalAftertext = postActionText != null ? postActionText + (aftertext != null ? " " + aftertext : "") :  null;
      string strikeDescription3 = StrikeRules.CreateBasicStrikeDescription3(strikeModifiers, additionalSuccessText: additionalSuccessText, additionalCriticalSuccessText: additionalCriticalSuccessText, additionalFailureText: str2, additionalAftertext: additionalAftertext, prologueText: str1);
      strike.Description = strikeDescription3;
      return strike;

      CreatureTarget AddAdditionalRestrictions(CreatureTarget strikeTarget)
      {
          if (spellcastingStrikeKind == SpellcastingStrikeKind.Spellstrike)
          {
              int? rangeIncrement = weapon.WeaponProperties?.RangeIncrement;
              if (rangeIncrement.HasValue)
              {
                  int valueOrDefault = rangeIncrement.GetValueOrDefault();
                  if (valueOrDefault > 0)
                      strikeTarget.WithAdditionalConditionOnTargetCreature(
                          new MaximumRangeCreatureTargetingRequirement(valueOrDefault));
              }

              strikeTarget.WithAdditionalConditionOnTargetCreature((a, _) =>
                  a.HasEffect(QEffectId.SpellstrikeDischarged)
                      ? Usability.NotUsable(
                          "You must first recharge your Spellstrike by spending an action or casting a conflux spell.")
                      : Usability.Usable);
          }

          PsychicAmpInformation? psychicAmpInformation = spell.PsychicAmpInformation;
          if ((psychicAmpInformation != null ? psychicAmpInformation.Amped ? 1 : 0 : 0) != 0)
              strikeTarget.WithAdditionalConditionOnTargetCreature((a, _) =>
                  a.Spellcasting == null || a.Spellcasting.FocusPoints == 0
                      ? Usability.CommonReasons.NoFocusPoints
                      : Usability.Usable);
          if (finalSpellTarget != null)
          {
              foreach (CreatureTargetingRequirement targetingRequirement in finalSpellTarget
                           .CreatureTargetingRequirements)
              {
                  switch (targetingRequirement)
                  {
                      case AdjacencyCreatureTargetingRequirement _:
                      case MaximumRangeCreatureTargetingRequirement _:
                      case MeleeReachCreatureTargetingRequirement _:
                      case NaturalReachCreatureTargetingRequirement _:
                          continue;
                      default:
                          strikeTarget.WithAdditionalConditionOnTargetCreature(targetingRequirement);
                          continue;
                  }
              }
          }
          return strikeTarget;
      }
      async Task ApplySpellToTargets(
              Creature a,
              Creature d,
              CheckResult result,
              Creature? target2,
              CheckResult checkResult2,
              List<Creature>? creatures = null)
          {
              if (spellcastingStrikeKind == SpellcastingStrikeKind.Spellstrike)
                  Steam.CollectAchievement("MAGUS");
              a.Spellcasting?.UseUpSpellcastingResources(spell);
              CombatAction effectuatedSpell = Spell.DuplicateSpell(spell).CombatActionSpell;
              effectuatedSpell.ChosenTargets = ChosenTargets.CreateSingleTarget(d);
              effectuatedSpell.SpentActions = 2;
              effectuatedSpell.Traits.Add(Trait.FromSpellcastingStrike);
              if (overwhelmingSpellstrike)
                  effectuatedSpell.Traits.Add(Trait.Overwhelming);
              if ((isSavingThrowSpellStrike && result > CheckResult.CriticalFailure) || result >= CheckResult.Success)
              {
                  var flag = false;
                  QEffect? qeffect = a.FindQEffect(QEffectId.Stupefied);
                  if (qeffect != null)
                  {
                      (CheckResult, string) tuple = Checks.RollFlatCheck(5 + qeffect.Value);
                      flag = tuple.Item1 < CheckResult.Success;
                      a.Battle.Log(
                          flag
                              ? $"{effectuatedSpell.Name} from {name} {{Red}}fizzled{{/}} due to stupefied: {tuple.Item2}"
                              : $"{name} stupefied flat check {{Green}}passed{{/}}: {tuple.Item2}");
                  }
                  if (!flag)
                  {
                      if (usesExpansiveSpellStrike)
                      {
                          CombatAction areaSpell =
                              effectuatedSpell.WithActionCost(0).WithExtraTrait(Trait.DoNotExpendResources);
                          Target targetA = effectuatedSpell.Target is DependsOnActionsSpentTarget target4
                              ? target4.IfTwoActions
                              : effectuatedSpell.Target;
                          areaSpell.Traits.Add(Trait.AvoidAnimatingBurstMissile);
                          areaSpell.Traits.Add(Trait.AlwaysDrawBurstOriginCorners);
                          areaSpell.Target = targetA;
                          switch (areaSpell.Target)
                          {
                              case BurstAreaTarget target3:
                                  target3.AlternateCreatureOfOrigin = d;
                                  target3.Range = 1000;
                                  target3.MustBeWithinShortDistanceOfVector2 = d.Space.CenterVector;
                                  target3.MustBeWithinShortDistanceOf_Distance = d.Space.SizeInSquares;
                                  _ = await a.Battle.GameLoop.FullCast(areaSpell) ? 1 : 0;
                                  break;
                              case LineAreaTarget when creatures is not null:
                              {
                                  if (!creatures.Contains(d))
                                      creatures.Add(d);
                                  foreach (Creature t in creatures)
                                  {
                                      CheckResult checkResult;
                                      if (isSavingThrowSpellStrike && effectuatedSpell.SavingThrow != null)
                                          checkResult = await CommonSpellEffects.RollSavingThrowAsync(t, effectuatedSpell,
                                              effectuatedSpell.SavingThrow);
                                      else
                                          checkResult = t == d ? result : checkResult2;
                                      if (effectuatedSpell.EffectOnOneTarget != null)
                                          await effectuatedSpell.EffectOnOneTarget(effectuatedSpell, a, t, checkResult);
                                      if (effectuatedSpell.EffectOnChosenTargets != null)
                                          await effectuatedSpell.EffectOnChosenTargets(effectuatedSpell, a,
                                              ChosenTargets.CreateSingleTarget(t));
                                  }
                                  break;
                              }
                              case CloseAreaTarget closeAreaTarget:
                              {
                                  await a.FictitiousSingleTileMoveBack();
                                  closeAreaTarget.VerifyLegality =
                                      (Func<HashSet<Tile>, bool>)(tiles => tiles.ContainsOneOf(d.Space.Tiles));
                                  if (!a.IsAdjacentTo(d))
                                  {
                                      Tile? chooseAtile = await a.Battle.AskToChooseATile(a,
                                          d.Space.GetNeighbours(), areaSpell.Illustration,
                                          $"Where should {areaSpell.Name} emanate from?", "Emanate from this square.",
                                          true,
                                          false, null, "Don't cast the spell");
                                      if (chooseAtile != null)
                                      {
                                          closeAreaTarget.AlternateOriginOfAnimation =
                                              chooseAtile.ToCenterVector();
                                          Tile originalTile = a.Space.TopLeftTile;
                                          a.Space.TemporaryTranslateTo(chooseAtile);
                                          areaSpell.Traits.Add(Trait.DoesNotProvoke);
                                          _ = await a.Battle.GameLoop.FullCast(areaSpell) ? 1 : 0;
                                          a.Space.TemporaryTranslateTo(originalTile);
                                      }
                                  }
                                  else
                                  {
                                      _ = await a.Battle.GameLoop.FullCast(areaSpell) ? 1 : 0;
                                  }

                                  break;
                              }
                          }
                      }
                      else
                      {
                          if (effectuatedSpell.SoundEffectName.HasValue && !sprintingSpellstrike)
                              Sfxs.Play(effectuatedSpell.SoundEffectName);
                          Creature[] creatureArray1;
                          if (target2 != null && effectuatedSpell.Target is MultipleCreatureTargetsTarget
                              {
                                  Targets.Length: >= 2
                              })
                              creatureArray1 = [d, target2];
                          else
                              creatureArray1 = [d];
                          Creature[] creatureArray = creatureArray1;
                          foreach (Creature t in creatureArray)
                          {
                              CheckResult checkResult;
                              if (isSavingThrowSpellStrike && effectuatedSpell.SavingThrow != null)
                                  checkResult = await CommonSpellEffects.RollSavingThrowAsync(t, effectuatedSpell,
                                      effectuatedSpell.SavingThrow);
                              else
                                  checkResult = t == d ? result : checkResult2;
                              if (effectuatedSpell.EffectOnOneTarget != null)
                                  await effectuatedSpell.EffectOnOneTarget(effectuatedSpell, a, t, checkResult);
                              if (effectuatedSpell.EffectOnChosenTargets != null)
                                  await effectuatedSpell.EffectOnChosenTargets(effectuatedSpell, a,
                                      ChosenTargets.CreateSingleTarget(t));
                          }
                      }
                  }
              }
          }
    }

    public static string SpellCrashDescription(bool featDescription = true, bool hasTarget = false, bool hasTrip = false, bool hasShove = false, int? spellLevel = null, List<DamageKind>? kinds = null)
    {
        string damage = spellLevel == null 
            ? "1d10 force damage per spell rank of the expended spell," 
            : $"{S.HeightenedVariable(spellLevel.Value, 1)}d10 {(kinds == null ? "force" : S.ConstructOrList(kinds.Select(k => k.ToString())))} damage,";
        string area = !hasTarget
            ? " The magic of the spell explodes out in your choice of a 20-foot emanation, 30-foot cone, or 60-foot line."
            : "";
        string toTrip = featDescription
            ? " If your weapon has the shove trait, you push any creature that fails its save 10 feet; if your weapon has the trip trait, any creature that fails its save falls prone."
            : hasShove && hasTrip
                ? " Any creature that fails its save is pushed 10 feet and falls prone."
                : hasShove
                    ? " Any creature that fails its save is pushed 10 feet."
                    : hasTrip
                        ? " Any creature that fails its save falls prone."
                        : "";
            
        return "{b}Requirements{/b} Your Spellstrike is charged, and you have at least one spell prepared in a spell slot." +
               $"\n\nExpend a spell prepared in one of your spell slots.{area} Each creature in the area takes {damage} (basic Reflex save against your Spell DC mitigates).{toTrip} Your Spellstrike is no longer charged." +
               $"{(kinds == null ?"\n\nIf the spell you expended could deal a type of damage other than force, you can change the explosion's damage to that type and replace Spell Crash's force trait with the corresponding trait." 
                   : "")}";
    }
}