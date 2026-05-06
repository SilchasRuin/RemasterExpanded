using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.AuraAnimations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public class NewFocusSpells : NewSpells
{
    public static void Load()
    {
        #region Removed
        // HandOfTheApprentice = ModManager.RegisterNewSpell("PM_HandOfTheApprentice", 1, (_, _, level, _, _) =>
        // {
        //     CombatAction hand = Spells.CreateModern(MIllustrations.CreateIllustration("Apprentice"), "Hand of the Apprentice", [Trait.Uncommon, Trait.Focus, Trait.Attack, Trait.Wizard, Trait.Manipulate, Trait.Ranged, Trait.SomaticOnly, Trait.AttackDoesNotIncreaseMultipleAttackPenalty],
        //         "You take advantage of one of the most fundamental lessons of magic to levitate and propel your weapon.",
        //         "You hurl a held melee weapon with which you are trained at the target, making a spell attack roll.\nOn a success, you deal the weapon's damage as if you had hit with a melee Strike, but adding your spellcasting ability modifier to damage, rather than your Strength modifier.\nOn a critical success, you deal double damage, and you add the weapon's critical specialization effect.\nRegardless of the outcome, the weapon flies back to you and returns to your hand.",
        //         Target.Ranged(100).WithAdditionalConditionOnTargetCreature((caster, _) => caster.HeldItems.Any(item => caster.Proficiencies.Get(item.Traits) >= Proficiency.Trained && item.WeaponProperties != null && item.HasTrait(Trait.Melee)) ? Usability.Usable : Usability.NotUsable("You must be wielding a melee weapon you are trained in.")), level, null)
        //         .WithActionCost(1).WithSoundEffect(SfxName.SwordStrike)
        //         .WithProjectileCone(VfxStyle.NoAnimation())
        //         .WithCastsAsAReaction((qf, spell, canCast) =>
        //         {
        //             if (!canCast()) return;
        //             Creature caster = qf.Owner;
        //             qf.ProvideStrikeModifier = weapon =>
        //             {
        //                 if (!weapon.HasTrait(Trait.Melee) || !caster.HeldItems.Contains(weapon) ||
        //                     caster.Proficiencies.Get(weapon.Traits) < Proficiency.Trained) return null;
        //                 if (!canCast()) return null;
        //                 Item duplicate = weapon.Duplicate();
        //                 duplicate.Traits.Add(Trait.AddSpellcastingAbilityModifierToDamage);
        //                 duplicate.Traits.Remove(Trait.Melee);
        //                 duplicate.Traits.Add(Trait.Ranged);
        //                 duplicate.Traits.Add(Trait.CountsAsThrownForThePurposesOfAddingStrengthModifier);
        //                 duplicate.WithWeaponProperties(duplicate.WeaponProperties!.WithRangeIncrement(100).WithMaximumRange(100));
        //                 CombatAction strike = caster.CreateStrike(duplicate);
        //                 strike.WithSpellAttackRoll();
        //                 strike.Illustration =
        //                     new SideBySideIllustration(MIllustrations.CreateIllustration("Apprentice"),
        //                         weapon.Illustration);
        //                 strike.Traits.Add(Trait.Spell);
        //                 strike.Traits.Remove(Trait.Strike);
        //                 strike.Traits.Add(Trait.Manipulate);
        //                 strike.SpellId = spell.SpellId;
        //                 strike.SpellcastingSource = spell.SpellcastingSource;
        //                 strike.SpellInformation = spell.SpellInformation;
        //                 strike.WithProjectileCone(weapon.Illustration, 1, ProjectileKind.Arrow);
        //                 strike.Name = spell.Name;
        //                 strike.Description = StrikeRules.CreateBasicStrikeDescription4(strike.StrikeModifiers,
        //                     additionalCriticalSuccessText: "Apply the critical specialization effect of the weapon you used.");
        //                 strike.Description = strike.Description.Replace("an attack roll", "a spell attack roll");
        //                 strike.WithPrologueEffectOnChosenTargetsBeforeRolls((_, _, _) =>
        //                 {
        //                     caster.Spellcasting!.UseUpSpellcastingResources(spell);
        //                     return Task.FromResult(caster.HeldItems.Remove(weapon));
        //                 });
        //                 strike.EffectOnOneTarget += async (action, _, target, result) =>
        //                 {
        //                     if (caster.QEffects.All(qff => qff.YouHaveCriticalSpecialization == null) && result == CheckResult.CriticalSuccess) 
        //                         await CommonAbilityEffects.CriticalSpecializationEffect(action, target);
        //                     caster.HeldItems.Add(weapon);
        //                 };
        //                 return strike;
        //             };
        //         })
        //         .WithVariantsCreator(caster =>
        //         {
        //             if (caster.HeldItems.Count <= 0) return [];
        //             List<SpellVariant> variants = [];
        //             variants.AddRange(caster.HeldItems.Where(item => item.WeaponProperties != null && item.HasTrait(Trait.Melee) && caster.Proficiencies.Get(item.Traits) >= Proficiency.Trained).Select(weapon => new SpellVariant(weapon.Name, $"Hand of the Apprentice - {weapon.BaseHumanName}", new SideBySideIllustration(MIllustrations.CreateIllustration("Apprentice"), weapon.Illustration))));
        //             return variants.ToArray();
        //         })
        //         .WithTargetingTooltip((spell, targetCreature, _) =>
        //         {
        //             CombatAction pseudoStrike = new CombatAction(spell.Owner, spell.Illustration, spell.Name, spell.Traits.ToArray(), spell.Description, spell.Target)
        //             {
        //                 SpellcastingSource = spell.SpellcastingSource,
        //                 SpellLevel = spell.SpellLevel,
        //                 SpellInformation = spell.SpellInformation
        //             }.WithSpellAttackRoll();
        //             return CombatActionExecution.BreakdownAttackForTooltip(pseudoStrike, targetCreature)
        //                 .TooltipDescription;
        //         })
        //         .WithEffectOnEachTarget(async (spell, caster, target, result) =>
        //         {
        //             if (spell.ChosenVariant == null)
        //             {
        //                 spell.RevertRequested = true;
        //                 return;
        //             }
        //             Item? weapon = caster.HeldItems.FirstOrDefault(item => item.Name == spell.ChosenVariant.Id);
        //             if (weapon == null)
        //             {
        //                 spell.RevertRequested = true;
        //                 return;
        //             }
        //             Item duplicate = weapon.Duplicate();
        //             duplicate.Traits.Add(Trait.AddSpellcastingAbilityModifierToDamage);
        //             duplicate.Traits.Remove(Trait.Melee);
        //             duplicate.Traits.Add(Trait.Ranged);
        //             duplicate.Traits.Add(Trait.CountsAsThrownForThePurposesOfAddingStrengthModifier);
        //             duplicate.WithWeaponProperties(duplicate.WeaponProperties!.WithRangeIncrement(100).WithMaximumRange(100));
        //             CombatAction strike = caster.CreateStrike(duplicate);
        //             strike.WithSpellAttackRoll();
        //             strike.Traits.Add(Trait.DoNotShowOverheadOfActionName);
        //             strike.Traits.Add(Trait.DoNotShowInCombatLog);
        //             strike.Traits.Add(Trait.Spell);
        //             strike.Traits.Remove(Trait.Strike);
        //             strike.Traits.Add(Trait.DoesNotProvoke);
        //             strike.SpellId = spell.SpellId;
        //             strike.SpellcastingSource = spell.SpellcastingSource;
        //             strike.SpellInformation = spell.SpellInformation;
        //             strike.WithProjectileCone(weapon.Illustration, 1, ProjectileKind.Arrow);
        //             strike.Name = spell.Name;
        //             strike.WithActionCost(0);
        //             caster.HeldItems.Remove(weapon);
        //             await caster.Battle.GameLoop.FullCast(strike, ChosenTargets.CreateSingleTarget(target));
        //             if (caster.QEffects.All(qf => qf.YouHaveCriticalSpecialization == null) && strike.CheckResult == CheckResult.CriticalSuccess) 
        //                 await CommonAbilityEffects.CriticalSpecializationEffect(strike, target);
        //             caster.HeldItems.Add(weapon);
        //         });
        //     return hand;
        //
        // });
        #endregion
        CommunityRestoration = ModManager.RegisterNewSpell("PM_CommunityRestoration", 4, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Restoration"), "Community Restoration", [Trait.Uncommon, Trait.Concentrate, Trait.Focus, Trait.Healing, Trait.Wizard, Trait.NoHeightening],
                    "When you use your magic to support your allies, shared strength bolsters you all.",
                    "When you Cast a Spell from a wizard spell slot, and the spell affects one or more willing allies without damaging them, you may cast this spell as a {icon:Reaction} reaction. You then gain 2 temporary Hit Points per rank of the triggering spell, and can grant an equal number divided equally among allies of your choice affected by the triggering spell. These temporary Hit Points last until the end of the encounter.",
                    Target.Uncastable(), level, null)
                .WithActionCost(-2)
                .WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    if (!canCast()) return;
                    effect.AfterYouTakeAction = async (qf, action) =>
                    {
                        Creature caster = qf.Owner;
                        if (!canCast() || action.SpellcastingSource is not { ClassOfOrigin: Trait.Wizard } ||
                            action.HasTrait(Trait.Cantrip) || action.HasTrait(Trait.Focus)) return;
                        if (!action.ChosenTargets.ChosenCreatures.Any(cr => cr.FriendOfAndNotSelf(caster))) return;
                        CombatAction fake = CombatAction.CreateSimple(caster, "Fake", Trait.Concentrate, Trait.Healing, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell).WithActionCost(0);
                        fake.SpellInformation = spell.SpellInformation;
                        if (action.IsHostileAction || !fake.CanBeginToUse(caster)) return;
                        bool react = await caster.Battle.AskToUseReaction(caster, $"Would you like to cast {AllSpells.CreateSpellLink(spell.SpellId, spell.SpellcastingSource!.ClassOfOrigin)} as a {{icon:Reaction}} reaction?", spell.Illustration, spell.Traits.ToArray());
                        if (!react) return;
                        caster.Spellcasting!.UseUpSpellcastingResources(spell);
                        bool cast = await caster.Battle.GameLoop.FullCast(fake);
                        if (!cast || fake.Disrupted)
                        {
                            caster.Overhead(spell.Name, Color.Black,
                                $"{caster} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                                spell.Name + " {icon:Reaction}",
                                spell.Description, spell.Traits);
                            return;
                        }
                        caster.Overhead(action.Name, Color.Black, $"{caster} casts {{b}}{spell.Name}{{/b}}.",
                            spell.Name + " {icon:Reaction}",
                            spell.Description, spell.Traits);
                        switch (action.ChosenTargets.ChosenCreatures.Count)
                        {
                            case 1:
                                caster.TemporaryHP = action.SpellLevel * 2;
                                action.ChosenTargets.ChosenCreatures[0].TemporaryHP = action.SpellLevel * 2;
                                break;
                            case > 1:
                                int amount = action.SpellLevel * 2;
                                caster.TemporaryHP = amount;
                                List<Creature> chosenCreatures = action.ChosenTargets.ChosenCreatures.Where(cr => cr.FriendOfAndNotSelf(caster)).ToList();
                                CombatAction chooser = CombatAction.CreateSimple(caster, "chooser", Trait.DoNotShowOverheadOfActionName, Trait.DoNotShowInCombatLog, Trait.DoesNotProvoke).WithActionCost(0);
                                chooser.Target = (Target.MultipleCreatureTargets(chosenCreatures.Count, () => Target.RangedFriend(100).WithAdditionalConditionOnTargetCreature((_, creature1) => chosenCreatures.Contains(creature1) ? Usability.Usable : Usability.NotUsableOnThisCreature("Can only target affected allies."))) as MultipleCreatureTargetsTarget)!.WithMinimumTargets(1).WithMustBeDistinct();
                                chooser.Illustration = spell.Illustration;
                                await caster.Battle.GameLoop.FullCast(chooser);
                                List<Creature> choices = chooser.ChosenTargets.ChosenCreatures.Where(cr => cr.TemporaryHP < amount).ToList();
                                int newAmount = amount / choices.Count;
                                int remainder = amount % choices.Count;
                                foreach (Creature choice in choices)
                                {
                                    if (choice.TemporaryHP >= newAmount + remainder) continue;
                                    choice.TemporaryHP = newAmount;
                                    choice.TemporaryHP += remainder;
                                    remainder = 0;
                                }
                                break;
                        }
                    };
                });
        });
        SpiralOfHorrors = ModManager.RegisterNewSpell("PM_SpiralOfHorrors", 4, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("SpiralOfHorrors"), "Spiral of Horrors", 
                    [Trait.Uncommon, Trait.Focus, Trait.Aura, Trait.Concentrate, Trait.Emotion, Trait.Fear, Trait.Manipulate, Trait.Mental, Trait.Wizard, Trait.DoesNotRequireAttackRollOrSavingThrow, Trait.NoHeightening],
                    "Shades and spirits howl and whirl around you in a display that strikes fear into the hearts of all who witness it.",
                    "Enemies in the area are frightened 1 and can't reduce their frightened value below 1 for as long as you Sustain the spell.",
                    Target.EnemiesOnlyEmanation(6), level, null)
                .WithActionCost(2).WithSoundEffect(SfxName.Fear)
                .WithEffectOnChosenTargets((spell, caster, _) =>
                {
                    QEffect spiral = new(ExpirationCondition.ExpiresAtEndOfYourTurn)
                    {
                        CannotExpireThisTurn = true,
                        Id = QEffectId.DirgeOfDoomSource,
                        SpawnsAura = _ => new MagicCircleAuraAnimation(IllustrationName.AngelicHaloCircleWhite, Color.DarkViolet, 6)
                    };
                    spiral.AddGrantingOfTechnical(cr => cr.DistanceTo(caster) <= 6 && cr.EnemyOf(caster) && !cr.IsImmuneTo(Trait.Fear) && !cr.IsImmuneTo(Trait.Mental) && !cr.IsImmuneTo(Trait.Emotion),
                        qfTech =>
                        {
                            qfTech.StateCheck = qf =>
                            {
                                if (!qf.Owner.HasEffect(QEffectId.Frightened)) 
                                    qf.Owner.AddQEffect(QEffect.Frightened(1).WithSourceAction(spell));
                            };
                            qfTech.Id = QEffectId.DirgeOfDoomFrightenedSustainer;
                            qfTech.Illustration = spell.Illustration;
                            qfTech.Name =  spell.Name;
                            qfTech.Description = "You cannot reduce your fear below 1 as long as you remain within the aura.";
                            qfTech.Source = caster;
                        });
                    QEffect sustain = QEffect.Sustaining(spell, spiral);
                    sustain.Illustration = spell.Illustration;
                    caster.AddQEffect(spiral);
                    caster.AddQEffect(sustain);
                    return Task.CompletedTask;
                });
        });
        InterdisciplinaryIncantation = ModManager.RegisterNewSpell("PM_InterdisciplinaryIncantation", 4, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Incantation"), "Interdisciplinary Incantation", [Trait.Uncommon, Trait.Focus, Trait.Wizard, Trait.Concentrate, Trait.NoHeightening, Trait.DoesNotRequireAttackRollOrSavingThrow],
                    "You gather the embers of another caster's spell, using your understanding of unified magic to pick apart their formulas and incantations well enough to imitate the spell yourself, if only for a short time.",
                    "When a creature within 30 feet casts an arcane spell, you can Cast this Spell as a {icon:Reaction} reaction. Until the end of your next turn, you can Cast the triggering Spell by expending a wizard spell slot of the same rank. That caster's spells can't trigger your interdisciplinary incantation again until after your next long rest.",
                    Target.Uncastable(), level, null)
                .WithActionCost(-2)
                .WithCastsAsAReaction((qf, spell, canCast) =>
                {
                    Creature caster = qf.Owner;
                    if (!canCast()) return;
                    qf.AddGrantingOfTechnical(cr => cr != caster && !cr.HasEffect(MQEffectIds.CantTrigger) && !cr.PersistentUsedUpResources.UsedUpActions.Contains(spell.Name+caster.Name), qfTech =>
                    {
                        qfTech.AfterYouTakeAction = async (effect, action) =>
                        {
                            if (!canCast()) return;
                            if (action.SpellcastingSource is not {SpellcastingTradition: Trait.Arcane} || action.SpellId == SpellId.None) return;
                            if (caster.Spellcasting!.GetSourceByOrigin(Trait.Wizard)!.Spells.Where(sp => sp.SpellLevel == action.SpellLevel).ToList().Count <= 0) return;
                            CombatAction fake = CombatAction.CreateSimple(caster, "Fake", Trait.Concentrate, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell).WithActionCost(0);
                            fake.SpellInformation = spell.SpellInformation;
                            if (!fake.CanBeginToUse(caster)) return;
                            bool react = await caster.Battle.AskToUseReaction(caster, $"Would you like to cast {AllSpells.CreateSpellLink(spell.SpellId, spell.SpellcastingSource!.ClassOfOrigin)} as a {{icon:Reaction}} reaction?", spell.Illustration, spell.Traits.ToArray());
                            if (!react) return;
                            caster.Spellcasting!.UseUpSpellcastingResources(spell);
                            bool cast = await caster.Battle.GameLoop.FullCast(fake);
                            if (!cast || fake.Disrupted)
                            {
                                caster.Overhead(spell.Name, Color.Black,
                                    $"{caster} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                                    spell.Name + " {icon:Reaction}",
                                    spell.Description, spell.Traits);
                                return;
                            }
                            caster.Overhead(action.Name, Color.Black, $"{caster} casts {{b}}{spell.Name}{{/b}}.",
                                spell.Name + " {icon:Reaction}",
                                spell.Description, spell.Traits);
                            effect.Owner.AddQEffect(new QEffect { Id = MQEffectIds.CantTrigger });
                            if (effect.Owner.PersistentCharacterSheet != null)
                                effect.Owner.PersistentUsedUpResources.UsedUpActions.Add(spell.Name+caster.Name);
                            caster.AddQEffect(new QEffect(spell.Name,
                                $"You can cast {AllSpells.CreateSpellLink(action.SpellId, Trait.Wizard)} by expending a rank {action.SpellLevel} spell slot.",
                                ExpirationCondition.ExpiresAtEndOfYourTurn, caster, spell.Illustration)
                            {
                                CannotExpireThisTurn = true,
                                ProvideContextualAction = _ =>
                                {
                                    CombatAction newSpell = Spell.DuplicateSpell(action).CombatActionSpell.WithActionCost(0);
                                    newSpell.Owner = caster;
                                    newSpell.SpellcastingSource = caster.Spellcasting.GetSourceByOrigin(Trait.Wizard);
                                    newSpell.Traits.Remove(Trait.Prepared);
                                    newSpell.Traits.Remove(Trait.Spontaneous);
                                    newSpell.Traits.Add(Trait.AtWill);
                                    CombatAction idCast = new CombatAction(caster, action.Illustration, "Interdisciplinary " + action.Name, [Trait.DoNotShowInCombatLog],
                                            "Expend a spell slot of the same rank to cast this spell.", Target.Self()).WithActionCost(action.ActionCost)
                                        .WithEffectOnChosenTargets(async (combatAction, self, _) =>
                                        {
                                            bool castSpell;
                                            if (newSpell.HasTrait(Trait.Cantrip))
                                            {
                                                castSpell = await self.Battle.GameLoop.FullCast(newSpell);
                                                if (castSpell) return;
                                                combatAction.RevertRequested = true;
                                                return;
                                            }
                                            List<CombatAction> spellsOfLevel = [];
                                            List<string> spellsNames = [];
                                            foreach (CombatAction useUp in
                                                     self.Spellcasting!.Sources.FirstOrDefault(source =>
                                                             source.ClassOfOrigin == Trait.Wizard)!.Spells
                                                         .Where(sp => sp.SpellLevel == newSpell.SpellLevel))
                                            {
                                                spellsOfLevel.Add(useUp);
                                                spellsNames.Add(useUp.Name);
                                            }
                                            spellsNames.Add("Cancel");
                                            ChoiceButtonOption choice =
                                                await self.AskForChoiceAmongButtons(spell.Illustration,
                                                    $"Choose a spell to expend to cast {newSpell.Name}.",
                                                    spellsNames.ToArray());
                                            if (spellsNames[choice.Index] == "Cancel")
                                            {
                                                combatAction.RevertRequested = true;
                                                return;
                                            }
                                            castSpell = await self.Battle.GameLoop.FullCast(newSpell);
                                            if (!castSpell)
                                            {
                                                combatAction.RevertRequested = true;
                                                return;
                                            }
                                            self.Spellcasting!.UseUpSpellcastingResources(
                                                spellsOfLevel[choice.Index]);
                                        });
                                    return new ActionPossibility(idCast);
                                }
                            });
                        };
                    });
                });
        });
    }
}