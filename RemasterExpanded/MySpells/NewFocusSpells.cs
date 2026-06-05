using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.AuraAnimations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
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

        #region  SchoolSpells

        CommunityRestoration = ModManager.RegisterNewSpell("PM_CommunityRestoration", 0, (_, _, level, _, _) =>
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
        SpiralOfHorrors = ModManager.RegisterNewSpell("PM_SpiralOfHorrors", 0, (_, _, level, _, _) =>
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
        InterdisciplinaryIncantation = ModManager.RegisterNewSpell("PM_InterdisciplinaryIncantation", 0, (_, _, level, _, _) =>
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
        #endregion
        #region VigilDomain

        ObjectMemory = ModManager.RegisterNewSpell("RE_ObjectMemory", 0, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(IllustrationName.MagicWeapon, "Object Memory", [Trait.Uncommon, Trait.Cleric, Trait.Focus, Trait.Rebalanced, Trait.Manipulate, Trait.Concentrate],
                    "By touching an object, you draw forth the experience of those who created and used it.",
                    "Your proficiency in a weapon you are wielding increases to match your proficiency with simple weapons.", Target.Self(), level, null)
                .WithHeightenedAtSpecificLevel(level, 3, inCombat, "You gain a +1 status bonus to attack rolls with the weapon until the end of the encounter.")
                .WithSoundEffect(SfxName.MagicWeapon)
                .WithActionCost(1)
                .WithVariantsCreator(caster =>
                {
                    if (caster.HeldItems.Count <= 0) return [];
                    List<SpellVariant> variants = [];
                    variants.AddRange(caster.HeldItems.Where(item => item.WeaponProperties != null).Select(weapon => new SpellVariant(weapon.Name, $"{weapon.BaseHumanName}", weapon.Illustration)));
                    return variants.ToArray();
                })
                .WithEffectOnChosenTargets(async (spell, caster, _) =>
                {
                    if (spell.ChosenVariant == null)
                    {
                        spell.RevertRequested = true;
                        return;
                    }
                    Item? weapon = caster.HeldItems.FirstOrDefault(item => item.Name == spell.ChosenVariant.Id);
                    if (weapon == null)
                    {
                        spell.RevertRequested = true;
                        return;
                    }

                    QEffect memory = new("Object Memory", $"Your proficiency in {weapon.Name} increases to {(level >= 6 ? "expert" : "trained")} (if it is not already {(level >= 6 ? "expert" : "trained")} or better). You also gain a +{S.HeightenedVariable(level >= 6 ? 2 : 1, 1)} status bonus to attack rolls made with {weapon.Name} until the end of the encounter.", ExpirationCondition.Never, caster, IllustrationName.MagicWeapon)
                    {
                        BonusToAttackRolls = (_, action, _) => action.Item == weapon && action.HasTrait(Trait.Attack) ? new Bonus(level >= 6 ? 2 : 1, BonusType.Status, "Object Memory") : null,
                    };
                    caster.Proficiencies.Set(weapon.Traits.ToArray(), caster.Proficiencies.Get(Trait.Simple));
                    caster.AddQEffect(memory);
                });
        });
        RememberTheLost = ModManager.RegisterNewSpell("RE_RememberTheLost", 0, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("RememberTheLost"), "Remember the Lost",
                    [Trait.Cleric, Trait.Focus, Trait.Mental],
                    "You call upon the lost and forgotten, assailing your foes' minds with the memories of those who died with a grievance toward them.",
                    $"Enemies in the area take {S.HeightenedVariable(2 * (level - 1), 6)}d6 mental damage (basic Will save) and are frightened 1 on a critical failure. If you know the names of anyone murdered or grievously wronged by an enemy in the area, you can chant those victims’ names when you Cast the Spell to improve the clarity of the visions, increasing the damage to the corresponding enemy from {S.HeightenedVariable(2 * (level - 1), 6)}d6 to {S.HeightenedVariable(2 * (level - 1), 6)}d10; you can do so for multiple enemies if you know specific victims of each enemy.",
                    Target.EnemiesOnlyEmanation(6), level, SpellSavingThrow.Basic(Defense.Will))
                .WithSoundEffect(SfxName.FemaleDeath)
                .WithHeighteningNumerical(level, 4, inCombat, 1, "The damage increases by 2d6 (or 2d10 to an enemy when you name a specific victim).")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    bool grievance = HasGrievance(target);
                    if (grievance)
                        await caster.Battle.Cinematics.ShowQuickBubble(caster, AggrievedParty(target), null);
                    var dice = $"{2 * (level - 1)}{(grievance ? "d10" : "d6")}";
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText(dice, "Remember the Lost"), DamageKind.Mental);
                    if (result == CheckResult.CriticalFailure)
                        target.AddQEffect(QEffect.Frightened(1));
                });
        });

        #endregion
        #region PainDomain

        SavorTheSting = ModManager.RegisterNewSpell("RE_SavorTheSting", 0, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("SavorTheSting"), "Savor the Sting",
                    [Trait.Cleric, Trait.Uncommon, Trait.Focus, Trait.Mental, Trait.Nonlethal, Trait.SomaticOnly],
                    "You inflict pain upon the target and revel in their anguish.",
                    $"This deals {S.HeightenedVariable(level, 1)}d4 mental damage and {S.HeightenedVariable(level, 1)}d4 persistent mental damage; the target must attempt a Will save. As long as the target is taking persistent damage from this spell, you gain a +1 status bonus to attack rolls and skill checks against the target." +
                    S.FourDegreesOfSuccess("The target is unaffected.", "The target takes half damage and no persistent damage.", "The target takes full initial and persistent damage.", "The target takes double initial and persistent damage."),
                    Target.Touch(), level, SpellSavingThrow.Standard(Defense.Will))
                .WithSoundEffect(SfxName.FemaleDeath)
                .WithHeighteningNumerical(level, 1, inCombat, 1,
                    "The initial damage increases by 1d4 and the persistent damage increases by 1d4.")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText($"{level}d4", "Savor the Sting"), DamageKind.Mental);
                    DiceFormula? diceExpression1 = Checks.ModifyDamageFromBasicSave(DiceFormula.FromText($"{level}d4", "Persistent damage"), result);
                    if (diceExpression1 == null)
                        return;
                    QEffect sting = QEffect.PersistentDamage(diceExpression1, DamageKind.Mental);
                    sting.Description += $" {caster.Name} gains a +1 status bonus to attack rolls and skill checks against {target.Name}.";
                    sting.StateCheck = effect =>
                    {
                        caster.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToAttackRolls = (_, action, creature) => creature != effect.Owner ||
                                                                           (action.ActiveRollSpecification
                                                                                ?.TaggedDetermineBonus.InvolvedSkill ==
                                                                            null && !action.HasTrait(Trait.Attack)) ? null : new Bonus(1, BonusType.Status, "Savor the Sting", true)
                        });
                    };
                    target.AddQEffect(sting);
                });
        });

        RetributivePain = ModManager.RegisterNewSpell("RE_RetributivePain", 0, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("RetributivePain"),  "Retributive Pain", [Trait.Uncommon, Trait.Cleric, Trait.Focus, Trait.SomaticOnly, Trait.Mental, Trait.Nonlethal],
                "",
                "",
                Target.Uncastable(), level, SpellSavingThrow.Basic(Defense.Fortitude))
                .WithSoundEffect(SfxName.Boneshaker)
                .WithCastsAsAReaction((effect, thisSpell, canCast) =>
                {
                    
                });

        });

        #endregion

        #region KnowledgeDomain

        ScholarlyRecollection = ModManager.RegisterNewSpell("RE_ScholarlyRecollection", 0, (_, _, level, _, _) =>
            {
                return Spells.CreateModern(MIllustrations.CreateIllustration("ScholarlyRecollection"),
                    "Scholarly Recollection", [Trait.Cleric, Trait.Focus, Trait.Fortune, Trait.Uncommon, Trait.NoHeightening],
                    "Speaking a short prayer as you gather your thoughts, you're blessed to find yourself pointed in the right direction.",
                    "When you attempt a Perception check to Seek, or you attempt a skill check to Recall Weakness with a skill you're trained in, you can cast this spell as a reaction {icon:Reaction}, gaining the following effect: roll the triggering check twice and use the better result.",
                    Target.Uncastable(),  level, null)
                    .WithActionCost(-2)
                    .WithCastsAsAReaction((effect, thisSpell, canCast) =>
                    {
                        if (!canCast())
                            return;
                        effect.YouBeginActionReaction = (qf, action) =>
                        {
                            if (action.ActionId != ActionId.Seek && action.ActionId != RecallWeakness.RWActionId)
                                return null;
                            Creature caster = qf.Owner;
                            ReactionOption recollection = ReactionOption.CreateFromSpellAsAReaction(thisSpell, "Roll the triggering check twice, using the better result.", async () =>
                            {
                                caster.Overhead(thisSpell.Name, Color.Black,
                                    $"{caster} casts {{b}}{thisSpell.Name}{{/b}}.",
                                    thisSpell.Name + " {icon:Reaction}",
                                    thisSpell.Description, thisSpell.Traits);
                                caster.AddQEffect(new QEffect
                                {
                                    RerollActiveRoll = async (_, _, cAction, _) =>
                                    {
                                        if (cAction.ActionId != ActionId.Seek && cAction.ActionId != RecallWeakness.RWActionId)
                                            return RerollDirection.DoNothing;
                                        return RerollDirection.RerollAndKeepBest;
                                    },
                                    AfterYouTakeAction = async (qff, cAction) =>
                                    {
                                        if (cAction.ActionId != ActionId.Seek &&
                                            cAction.ActionId != RecallWeakness.RWActionId)
                                            return;
                                        qff.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                });

                            }).WithIsReaction();
                            return recollection;
                        };
                    });
            });
        KnowTheEnemy = ModManager.RegisterNewSpell("RE_KnowTheEnemy", 0, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("KnowTheEnemy"), "Know the Enemy", [Trait.Uncommon, Trait.Cleric, Trait.Focus, Trait.Fortune, Trait.SomaticOnly, Trait.NoHeightening],
                "You quickly remind yourself of useful information.",
                $"When you roll initiative and can see a creature, you succeed at an attack roll against a creature, or a creature fails a saving throw against one of your spells you can cast this spell as a reaction {{icon:Reaction}}, with the following effect: you {RecallWeakness.GetActionLink()} on the target. You can roll your check twice and use the better result.",
                Target.Uncastable(), level, null)
                .WithActionCost(-2)
                .WithCastsAsAReaction((effect, thisSpell, canCast) =>
                {
                    if (!canCast())
                        return;
                    Creature caster = effect.Owner;
                    CombatAction recallWeakness = RecallWeakness.CreateRecallWeaknessAction(caster).WithActionCost(0);
                    recallWeakness.Target = Target.Distance(200);
                    CombatAction dummy = CombatAction.CreateSimple(caster, "Know the Enemy", Trait.DoNotShowInCombatLog,
                        Trait.DoNotShowOverheadOfActionName, Trait.Manipulate, Trait.Spell);
                    QEffect fortune = new()
                    {
                        RerollActiveRoll = async (_, _, cAction, _) => cAction.ActionId != RecallWeakness.RWActionId ? RerollDirection.DoNothing : RerollDirection.RerollAndKeepBest,
                        AfterYouTakeAction = async (qff, cAction) =>
                        {
                            if (cAction.ActionId != RecallWeakness.RWActionId)
                                return;
                            qff.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    effect.StartOfCombatReaction = qf =>
                    {
                        if (!qf.Owner.Battle.HostileEnemiesExist)
                            return null;
                        return ReactionOption.CreateFromSpellAsAReaction(thisSpell, $"Cast this spell to use a {RecallWeakness.GetActionLink("Recall Weakness {icon:FreeAction}")} on one creature you can see.", async () =>
                        {
                            await caster.Battle.GameLoop.FullCast(dummy);
                            if (dummy.Disrupted)
                            {
                                caster.Overhead(thisSpell.Name, Color.Black,
                                    $"{caster} attempts to cast {{b}}{thisSpell.Name}{{/b}}, but it was disrupted!",
                                    thisSpell.Name + " {icon:Reaction}",
                                    thisSpell.Description, thisSpell.Traits);
                                return;
                            }
                            caster.Overhead(thisSpell.Name, Color.Black,
                                $"{caster} casts {{b}}{thisSpell.Name}{{/b}}.",
                                thisSpell.Name + " {icon:Reaction}",
                                thisSpell.Description, thisSpell.Traits);
                            caster.AddQEffect(fortune);
                            await caster.Battle.GameLoop.FullCast(recallWeakness);
                        }).WithIsReaction();
                    };
                    effect.AfterYouTakeAction = async (_, action) =>
                    {
                        if ((!action.HasTrait(Trait.Attack) && action.SavingThrow == null) ||
                            action.ChosenTargets.ChosenCreature is not {} enemy)
                            return;
                        if (action.HasTrait(Trait.Attack) && action.CheckResult <= CheckResult.Failure)
                            return;
                        if (action is { SavingThrow: not null, CheckResult: >= CheckResult.Success } or { SavingThrow: not null, SpellInformation: null })
                            return;
                        if (!await caster.Battle.AskToUseReaction(caster,
                                $"Would you like to cast {{i}}scholarly recollection{{/i}} as a reaction {{icon:Reaction}} to {RecallWeakness.GetActionLink()} targeting {enemy}?"))
                            return;
                        await caster.Battle.GameLoop.FullCast(dummy);
                        if (dummy.Disrupted)
                        {
                            caster.Overhead(thisSpell.Name, Color.Black,
                                $"{caster} attempts to cast {{b}}{thisSpell.Name}{{/b}}, but it was disrupted!",
                                thisSpell.Name + " {icon:Reaction}",
                                thisSpell.Description, thisSpell.Traits);
                            return;
                        }
                        caster.Overhead(thisSpell.Name, Color.Black,
                            $"{caster} casts {{b}}{thisSpell.Name}{{/b}}.",
                            thisSpell.Name + " {icon:Reaction}",
                            thisSpell.Description, thisSpell.Traits);
                        caster.AddQEffect(fortune);
                        await caster.Battle.GameLoop.FullCast(recallWeakness);
                    };
                });
        });

        #endregion
    }

    public static bool HasGrievance(Creature villain)
    {
        return villain.CreatureId switch
        {
            CreatureId.TheInevitableDeath or CreatureId.Tiberius or CreatureId.GrandmotherDemay => true,
            CreatureId.ShadowDemon => villain.Battle.CampaignState is { AdventurePath.IsGoodLittleChildren: true },
            _ => false
        };
    }

    public static string AggrievedParty(Creature villain)
    {
        return villain.CreatureId switch
        {
            CreatureId.TheInevitableDeath => "Lenka!",
            CreatureId.ShadowDemon or CreatureId.Tiberius => "Talia!",
            CreatureId.GrandmotherDemay => "Liandra!",
            _ => ""
        };
    }
}