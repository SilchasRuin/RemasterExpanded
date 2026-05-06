using Dawnsbury.Audio;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.StatBlocks;
using Dawnsbury.Display;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MySpells;

public class NewSpells1st : NewSpells
{
    public static void Load()
    {
        ModManager.RegisterNewSpell("PM_LiberatingCommand", 1, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.LiberatingCommand, "Liberating Command", [Trait.Auditory, Trait.Concentrate, Trait.Manipulate, Trait.Occult, Trait.NoHeightening], 
                    "You call out a liberating cry, urging an ally to break free of an effect that holds them in place.", 
                    "Target one ally within range, if the target is grabbed, immobilized, or restrained, it can immediately use a reaction to attempt to Escape.",
                    Target.RangedFriend(6).WithAdditionalConditionOnTargetCreature((a, b) =>
                    {
                        if (!b.HasEffect(QEffectId.Grabbed) && !b.HasEffect(QEffectId.Immobilized) &&
                            !b.HasEffect(QEffectId.Restrained))
                        {
                            return Usability.NotUsableOnThisCreature("You must target a creature who is grabbed, immobilized, or restrained.");
                        }
                        return a != b
                            ? Usability.Usable
                            : Usability.NotUsableOnThisCreature("You cannot target yourself");
                    }), level,null)
                .WithActionCost(1).WithSoundEffect(SfxName.Victory)
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    Creature ally = targets.ChosenCreatures[0];
                    if (ally.Possibilities.Filter(ap =>
                        {
                            if (ap.CombatAction.ActionId != ActionId.Escape)
                                return false;
                            ap.CombatAction.ActionCost = 0;
                            ap.RecalculateUsability();
                            return true;
                        }).CreateActions(true).FirstOrDefault() is not CombatAction escape || !await ally.AskToUseReaction("Would you like to use a reaction to escape?", ally.Illustration))
                    {
                        spell.RevertRequested = true;
                        return;
                    }
                    await ally.Battle.GameLoop.FullCast(escape);
                });
        });
        ModManager.RegisterNewSpell("PM_CurseOfRecoil", 1, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CurseOfRecoil,  "Curse of Recoil", [Trait.Concentrate, Trait.Curse, Trait.Divine, Trait.Occult, Trait.NoHeightening], 
                "You curse an enemy to suffer a kickback as they make a ranged attack, potentially causing them to miss.",
                "When an enemy within 120 feet who you can see begins to make a ranged attack, you can use a {icon:Reaction} reaction to cause that enemy to make a Will save, with the following results:"+
                S.FourDegreesOfSuccess("The target is unaffected.", "The recoil from their ranged attack causes the target to be off-guard until the beginning of their next turn.", 
                    "The recoil imposes a –1 status penalty to the ranged attack and renders the target off-guard until the beginning of their next turn.",
                    "The recoil imposes a –2 status penalty to the ranged attack and renders the target off-guard until the beginning of their next turn. Until the start of their next turn, any additional ranged attacks made with the same weapon, spell, or ability take the same penalty."),
                Target.Uncastable(), level, null)
                .WithActionCost(-2).WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    if (!canCast()) return;
                    Creature self = effect.Owner;
                    effect.AddGrantingOfTechnical(
                        cr => cr.EnemyOf(self) && self.CanSee(cr) && cr.DistanceTo(self) <= 24,
                        qfTech =>
                        {
                            qfTech.YouBeginAction = async (qf, action) =>
                            {
                                if (!action.HasTrait(Trait.Attack) || !action.HasTrait(Trait.Ranged) || !canCast()) return;
                                CombatAction fake = CombatAction.CreateSimple(self, "Fake", Trait.Concentrate, Trait.Curse, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName).WithActionCost(0);
                                fake.Target = Target.Ranged(24);
                                fake.SpellInformation = spell.SpellInformation;
                                Creature enemy = qf.Owner;
                                if (!fake.CanBeginToUse(self)) return;
                                if (!await self.AskToUseReaction(
                                        $"{enemy.Name} is about to use {action.Name}, use a reaction to cast {{i}}curse of recoil{{/i}} at spell level {level}?", self.Illustration)) return;
                                self.Spellcasting!.UseUpSpellcastingResources(spell);
                                bool cast = await self.Battle.GameLoop.FullCast(fake, ChosenTargets.CreateSingleTarget(enemy));
                                if (!cast || fake.Disrupted) return;
                                self.Overhead(spell.Name, Color.Black, $"{self} casts {{b}}{spell.Name}{{/b}}.",
                                    spell.Name + " {icon:Reaction}",
                                    spell.Description, action.Traits);
                                CheckResult save = await CommonSpellEffects.RollSpellSavingThrowAsync(enemy, spell, Defense.Will);
                                switch (save)
                                {
                                    case CheckResult.CriticalSuccess:
                                        break;
                                    case CheckResult.Success:
                                        enemy.AddQEffect(QEffect.FlatFooted("curse of recoil")
                                            .WithExpirationAtStartOfOwnerTurn());
                                        break;
                                    case CheckResult.Failure:
                                        enemy.AddQEffect(QEffect.FlatFooted("curse of recoil")
                                            .WithExpirationAtStartOfOwnerTurn());
                                        enemy.AddQEffect(new QEffect
                                        {
                                            BonusToAttackRolls = (_, combatAction, _) => combatAction != action ? null : new Bonus(-1, BonusType.Status, "Curse of Recoil", false),
                                            AfterYouTakeAction = (qEffect, combatAction) =>
                                            {
                                                if (action != combatAction) return Task.CompletedTask;
                                                qEffect.ExpiresAt = ExpirationCondition.Immediately;
                                                return Task.CompletedTask;
                                            }
                                        }.WithExpirationAtStartOfOwnerTurn());
                                        break;
                                    case CheckResult.CriticalFailure:
                                        enemy.AddQEffect(QEffect.FlatFooted("curse of recoil")
                                            .WithExpirationAtStartOfOwnerTurn());
                                        enemy.AddQEffect(new QEffect("Curse of Recoil", "You have a -2 status penalty to ranged attacks made with the same weapon, spell, or ability as the triggering action.", ExpirationCondition.ExpiresAtStartOfYourTurn, self, MIllustrations.CurseOfRecoil)
                                        {
                                            BonusToAttackRolls = (_, combatAction, _) =>
                                            {
                                                if ((combatAction.Item == null || combatAction.Item != action.Item) && combatAction.Name != action.Name)
                                                    return null;
                                                return new Bonus(-2, BonusType.Status, "Curse of Recoil", false);
                                            }
                                        });
                                        break;
                                }
                            };
                        });
                });
        });
        SpellIds.SummonConstruct = ModManager.RegisterNewSpell("PM_SummonConstruct", 1,
            (_, caster, level, inCombat, _) =>
            {
                int maximumSummonLevel1 = CommonSpellEffects.GetMaximumSummonLevel(level);
                return Spells.CreateModern(MIllustrations.CreateIllustration("Inkdrop"), "Summon Construct",
                        [Trait.Summon, Trait.Arcane], "You conjure a construct to fight for you.",
                        $"You summon a construct creature whose level is {S.HeightenedVariable(maximumSummonLevel1, 1)} to fight for you.",
                        Target.RangedEmptyTileForSummoning(6),
                        level, null)
                    .WithActionCost(3)
                    .WithSoundEffect(SfxName.Summoning)
                    .WithHeighteningForSummonSpells(level, inCombat, 1)
                    .WithVariants(SpellVariant.CreateSummoningVariants(Trait.Construct, maximumSummonLevel1, caster))
                    .WithCreateVariantDescription((_, variant) =>
                        RulesBlock.CreateCreatureDescription(MonsterStatBlocks.MonsterExemplarsByName[variant?.Id!]))
                    .WithEffectOnChosenTargets(async (spell, self, targets) =>
                    {
                        await CommonSpellEffects.SummonMonster(spell, self, targets.ChosenTile!);
                    });
            });
    }
}