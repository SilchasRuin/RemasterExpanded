using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using SpiritDamage;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

public class NewItems
{
    public static ItemName AutoloadLeathers { get; set; }
    // public static ItemName GlueBombLesser { get; set; }
    // public static ItemName GlueBombModerate { get; set; }
    
    public static void LoadItems()
    {
        ModManager.RegisterNewItemIntoTheShop("RE_TrustyHelmet", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/TrustyHelmet.png"), "trusty helmet", 2, 30, Trait.Worn, Trait.Invested, Trait.Magical)
                .WithDescription("{i}You keep yourself protected from incoming projectiles with this sturdy steel helmet, painted brown.{/i}" +
                                 "\n\nYou gain the following actions:" +
                                 "\n\n{b}Block Manipulation{/b} {icon:Reaction} (concentrate); Once per day as a reaction {icon:Reaction}, when you gain the stupefied condition, you can decrease the value of your stupefied condition by 1." +
                                 "\n\n{b}Hunker Down{/b} {icon:Action} (manipulate); You gain a +1 circumstance bonus to your AC against ranged attacks until the start of your next turn.")
                .WithWornAt(Trait.Headwear).WithItemAction((item, creature) =>
                    {
                        return new CombatAction(creature, item.Illustration, "Hunker Down", [Trait.Manipulate, Trait.Basic], "{i}You hunker down, protecting your head using your helmet.{/i}" +
                                "\n\nYou gain a +1 circumstance bonus to your AC against ranged attacks until the start of your next turn.",
                                Target.Self())
                            .WithActionCost(1)
                            .WithSoundEffect(SfxName.RaiseShield)
                            .WithEffectOnChosenTargets((self, _) =>
                            {
                                self.AddQEffect(new QEffect("Hunker Down", "You have a +1 circumstance bonus to your AC against ranged attacks.", ExpirationCondition.ExpiresAtStartOfYourTurn, self, item.Illustration)
                                {
                                    BonusToDefenses = (_, action, defense) =>
                                    {
                                        if (defense != Defense.AC || action == null || !action.HasTrait(Trait.Ranged))
                                            return null;
                                        return new Bonus(1, BonusType.Circumstance, "Hunker Down", true);
                                    }
                                });
                                return Task.CompletedTask;
                            });
                    },
                    (_, _) => true)
                .WithPermanentQEffectWhenWorn((effect, item) =>
                {
                    Creature self = effect.Owner;
                    effect.StateCheckWithVisibleChanges = async _ =>
                    {
                        if (self.PersistentUsedUpResources.UsedUpActions.Contains("BlockManipulation")) return;
                        CombatAction simpleConcentrate = CombatAction.CreateSimple(self, "fake").WithActionCost(0);
                        simpleConcentrate.Traits.Add(Trait.Concentrate);
                        if (!simpleConcentrate.CanBeginToUse(self) || self.Actions.IsReactionUsedUp) return;
                        if (self.Battle.GameLoop.QEffectsAcquiredSinceLastStateCheck.FirstOrDefault(qf => qf.Id == QEffectId.Stupefied) is not {} stupefied|| !self.HasEffect(QEffectId.Stupefied)) return;
                        if (!await self.Battle.AskToUseReaction(self,
                                $"You just became stupefied {stupefied.Value}, would you like to use your Trusty Helmet's Block Manipulation {{icon:Reaction}} to reduce that by 1?",
                                item.Illustration, () => false, [Trait.Concentrate])) return;
                        switch (stupefied.Value)
                        {
                            case > 1:
                                stupefied.Value -= 1;
                                break;
                            case 1:
                                self.RemoveAllQEffects(qff => qff == stupefied);
                                break;
                        }
                        self.PersistentUsedUpResources.UsedUpActions.Add("BlockManipulation");
                    };
                });
            

        });
        AutoloadLeathers = ModManager.RegisterNewItemIntoTheShop("RE_AutoloadLeathers", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/AutoloadLeathers.png"), "autoload leathers", 9, 700, Trait.Armor, Trait.LightArmor, Trait.Leather, Trait.Magical, Trait.Invested, Trait.DoNotAddToShop)
                .WithArmorProperties(new ArmorProperties(2, 3, -1, 0, 12))
                .WithDescription("{i}This studded leather armor has a built in ammunition bandolier that, once set up, can be used to almost instantaneously reload a weapon.{/i}" +
                                 "\n\n{b}Autoload{/b} {icon:FreeAction} (manipulate); Once per day, you reload a weapon with reload 1 as a free action {icon:FreeAction}.")
                .WithOncePerDayWhenWornAction((item, self) =>
                {
                    if (!self.HeldItems.Any(weapon => weapon.EphemeralItemProperties.NeedsReload && weapon.HasTrait(Trait.Reload1))) return null;
                    return new CombatAction(self, item.Illustration, "Autoload", [Trait.Manipulate, Trait.Basic], "Once per day, you reload a weapon with reload 1 as a free action.", Target.Self())
                        .WithActionCost(0)
                        .WithEffectOnChosenTargets(async (_, _) =>
                        {
                            Possibilities possibles = self.Possibilities.Filter(ap =>
                            {
                                if (ap.CombatAction.ActionId != ActionId.Reload) return false;
                                ap.CombatAction.WithActionCost(0);
                                ap.RecalculateUsability();
                                return true;
                            });
                            List<Option> options = await self.Battle.GameLoop.CreateActions(self, possibles, null);
                            await self.Battle.GameLoop.OfferOptions(self, options, true);
                        });
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_SquiresTabard", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/SquiresTabard.png"), "squire's tabard", 2, 25,
                    Trait.Invested, Trait.Magical, Trait.Worn)
                .WithWornAt(MTraits.Garment) 
                .WithDescription("{i}Squires with aspirations of being knights wear these loose, colorful tunics, typically emblazoned with the crest of the knight or kingdom they serve.{/i}" +
                                 "\n\n{b}At Your Aid{/b} {icon:Action} (concentrate); Once per day, you race to the side of an ally who needs your help. You Stride twice, ignoring difficult terrain, but your movement must end adjacent to an ally.")
                .WithOncePerDayWhenWornAction((item, creature) =>
                {
                    return new CombatAction(creature, item.Illustration, "At Your Aid", [Trait.Concentrate],
                            "You Stride twice, ignoring difficult terrain, but your movement must end adjacent to an ally.",
                            Target.RangedFriend(creature.Speed * 2)
                                .WithAdditionalConditionOnTargetCreature((cr1, cr2) =>
                                    cr1 != cr2
                                        ? Usability.Usable
                                        : Usability.NotUsableOnThisCreature("You cannot target yourself.")))
                        .WithActionCost(1)
                        .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                        {
                            Creature friend = targets.ChosenCreatures[0];
                            QEffect ignore = new()
                            {
                                Id = QEffectId.IgnoresDifficultTerrain
                            };
                            caster.AddQEffect(ignore);
                            if (!await caster.StrideAsync("Stride towards an ally. You must end adjacent to an ally.",
                                    allowCancel: true, strideTowards: friend.Space.CenterTile))
                            {
                                ignore.ExpiresAt = ExpirationCondition.Immediately;
                                spell.RevertRequested = true;
                                return;
                            }
                            List<Option> tileOptions = [];
                            CombatAction? moveAction = (caster.Possibilities
                                    .Filter(ap =>
                                    {
                                        if (ap.CombatAction.ActionId != ActionId.Stride)
                                            return false;
                                        ap.CombatAction.ActionCost = 0;
                                        ap.RecalculateUsability();
                                        return true;
                                    })
                                    .CreateActions(true)
                                    .FirstOrDefault(pw => pw.Action.ActionId == ActionId.Stride) as CombatAction)
                                ?.WithActionCost(0);
                            List<Tile> floodFill = Pathfinding.Floodfill(caster, caster.Battle,
                                    new PathfindingDescription
                                    {
                                        Squares = caster.Speed,
                                        Style =
                                        {
                                            PermitsStep = false
                                        }
                                    })
                                .Where(tile => tile.IsAdjacentTo(friend))
                                .ToList();
                            floodFill.ForEach(tile =>
                            {
                                if (moveAction == null || !(bool)moveAction.Target.CanBeginToUse(caster)) return;
                                tileOptions.Add(moveAction.CreateUseOptionOn(tile)
                                    .WithIllustration(moveAction.Illustration));
                            });
                            Option move = (await caster.Battle.SendRequest(
                                new AdvancedRequest(caster,
                                    "Stride towards an ally. You must end adjacent to an ally.",
                                    tileOptions)
                                {
                                    IsMainTurn = true,
                                    IsStandardMovementRequest = true,
                                    TopBarIcon = caster.Illustration,
                                    TopBarText = "Stride towards an ally. You must end adjacent to an ally."
                                })).ChosenOption;
                            if (move is not TileOption tileOption) return;
                            await caster.StrideAsync("Stride towards an ally. You must end adjacent to an ally.",
                                strideTowards: tileOption.Tile);
                            ignore.ExpiresAt = ExpirationCondition.Immediately;
                        });
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_GoldenGreaves", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/GoldenGreaves.png"), "golden greaves", 4, 80, Trait.Invested, Trait.Magical, Trait.Worn)
                .WithDescription("{i}These shiny greaves made of splinted metal coated with gold protect the shins and help you stand your ground in the heat of battle.{/i}" +
                                 "\n\nWhile wearing the greaves, you gain a +1 item bonus to your Fortitude DC against forced movement effects and to your Reflex DC against effects that would knock you prone." +
                                 "\n\n{b}Make Them Fall{/b} {icon:Reaction} (concentrate, misfortune); Once per day when an enemy fails to Reposition, Shove, or Trip you; your opponent instead critically fails on the triggering check.")
                .WithPermanentQEffectWhenWorn((effect, item) =>
                {
                    effect.BonusToDefenses = (_, action, defense) =>
                    {
                        if (action == null || (!action.Description.ContainsIgnoreCase("forced movement") && !action.Description.ContainsIgnoreCase("shove") && !action.HasTrait(Trait.Trip) && action.ActionId != ActionId.Trip && action.ActionId != ActionId.Shove && !action.Description.Contains("prone") && !action.Description.ContainsIgnoreCase("reposition"))) return null;
                        if (defense != Defense.Reflex && defense != Defense.Fortitude) return null;
                        return new Bonus(1, BonusType.Item, "Golden Greaves", true);
                    };
                    effect.AddGrantingOfTechnical(cr => cr.EnemyOf(effect.Owner), qfTech =>
                    {
                        qfTech.RerollActiveRoll = async (_, result, action, creature) =>
                        {
                            if ((action.ActionId != ActionId.Shove && action.ActionId != ActionId.Trip &&
                                 (!ModManager.TryParse("FC_Reposition", out ActionId reposition) ||
                                  action.ActionId != reposition)) || effect.Owner != creature || effect.Owner.PersistentUsedUpResources.UsedUpActions.Contains("MakeThemFall") 
                                || result.CheckResult != CheckResult.Failure || result.MisfortuneEffectUsed) return RerollDirection.DoNothing;
                            bool confirm = await creature.AskToUseReaction(qfTech.Owner.Name+"rolled a failure on their attempt to "+action.Name+" vs you. Use a reaction to turn that failure into a critical failure?", item.Illustration);
                            if (!confirm) return RerollDirection.DoNothing;
                            qfTech.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                            {
                                AdjustActiveRollCheckResult = (_, _, _, _) => CheckResult.CriticalFailure
                            });
                            result.MisfortuneEffectUsed = true;
                            IEnumerable<Trait> traits = [Trait.Concentrate, Trait.Misfortune];
                            creature.PersistentUsedUpResources.UsedUpActions.Add("MakeThemFall");
                            creature.Overhead("Make Them Fall", Color.White, creature.Name + " uses {b}Make Them Fall {icon: Reaction}", "{b}Make Them Fall {icon: Reaction}", 
                                "When your opponent fails to Reposition, Shove, or Trip you, your opponent critically fails instead.", new Traits(traits));
                            return RerollDirection.KeepRollButRedoCalculation;
                        };
                    });
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_GrippyGloves", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/GrippyGloves.png"), "grippy gloves", 4, 90, Trait.Magical, Trait.Invested, Trait.Worn)
                .WithDescription("{i}A good pair of gloves is a critical item for any soldier, particularly if you plan to get up close to your enemy.{/i}" +
                                 $"\n\nYou gain a +1 item bonus to Athletics checks to Grapple{(ModManager.TryParse("FC_Reposition", out ActionId _) ? " and Reposition" : "")}." +
                                 "\n\n{b}Sticky Grip{/b} {icon:Action} (manipulate); Once per day, while you have an enemy grabbed or restrained, that enemy becomes slowed 1 for 1 round.")
                .WithPermanentQEffectWhenWorn((effect, _) =>
                {
                    effect.BonusToSkillChecks = (skill, action, _) =>
                    {
                        if (skill != Skill.Athletics || action.ActionId != ActionId.Grapple ||
                            (ModManager.TryParse("FC_Reposition", out ActionId reposition) &&
                             action.ActionId != reposition)) return null;
                        return new Bonus(1, BonusType.Item, "Grippy Gloves", true);
                    };
                })
                .WithOncePerDayWhenWornAction((item, creature) =>
                {
                    return new CombatAction(creature, item.Illustration, "Sticky Grip", [Trait.Manipulate],
                            "Once per day, while you have an enemy grabbed or restrained, that enemy becomes slowed 1 for 1 round.", 
                            Target.AdjacentCreature().WithAdditionalConditionOnTargetCreature((creature1, creature2) => creature1.HeldItems.Any(grapplee => grapplee.HasTrait(Trait.Grapplee) && grapplee.Grapplee == creature2) ? Usability.Usable : Usability.NotUsableOnThisCreature("You must have an enemy grabbed or restrained.")))
                        .WithActionCost(1)
                        .WithEffectOnEachTarget((_, _, target, _) =>
                        {
                            target.AddQEffect(QEffect.Slowed(1).WithExpirationAtEndOfOwnerTurn());
                            return Task.CompletedTask;
                        });
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_GriffonMedal", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/GriffonMedal.png"), "medal of griffon's heart", 4, 80, Trait.Magical, Trait.Invested, Trait.Worn)
                .WithDescription("{i}This copper medal features a griffon's face, wings, and talons in profile.{/i}" +
                                 "\n\nYou gain a +1 item bonus to saving throws against fear and mental effects. While wearing this pendant, you can also cast {i}forbidding ward {icon:TwoActions}{/i} as an innate cantrip.")
                .WithPermanentQEffectWhenWorn((effect, _) =>
                {
                    effect.BonusToDefenses = (_, action, _) =>
                    {
                        if (action == null || (!action.HasTrait(Trait.Fear) && !action.HasTrait(Trait.Mental))) return null;
                        return new Bonus(1, BonusType.Item, "Griffon Medal", true);
                    };
                })
                .WithOnCreatureWhenWorn((_, self) =>
                {
                    self.GetOrCreateSpellcastingSource(SpellcastingKind.Innate, Trait.Innate, Ability.Charisma,
                        Trait.Divine).WithSpells([SpellId.ForbiddingWard], 1);
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_WolfMedal", name =>
        {
            return new Item(name, new ModdedIllustration("PMAssets/WolfMedal.png"), "medal of the wolf pack", 7, 350,  Trait.Magical, Trait.Invested, Trait.Worn)
                .WithDescription("{i}There are three wolf's heads engraved on this pewter medal, typically awarded to squads who demonstrate exceptional teamwork.{/i}" +
                                 "\n\nWhile wearing the medal of the wolf pack, you gain a +2 circumstance bonus to damage rolls against enemies you are flanking.")
                .WithPermanentQEffectWhenWorn((effect, _) =>
                {
                    effect.BonusToDamage = (_, action, target) =>
                    {
                        Creature self = effect.Owner;
                        if (!self.Battle.AllCreatures.Any(cr => cr.FriendOf(self) && FlankingRules.IsFlanking(self, target, cr)) && target.QEffects.All(qf => qf.IsFlatFootedTo?.Invoke(qf, self, action) != "flanking")) return null;
                        return new Bonus(2, BonusType.Circumstance, "Medal of the Wolf Pack", true);
                    };
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_AstralRune", name =>
        {
            return new Item(name, MIllustrations.CreateIllustration("AstralRune"), "runestone of {i}astral{/i}", 8, 450, Trait.Runestone, Trait.Magical, SpiritTrait.Spirit)
                .WithItemGreaterGroup(ItemGreaterGroup.PropertyRunes)
                .WithRuneProperties(new RuneProperties("astral", RuneKind.WeaponProperty, "The enchanted weapon is empowered by powerful spiritual energy.", 
                    "This weapon deals an extra 1d6 spirit damage, and bypasses the damage resistance of incorporeal creatures.",
                    item =>
                    {
                        item.Traits.Add(Trait.GhostTouch);
                        item.WithAdditionalWeaponProperties(properties =>
                        {
                            properties.WithAdditionalDamage("1d6", ModData.SpiritDamage);
                        });
                    }));
        });
        #region removed
        // GlueBombLesser = ModManager.RegisterNewItemIntoTheShop("RE_GlueBombLesser", name =>
        // {
        //     Item bomb = new(name, MIllustrations.GlueBomb, "glue bomb (lesser)", 1, 3, Trait.Alchemical,
        //         Trait.Consumable, Trait.Thrown, Trait.Martial)
        //     {
        //         Description = "{b}Range increment{/b} 20 feet" +
        //                       "\n\n{i}A glue bomb is a harmless explosive mechanism bursting with sticky substances.{/i}" +
        //                       "\n\nWhen you hit a creature with a glue bomb, that creature takes a 10-foot status penalty to its Speeds for 1 minute." +
        //                       "\nOn a critical hit, a creature becomes immobilized for 1 round. Glue bombs aren't effective when used on a creature that's submerged in water.\nThe target can end any effects by Escaping with a DC of 17.",
        //         ProvidesItemAction = (creature, item) =>
        //         {
        //             Item weapon = new(item.Illustration, item.Name, item.Traits.ToArray());
        //             weapon.WithWeaponProperties(new WeaponProperties("0d0", DamageKind.Untyped).WithRangeIncrement(4));
        //             CombatAction strike = creature.CreateStrike(weapon);
        //             strike.Description = strike.Description.Replace("You deal 00 untyped damage.", "Your target takes a 10-foot status penalty to its Speeds for 1 minute.");
        //             strike.Description = strike.Description.Replace("Double damage.", "As success, but your target is also immobilized for 1 round.");
        //             strike.EffectOnChosenTargets = null;
        //             strike.EffectOnOneTarget = null;
        //             strike.WithEffectOnEachTarget((_, caster, target, result) =>
        //             {
        //                 caster.HeldItems.Remove(item);
        //                 if (result <= CheckResult.Failure ||
        //                     target.Space.Tiles.All(tile => tile.Kind == TileKind.Water) ||
        //                     target.HasEffect(QEffectId.AquaticCombat)) return Task.CompletedTask;
        //                 QEffect glueBomb = result == CheckResult.CriticalSuccess
        //                     ? QEffect.Immobilized()
        //                     : QEffect.StatusSpeedReduction(2);
        //                 if (result == CheckResult.CriticalSuccess)
        //                 {
        //                     glueBomb.WithExpirationAtStartOfSourcesTurn(caster, 1);
        //                     glueBomb.WhenExpires = qf =>
        //                     {
        //                         if (qf.Owner.Actions.ActionHistoryThisTurn.Count > 0 &&
        //                             qf.Owner.Actions.ActionHistoryThisTurn.Last().ActionId == ActionId.Escape)
        //                             return;
        //                         QEffect newGlue = QEffect.StatusSpeedReduction(2)
        //                             .WithExpirationAtStartOfSourcesTurn(caster, 9);
        //                         newGlue.ProvideContextualAction = effect =>
        //                         {
        //                             Creature owner = effect.Owner;
        //                             return new ActionPossibility(
        //                                 Possibilities.CreateEscapeAgainstEffect(owner, newGlue,
        //                                     "Glue Bomb (lesser)", 17));
        //                         };
        //                         qf.Owner.AddQEffect(newGlue);
        //                     };
        //                 }
        //                 else
        //                 {
        //                     glueBomb.WithExpirationAtStartOfSourcesTurn(caster, 10);
        //                 }
        //
        //                 glueBomb.ProvideContextualAction = qf =>
        //                 {
        //                     Creature owner = qf.Owner;
        //                     return new ActionPossibility(
        //                         Possibilities.CreateEscapeAgainstEffect(owner, glueBomb, "Glue Bomb (lesser)",
        //                             17));
        //                 };
        //                 target.AddQEffect(glueBomb);
        //                 return Task.CompletedTask;
        //             });
        //             return new ActionPossibility(strike);
        //         }
        //     };
        //     bomb.WithItemGroup("Lesser bombs").WithItemGreaterGroup(ItemGreaterGroup.Bombs);
        //     return bomb;
        // });
        // GlueBombModerate = ModManager.RegisterNewItemIntoTheShop("RE_GlueBombModerate", name =>
        // {
        //     Item bomb = new(name, MIllustrations.GlueBomb, "glue bomb (moderate)", 3, 10, Trait.Alchemical, Trait.Consumable, Trait.Thrown, Trait.Martial)
        //     {
        //         Description = "{b}Range increment{/b} 20 feet" +
        //                       "\n\n{i}A glue bomb is a harmless explosive mechanism bursting with sticky substances.{/i}" +
        //                       "\n\nYou have a +1 item bonus to attack rolls and when you hit a creature with a glue bomb, that creature takes a 15-foot status penalty to its Speeds for 1 minute." +
        //                       "\nOn a critical hit, a creature becomes immobilized for 1 round. Glue bombs aren't effective when used on a creature that's submerged in water.\nThe target can end any effects by Escaping with a DC of 19.",
        //         ProvidesItemAction = (creature, item) =>
        //         {
        //             Item weapon = new(item.Illustration, item.Name, item.Traits.ToArray());
        //             weapon.WithWeaponProperties(new WeaponProperties("0d0", DamageKind.Untyped).WithRangeIncrement(4).WithItemBonus(1));
        //             CombatAction strike = creature.CreateStrike(weapon);
        //             strike.Description = strike.Description.Replace("You deal 00 untyped damage.", "Your target takes a 15-foot status penalty to its Speeds for 1 minute.");
        //             strike.Description = strike.Description.Replace("Double damage.", "As success, but your target is also immobilized for 1 round.");
        //             strike.EffectOnChosenTargets = null;
        //             strike.EffectOnOneTarget = null;
        //             strike.WithEffectOnEachTarget((_, caster, target, result) =>
        //             {
        //                 caster.HeldItems.Remove(item);
        //                 if (result <= CheckResult.Failure ||
        //                     target.Space.Tiles.All(tile => tile.Kind == TileKind.Water) ||
        //                     target.HasEffect(QEffectId.AquaticCombat)) return Task.CompletedTask;
        //                 QEffect glueBomb = result == CheckResult.CriticalSuccess
        //                     ? QEffect.Immobilized()
        //                     : QEffect.StatusSpeedReduction(3);
        //                 if (result == CheckResult.CriticalSuccess)
        //                 {
        //                     glueBomb.WithExpirationAtStartOfSourcesTurn(caster, 1);
        //                     glueBomb.WhenExpires = qf =>
        //                     {
        //                         if (qf.Owner.Actions.ActionHistoryThisTurn.Count > 0 &&
        //                             qf.Owner.Actions.ActionHistoryThisTurn.Last().ActionId == ActionId.Escape)
        //                             return;
        //                         QEffect newGlue = QEffect.StatusSpeedReduction(3)
        //                             .WithExpirationAtStartOfSourcesTurn(caster, 9);
        //                         newGlue.ProvideContextualAction = effect =>
        //                         {
        //                             Creature owner = effect.Owner;
        //                             return new ActionPossibility(
        //                                 Possibilities.CreateEscapeAgainstEffect(owner, newGlue,
        //                                     "Glue Bomb (moderate)", 19));
        //                         };
        //                         qf.Owner.AddQEffect(newGlue);
        //                     };
        //                 }
        //                 else
        //                 {
        //                     glueBomb.WithExpirationAtStartOfSourcesTurn(caster, 10);
        //                 }
        //
        //                 glueBomb.ProvideContextualAction = qf =>
        //                 {
        //                     Creature owner = qf.Owner;
        //                     return new ActionPossibility(
        //                         Possibilities.CreateEscapeAgainstEffect(owner, glueBomb, "Glue Bomb (moderate)",
        //                             19));
        //                 };
        //                 target.AddQEffect(glueBomb);
        //                 return Task.CompletedTask;
        //             });
        //             return new ActionPossibility(strike);
        //         }
        //     };
        //     bomb.WithItemGroup("Moderate bombs");
        //     return bomb;
        // });
        // ModManager.RegisterNewItemIntoTheShop("RE_DoomSwitch", name =>
        // {
        //     Item item = new Item(name, new ModdedIllustration("PMAssets/DoomSwitch.png"), "doom switch", 3, 50,
        //             Trait.Magical, Trait.OneHanded, Trait.Simple, Trait.SpecificMagicWeapon, Trait.Nonlethal)
        //         .WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Bludgeoning))
        //         .WithDescription(
        //             "{i}This short length of wood is decorated with fine carvings of symbols representing fate.{/i}" +
        //             "\n\nBragging Rights {icon:Action} (attack, manipulate); Once per day you may attempt to Strike an enemy with the doom switch, marking them for defeat. The switch is treated as a simple melee weapon for the purpose of proficiency. This attack deals no damage."
        //             + S.FourDegreesOfSuccess(
        //                 "You and your allies gain a +1 status bonus to attack rolls against the target for 1 minute. If you reduce the target to 0 Hit Points during this time, you gain temporary Hit Points equal to twice the target's level for 1 round.",
        //                 "As critical success, except you gain temporary Hit Points equal to the target's level.",
        //                 "You and your allies take a –1 status penalty to attack rolls against the target for 1 round.",
        //                 "Critical Failure You and your allies take a –2 status penalty to attack rolls against the target for 1 round."))
        //         .WithModification(new ItemModification(ItemModificationKind.CustomPermanent));
        //     item.StateCheckWhenWielded = (self, item1) =>
        //     {
        //         QEffect effect = new(ExpirationCondition.Ephemeral)
        //         {
        //             ProvideStrikeModifier = item2 =>
        //             {
        //                 if (self.PersistentUsedUpResources.UsedUpActions.Contains("BraggingRights") ||
        //                     item2 != item1) return null;
        //                 if (self.CreateStrike(item2).WithActionCost(0).Target is not CreatureTarget target) return null;
        //                 return new CombatAction(self, item1.Illustration, "Bragging Rights", [Trait.Manipulate, Trait.Basic], "Once per day, you attempt to Strike an enemy with the doom switch, marking them for defeat. The switch is treated as a simple melee weapon for the purpose of proficiency. This attack deals no damage."
        //                         + S.FourDegreesOfSuccess("You and your allies gain a +1 status bonus to attack rolls against the target for 1 minute. If you reduce the target to 0 Hit Points during this time, you gain temporary Hit Points equal to twice the target's level for 1 round.",
        //                             "As critical success, except you gain temporary Hit Points equal to the target's level.",
        //                             "You and your allies take a –1 status penalty to attack rolls against the target for 1 round.",
        //                             "Critical Failure You and your allies take a –2 status penalty to attack rolls against the target for 1 round."),
        //                         target)
        //                     .WithActionCost(1)
        //                     .WithSoundEffect(SfxName.Victory)
        //                     .WithTargetingTooltip((action, targeted, _) =>
        //                     {
        //                         CombatAction strike = action.Owner.CreateStrike(item);
        //                         return CombatActionExecution.BreakdownAttackForTooltip(strike, targeted).TooltipDescription;
        //                     })
        //                     .WithEffectOnEachTarget(async (_, caster, creature, _) =>
        //                     {
        //                         CombatAction strike = self.CreateStrike(item2, 0).WithActionCost(0);
        //                         strike.EffectOnChosenTargets = null;
        //                         strike.EffectOnOneTarget = null;
        //                         strike.Description = S.FourDegreesOfSuccess(
        //                             "You and your allies gain a +1 status bonus to attack rolls against the target for 1 minute. If you reduce the target to 0 Hit Points during this time, you gain temporary Hit Points equal to twice the target's level for 1 round.",
        //                             "As critical success, except you gain temporary Hit Points equal to the target's level.",
        //                             "You and your allies take a –1 status penalty to attack rolls against the target for 1 round.",
        //                             "Critical Failure You and your allies take a –2 status penalty to attack rolls against the target for 1 round.");
        //                         CheckResult? result =
        //                             await caster.Battle.GameLoop.FullCast(strike,
        //                                 ChosenTargets.CreateSingleTarget(creature))
        //                                 ? strike.CheckResult
        //                                 : null;
        //                         if (result == null) return;
        //                         QEffect brag = new QEffect("Bragging Rights",
        //                             caster.Name +
        //                             " and their allies have a +1 status bonus to attack rolls against this creature.")
        //                         {
        //                             Illustration = item1.Illustration,
        //                             Source = caster,
        //                             WhenCreatureDiesAtStateCheckAsync = _ =>
        //                             {
        //                                 caster.GainTemporaryHP(result == CheckResult.CriticalSuccess ? creature.Level * 2 :  creature.Level);
        //                                 return Task.CompletedTask;
        //                             }
        //                         }.WithExpirationAtStartOfSourcesTurn(caster, 10).AddGrantingOfTechnical(
        //                             cr => cr.FriendOf(caster),
        //                             qfTech =>
        //                             {
        //                                 qfTech.BonusToAttackRolls = (_, action, enemy) =>
        //                                 {
        //                                     if (enemy != creature || !(action.HasTrait(Trait.Strike) ||
        //                                                                (action.SpellcastingSource != null &&
        //                                                                 action.HasTrait(Trait.Attack)))) return null;
        //                                     return new Bonus(1, BonusType.Status, "Bragging Rights", true);
        //                                 };
        //                             });
        //                         QEffect humble = new QEffect("Humbled",
        //                             caster.Name +
        //                             $" and their allies have a -{(result == CheckResult.CriticalFailure ? 2 : 1)} status penalty to attack rolls against this creature.")
        //                         {
        //                             Illustration = item1.Illustration,
        //                             Source = caster,
        //                         }.WithExpirationAtStartOfSourcesTurn(caster, 1).AddGrantingOfTechnical(
        //                             cr => cr.FriendOf(caster),
        //                             qfTech =>
        //                             {
        //                                 qfTech.BonusToAttackRolls = (_, action, enemy) =>
        //                                 {
        //                                     if (enemy != creature || !(action.HasTrait(Trait.Strike) ||
        //                                                                (action.SpellcastingSource != null &&
        //                                                                 action.HasTrait(Trait.Attack)))) return null;
        //                                     return new Bonus(result == CheckResult.CriticalFailure ? -2 : -1, BonusType.Status, "Humbled");
        //                                 };
        //                             });
        //                         switch (result)
        //                         {
        //                             case CheckResult.CriticalSuccess:
        //                             case CheckResult.Success:
        //                                 creature.AddQEffect(brag);
        //                                 break;
        //                             case CheckResult.CriticalFailure:
        //                             case CheckResult.Failure:
        //                                 creature.AddQEffect(humble);
        //                                 break;
        //                         }
        //                         caster.PersistentUsedUpResources.UsedUpActions.Add("BraggingRights");
        //                     });
        //             }
        //         };
        //         self.AddQEffect(effect);
        //     };
        //     return item;
        // });
        #endregion
    }
}