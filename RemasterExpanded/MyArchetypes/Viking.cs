using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
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
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MyArchetypes;

public class Viking
{
    public static IEnumerable<Feat> VikingFeats()
    {
        Feat vikingDedication = ArchetypeFeats.CreateAgnosticArchetypeDedication(MTraits.Viking, "Vikings spend long periods of time at sea, only to leap from their boats at a moment's notice, charge through the surf, and take their enemies by storm.", 
            "You gain the Additional Lore general feat for Maritime Lore and Warfare Lore. If you were already trained in Warfare or Maritime Lore, you also become trained in a lore skill of your choice.\nYou ignore difficult terrain from shallow water. In addition, while underwater, you gain a 10 foot bonus to your Speed.")
            .WithPermanentQEffect("You ignore difficult terrain from shallow water. In addition, while underwater, you gain a 10 foot bonus to your Speed.", qf =>
            {
                qf.Id = QEffectId.IgnoresShallowWater;
                qf.BonusToAllSpeeds = _ => !qf.Owner.HasEffect(QEffectId.AquaticCombat) ? null : new Bonus(2, BonusType.Untyped, "Viking Dedication", true);
            })
            .WithOnSheet(values =>
            {
                if (values.GetProficiency(RemasterLore.MaritimeLore.Trait) >= Proficiency.Trained)
                {
                    values.TrainInThisOrSubstitute(RemasterLore.MaritimeLore, true);
                }
                Lores.GrantAdditionalLore(values, RemasterLore.MaritimeLore);
                if (Lores.AllPublicLores.FirstOrDefault(lore => lore.Name == "Warfare Lore") is not { } wLore) return;
                if (values.GetProficiency(wLore.Trait) >= Proficiency.Trained)
                {
                    values.TrainInThisOrSubstitute(wLore, true);
                }
                Lores.GrantAdditionalLore(values, wLore);
            });
        yield return vikingDedication;
        
        Feat hurlingCharge = new TrueFeat(ModManager.RegisterFeatName("RE_HurlingCharge", "Hurling Charge"), 4, "", 
                "Make a ranged Strike with a thrown weapon, Stride, and then Interact to draw another weapon. This Interact action doesn't trigger reactions.\n\n{b}Special{/b} If you are raging and end the Stride adjacent to an enemy, that enemy is off-guard against the next Strike you make against it with the weapon you drew before the end of your next turn.", [])
            .WithActionCost(2)
            .WithAvailableAsArchetypeFeat(MTraits.Viking)
            .WithPermanentQEffect("", qf =>
            {
                qf.ProvideStrikeModifier = weapon=>
                {
                    CombatAction hurl = StrikeRules.CreateStrike(qf.Owner, weapon, RangeKind.Ranged, 0, true);
                    hurl.WithActionCost(2);
                    hurl.Traits.Add(Trait.Basic);
                    hurl.Name = "Hurling Charge";
                    hurl.Illustration = new SideBySideIllustration(IllustrationName.Throw, IllustrationName.FleetStep);
                    hurl.Description =
                        StrikeRules.CreateBasicStrikeDescription2(hurl.StrikeModifiers, additionalAftertext: "The weapon falls on the target's square. You then stride and draw a new weapon.");
                    Target hurlTarg = hurl.Target;
                    if (hurlTarg is CreatureTarget h2Targ)
                        hurl.Target = h2Targ.WithAdditionalConditionOnTargetCreature((self, _) =>
                        {
                            if (!self.CarriedItems.Any(i => i.HasTrait(Trait.Weapon)))
                                return Usability.NotUsable("You must be carrying a weapon to draw.");
                            bool canUse = CommonCombatActions.StepByStepStride(self).WithActionCost(0).CanBeginToUse(self) && !self.HasEffect(QEffectId.Immobilized);
                            return !canUse ? Usability.NotUsableOnThisCreature(self.Name + " cannot move.") : Usability.Usable;
                        });
                    hurl.EffectOnChosenTargets = Delegates.SmartCombineDelegates(hurl.EffectOnChosenTargets,
                        async (spell, caster, _) =>
                        {
                            if (!await caster.StrideAsync("Choose where to stride.", allowCancel: true))
                            {
                                caster.Battle.Log("Hurling Charge converted to basic Strike.");
                                caster.Actions.ActionsLeft += 1;
                                return;
                            }
                            if (!caster.HasFreeHand)
                                return;
                            List<Item> weapons = [];
                            List<string> weaponNames = [];
                            foreach (Item item in caster.CarriedItems.Where(i => i.HasTrait(Trait.Weapon)))
                            {
                                weapons.Add(item);
                                weaponNames.Add(item.Name);
                            }
                            ChoiceButtonOption choice = await caster.AskForChoiceAmongButtons(IllustrationName.Swords, "Choose a weapon to draw.",
                                weaponNames.ToArray());
                            Item drawn = weapons[choice.Index];
                            caster.HeldItems.Add(drawn);
                            if (caster.HasEffect(QEffectId.Rage) && caster.Battle.AllCreatures.Any(cr => cr.IsAdjacentTo(caster) && cr.EnemyOf(caster)))
                            {
                                QEffect offGuard = new("Off-Guard", $"Off-guard to the next Strike made with {weaponNames[choice.Index]} by {caster} due to Hurling Charge.", ExpirationCondition.ExpiresAtEndOfSourcesTurn, caster, IllustrationName.Flatfooted)
                                {
                                    AfterYouAreTargeted = (effect, action) =>
                                    {
                                        if (!action.HasTrait(Trait.Strike) || action.Owner != caster || action.Item != drawn)
                                            return Task.CompletedTask;
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                        return Task.CompletedTask;
                                    },
                                    CannotExpireThisTurn = true,
                                    IsFlatFootedTo = (_, _, strike) =>
                                    {
                                        if (strike == null || !strike.HasTrait(Trait.Strike) ||
                                            strike.Owner != caster || strike.Item != drawn)
                                            return null;
                                        return "Hurling Charge";
                                    }
                                };
                                foreach (Creature enemy in caster.Battle.AllCreatures.Where(cr => cr.IsAdjacentTo(caster) && cr.EnemyOf(caster)))
                                {
                                    enemy.AddQEffect(offGuard);
                                }
                            }
                        });
                    return weapon.WeaponProperties is { Throwable: true } ? hurl : null;
                };
            });
        yield return hurlingCharge;
        
        yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.ReactiveShield, MTraits.Viking, 4);
        
        if (ModManager.TryParse("MoreDedications." + "Class.Fighter.ShieldedStride", out FeatName shieldStride))
            yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(shieldStride, MTraits.Viking, 6);
        yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.QuickShieldBlock,  MTraits.Viking, 10);
        
        List<Trait> vikingWeapons = [Trait.Longsword, Trait.BattleAxe, Trait.Shortsword, Trait.Shield];
        if (ModManager.TryParse("RL_Hatchet", out Trait hatchet))
            vikingWeapons.Add(hatchet);
        Feat vikingCombat = new TrueFeat(
                ModManager.RegisterFeatName("RE_VikingWeaponFamiliarity", "Viking Weapon Familiarity"), 4,
                "From childhood, you have been exposed to traditional viking combat techniques, and you soon learned to handle axe, sword, and shield in battle. Now, you can raid proudly alongside your fellows.",
                "You gain the Shield Block reaction. Additionally, you have familiarity with the battle axe, hatchet, longsword, shield, and shortsword—for the purposes of proficiency, you treat any of these weapons as simple weapons." +
                "\n\nAt 5th level, whenever you get a critical hit with one of these weapons, you get its critical specialization effect.",
                [])
            .WithAvailableAsArchetypeFeat(MTraits.Viking)
            .WithOnSheet(values =>
            {
                values.GrantFeat(FeatName.ShieldBlock); ;
                foreach (Trait weapon in vikingWeapons)
                {
                    values.Proficiencies.AutoupgradeAlongBestWeaponProficiency(
                        [Trait.Simple, weapon]);
                }
                values.Proficiencies.AddProficiencyAdjustment(
                    traits => traits.Any(vikingWeapons.Contains) && traits.Contains(Trait.Martial),
                    Trait.Simple);
                
            })
            .WithPermanentQEffect("As long as you're at least level 5, Viking weapons trigger {tooltip:criteffect}critical specialization effects{/}.",
                qf =>
                {
                    qf.YouHaveCriticalSpecialization = (qfThis, item,_,_) =>
                        qfThis.Owner.Level >= 5 && item.Traits.Any(vikingWeapons.Contains);
                });
        yield return vikingCombat;

        const string intoTheFrayDescription = " Make two melee Strikes during this movement, one with your one-handed melee weapon and one with your shield. You can make these Strikes at any points during your movement (if you Leap, you may only make these Strikes before or after you Leap), and each must target a different enemy. Both attacks count toward your multiple attack penalty, but don't increase your penalty until you have made both attacks.";
        Feat intoTheFray = new TrueFeat(ModManager.RegisterFeatName("RE_IntoTheFray", "Into the Fray"), 8,
                "You charge into battle with shield-splintering fury.",
                "You Leap or Stride." + intoTheFrayDescription, [])
            .WithAvailableAsArchetypeFeat(MTraits.Viking)
            .WithActionCost(2)
            .WithPermanentQEffect("You Leap or Stride, making two melee Strikes during the movement with a one-handed melee weapon and a shield, without increasing your multiple attack penalty until the strikes are made.", qf =>
            {
                qf.ProvideMainAction = _ =>
                {
                    CombatAction frayStride = IntoTheFray(qf.Owner, true);
                    CombatAction frayLeap =  IntoTheFray(qf.Owner, false);
                    return new SubmenuPossibility(MIllustrations.CreateIllustration("IntoTheFray"), "Into the Fray")
                    {
                        SpellIfAny = new CombatAction(qf.Owner, MIllustrations.CreateIllustration("IntoTheFray"), "Into the Fray", [],
                            "You Leap or Stride." + intoTheFrayDescription, Target.Self()).WithActionCost(2),
                        Subsections = [new PossibilitySection("Into the Fray")
                        {
                            Possibilities = [new ActionPossibility(frayStride), new ActionPossibility(frayLeap)]
                        }]
                        
                    };

                    static CombatAction IntoTheFray(Creature owner, bool stride)
                    {
                        return new CombatAction(owner, new SideBySideIllustration(MIllustrations.CreateIllustration("IntoTheFray"), stride ? IllustrationName.FleetStep : IllustrationName.Jump), "Into the Fray - " + (stride ? "Stride" : "Leap"),
                        [Trait.Basic], (stride ? "You Stride." : "You Leap.") + (stride ? intoTheFrayDescription.Replace(" (if you Leap, you may only make these Strikes before or after you Leap)", "") : intoTheFrayDescription),
                        Target.Self()
                            .WithAdditionalRestriction(self =>
                            {
                                if (self.HasEffect(QEffectId.Immobilized) || !CommonCombatActions.StepByStepStride(self)
                                        .WithActionCost(0).CanBeginToUse(self))
                                    return "Must be able to move.";
                                return self.MeleeWeapons.Any(item =>
                                           item.HasTrait(Trait.Melee) && !item.HasTrait(Trait.TwoHanded) &&
                                           !item.HasTrait(Trait.Shield)) &&
                                       self.HeldItems.Any(item => item.HasTrait(Trait.Shield) && item.HasTrait(Trait.Melee))
                                    ? null
                                    : "Must be wielding a one-handed melee weapon and a shield.";
                            }))
                        .WithActionCost(2)
                        .WithEffectOnChosenTargets(async (action, self, _) =>
                        {
                            int map = self.Actions.AttackedThisManyTimesThisTurn;
                            QEffect counter = Counter();
                            self.AddQEffect(counter);
                            var movedSoFar = 0;
                            var leaped = 0;
                            Option chosen;
                            List<Item> weaponsAvailable = [self.HeldItems[0], self.HeldItems[1]];
                            HashSet<Creature> attacked = [];
                            var canRevert = true;

                            // Do once, showing movement options and strike options.
                            do
                            {
                                movedSoFar += counter.Value + leaped;
                                counter.Value = 0; // Reset so it doesn't keep adding

                                if (weaponsAvailable.Count < 2
                                    || movedSoFar > 0)
                                    canRevert = false;

                                List<Option> options =
                                [
                                    new CancelOption(true),
                                    new PassViaButtonOption(canRevert ? "Cancel" : "Pass")
                                ];
                                CombatAction leapAction = CommonCombatActions.Leap(self).WithActionCost(0)
                                    .With(ca =>
                                    {
                                        ca.WithEffectOnChosenTargets(async (_, _, _) =>
                                        {
                                            leaped += 1;
                                        });
                                        if (ca.Target is TileTarget tileTarget)
                                            tileTarget.WithAdditionalSelfRequirement(_ =>
                                                leaped == 0 ? Usability.Usable : Usability.NotUsable("Unusable"));
                                    });
                                switch (stride)
                                {
                                    // Get the hidden basic Stride from your possibilities
                                    // // Add move options to the list
                                    case true when Possibilities.Create(self).Filter(ap =>
                                                       {
                                                           if (ap.CombatAction.ActionId != ActionId.Stride)
                                                               return false;
                                                           ap.CombatAction.ActionCost = 0;
                                                           ap.RecalculateUsability();
                                                           return true;
                                                       })
                                                       .CreateActions(true)
                                                       .FirstOrDefault(ica =>
                                                           ica.Action.ActionId == ActionId.Stride) is CombatAction moveAction
                                                   && moveAction.Target.CanBeginToUse(self):
                                    {
                                        IList<Tile> floodfill = Pathfinding
                                            .Floodfill(
                                                self, self.Battle,
                                                new PathfindingDescription
                                                {
                                                    Squares = self.Speed - movedSoFar,
                                                    Style = new MovementStyle
                                                        { MaximumSquares = self.Speed - movedSoFar }
                                                })
                                            .Where(tile => tile.LooksFreeTo(self))
                                            .ToList();

                                        options.AddRange(floodfill
                                            .Select(tile => moveAction
                                                .CreateUseOptionOn(tile)
                                                .WithIllustration(moveAction.Illustration))
                                            .ToList());
                                        break;
                                    }
                                    case false when leapAction.CanBeginToUse(self):
                                        IList<Tile> floodfill2 = Pathfinding
                                            .Floodfill(
                                                self, self.Battle,
                                                new PathfindingDescription
                                                {
                                                    Squares = CommonCombatActions.DetermineLeapDistance(self)
                                                })
                                            .Where(tile => tile.LooksFreeTo(self))
                                            .ToList();
                                        options.AddRange(floodfill2
                                            .Select(tile => leapAction
                                                .CreateUseOptionOn(tile)
                                                .WithIllustration(leapAction.Illustration))
                                            .ToList());
                                        break;
                                }
                                // Add both weapons to the list
                                foreach (Item weapon in weaponsAvailable)
                                {
                                    // Get strike options that use this weapon
                                    options.AddRange(GetStrikePossibilities(
                                            self,
                                            strike =>
                                                strike.Item == weapon
                                                && strike.HasTrait(Trait.Melee),
                                        strike =>
                                            strike.WithEffectOnEachTarget(async (_, _, target, _) =>
                                            {
                                                weaponsAvailable.Remove(weapon);
                                                attacked.Add(target);
                                            }),
                                        cr => !attacked.Contains(cr), null, map));
                                }

                                if (options.Count == 2)
                                    chosen = options[0];
                                else
                                    chosen = (await self.Battle.SendRequest(new AdvancedRequest(
                                            self,
                                            $"Choose where to {(stride ? "move" : "leap to")} and who to Strike with Into the Fray, or right-click to cancel.",
                                            options)
                                        {
                                            IsMainTurn = false,
                                            IsStandardMovementRequest = true,
                                            TopBarIcon = action.Illustration,
                                            TopBarText =
                                                $"Choose where to {(stride ? "move" : "leap to")} and who to Strike with Into the Fray."
                                        }))
                                        .ChosenOption;

                                if (chosen is CancelOption or PassViaButtonOption)
                                {
                                    if (canRevert)
                                        action.RevertRequested = true;
                                    else if (weaponsAvailable.Count == 1 && movedSoFar == 0) // Only struck once
                                    {
                                        action.RevertRequested = true;
                                        action.SpentActions = 1;
                                        self.Battle.Log("Into the Fray converted into a simple Strike.");
                                    }
                                    else if (weaponsAvailable.Count == 2 && movedSoFar > 0) // Only moved
                                    {
                                        action.RevertRequested = true;
                                        action.SpentActions = 1;
                                        self.Battle.Log($"Into the Fray converted into a simple {(stride ? "Stride" : "Leap")}.");
                                    }
                                    return;
                                }

                                await chosen.Action();
                            }
                            // End loop whenever you cancel it.
                            while (chosen is not CancelOption and not PassViaButtonOption);
                            counter.ExpiresAt = ExpirationCondition.Immediately;
                        });
                    }
                };
            });
        yield return intoTheFray;
    }

    public static QEffect Counter()
    {
        return new QEffect
        {
            StateCheck = qff =>
            {
                Creature innerSelf = qff.Owner;
                if (innerSelf.AnimationData.LongMovement?.Path == null) return;
                var move = 0;
                var diagonals = 0;
                for (var index = 0;
                     index < innerSelf.AnimationData.LongMovement.Path
                         .Count;
                     index++)
                {
                    Tile tile =
                        innerSelf.AnimationData.LongMovement.Path[index];
                    List<Tile> tiles = innerSelf.AnimationData.LongMovement.Path.ToList();
                    if (tile.GetWalkDifficulty(innerSelf) >= 1)
                        move += tile.GetWalkDifficulty(innerSelf);
                    switch (index)
                    {
                        case >= 1 when tiles.Count > 1:
                        {
                            if (Equals(tile.Neighbours.BottomLeft?.Tile,
                                    tiles[index - 1]) ||
                                Equals(tile.Neighbours.BottomRight?.Tile,
                                    tiles[index - 1]) ||
                                Equals(tile.Neighbours.TopLeft?.Tile,
                                    tiles[index - 1]) ||
                                Equals(tile.Neighbours.TopRight?.Tile,
                                    tiles[index - 1]))
                                diagonals += 1;
                            break;
                        }
                        case 0 when tiles.Count > 1:
                        {
                            if (Equals(tile.Neighbours.BottomLeft?.Tile,
                                    innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                Equals(tile.Neighbours.BottomRight?.Tile,
                                    innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                Equals(tile.Neighbours.TopLeft?.Tile,
                                    innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                Equals(tile.Neighbours.TopRight?.Tile,
                                    innerSelf.AnimationData.LongMovement.OriginalTile))
                                diagonals += 1;
                            break;
                        }
                    }
                }
                if (diagonals > 1) move += diagonals / 2;
                qff.Value = move;
            },
            Id = MQEffectIds.Counter
        };
    }

    internal static async Task<bool> LimitedStride(int value, Creature mover, string description)
    {
        List<Option> tileOptions =
        [
            new CancelOption(true)
        ];
        CombatAction? moveAction = Possibilities.Create(mover)
            .Filter(ap =>
            {
                if (ap.CombatAction.ActionId != ActionId.Stride)
                    return false;
                ap.CombatAction.ActionCost = 0;
                ap.RecalculateUsability();
                return true;
            }).CreateActions(true).FirstOrDefault(pw =>
                pw.Action.ActionId == ActionId.Stride) as CombatAction;
        IList<Tile> floodFill = Pathfinding.Floodfill(mover, mover.Battle,
                new PathfindingDescription
                {
                    Squares = mover.Speed - value,
                    Style = { MaximumSquares = mover.Speed - value }
                })
            .Where(tile =>
                tile.LooksFreeTo(mover))
            .ToList();
        floodFill.ForEach(tile =>
        {
            if (moveAction == null ||
                !(bool)moveAction.Target.CanBeginToUse(mover)) return;
            tileOptions.Add(moveAction.CreateUseOptionOn(tile)
                .WithIllustration(moveAction.Illustration));
        });
        Option chosenTile = (await mover.Battle.SendRequest(
            new AdvancedRequest(mover,
                description,
                tileOptions)
            {
                IsMainTurn = false,
                IsStandardMovementRequest = true,
                TopBarIcon = mover.Illustration,
                TopBarText = description
            })).ChosenOption;
        switch (chosenTile)
        {
            case CancelOption:
                break;
            case TileOption tOpt:
                await tOpt.Action();
                return true;
        }
        return false;
    }
    
    public static List<Option> GetStrikePossibilities(
        Creature self,
        Func<CombatAction, bool>? isValidStrike,
        Action<CombatAction>? adjustStrike,
        Func<Creature, bool>? isValidTarget,
        StrikeModifiers? strikeModifiers = null,
        int map = -1)
    {
        List<Option> options = [];
        foreach (Item item in self.Weapons)
        {
            CombatAction strike = StrikeRules.CreateStrike(
                self,
                item,
                item.HasTrait(Trait.Ranged)
                    ? RangeKind.Ranged
                    : RangeKind.Melee,
                map, item.WeaponProperties!.Throwable && !item.HasTrait(Trait.Melee), strikeModifiers);
            FilterAndAdd(strike);
            // If this is a melee weapon that can be thrown, add another possibility
            if (item.HasTrait(Trait.Melee) && item.WeaponProperties!.Throwable)
            {
                CombatAction thrown = StrikeRules.CreateStrike(
                    self,
                    item,
                    RangeKind.Ranged,
                    map,
                    true, strikeModifiers);
                FilterAndAdd(thrown);
            }
        }
        return options;

        void FilterAndAdd(CombatAction strike)
        {
            strike.WithActionCost(0);
            if (strike.Item!.HasTrait(Trait.Ranged))
                strike.WithSoundEffect(strike.SoundEffectName ?? SfxName.Bow);
            if (isValidStrike?.Invoke(strike) is false)
                return;
            adjustStrike?.Invoke(strike);
            if (isValidTarget != null)
                ((CreatureTarget) strike.Target).CreatureTargetingRequirements.Add(new LegacyCreatureTargetingRequirement((a, d) =>
                    !isValidTarget(d)
                        ? Usability.NotUsableOnThisCreature("excluded")
                        : Usability.Usable));
            GameLoop.AddDirectUsageOnCreatureOptions(strike, options);
        }
    }
}