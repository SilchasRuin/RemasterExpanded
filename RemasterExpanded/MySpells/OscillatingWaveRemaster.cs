using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.Remaster.Spellbook;
using PsychicExpanded;
using static PsychicExpanded.ConsciousMind;

namespace RemasterExpanded.MySpells;

public class OscillatingWaveRemaster
{
    public static void RemasterOscillatingWave()
    {
        ModManager.RegisterActionOnEachSpell(spell =>
        {
            if (spell.SpellId.ToStringOrTechnical() != "Frostbite")
                return;
            spell.Traits.Add(Trait.Level1PsychicCantrip);
            SpellInformation? spellInformation = spell.SpellInformation;
            if (spellInformation == null)
                return;
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (psychicAmpInformation != null)
                spell.PsychicAmpInformation =  psychicAmpInformation;
            bool amped = psychicAmpInformation is { Amped: true };
            if (spellInformation.PsychicAmpInformation == null) return;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            bool inCombat = spell.Owner != null && spell.Owner.Battle != TBattle.Pseudobattle;
            int level = spell.SpellLevel;
            spell.Description = string.Empty;
            spell.WithDescription("An orb of biting cold coalesces around your target, freezing its body.", "{b}Range{/b} {Blue}120 feet{/Blue}" +
                "\n\nThe target takes " +
                (amped ? $"{{Blue}}{S.HeightenedVariable(3 + 2 * (level - 1), 3)}{{/Blue}}" : S.HeightenedVariable(2 + (level-1), 2)) + $"d4 cold damage with a basic Fortitude save. On a critical failure, the target also gains weakness {S.HeightenedVariable(level, 1)} to bludgeoning until the start of your next turn." +
                (amped ? "\n\n{Blue}You gain temporary Hit Points equal to half the damage the target takes (after applying resistances and the like). You lose any remaining temporary Hit Points at the end of the encounter.{/Blue}" : "") +
                (!inCombat ? "\n\n{Blue}The range of your frostbite increases to 120 feet. Your frostbite also gains the following amp.{/Blue}" +
                             $"\n\n{{Blue}}{{b}}Amp{{/b}} You deal {S.HeightenedVariable(3 + 2 * (level - 1), 3)}d4 cold damage instead. You gain temporary Hit Points equal to half the damage the target takes (after applying resistances and the like). You lose any remaining temporary Hit Points at the end of the encounter.{{/Blue}}" : "")+
                                S.HeightenText(level, 1, inCombat, "{b}Heightened (+" + 1 + "){/b} The damage increases by 1d4 and the weakness on a critical failure increases by 1."));
            spell.Description += inCombat
                ? ""
                : "\n{Blue}{b}Amp Heightened (+1){/b} The initial damage increases by 2d4 instead of 1d4. The weakness on a critical failure increases by 1.{/Blue}";
            spell.Traits.Add(Trait.Psi);
            spell.Target = Target.Ranged(24);
            // spell.Target.OverriddenTargetLine = "{b}Range{/b} {Blue}120 feet{/Blue}";
            if (!amped) return;
            spell.EffectOnOneTarget = null;
            spell.WithEffectOnEachTarget(async (action, caster, target, result) =>
            {
                QEffect tempGain = new()
                {
                    AfterYouTakeAmountOfDamageOfKind = async (_, combatAction, amount, _) =>
                    {
                        if (combatAction?.SpellId == action.SpellId)
                        {
                            caster.GainTemporaryHP(amount / 2);
                        }
                    }
                };
                if (result != CheckResult.CriticalSuccess)
                {
                    target.AddQEffect(tempGain);
                }
                await CommonSpellEffects.DealBasicDamage(action, caster, target, result, DiceFormula.FromText($"{3 + (level-1)*2}d4", "Frostbite (amped)"), DamageKind.Cold);
                if (result == CheckResult.CriticalFailure)
                {
                    target.AddQEffect(QEffect.DamageWeakness(DamageKind.Bludgeoning, level).WithExpirationAtStartOfOwnerTurn());
                }
                tempGain.ExpiresAt = ExpirationCondition.Immediately;
            });

        });
        ModManager.RegisterActionOnEachSpell(spell =>
        {
            if (spell.SpellId.ToStringOrTechnical() != "Ignition")
                return;
            spell.Traits.Add(Trait.Level1PsychicCantrip);
            SpellInformation? spellInformation = spell.SpellInformation;
            if (spellInformation == null)
                return;
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (psychicAmpInformation != null)
                spell.PsychicAmpInformation =  psychicAmpInformation;
            bool amped = psychicAmpInformation is { Amped: true };
            if (spellInformation.PsychicAmpInformation == null) return;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            bool inCombat = spell.Owner != null && spell.Owner.Battle != TBattle.Pseudobattle;
            int level = spell.SpellLevel;
            spell.Description = string.Empty;
            spell.WithDescription("You snap your fingers and point at a target, which begins to smolder.", "{b}Range{/b} {Blue}60 feet{/Blue}" +
                "\n\n Make a spell attack roll against the target's AC, dealing " +
                (amped ? $"{{Blue}}{S.HeightenedVariable(level, 1)}d10{{/Blue}}" : $"{S.HeightenedVariable(1 + level, 2)}d4") + $" fire damage {(amped ? $"and {S.HeightenedVariable(level, 1)} fire splash damage (you are not harmed by this splash damage) " : "")}on a hit. If the target is within your melee reach (increased by 5 feet), you make a melee spell attack with the flame instead of a ranged spell attack, which increases all the spell's damage dice to " +
                (amped ? "{Blue}d12s{/Blue}." : "d6s.") +
                S.FourDegreesOfSuccess($"The target takes double damage and {S.HeightenedVariable(level, 1)}d4 persistent fire damage.", "The target takes full damage.", null, null) +
                (!inCombat ? "\n\n{Blue}The range of your ignition increases to 60 feet. Your ignition also gains the following amp.{/Blue}" +
                             $"\n\n{{Blue}}{{b}}Amp{{/b}} The initial damage changes to {S.HeightenedVariable(level, 1)}d10 fire damage plus {S.HeightenedVariable(level, 1)} fire splash damage. When using amped ignition as a melee attack, increase the damage dice of the initial damage from d10s to d12s. You are not harmed by splash damage from amped ignition.{{/Blue}}" : "")+
                S.HeightenText(level, 1, inCombat, "{b}Heightened (+" + 1 + "){/b} The damage increases by 1d4 and the persistent fire damage on a critical hit increases by 1d4."));
            spell.Description += inCombat
                ? ""
                : "\n{Blue}{b}Amp Heightened (+1){/b} Instead of using ignition's normal heightened entry, the initial damage increases by 1d10 (1d12 for melee) and the splash damage increases by 1. The persistent fire damage on a critical hit increases by 1d4.{/Blue}";
            spell.Traits.Add(Trait.Psi);
            spell.Target = Target.Ranged(12);
            spell.EffectOnOneTarget = null;
            spell.WithActionId(ModData.RActionIds.PsychicIgnition);
            spell.WithPrologueEffectOnChosenTargetsBeforeRolls(async (action, self, _) =>
            {
                self.AddQEffect(new QEffect
                {
                    Id = ModData.MQEffectIds.IncreasedReach,
                    AfterYouTakeAction = async (effect, combatAction) =>
                    {
                        if (combatAction != action)
                            return;
                        effect.ExpiresAt = ExpirationCondition.Immediately;
                    }
                });
                self.Battle.GameLoop.RecalculateFlankingFor(self);
            });
            spell.WithEffectOnEachTarget(async (action, caster, target, result) =>
            {
                string melee = amped ? "d12" : "d6";
                string ranged = amped ? "d10" : "d4";
                string dieSize = caster.DistanceTo(target) <= caster.Space.NaturalReach + 1 ? melee : ranged;
                string baseDieSize = caster.DistanceTo(target) <= caster.Space.NaturalReach + 1 ? "d6" : "d4";
                int baseValue = amped ? 0 : 1;
                await CommonSpellEffects.DealAttackRollDamage(action, caster, target, result, level + baseValue + dieSize, DamageKind.Fire);
                if (result == CheckResult.CriticalSuccess)
                {
                    target.AddQEffect(QEffect.PersistentDamage(action.SpellLevel + baseDieSize, DamageKind.Fire));
                }
                if (amped)
                {
                    foreach (Creature creature in caster.Battle.AllCreatures.Where(cr =>
                                 (cr.IsAdjacentTo(target) && cr != caster) || cr == target))
                    {
                        await CommonSpellEffects.DealDirectSplashDamage(action,
                            DiceFormula.FromText($"{level}", "Ignition (amped)"), creature, DamageKind.Fire);
                    }
                }
            });
        });
        LoadOrder.WhenFeatsBecomeLoaded += () =>
        {
            SpellId frostbite = RemasterSpells.GetSpellIdByName("Frostbite");
            SpellId ignition = RemasterSpells.GetSpellIdByName("Ignition");
            WaveSpells.AddRange([frostbite, ignition, SpellIds.HowlingBlizzard, SpellIds.IceStorm]);
            WaveSpells.RemoveAll(spell =>
                spell is SpellId.ProduceFlame or SpellId.RayOfFrost or SpellId.FireShield or SpellId.ConeOfCold);
            if (AllFeats.GetFeatByFeatNameOrStringOptional(null, "OscillatingWave") is { } wave)
            {
                wave.WithOnSheet(sheet =>
                {
                    SpellRepertoire repertoire = sheet.SpellRepertoires[Trait.Psychic];
                    Spell psiCantrip1 = AllSpells.CreateModernSpell(frostbite, null,
                        sheet.MaximumSpellLevel, false, new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        });
                    repertoire.SpellsKnown.Add(psiCantrip1);
                    repertoire.SpellsKnown.RemoveAll(spell => spell.SpellId == SpellId.RayOfFrost);
                    Spell psiCantrip2 = AllSpells.CreateModernSpell(ignition, null,
                        sheet.MaximumSpellLevel, false, new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        });
                    repertoire.SpellsKnown.Add(psiCantrip2);
                    repertoire.SpellsKnown.RemoveAll(spell => spell.SpellId == SpellId.ProduceFlame);
                    repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(SpellIds.HowlingBlizzard, Trait.Psychic, 5));
                    repertoire.SpellsKnown.RemoveAll(spell => spell.SpellId == SpellId.ConeOfCold);
                    repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(SpellIds.IceStorm, Trait.Psychic, 4));
                    repertoire.SpellsKnown.RemoveAll(spell => spell.SpellId == SpellId.FireShield);

                });
                string rulesText = wave.RulesText.Replace(
                    AllSpells.CreateModernSpellTemplate(SpellId.RayOfFrost, Trait.Psychic).ToSpellLink(),
                    CreatePsychicSpellTemplate(frostbite)
                        .ToSpellLink());
                rulesText = rulesText.Replace(AllSpells.CreateModernSpellTemplate(SpellId.ProduceFlame, Trait.Psychic).ToSpellLink(),
                    CreatePsychicSpellTemplate(ignition)
                        .ToSpellLink());
                rulesText = rulesText.Replace(AllSpells.CreateModernSpellTemplate(SpellId.ConeOfCold, Trait.Psychic).ToSpellLink(),
                    AllSpells.CreateModernSpellTemplate(SpellIds.HowlingBlizzard, Trait.Psychic)
                        .ToSpellLink());
                rulesText = rulesText.Replace(AllSpells.CreateModernSpellTemplate(SpellId.FireShield, Trait.Psychic).ToSpellLink(),
                    AllSpells.CreateModernSpellTemplate(SpellIds.IceStorm, Trait.Psychic)
                        .ToSpellLink());
                wave.RulesText = rulesText;
            }

            if (AllFeats.GetFeatByFeatNameOrStringOptional(null, "OscillatingWaveRoF") is { } wave2)
            {
                wave2.WithCustomName("Oscillating Wave - Frostbite");
                string rulesText = wave2.RulesText.Replace(
                    AllSpells.CreateModernSpellTemplate(SpellId.RayOfFrost, Trait.Psychic).ToSpellLink(),
                    CreatePsychicSpellTemplate(frostbite)
                        .ToSpellLink());
                wave2.RulesText = rulesText;
                wave2.ShowRulesBlockFor = frostbite;
                wave2.WithOnSheet(sheet =>
                {
                    SpellRepertoire repertoire = sheet.SpellRepertoires[Trait.Psychic];
                    Spell psiCantrip1 = AllSpells.CreateModernSpell(frostbite, null,
                        sheet.MaximumSpellLevel, false, new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        });
                    repertoire.SpellsKnown.Add(psiCantrip1);
                    repertoire.SpellsKnown.RemoveAll(spell => spell.SpellId == SpellId.RayOfFrost);
                    sheet.Tags["PsychicOtherCantrip"] = ignition;
                });
            }
            if (AllFeats.GetFeatByFeatNameOrStringOptional(null, "OscillatingWavePF") is { } wave3)
            {
                wave3.WithCustomName("Oscillating Wave - Ignition");
                string rulesText = wave3.RulesText.Replace(
                    AllSpells.CreateModernSpellTemplate(SpellId.ProduceFlame, Trait.Psychic).ToSpellLink(),
                    CreatePsychicSpellTemplate(ignition)
                        .ToSpellLink());
                wave3.RulesText = rulesText;
                wave3.ShowRulesBlockFor = ignition;
                wave3.WithOnSheet(values =>
                {
                    SpellRepertoire repertoire = values.SpellRepertoires[Trait.Psychic];
                    Spell psiCantrip1 = AllSpells.CreateModernSpell(ignition, null,
                        values.MaximumSpellLevel, false, new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        });
                    repertoire.SpellsKnown.Add(psiCantrip1);
                    values.Tags["PsychicOtherCantrip"] = frostbite;
                });
            }

            if (AllFeats.GetFeatByFeatNameOrStringOptional(null,
                    AllSpells.CreateModernSpellTemplate(SpellId.RayOfFrost, Trait.Psychic).Name) is { } wave4)
            {
                wave4.WithCustomName("Frostbite");
                wave4.ShowRulesBlockFor = frostbite;
                wave4.Prerequisites.Clear();
                wave4.WithPrerequisite(values =>
                            !values.SpellRepertoires.TryGetValue(Trait.Psychic,
                                out SpellRepertoire? spellRepertoire1) ||
                            !spellRepertoire1.SpellsKnown.Any(sk =>
                                sk.SpellId == frostbite && sk.CombatActionSpell.PsychicAmpInformation == null),
                        "You have already selected this spell as a non-psi cantrip.\n")
                    .WithEquivalent(values =>
                        values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire2) &&
                        spellRepertoire2.SpellsKnown.Any(sk =>
                            sk.SpellId == frostbite && sk.CombatActionSpell.PsychicAmpInformation != null));
                wave4.OnSheet = null;
                wave4.WithOnSheet(values => values.SpellRepertoires[Trait.Psychic]
                    .SpellsKnown
                    .Add(AllSpells.CreateModernSpell(frostbite, null, values.MaximumSpellLevel, false,
                        new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        })));
                string rulesText = wave4.RulesText.Replace(
                    ConsciousMind.CreatePsychicSpellTemplate(SpellId.RayOfFrost).ToSpellLink(),
                    CreatePsychicSpellTemplate(frostbite)
                        .ToSpellLink());
                wave4.RulesText = rulesText;
            }

            if (AllFeats.GetFeatByFeatNameOrStringOptional(null,
                    AllSpells.CreateModernSpellTemplate(SpellId.ProduceFlame, Trait.Psychic).Name) is not
                { } wave5) return;
            {
                wave5.WithCustomName("Ignition");
                wave5.ShowRulesBlockFor = ignition;
                wave5.Prerequisites.Clear();
                wave5.WithPrerequisite(values =>
                            !values.SpellRepertoires.TryGetValue(Trait.Psychic,
                                out SpellRepertoire? spellRepertoire1) ||
                            !spellRepertoire1.SpellsKnown.Any(sk =>
                                sk.SpellId == ignition && sk.CombatActionSpell.PsychicAmpInformation == null),
                        "You have already selected this spell as a non-psi cantrip.\n")
                    .WithEquivalent(values =>
                        values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire2) &&
                        spellRepertoire2.SpellsKnown.Any(sk =>
                            sk.SpellId == ignition && sk.CombatActionSpell.PsychicAmpInformation != null));
                wave5.OnSheet = null;
                wave5.WithOnSheet(values => values.SpellRepertoires[Trait.Psychic]
                    .SpellsKnown
                    .Add(AllSpells.CreateModernSpell(ignition, null, values.MaximumSpellLevel, false,
                        new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        })));
                string rulesText = wave5.RulesText.Replace(
                    ConsciousMind.CreatePsychicSpellTemplate(SpellId.ProduceFlame).ToSpellLink(),
                    CreatePsychicSpellTemplate(ignition)
                        .ToSpellLink());
                wave5.RulesText = rulesText;
            }
        };
    }

    public static Spell CreatePsychicSpellTemplate(SpellId spellId)
    {
        return AllSpells.CreateModernSpell(spellId, null, -1, false , new SpellInformation
        {
            ClassOfOrigin = Trait.Psychic,
            PsychicAmpInformation = new PsychicAmpInformation()
        });
    }
}