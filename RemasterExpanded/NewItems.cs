using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Campaign.LongTerm;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
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
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using RemasterExpanded.Technical;
using SpiritDamage;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

public static class NewItems
{
    public static ItemName AutoloadLeathers { get; set; }
    public static ItemName AstralRune { get; set; }
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
                .WithWornAt(Trait.Headwear)
                .WithItemAction((item, creature) =>
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
        AstralRune = ModManager.RegisterNewItemIntoTheShop("RE_AstralRune", name =>
        {
            return new Item(name, MIllustrations.CreateIllustration("AstralRune"), "runestone of {i}astral{/i}", 8, 450, Trait.Runestone, Trait.Magical, SpiritTrait.Spirit)
                .WithItemGreaterGroup(ItemGreaterGroup.PropertyRunes)
                .WithRuneProperties(new RuneProperties("astral", RuneKind.WeaponProperty, "The enchanted weapon is empowered by powerful spiritual energy.", 
                    "This weapon deals an extra 1d6 spirit damage, and bypasses the damage resistance of incorporeal creatures.",
                    item =>
                    {
                        if (item.WeaponProperties == null)
                            return;
                        item.Traits.Add(Trait.GhostTouch);
                        item.WithAdditionalWeaponProperties(properties =>
                        {
                            properties.WithAdditionalDamage("1d6", ModData.SpiritDamage);
                        });
                    }));
        });
        ModManager.RegisterNewItemIntoTheShop("RE_TassetsOfFlexibility", name =>
        {
            return new Item(name, MIllustrations.CreateIllustration("Tassets"), "tassets of flexibility", 4, 100,
                Trait.Invested, Trait.Magical, Trait.Worn)
                .WithDescription("{i}You can attach these light-brown leather flaps adorned with gold stitching to a breastplate or even clothing to protect your upper legs in battle. They give you the freedom to move your body to its limit without worrying about exposing yourself to a hit.{/i}" +
                                 "\n\nWhile wearing the tasset of flexibility, you gain a +1 item bonus to Acrobatics checks and once per day can take the following action:" +
                                 "\n\n{b}Lunging Attack{/b} {icon:Action} (concentrate); Make a Strike with a melee weapon, increasing your reach by 5 feet for that Strike.")
                .WithOnCreatureWhenWorn((item, self) =>
                {
                    self.AddQEffect(new QEffect
                    {
                        ProvideStrikeModifier = weapon =>
                        {
                            if (!weapon.HasTrait(Trait.Melee) || weapon.HasTrait(Trait.Unarmed) || self.PersistentUsedUpResources.UsedUpActions.Contains("Lunging Attack"))
                                return null;
                            CombatAction strike = self.CreateStrike(weapon);
                            strike.WithPrologueEffectOnChosenTargetsBeforeRolls(async (action, cr, _) =>
                            {
                                cr.AddQEffect(new QEffect
                                {
                                    Id = MQEffectIds.LungingReach,
                                    AfterYouTakeAction = async (qEffect, combatAction) =>
                                    {
                                        if (combatAction != action)
                                            return;
                                        qEffect.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                });
                                cr.Battle.GameLoop.RecalculateFlankingFor(cr);
                            });
                            strike.EffectOnOneTarget += async (_, caster, _, _) =>
                            {
                                caster.PersistentUsedUpResources.UsedUpActions.Add("Lunging Attack");
                            };
                            strike.Name = strike.Name.Replace("Strike", "Lunging Attack");
                            strike.ContextMenuName = strike.ContextMenuName?.Replace("Strike", "Lunging Attack") ?? strike.ContextMenuName;
                            strike.ShortName = strike.ShortName.Replace("Strike", "Lunging Attack");
                            strike.Target = FeatLoader.ReachPlusFive(item, self).WithAdditionalConditionOnTargetCreature((me, _) => me.PersistentUsedUpResources.UsedUpActions.Contains("Lunging Attack") ? Usability.NotUsable("Lunging Attack may only be used once per day.") : Usability.Usable);
                            strike.Description = strike.Description.Replace($"{{b}}Reach{{/b}} {item.DetermineReach(self)*5}", $"{{b}}Reach{{/b}} {(item.DetermineReach(self)+1)*5}");
                            strike.Traits.Add(Trait.Concentrate);
                            return strike;
                        },
                        BonusToSkillChecks = (skill, _, _) => skill == Skill.Acrobatics ? new Bonus(1, BonusType.Item, "Tasset of Flexibility") : null
                    });
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHeal", name => FaithSymbol(name, 18, 1, 4, 80, true));
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHarm", name => FaithSymbol(name, 18, 1, 4, 80, false));
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHealGreater", name => FaithSymbol(name, 24, 3, 8, 425, true, "greater"));
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHarmGreater", name => FaithSymbol(name, 24, 3, 8, 425, false, "greater"));
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHealMajor", name => FaithSymbol(name, 30, 5, 12, 1700, true, "major"));
        ModManager.RegisterNewItemIntoTheShop("RE_FaithSymbolHarmMajor", name => FaithSymbol(name, 30, 5, 12, 1700, false, "major"));
        ModManager.RegisterInlineTooltip("sanctify", MTraits.Sanctified.GetTraitProperties().RulesText ?? "");
        ModManager.RegisterInlineTooltip("holy", HolyTrait.Holy.GetTraitProperties().RulesText ?? "");
        ModManager.RegisterInlineTooltip("unholy",  UnholyTrait.Unholy.GetTraitProperties().RulesText ?? "");
        ModManager.RegisterInlineTooltip("interact", "You can use the Interact action to:" +
                                                     "\n• Draw, put away, or swap an item." +
                                                     "\n• Pick up an item from the ground." +
                                                     "\n• Hand off a held item to a willing creature with a free hand." +
                                                     "\n• Change your grip by adding a hand to an item.");
        ModManager.RegisterInlineTooltip("ReactiveStrike", "Reactive Strike is another name for Attack of Opportunity.");
        ModManager.RegisterNewItemIntoTheShop("RE_SplintArmor", name => new Item(name, MIllustrations.CreateIllustration("Splint"), "splint mail", 1, 13, Trait.HeavyArmor, Trait.Composite)
            .WithArmorProperties(new ArmorProperties(5, 1, -3, -2, 16)
            {
                IsMetal = true
            }));
        LongTermEffects.RegisterWithNumberArgument("RETacticianCharges", LongTermEffectDuration.Forever, TacticianCharges);
        ModManager.RegisterNewItemIntoTheShop("RE_TacticiansHelm", name =>
         {
            return new Item(name, MIllustrations.CreateIllustration("TacticiansHelm"), "tactician's helm", 5, 160,
                    Trait.Invested, Trait.Magical, Trait.Worn)
                .WithDescription("Repurposing and enchanting a helmet worn by a battlefield commander can create a tactician's helm, imparting knowledge of battlefield tactics that feeds off your minor victories.",
                                 "You have a +1 item bonus to Warfare Lore.\n\n" +
                                 "The helmet gains a charge each time you hit a creature with a {tooltip:ReactiveStrike}Reactive Strike{/tooltip}. A {i}tactician's helm{/i} can hold up to 2 charges, and its charges begin at 0.\n\n" +
                                 "{b}Activate {icon:Action}{/b} ({tooltip:concentrate}concentrate{/tooltip}); {b}Cost{/b} 1 charge from the helm; {b}Frequency{/b} once per encounter; {b}Effect{/b} You choose one of the following effects." +
                                 "\n{b}• Charge!{/b} Stride twice." +
                                 "\n{b}• Move It!{/b} You gain a +2 status bonus to Acrobatics and Athletics checks until the end of this turn." +
                                 "\n{b}• Protect!{/b} If you're wielding a shield, Stride to a space adjacent to an ally, then Raise your Shield." +
                                 "\n{b}• Re-Arm!{/b} You can make up to three interact actions as free actions. Each of these actions must be used to do something listed under {tooltip:interact}Interact{/tooltip} and must be completed in sequence.")
                .WithWornAt(Trait.Headwear)
                .WithPermanentQEffectWhenWorn((qfItem, item) =>
                {
                    qfItem.AfterYouTakeActionAgainstTarget = async (qf, action, _, result) =>
                    {
                        if (!action.HasTrait(Trait.AttackOfOpportunity) || result <= CheckResult.Failure)
                            return;
                        Creature self = qf.Owner;
                        if (self.FindQEffect(MQEffectIds.TacticianCharges) is not {} charges)
                        {
                            self.AddQEffect(TacticianCharges(1));
                        }
                        else if (charges.Value < 2)
                        {
                            charges.Value += 1;
                        }
                    };
                    qfItem.ProvideActionIntoPossibilitySection = (effect, section) =>
                    {
                        if (section.PossibilitySectionId != PossibilitySectionId.ItemActions)
                            return null;
                        return new SubmenuPossibility(item.Illustration, "Tactician's Helm")
                        {
                            Subsections = 
                            [
                                new PossibilitySection("Tactician's Helm")
                                {
                                    Possibilities = TacticianCombatActions(effect.Owner).ToList()
                                }
                            ]
                        };
                    };
                    qfItem.BonusToSkills = skill =>
                        skill == Lores.GetRegisteredLore("Warfare Lore", null)?.Skill
                            ? new Bonus(1, BonusType.Item, "Tactician's Helm")
                            : null;
                });
        });
        ModManager.RegisterInlineTooltip("manipulate", Trait.Manipulate.GetTraitProperties().RulesText ?? "");
        ModManager.RegisterInlineTooltip("concentrate", "Actions with this trait cannot be used while Raging. Rarely, actions with this trait can trigger reactions.");
        ModManager.RegisterNewItemIntoTheShop("RE_CassisianHelmet", name =>
        {
            return new Item(name, MIllustrations.CreateIllustration("Cassisian"), "cassisian helmet", 6, 225,
                    Trait.Invested, Trait.Magical, Trait.Worn)
                .WithWornAt(Trait.Headwear)
                .WithStaticDC(20)
                .WithDescription(
                    "Small brass wings are attached to either side of this ornate helmet.",
                    "You have a +1 status bonus to AC and saves against {tooltip:unholy}unholy{/tooltip} creatures and effects." +
                    "\n\nOnce per encounter, you can activate the helm with two actions {icon:TwoActions} ({tooltip:concentrate}concentrate{/tooltip}, {tooltip:manipulate}manipulate{/tooltip}), to send out eye beams that deal your choice of 2d6 cold or fire damage (DC 20 basic Reflex save mitigates) to all creatures in a 15-foot line.")
                .WithPermanentQEffectWhenWorn((qfItem, item) =>
                {
                    qfItem.Name = "Cassisian Helmet";
                    qfItem.Description = "You have a +1 status bonus to AC and saves against unholy creatures and effects.";
                    qfItem.BonusToDefenses = (_, action, defense) =>
                        action is { Owner: var owner } && (owner.HasTrait(UnholyTrait.Unholy) || action.HasTrait(UnholyTrait.Unholy)) && (defense.IsSavingThrow() || defense == Defense.AC)
                            ? new Bonus(1, BonusType.Status, "Cassisian Helmet")
                            : null;
                    qfItem.FixedDC = item.StaticDC;
                    qfItem.ProvideActionIntoPossibilitySection = (effect, section) =>
                    {
                        if (section.PossibilitySectionId != PossibilitySectionId.ItemActions)
                            return null;
                        Creature self = effect.Owner;
                        return new SubmenuPossibility(item.Illustration, "Cassisian Helmet")
                        {
                            Subsections = 
                            [
                                new PossibilitySection("Cassisian Helmet")
                                {
                                    Possibilities =
                                    [
                                        new ActionPossibility(CassisianBlast(self, IllustrationName.FireRay,
                                            DamageKind.Fire, qfItem)),
                                        new ActionPossibility(CassisianBlast(self, IllustrationName.RayOfFrost,
                                            DamageKind.Cold, qfItem))
                                    ]
                                }
                            ]
                        };
                    };
                });
        });
        ModManager.RegisterNewItemIntoTheShop("RE_AspCoil", name => 
            new Item(name, MIllustrations.CreateIllustration("AspCoil"), "asp coil", 0, 10, Trait.Reach,
                Trait.VersatileP, Trait.Sword, Trait.Martial)
            .WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Slashing))
            .WithMainTrait(MTraits.AspCoil));
        ModManager.RegisterNewItemIntoTheShop("RE_Scourge", name =>
            new Item(name, IllustrationName.Whip, "scourge", 0, 1, Trait.Agile, Trait.Disarm,
                    Trait.Finesse, Trait.Nonlethal, Trait.Sweep, Trait.Martial, Trait.Flail)
                .WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Slashing))
                .WithMainTrait(MTraits.Scourge));
        ModManager.RegisterNewItemIntoTheShop("RE_MasterMagusRing", name =>
        {
            return new Item(name, MIllustrations.CreateIllustration("MagusRing"), "master magus ring", 11, 1250,
                    Trait.Arcane, Trait.Invested, Trait.Worn)
                .WithDescription("Each master magus ring has a significant metal and symbol to represent a particular hybrid study, such as a heavy iron ring with an icon of a mountain for inexorable iron, or glittering silver with a shield-like emblem for sparkling targe.",
                    "You have a +2 item bonus to Arcana.\n\n" +
                    "The first magus focus spell you cast each encounter doesn't cost you a focus point." +
                    "\n\nOnce per day, you can activate the ring as a free action {icon:FreeAction} ({tooltip:concentrate}concentrate{/tooltip}) and teleport to an unoccupied space you can see within a range equal to your speed.")
                .WithItemBonusToSkill(Skill.Arcana, 2)
                .WithOncePerDayWhenWornAction((itm, self) =>
                {
                    return CombatAction.CreateAction(self, new SideBySideIllustration(itm.Illustration, IllustrationName.DimensionDoor), "Master magus ring teleport",
                            [Trait.Concentrate],
                            "You teleport to the chosen space.",
                            Target.TileYouCanSeeAndTeleportTo(self.Speed), 0, SfxName.PhaseBolt, null)
                        .WithEffectOnChosenTargets(async (creature, targets) =>
                            await CommonSpellEffects.Teleport(creature, targets.ChosenTile!));
                })
                .WithOnCreatureWhenWorn((_, self) =>
                {
                    self.AddQEffect(new QEffect("Master magus ring",
                        "The first conflux focus spell you cast each encounter doesn't cost you a focus point.")
                    {
                        AfterYouExpendSpellcastingResources = (effect, action) =>
                        {
                            if (!action.HasTrait(Trait.Magus) || !action.HasTrait(Trait.Focus) ||
                                effect.ExpiresAt == ExpirationCondition.Immediately)
                                return;
                            if (action.Owner.Spellcasting != null)
                                ++action.Owner.Spellcasting.FocusPoints;
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    });
                });
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

    public static Item FaithSymbol(ItemName name, int dc, int rank, int level, int price, bool heal, string greater = "")
    {
        string major = greater != "" ? $" ({greater})" : "";
        string healOrHarm = heal ? "heal" : "harm";
        FeatName allowedFont = heal ? FeatName.HealingFont : FeatName.HarmfulFont;
        var humanName = $"faith symbol of {healOrHarm}{major}";
        Illustration faithIllustration = heal ? MIllustrations.CreateIllustration("Faith") : MIllustrations.CreateIllustration("FaithHarm");
        string spellLink = heal ? AllSpells.CreateModernSpellTemplate(SpellId.Heal, Trait.Innate, rank).ToSpellLink() : AllSpells.CreateModernSpellTemplate(SpellId.Harm, Trait.Innate, rank).ToSpellLink();
        return new Item(name, faithIllustration, humanName, level, price, Trait.Worn,
                Trait.Divine, Trait.Invested)
            .WithDescription("{i}You have a symbol showing devotion to your deity.{/i}" +
                             $"\n\n{{b}}Requirements:{{/b}} You must worship a deity and your deity's font must include {spellLink}." +
                             "\n\nIf you are not {tooltip:sanctify}sanctified{/} and you are good aligned, you become sanctified, gaining the {tooltip:holy}holy{/} trait. If you are not sanctified and you are evil aligned, you become sanctified, gaining the {tooltip:unholy}unholy{/} trait." +
                             $"\n\nIf you have the spellcasting feature, the symbol can cast {spellLink}, or the 1st-rank spell from your deity's cleric spells once per day. The DC for any of these spells is {dc} and the spells are cast at rank {rank}.")
            .WithCanUse((values, _) => values.Deity is {} deity && deity.AllowedFonts.Contains(allowedFont))
            .WithWornAt(MTraits.FaithSymbol)
            .WithStaticDC(dc)
            .WithOnCreatureWhenWorn((item, creature) =>
            {
                if (creature.PersistentCharacterSheet is not { } sheet)
                    return;
                if (creature.HasTrait(Trait.Good) && !creature.HasTrait(HolyTrait.Holy))
                    creature.Traits.Add(HolyTrait.Holy);
                else if (creature.HasTrait(Trait.Evil) && !creature.HasTrait(UnholyTrait.Unholy)) 
                    creature.Traits.Add(UnholyTrait.Unholy);
                QEffect menu = new(ExpirationCondition.Ephemeral)
                {
                    ProvideActionIntoPossibilitySection = (_, section) =>
                    {
                        if (section.PossibilitySectionId != PossibilitySectionId.ItemActions)
                            return null;
                        return new SubmenuPossibility(IllustrationName.CastASpell, "Faith Symbol")
                        {
                            SubmenuId = MSubmenuIds.FaithSymbol,
                            PossibilityGroup = "Faith Symbol"
                        };
                    },
                    Key = "FaithSymbol"
                };
                CombatAction harmOrHeal = !heal ? AllSpells.CreateMonsterSpellInCombat(SpellId.Harm, creature, rank, dc) : AllSpells.CreateMonsterSpellInCombat(SpellId.Heal, creature, rank, dc);
                harmOrHeal.CastFromScroll = item;
                AddCastRestriction(harmOrHeal, "faith symbol");
                CombatAction deitySpellAction;
                QEffect cast = new(ExpirationCondition.Ephemeral)
                {
                    ProvideSectionIntoSubmenu = (_, section) =>
                    {
                        if (section.SubmenuId != MSubmenuIds.FaithSymbol)
                            return null;
                        PossibilitySection symbol = new("Faith Symbol");
                        if (sheet.Calculated.Deity is {} deity)
                        {
                            SpellId deitySpell = deity.GrantedSpells.FirstOrDefault(sp =>
                                AllSpells.CreateModernSpellTemplate(sp, Trait.Innate).MinimumSpellLevel == 1);
                            if (deitySpell != SpellId.None)
                            {
                                deitySpellAction = AllSpells.CreateMonsterSpellInCombat(deitySpell, creature, rank, dc);
                                deitySpellAction.CastFromScroll = item;
                                AddCastRestriction(deitySpellAction, "faith symbol");
                                symbol.AddPossibility(Possibilities.CreateSpellPossibility(deitySpellAction));
                            }
                        }
                        symbol.AddPossibility(Possibilities.CreateSpellPossibility(harmOrHeal));
                        return symbol;
                    }
                };
                creature.AddQEffect(new QEffect
                {
                    StateCheck = qf =>
                    {
                        Creature self = qf.Owner;
                        if (self.Spellcasting == null) return;
                        creature.AddQEffect(menu);
                        creature.AddQEffect(cast);
                    }
                });
            });
    }

    public static void AddCastRestriction(CombatAction spell, string baseName)
    {
        switch (spell.Target)
        {
            case AreaTarget target:
                target.AdditionalRequirementOnAreaCaster += creature =>
                    creature.PersistentUsedUpResources.UsedUpActions.Contains(
                        $"{baseName}")
                        ? Usability.NotUsable(
                            $"You may cast a spell from a {baseName} once per day.")
                        : Usability.Usable;
                break;
            case CreatureTarget target2:
                target2.WithAdditionalConditionOnTargetCreature((creature, _) =>
                    creature.PersistentUsedUpResources.UsedUpActions.Contains(
                        $"{baseName}")
                        ? Usability.NotUsable(
                            $"You may cast a spell from a {baseName} once per day.")
                        : Usability.Usable);
                break;
            case SelfTarget target2:
                target2.WithAdditionalRestriction2(spell.Owner.PersistentUsedUpResources.UsedUpActions.Contains(
                        $"{baseName}"), $"You may cast a spell from a {baseName} once per day.");
                break;
            case DependsOnActionsSpentTarget target2:
                switch (target2.IfOneAction)
                {
                    case CreatureTarget target1:
                        target1.WithAdditionalConditionOnTargetCreature((creature, _) =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable);
                        break;
                    case AreaTarget areaTarget:
                        areaTarget.AdditionalRequirementOnAreaCaster += creature =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable;
                        break;
                    case SelfTarget selfTarget:
                        selfTarget.WithAdditionalRestriction2(spell.Owner.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}"), $"You may cast a spell from a {baseName} once per day.");
                        break;
                }

                switch (target2.IfTwoActions)
                {
                    case CreatureTarget target1:
                        target1.WithAdditionalConditionOnTargetCreature((creature, _) =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable);
                        break;
                    case AreaTarget areaTarget:
                        areaTarget.AdditionalRequirementOnAreaCaster += creature =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable;
                        break;
                    case SelfTarget selfTarget:
                        selfTarget.WithAdditionalRestriction2(spell.Owner.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}"), $"You may cast a spell from a {baseName} once per day.");
                        break;
                }

                switch (target2.IfThreeActions)
                {
                    case CreatureTarget target1:
                        target1.WithAdditionalConditionOnTargetCreature((creature, _) =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable);
                        break;
                    case AreaTarget areaTarget:
                        areaTarget.AdditionalRequirementOnAreaCaster += creature =>
                            creature.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}")
                                ? Usability.NotUsable(
                                    $"You may cast a spell from a {baseName} once per day.")
                                : Usability.Usable;
                        break;
                    case SelfTarget selfTarget:
                        selfTarget.WithAdditionalRestriction2(spell.Owner.PersistentUsedUpResources.UsedUpActions.Contains(
                                $"{baseName}"), $"You may cast a spell from a {baseName} once per day.");
                        break;
                }
                break;
        }
        spell.EffectOnChosenTargets += async (_, caster, _) =>
        {
            caster.PersistentUsedUpResources.UsedUpActions.Add(
                $"{baseName}");
        };
    }

    public static QEffect TacticianCharges(int value)
    {
        int oValue = value;
        return new QEffect
        {
            Id = MQEffectIds.TacticianCharges,
            Value = value,
            Name = "Tactician Charges",
            Description = $"Your {{i}}tactician's helm{{/i}} has {oValue} charge{(oValue == 1 ? "" : "s")}.",
            Illustration = MIllustrations.CreateIllustration("TacticiansHelm"),
            EndOfCombat = async (effect, b) =>
            {
                int newValue = effect.Value;
                if (!b)
                    return;
                if (effect.Owner.LongTermEffects is not {} lte)
                    return;
                if (WellKnownLongTermEffects.CreateLongTermEffect("RETacticianCharges", null, newValue) is {} charges)
                {
                    if (lte.Effects.FirstOrDefault(lt => lt.Id == charges.Id) is {} charge)
                        charge.Number = newValue;
                    else
                        lte.Add(charges);
                }
            },
            StateCheck = qf =>
            {
                if (qf.Value == oValue)
                    return;
                oValue = qf.Value;
                qf.Illustration = oValue switch
                {
                    0 => IllustrationName.None,
                    >= 1 => MIllustrations.CreateIllustration("TacticiansHelm"),
                    _ => qf.Illustration
                };
            }
        };
    }

    public static IEnumerable<Possibility> TacticianCombatActions(Creature owner)
    {
        yield return new ActionPossibility(TacticianAction(owner, MIllustrations.CreateIllustration("Charge"), "Charge!",
            "Stride twice.")
            .WithEffectOnEachTarget(async (spell, caster, _, _) =>
            {
                if (!await caster.StrideAsync("Choose where to stride.", allowCancel: true))
                {
                    spell.RevertRequested = true;
                    return;
                }
                if (!await caster.StrideAsync("Choose where to stride or cancel to convert to simple stride.", allowCancel: true))
                {
                    return;
                }
                TacticianUsed(caster);
            }));
        yield return new ActionPossibility(TacticianAction(owner, MIllustrations.CreateIllustration("MoveIt"),
            "Move It!", "You gain a +2 status bonus to Acrobatics and Athletics checks until the end of this turn.")
            .WithEffectOnEachTarget(async (spell, caster, _, _) =>
            {
                caster.AddQEffect(new QEffect("Move It!",
                    "You have a +2 status bonus to Acrobatics and Athletics checks.",
                    ExpirationCondition.ExpiresAtEndOfYourTurn, caster, spell.Illustration)
                {
                    BonusToSkillChecks = (skill, _, _) => skill is Skill.Acrobatics or Skill.Athletics ? new Bonus(2, BonusType.Status, "Move It!") : null
                });
                TacticianUsed(caster);
            }));
        yield return new ActionPossibility(TacticianAction(owner, MIllustrations.CreateIllustration("Protect"),
            "Protect!",
            "If you're wielding a shield, Stride to a space adjacent to an ally, then Raise your Shield.", 
            Target.Self().WithAdditionalRestrictions([new Tuple<bool, string>(owner.FindQEffect(MQEffectIds.TacticianCharges) is not { } charges || charges.Value == 0, "You must have at least 1 charge to use this action"),
                new Tuple<bool, string>(owner.HasEffect(MQEffectIds.TacticianUsed), "You can only use actions granted by a {i}tactician's helm{/i} once per encounter."),
                new Tuple<bool, string>(!owner.WieldsItem(Trait.Shield), "You must be wielding a shield to use this action."),
                new Tuple<bool, string>(owner.HasEffect(QEffectId.RaisingAShield), "You are already raising a shield.")]))
            .WithEffectOnEachTarget(async (spell, caster, _, _) =>
            {
                if (!await caster.StrideOrStepAdvancedAsync("Choose where to stride adjacent to an ally.", allowCancel: true, permissibleTarget: tile => caster.Battle.AllCreatures.Any(cr => cr.FriendOfAndNotSelf(caster) && cr.Space.Tiles.Any(t => t.IsAdjacentTo(tile) || (!caster.Space.IsSingleSquare && tile.TilesToTheBottomRight(caster.Space.SizeInSquares)
                        .Any(t2 => t2.IsAdjacentTo(t)))))))
                {
                    spell.RevertRequested = true;
                    return;
                }
                if (!caster.Battle.AllCreatures.Any(cr => cr.FriendOfAndNotSelf(caster) && caster.IsAdjacentTo(cr)))
                    return;
                caster.RegeneratePossibilities();
                Possibilities raiseShield = caster.Possibilities.Filter(ap =>
                {
                    if (ap.CombatAction.ActionId != ActionId.RaiseShield || ap.CombatAction.Name == "Raise shield (Devoted Guardian)")
                        return false;
                    ap.CombatAction.ActionCost = 0;
                    ap.RecalculateUsability();
                    return true;
                });
                caster.Possibilities = raiseShield;
                List<Option> options = await caster.Battle.GameLoop.CreateActions(caster, caster.Possibilities, null);
                await caster.Battle.GameLoop.OfferOptions(caster, options, true);
                TacticianUsed(caster);
            })
        );
        yield return new ActionPossibility(TacticianAction(owner, MIllustrations.CreateIllustration("ReArm"), "Re-Arm!",
            "You can make up to three interact actions as free actions. Each of these actions must be used to do something listed under {tooltip:interact}Interact{/tooltip} and must be completed in sequence.")
            .WithActionId(RActionIds.ReArm)
            .WithEffectOnChosenTargets(async (self, _) =>
                {
                    self.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
                        {
                            AfterYouTakeAction = async (q, action) =>
                            {
                                if (!IsInteractAction(action) && action.ActionId != RActionIds.ReArm)
                                {
                                    q.ExpiresAt = ExpirationCondition.Immediately;
                                }
                                else if (action.ActionId != RActionIds.ReArm)
                                    q.Value -= 1;
                            },
                            ModifyActionPossibility = (_, action) =>
                            {
                                if (IsInteractAction(action))
                                {
                                    action.ActionCost = 0;
                                }
                            },
                            Value = 3,
                            StateCheck = qf =>
                            {
                                if (qf.Value == 0)
                                    qf.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    );
                    TacticianUsed(self);
                }
            )
        );
    }

    public static CombatAction TacticianAction(Creature owner, Illustration illustration, string name, string description, Target? target = null)
    {
        return CombatAction.CreateAction(owner, illustration, name,
            [Trait.Concentrate, Trait.Basic],
            description,
            target ?? TacticianTarget(owner), 1, SfxName.Drum, null);
    }

    public static void TacticianUsed(Creature owner)
    {
        owner.AddQEffect(new QEffect
        {
            Id = MQEffectIds.TacticianUsed
        });
        owner.FindQEffect(MQEffectIds.TacticianCharges)?.Value -= 1;
    }

    public static Target TacticianTarget(Creature owner)
    {
        Target target = Target.Self();
        return target.WithAdditionalRestrictions([new Tuple<bool, string>(owner.FindQEffect(MQEffectIds.TacticianCharges) is not { } charges || charges.Value == 0, "You must have at least 1 charge to use this action"),
        new Tuple<bool, string>(owner.HasEffect(MQEffectIds.TacticianUsed), "You can only use actions granted by a {i}tactician's helm{/i} once per encounter.")]);
            
    }

    public static bool IsInteractAction(CombatAction action)
    {
        return action.ActionId is ActionId.ReplaceItemInHand or ActionId.DrawItem or ActionId.PickUpItem || action.Name.Contains("Hand over") || action.Name.Contains("Add hand") || action.Name.Contains("Stow");
    }

    public static CombatAction CassisianBlast(Creature owner, Illustration illustration, DamageKind kind, QEffect effect)
    {
        return CombatAction.CreateAction(owner, illustration, $"Cassisian Blast {kind.HumanizeTitleCase2()}",
                [Trait.Concentrate, Trait.Manipulate, Trait.Basic, Trait.DoNotRecheckTargetRequirements],
                $"Creatures in the area take 2d6 {kind.HumanizeLowerCase2()} damage (DC {effect.FixedDC} basic Reflex save mitigates).",
                Target.Line(3).WithAdditionalRequirementOnCaster(cr =>
                    cr.HasEffect(MQEffectIds.Cassisian)
                        ? Usability.NotUsable("The {i}cassisian helmet{/i} can only be activated once per encounter.")
                        : Usability.Usable),
                2, kind == DamageKind.Fire ? SfxName.FireRay : SfxName.RayOfFrost,
                new SavingThrow(Defense.Reflex, effect.FixedDC))
            .WithEffectOnSelf(self => self.AddQEffect(new QEffect {Id = MQEffectIds.Cassisian}))
            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
            {
                await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, DiceFormula.FromText("2d6", spell.Name), kind);
            });
    }
}