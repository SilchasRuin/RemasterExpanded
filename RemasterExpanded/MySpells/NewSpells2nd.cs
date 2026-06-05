using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Mechanics.Zoning;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace RemasterExpanded.MySpells;

public class NewSpells2nd : NewSpells
{
    public static void Load()
    {
        
        ModManager.RegisterNewSpell("RE_AlbatrossCurse", 2, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.AlbatrossCurse, "Albatross Curse", [Trait.Concentrate, Trait.Manipulate, Trait.Misfortune, Trait.Occult, Trait.Primal],
                    "You create a spectral albatross, a guiding bird for sailors, to hover around the target.", 
                    "You and allies within 30 feet of the target gain a +1 circumstance bonus to attacks against the target. The target creature can spend an action to Strike the albatross, which automatically succeeds and kills it. The target must then attempt a Will save against your spell DC."
                    + S.FourDegreesOfSuccess("The target is unaffected.", 
                        "The guilt of slaughtering a bird of good fortune weighs on the target's mind. The target is stupefied 1 for 1 round.", 
                        "The albatross hangs around a cord from the target's neck (or closest equivalent) for 1 minute, cursing them for their transgression. During this time, the target must roll twice and take the worse result on their next Will save, after which the albatross disappears.",
                        "As failure, but the duration is the rest of the encounter."), Target.Ranged(6), level, null)
                .WithActionCost(2).WithSoundEffect(SfxName.CrowCrowCrow)
                .WithEffectOnChosenTargets((spell, self, targets) =>
                {
                    Creature enemy = targets.ChosenCreatures[0];
                    QEffect albatrossCurse = new("Albatross Revenge", "You must roll twice and take the worse result on your next will save.")
                    {
                        Illustration = MIllustrations.AlbatrossCurse,
                        RerollSavingThrow = (effect, _, action) =>
                        {
                            if (action.SavingThrow is not { Defense: Defense.Will })
                                return Task.FromResult(RerollDirection.DoNothing);
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                            return Task.FromResult(RerollDirection.RerollAndKeepWorst);
                        }
                    };
                    QEffect albatrossBonus = new QEffect("Albatross Curse", "Allies of "+self.Name+" have a +1 circumstance bonus to attacks against this creature.")
                    {
                        Illustration = MIllustrations.AlbatrossCurse,
                        ProvideContextualAction = effect =>
                        {
                            Creature owner = effect.Owner;
                            return new ActionPossibility(new CombatAction(owner, MIllustrations.AlbatrossCurse, "Kill the Albatross", [Trait.Strike, Trait.Attack, Trait.Basic], "Use a strike to kill the albatross, removing the bonus to attack rolls but risking a curse.", Target.Self())
                                .WithActionCost(1).WithSoundEffect(SfxName.SwordStrike)
                                .WithGoodness((_, creature, _) => creature.Level * 2)
                                .WithEffectOnChosenTargets(async (creature, _) =>
                                {
                                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                                    CheckResult result = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Will);
                                    switch (result)
                                    {
                                        case CheckResult.Success:
                                            creature.AddQEffect(QEffect.Stupefied(1)
                                                .WithExpirationAtStartOfOwnerTurn());
                                            break;
                                        case CheckResult.Failure:
                                            creature.AddQEffect(albatrossCurse.WithExpirationAtStartOfSourcesTurn(self, 10));
                                            break;
                                        case CheckResult.CriticalFailure:
                                            creature.AddQEffect(albatrossCurse);
                                            break;
                                        case CheckResult.CriticalSuccess:
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                }));
                        }
                    }.WithExpirationAtStartOfSourcesTurn(self, 10);
                    albatrossBonus.AddGrantingOfTechnical(cr => cr.FriendOf(self), qfTech =>
                    {
                        qfTech.BonusToAttackRolls = (_ , action, _) => !action.HasTrait(Trait.Attack) ? null : new Bonus(1, BonusType.Circumstance, "Albatross Curse", true);
                    });
                    enemy.AddQEffect(albatrossBonus);
                    return Task.CompletedTask;
                });
        });
        Hidebound = ModManager.RegisterNewSpell("RE_Hidebound", 2, (_, _, level, inCombat, _) =>
        {
            int heighten = level % 2 == 0 ? level : level - 1;
            return Spells.CreateModern(MIllustrations.Hidebound, "Hidebound", [Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Primal], 
                    "The target's skin erupts in thick hide or dense scales.",
                    $"If you or an ally within 60 feet is hit with a Strike that deals physical damage, you can cast this spell as a {{icon:Reaction}} reaction, and that target gains resistance {S.HeightenedVariable(5 + 3 * ((heighten - 2) / 2), 5)} to physical damage, except adamantine, until the beginning of its next turn.",
                    Target.Uncastable(), level, null)
                .WithHeighteningNumerical(level, 2, inCombat, 2, "The resistance increases by 3.")
                .WithActionCost(-2).WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    if (!canCast()) return;
                    Creature caster = effect.Owner;
                    effect.AddGrantingOfTechnical(cr => cr.FriendOf(caster) && cr.DistanceTo(caster) <= 12, qfTech =>
                    {
                        qfTech.YouAreDealtDamageReaction = (effectQ, damage) =>
                        {
                            Creature defender = effectQ.Owner;
                            CombatAction? cAction = damage.CombatAction;
                            CombatAction fake = CombatAction.CreateSimple(caster, "Fake", Trait.Manipulate, Trait.Concentrate, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell).WithActionCost(0);
                            fake.SpellInformation = spell.SpellInformation;
                            int resistAmount = 5 + 3 * ((level - 2) / 2);
                            if (!canCast() || !damage.KindedDamages.Any(kind => kind.DamageKind.IsPhysical()) || cAction == null || !cAction.HasTrait(Trait.Strike) || cAction.HasTrait(Trait.Adamantine) || !fake.CanBeginToUse(caster)) return null;
                            int toReduce = 0;
                            List<DamageKind> affected = [];
                            foreach (KindedDamage kindedDamage in damage.KindedDamages.Where(kindedDamage => kindedDamage.DamageKind.IsPhysical()))
                            {
                                if (affected.Contains(kindedDamage.DamageKind)) continue;
                                if (defender.WeaknessAndResistance.Resistances.Count > 0 && defender.WeaknessAndResistance.Resistances.FirstOrDefault(resistance => resistance.Matches(cAction, kindedDamage.DamageKind)) is {} resist)
                                {
                                    toReduce += resistAmount - resist.Value;
                                    affected.Add(kindedDamage.DamageKind);
                                }
                                else
                                {
                                    toReduce += resistAmount;
                                    affected.Add(kindedDamage.DamageKind);
                                }
                            }
                            if (toReduce <= 0) return null;
                            int totalDamage = damage.KindedDamages.Sum(kindedDamage => kindedDamage.ResolvedDamage);
                            ReactionOption hidebound = ReactionOption.CreateFromSpellAsAReaction(spell,
                                defender.Name + " is about to take " + totalDamage +
                                " damage, would you like to use a reaction to reduce the damage by " + toReduce + "?",
                                async () =>
                                {
                                    bool cast = await caster.Battle.GameLoop.FullCast(fake);
                                    if (!cast || fake.Disrupted)
                                    {
                                        caster.Overhead(spell.Name, Color.Black,
                                            $"{caster} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                                            spell.Name + " {icon:Reaction}",
                                            spell.Description, spell.Traits);
                                        return;
                                    }

                                    caster.Overhead(spell.Name, Color.Black, $"{caster} casts {{b}}{spell.Name}{{/b}}.",
                                        spell.Name + " {icon:Reaction}",
                                        spell.Description, spell.Traits);
                                    QEffect hide = new("Hidebound",
                                        "You have resistance " + resistAmount +
                                        " to physical damage except adamantine.",
                                        ExpirationCondition.ExpiresAtStartOfYourTurn, caster, MIllustrations.Hidebound)
                                    {
                                        StateCheck = qf =>
                                            qf.Owner.WeaknessAndResistance.AddSpecialResistance("physical",
                                                (combatAction, dk) =>
                                                    dk.IsPhysical() && combatAction != null &&
                                                    !combatAction.HasTrait(Trait.Adamantine), resistAmount,
                                                "adamantine")
                                    };
                                    defender.AddQEffectAtPriority(hide, true);
                                    affected = [];
                                    foreach (KindedDamage kindedDamage in damage.KindedDamages.Where(d =>
                                                 d.DamageKind.IsPhysical()))
                                    {
                                        if (affected.Contains(kindedDamage.DamageKind)) continue;
                                        int num1 = resistAmount;
                                        if (defender.WeaknessAndResistance.Resistances.Count > 0 &&
                                            defender.WeaknessAndResistance.Resistances.FirstOrDefault(resistance =>
                                                resistance.Matches(cAction, kindedDamage.DamageKind)) is { } resist)
                                        {
                                            num1 -= resist.Value;
                                            num1 = Math.Max(num1, 0);
                                        }

                                        int num = Math.Min(kindedDamage.ResolvedDamage, num1);
                                        kindedDamage.ResolvedDamage -= num;
                                        affected.Add(kindedDamage.DamageKind);
                                    }

                                    damage.DamageEventDescription.AppendLine(
                                        $"{{b}}-{toReduce.ToString()}{{/b}} Hidebound");
                            });
                            return hidebound.WithTraits(spell.Traits.ToArray());
                        };
                        // qfTech.YouAreDealtDamageEvent = async (effectQ, damage) =>
                        // {
                        //     Creature defender = effectQ.Owner;
                        //     CombatAction? cAction = damage.CombatAction;
                        //     CombatAction fake = CombatAction.CreateSimple(caster, "Fake", Trait.Manipulate, Trait.Concentrate, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell).WithActionCost(0);
                        //     fake.SpellInformation = spell.SpellInformation;
                        //     int resistAmount = 5 + 3 * ((level - 2) / 2);
                        //     if (!canCast() || !damage.KindedDamages.Any(kind => kind.DamageKind.IsPhysical()) || cAction == null || !cAction.HasTrait(Trait.Strike) || cAction.HasTrait(Trait.Adamantine) || !fake.CanBeginToUse(caster)) return;
                        //     int toReduce = 0;
                        //     List<DamageKind> affected = [];
                        //     foreach (KindedDamage kindedDamage in damage.KindedDamages.Where(kindedDamage => kindedDamage.DamageKind.IsPhysical()))
                        //     {
                        //         if (affected.Contains(kindedDamage.DamageKind)) continue;
                        //         if (defender.WeaknessAndResistance.Resistances.Count > 0 && defender.WeaknessAndResistance.Resistances.FirstOrDefault(resistance => resistance.Matches(cAction, kindedDamage.DamageKind)) is {} resist)
                        //         {
                        //             toReduce += resistAmount - resist.Value;
                        //             affected.Add(kindedDamage.DamageKind);
                        //         }
                        //         else
                        //         {
                        //             toReduce += resistAmount;
                        //             affected.Add(kindedDamage.DamageKind);
                        //         }
                        //     }
                        //     if (toReduce <= 0) return;
                        //     int totalDamage = damage.KindedDamages.Sum(kindedDamage => kindedDamage.ResolvedDamage);
                        //     if (!await caster.AskToUseReaction(defender.Name+" is about to take "+totalDamage+" damage, would you like to use a reaction to cast {i}hidebound{i} at spell level "+level+" and reduce the damage by "+toReduce+"?", caster.Illustration)) return;
                        //     caster.Spellcasting!.UseUpSpellcastingResources(spell);
                        //     bool cast = await caster.Battle.GameLoop.FullCast(fake);
                        //     if (!cast || fake.Disrupted)
                        //     {
                        //         caster.Overhead(spell.Name, Color.Black,
                        //             $"{caster} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                        //             spell.Name + " {icon:Reaction}",
                        //             spell.Description, spell.Traits);
                        //         return;
                        //     }
                        //     caster.Overhead(spell.Name, Color.Black, $"{caster} casts {{b}}{spell.Name}{{/b}}.",
                        //         spell.Name + " {icon:Reaction}",
                        //         spell.Description, spell.Traits);
                        //     QEffect hide = new("Hidebound", "You have resistance "+resistAmount+" to physical damage except adamantine.", ExpirationCondition.ExpiresAtStartOfYourTurn, caster, MIllustrations.Hidebound)
                        //     {
                        //         StateCheck = qf => qf.Owner.WeaknessAndResistance.AddSpecialResistance("physical",  (combatAction, dk) => dk.IsPhysical() && combatAction != null && !combatAction.HasTrait(Trait.Adamantine), resistAmount, "adamantine")
                        //     };
                        //     defender.AddQEffectAtPriority(hide, true);
                        //     affected = [];
                        //     foreach (KindedDamage kindedDamage in damage.KindedDamages.Where(d => d.DamageKind.IsPhysical()))
                        //     {
                        //         if (affected.Contains(kindedDamage.DamageKind)) continue;
                        //         int num1 = resistAmount;
                        //         if (defender.WeaknessAndResistance.Resistances.Count > 0 && defender.WeaknessAndResistance.Resistances.FirstOrDefault(resistance => resistance.Matches(cAction, kindedDamage.DamageKind)) is {} resist)
                        //         {
                        //             num1 -= resist.Value;
                        //             num1 = Math.Max(num1, 0);
                        //         }
                        //         int num = Math.Min(kindedDamage.ResolvedDamage, num1);
                        //         kindedDamage.ResolvedDamage -= num;
                        //         affected.Add(kindedDamage.DamageKind);
                        //     }
                        //     damage.DamageEventDescription.AppendLine($"{{b}}-{toReduce.ToString()}{{/b}} Hidebound");
                        // };
                    });
                });
        });
        ModManager.RegisterNewSpell("RE_PyrefowlRebuke", 2, (_, _, level, inCombat, _) =>
        {
            int heighten = level % 2 == 0 ? level : level - 1;
            return Spells.CreateModern(MIllustrations.PyrefowlRebuke, "Pyrefowl Rebuke", [Trait.Fire, Trait.Manipulate, Trait.Arcane, Trait.Primal, Trait.Move], 
                    "Fiery wings briefly envelop your arms, and with a swift wingbeat, you flutter away from your attacker in a shower of searing sparks.",
                    $"When a creature within 10 feet of you Strikes and deals damage to you, you may use a {{icon:Reaction}} reaction to deal {S.HeightenedVariable(heighten/2, 1)}d6 fire damage to the triggering creature, with a basic Reflex save, and Fly up to {S.HeightenedVariable(10 + 5 * ((heighten - 2)/ 2), 10)} feet in a straight line directly away from it. If the creature critically fails its saving throw, your movement does not provoke reactions from it, and it's dazzled until the end of its next turn.",
                    Target.Uncastable(), level, SpellSavingThrow.Basic(Defense.Reflex))
                .WithActionCost(-2).WithHeighteningNumerical(level, 2, inCombat, 2, "The damage increases by 1d6, and the maximum distance you can Fly increases by 5 feet.")
                .WithCastsAsAReaction((effect, spell, canCast) =>
                {
                    if (!canCast()) return;
                    Creature self = effect.Owner;
                    CombatAction fake = CombatAction.CreateSimple(self, "Fake", Trait.Manipulate, Trait.Move, Trait.Fire, Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName, Trait.Spell).WithActionCost(0);
                    fake.SpellInformation = spell.SpellInformation;
                    effect.AfterYouTakeDamageReaction = (_, damageEvent) =>
                    {
                        CombatAction? action = damageEvent.CombatAction;
                        if (action == null || !action.HasTrait(Trait.Strike) || action.Owner.DistanceTo(self) > 2 ||
                            !canCast() || !fake.CanBeginToUse(self) || !self.Alive) return null;
                        Creature enemy = action.Owner;
                        ReactionOption rebuke = ReactionOption.CreateFromSpellAsAReaction(spell,
                            $"{enemy.Name} has damaged you. Use a reaction to deal {S.HeightenedVariable(heighten / 2, 1)}d6 fire damage to the triggering creature, with a basic Reflex save, and Fly up to {S.HeightenedVariable(10 + 5 * ((heighten - 2) / 2), 10)} feet in a straight line directly away from it.",
                            async () =>
                            {
                                bool cast = await self.Battle.GameLoop.FullCast(fake);
                                if (!cast || fake.Disrupted)
                                {
                                    self.Overhead(spell.Name, Color.Black,
                                        $"{self} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                                        spell.Name + " {icon:Reaction}",
                                        spell.Description, spell.Traits);
                                    return;
                                }

                                self.Overhead(spell.Name, Color.Black, $"{self} casts {{b}}{spell.Name}{{/b}}.",
                                    spell.Name + " {icon:Reaction}",
                                    spell.Description, spell.Traits);
                                CheckResult save =
                                    await CommonSpellEffects.RollSpellSavingThrowAsync(enemy, spell, Defense.Reflex);
                                await CommonSpellEffects.DealBasicDamage(spell, self, enemy, save,
                                    DiceFormula.FromText($"{1 + (level - 2) / 2}d6", "Pyrefowl Rebuke"),
                                    DamageKind.Fire);
                                QEffect flyingEffect = QEffect.Flying();
                                self.AddQEffect(flyingEffect);
                                await self.Battle.GameLoop.StateCheck();
                                self.RegeneratePossibilities();
                                CombatAction? fly = self.Possibilities.Filter(ap =>
                                {
                                    if (ap.CombatAction.ActionId != ActionId.Stride) return false;
                                    ap.CombatAction.ActionCost = 0;
                                    ap.RecalculateUsability();
                                    return true;
                                }).CreateActions(true).FirstOrDefault() as CombatAction;
                                if (save == CheckResult.CriticalFailure)
                                {
                                    enemy.AddQEffect(QEffect.Dazzled().WithExpirationAtEndOfOwnersNextTurn());
                                    fly?.Traits.Add(Trait.DoesNotProvoke);
                                }

                                if (fly == null)
                                {
                                    flyingEffect.ExpiresAt = ExpirationCondition.Immediately;
                                    return;
                                }

                                List<Option> tileOptions =
                                [
                                    new CancelOption(true)
                                ];
                                IList<Tile> floodFill = Pathfinding.Floodfill(self, self.Battle,
                                        new PathfindingDescription
                                        {
                                            Squares = 3,
                                            Style = { MaximumSquares = 3 }
                                        })
                                    .Where(tile =>
                                        tile.LooksFreeTo(self)
                                        && tile.Kind != TileKind.Chasm
                                        && tile.Kind != TileKind.Water
                                        && tile.Kind != TileKind.Lava
                                        && IsTileAway(self.Space.CenterTile.X, self.Space.CenterTile.Y,
                                            enemy.Space.CenterTile.X, enemy.Space.CenterTile.Y, 2, tile))
                                    .ToList();
                                floodFill.ForEach(tile =>
                                {
                                    if (!(bool)fly.Target.CanBeginToUse(self)) return;
                                    tileOptions.Add(fly.CreateUseOptionOn(tile)
                                        .WithIllustration(fly.Illustration));
                                });
                                Option chosenTile = (await self.Battle.SendRequest(
                                    new AdvancedRequest(self,
                                        "Choose where to Fly to or right-click to cancel. You must end your movement on solid ground.",
                                        tileOptions)
                                    {
                                        IsMainTurn = false,
                                        IsStandardMovementRequest = true,
                                        TopBarIcon = MIllustrations.PyrefowlRebuke,
                                        TopBarText =
                                            "Choose where to Fly to or right-click to cancel. You must end your movement on solid ground.",
                                    })).ChosenOption;
                                switch (chosenTile)
                                {
                                    case CancelOption:
                                        action.RevertRequested = true;
                                        self.RemoveAllQEffects(qf => qf == flyingEffect);
                                        break;
                                    case TileOption tOpt:
                                        await tOpt.Action();
                                        self.RemoveAllQEffects(qf => qf == flyingEffect);
                                        break;
                                }
                            });
                        return rebuke;
                    };
                    // effect.AfterYouTakeDamage = async (_, _, _, action, _) =>
                    // {
                    //     if (action == null || !action.HasTrait(Trait.Strike) || action.Owner.DistanceTo(self) > 2 ||
                    //         !canCast() || !fake.CanBeginToUse(self)) return;
                    //     Creature enemy = action.Owner;
                    //     if (!await self.AskToUseReaction(
                    //             enemy.Name +
                    //             " has damaged you with a strike, use a reaction to cast {i}pyrefowl rebuke{/i} at spell level " +
                    //             level + "?", self.Illustration)) return;
                    //     self.Spellcasting!.UseUpSpellcastingResources(spell);
                    //     bool cast = await self.Battle.GameLoop.FullCast(fake);
                    //     if (!cast || fake.Disrupted)
                    //     {
                    //         self.Overhead(spell.Name, Color.Black, $"{self} attempts to cast {{b}}{spell.Name}{{/b}}, but it was disrupted!",
                    //             spell.Name + " {icon:Reaction}",
                    //             spell.Description, spell.Traits);
                    //         return;
                    //     }
                    //     self.Overhead(spell.Name, Color.Black, $"{self} casts {{b}}{spell.Name}{{/b}}.",
                    //         spell.Name + " {icon:Reaction}",
                    //         spell.Description, spell.Traits);
                    //     CheckResult save = await CommonSpellEffects.RollSpellSavingThrowAsync(enemy, spell, Defense.Reflex);
                    //     await CommonSpellEffects.DealBasicDamage(spell, self, enemy, save,
                    //         DiceFormula.FromText($"{1 + (level - 2) / 2}d6", "Pyrefowl Rebuke"), DamageKind.Fire);
                    //     QEffect flyingEffect = QEffect.Flying();
                    //     self.AddQEffect(flyingEffect);
                    //     await self.Battle.GameLoop.StateCheck();
                    //     CombatAction? fly = self.Possibilities.Filter(ap =>
                    //     {
                    //         if (ap.CombatAction.ActionId != ActionId.Stride) return false;
                    //         ap.CombatAction.ActionCost = 0;
                    //         ap.RecalculateUsability();
                    //         return true;
                    //     }).CreateActions(true).FirstOrDefault() as CombatAction;
                    //     if (save == CheckResult.CriticalFailure)
                    //     {
                    //         enemy.AddQEffect(QEffect.Dazzled().WithExpirationAtEndOfOwnersNextTurn());
                    //         fly?.Traits.Add(Trait.DoesNotProvoke);
                    //     }
                    //     if (fly == null)
                    //     {
                    //         flyingEffect.ExpiresAt = ExpirationCondition.Immediately;
                    //         return;
                    //     }
                    //     List<Option> tileOptions =
                    //     [
                    //         new CancelOption(true)
                    //     ];
                    //     IList<Tile> floodFill = Pathfinding.Floodfill(self, self.Battle,
                    //             new PathfindingDescription
                    //             {
                    //                 Squares = 3,
                    //                 Style = { MaximumSquares = 3 }
                    //             })
                    //         .Where(tile =>
                    //             tile.LooksFreeTo(self) 
                    //             && tile.Kind != TileKind.Chasm
                    //             && tile.Kind != TileKind.Water
                    //             && tile.Kind != TileKind.Lava
                    //             && IsTileAway(self.Space.CenterTile.X, self.Space.CenterTile.Y, enemy.Space.CenterTile.X, enemy.Space.CenterTile.Y, 2, tile))
                    //         .ToList();
                    //     floodFill.ForEach(tile =>
                    //     {
                    //         if (!(bool)fly.Target.CanBeginToUse(self)) return;
                    //         tileOptions.Add(fly.CreateUseOptionOn(tile)
                    //             .WithIllustration(fly.Illustration));
                    //     });
                    //     Option chosenTile = (await self.Battle.SendRequest(
                    //         new AdvancedRequest(self,
                    //             "Choose where to Fly to or right-click to cancel. You must end your movement on solid ground.",
                    //             tileOptions)
                    //         {
                    //             IsMainTurn = false,
                    //             IsStandardMovementRequest = true,
                    //             TopBarIcon = MIllustrations.PyrefowlRebuke,
                    //             TopBarText =
                    //                 "Choose where to Fly to or right-click to cancel. You must end your movement on solid ground.",
                    //         })).ChosenOption;
                    //     switch (chosenTile)
                    //     {
                    //         case CancelOption:
                    //             action.RevertRequested = true;
                    //             self.RemoveAllQEffects(qf => qf == flyingEffect);
                    //             break;
                    //         case TileOption tOpt:
                    //             await tOpt.Action();
                    //             self.RemoveAllQEffects(qf => qf == flyingEffect);
                    //             break;
                    //     }
                    // };
                });
        });
        ModManager.RegisterNewSpell("RE_StickyFire", 2, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.StickyFire, "Sticky Fire",
                    [Trait.Attack, Trait.Concentrate, Trait.Fire, Trait.Manipulate, Trait.Arcane, Trait.Primal],
                    "You send a bubble of viscous liquid that sparks into flame.",
                    $"Make a spell attack roll against the target's AC, dealing {(inCombat ? $"{{blue}}{1 + (level - 2) / 2}d8{{/blue}}" : "1d8")} fire damage and {(inCombat ? $"{{blue}}{1 + (level - 2) / 2}d8{{/blue}}" : "1d8")} persistent fire damage on a hit. The target is enfeebled 1 until they recover from their persistent fire damage.",
                    Target.Ranged(12), level, null)
                .WithHeighteningNumerical(level, 2, inCombat, 2, "The initial and persistent fire damage increase by 1d8.")
                .WithSoundEffect(SfxName.FireRay).WithActionCost(2).WithSpellAttackRoll().WithProjectileCone(MIllustrations.StickyFire, 1, ProjectileKind.Arrow)
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    if (result <= CheckResult.Failure) return;
                    QEffect enfeeble = QEffect.Enfeebled(1);
                    QEffect stickyFire = QEffect.PersistentDamage(
                        DiceFormula.FromText($"{(result == CheckResult.CriticalSuccess ? (1 + (level - 2) / 2) * 2 : 1 + (level - 2) / 2)}d8", "Sticky Fire"), DamageKind.Fire);
                    stickyFire.WhenExpires = _ =>
                    {
                        enfeeble.ExpiresAt = ExpirationCondition.Immediately;
                    };
                    stickyFire.Illustration = spell.Illustration;
                    stickyFire.Name = spell.Name;
                    await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result,
                        DiceFormula.FromText($"{1 + (level - 2) / 2}d8", "Sticky Fire"), DamageKind.Fire);
                    target.AddQEffect(stickyFire);
                    target.AddQEffect(enfeeble);
                });
        });
        ModManager.RegisterNewSpell("RE_AxesOfLegend", 2, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(new ModdedIllustration("PMAssets/AxesOfLegend.png"), "Axes of Legend", [Trait.Auditory, Trait.Concentrate, Trait.Linguistic, Trait.Manipulate, Trait.Arcane, Trait.Occult, Trait.NoHeightening],
                    "You spin a tale of the legendary axes of a mighty warrior, inspiring an ally to greater heights.",
                    "{icon:Action} The target gains a +1 status bonus to attack rolls for the rest of the encounter.\n\n" +
                    "{icon:TwoActions} Until the end of the encounter, when the target is damaged by a creature's attack, the target gains a +2 circumstance bonus to damage against that creature for 1 round.",
                    Target.DependsOnActionsSpent(Target.RangedFriend(6), Target.RangedFriend(6), null!).WithOverriddenFullTargetLine("{b}Range{/b} 30 feet\n{b}Target{/b} 1 ally or self"), level, null)
                .WithActionCost(-3)
                .WithSoundEffect(SfxName.Bless).WithEffectOnEachTarget((spell, caster, target, _) =>
                {
                    switch (spell.SpentActions)
                    {
                        case 1:
                            target.AddQEffect(new QEffect("Axes of Legend", "You have a +1 status bonus to attack rolls.", ExpirationCondition.Never, caster, spell.Illustration)
                            {
                                BonusToAttackRolls = (_, action, _) => action.HasTrait(Trait.Strike) || (action.HasTrait(Trait.Attack) && action.SpellInformation != null) ? new Bonus(1, BonusType.Status, nameof(spell), true) : null
                            });
                            break;
                        case 2:
                            target.AddQEffect(new QEffect("Axes of Legend", "When you are damaged by a creature's attack, you gain a +2 circumstance bonus to damage against that creature for 1 round.", ExpirationCondition.Never, caster, spell.Illustration)
                            {
                                AfterYouTakeDamage = (effect, _, _, action, _) =>
                                {
                                    if (action == null || !action.HasTrait(Trait.Attack)) return Task.CompletedTask;
                                    Creature self = effect.Owner;
                                    Creature enemy = action.Owner;
                                    if (enemy.HasTrait(Trait.Pseudocreature)) return Task.CompletedTask;
                                    self.AddQEffect(new QEffect($"Axes against {enemy.Name}",
                                        $"You have a +2 circumstance bonus to damage against {enemy.Name}.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, enemy, enemy.Illustration)
                                    {
                                        BonusToDamage = (_, _, defender) => defender != enemy ? null : new Bonus(2, BonusType.Circumstance, nameof(spell), true),
                                        Key = enemy.Name
                                    });
                                    return Task.CompletedTask;
                                }
                            });
                            break;
                    }
                    return Task.CompletedTask;
                });
        });
        ModManager.RegisterNewSpell("RE_QueensRainbow", 2, (_, _, level, _, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Rainbow"), "The Queen's Rainbow", [Trait.Auditory, Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Occult, Trait.Primal, Trait.NoHeightening, Trait.DoesNotRequireAttackRollOrSavingThrow],
                    "You conjure forth a large, transparent rainbow.",
                    "Creatures who enter or begin their turn in the rainbow's space must succeed at a Fortitude saving throw or become dazzled for 1 round (or blinded for 1 round on a critical failure).",
                    Target.Line(12), level, null)
                .WithActionCost(3).WithSoundEffect(SfxName.PowerfulLight)
                .WithProjectileCone(VfxStyle.NoAnimation())
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    Zone rainbow = Zone.Spawn(caster, ZoneAttachment.StableBurst(targets.ChosenTiles));
                    rainbow.AfterCreatureBeginsItsTurnHere = async creature =>
                    {
                        CheckResult save = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Fortitude);
                        if (save == CheckResult.Failure)
                            creature.AddQEffect(QEffect.Dazzled().WithExpirationAtStartOfOwnerTurn()
                                .WithCannotExpireThisTurn());
                        else if (save == CheckResult.CriticalFailure)
                            creature.AddQEffect(QEffect.Blinded()
                                .WithExpirationAtStartOfOwnerTurn().WithCannotExpireThisTurn());
                    };
                    rainbow.AfterCreatureEnters = async creature =>
                    {
                        CheckResult save = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Fortitude);
                        if (save == CheckResult.Failure)
                            creature.AddQEffect(QEffect.Dazzled().WithExpirationAtStartOfOwnerTurn()
                                .WithCannotExpireThisTurn());
                        else if (save == CheckResult.CriticalFailure)
                            creature.AddQEffect(QEffect.Blinded()
                                .WithExpirationAtStartOfOwnerTurn().WithCannotExpireThisTurn());
                    };
                    rainbow.TileEffectCreator = _ => new TileQEffect
                    {
                        TransformsTileIntoHazardousTerrain = true,
                        Illustration = MIllustrations.CreateIllustration("Rainbow")
                    };
                    rainbow.Apply();
                });
        });
        AnimatedAssault = ModManager.RegisterNewSpell("RE_AnimatedAssault", 2, (_, _, level, inCombat, _) =>
        {
            int heighten = level % 2 == 0 ? level : level - 1; 
            return Spells.CreateModern(IllustrationName.PainfulVibrations3, "Animated Assault",
                    [Trait.Arcane, Trait.Occult, Trait.SpellWithDuration],
                    "You use your mind to manipulate unattended objects in the area, temporarily animating them to attack. The objects hover in the air, then hurl themselves at nearby creatures in a chaotic flurry of debris.",
                    $"Creatures in the area take {S.HeightenedVariable(heighten, 2)}d10 bludgeoning damage with a basic Reflex save. On subsequent rounds, the first time each round you Sustain this spell, it deals {S.HeightenedVariable(heighten/2, 1)}d10 bludgeoning damage (basic Reflex save) to each creature in the area.",
                    Target.Burst(24, 2), level, SpellSavingThrow.Basic(Defense.Reflex))
                .WithSoundEffect(SfxName.ElementalBlastWood)
                .WithHeighteningNumerical(level, 2, inCombat, 2, "The initial damage increases by 2d10, and the subsequent damage increases by 1d10.")
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    CheckResult result = spell.CheckResult;
                    foreach (Creature creature in targets.ChosenCreatures)
                    {
                        await CommonSpellEffects.DealBasicDamage(spell, caster, creature, result, DiceFormula.FromText($"{heighten}d10", "Animated Assault"), DamageKind.Bludgeoning);
                    }
                    Zone assault = Zone.Spawn(caster, ZoneAttachment.StableBurst(targets.ChosenTiles));
                    assault.TileEffectCreator = _ => new TileQEffect
                    {
                        Illustration = new Illustration[]
                        {
                            IllustrationName.Rubble,
                            IllustrationName.Rubble2,
                            IllustrationName.Rubble3,
                            IllustrationName.Rubble4,
                        }.GetRandomVisualOnly()
                    };
                    assault.ApplySustainment(spell, async _ =>
                    {
                        foreach (Creature creature in assault.CreaturesInZone)
                        {
                            CheckResult result2 = await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Reflex);
                            await CommonSpellEffects.DealBasicDamage(spell, caster, creature, result2,
                                DiceFormula.FromText($"{heighten / 2}d10", "Animated Assault"), DamageKind.Bludgeoning);
                        }
                    }, $"Sustain the spell to continue the duration and deal {heighten/2}d10 damage to all creatures in the area.");

                });

        });
    }
}