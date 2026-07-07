using System.Runtime.CompilerServices;
using Dawnsbury;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
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
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.ThirdParty.SteamApi;
using Microsoft.Xna.Framework;
using SpiritDamage;
using static RemasterExpanded.ClassChangesAndFeats.ChampionRemaster;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.ClassChangesAndFeats;

public static class ChampionReactionLogics
{
  internal static void AddChampionsReactionStateCheck(QEffect qfChampion, Action<QEffect> adjustTechnical)
  {
    qfChampion.StateCheck = qfChampionsReaction =>
    {
      Creature champion = qfChampionsReaction.Owner;
      if (!champion.Actions.CanTakeReaction()) return;
      foreach (Creature creature in champion.Battle.AllCreatures.Where(cr =>
                 cr.FriendOf(champion) && cr != champion && cr.DistanceTo(champion) <= GetChampionAuraRange(champion)))
      {
        QEffect qEffect = new(ExpirationCondition.Ephemeral);
        adjustTechnical(qEffect);
        creature.AddQEffect(qEffect);
      }
    };
  }

  internal static bool OathedAgainst(Creature champion, Creature attacker)
  {
    QEffect? effect = champion.FindQEffect(QEffectId.BasicOath);
    return effect != null && attacker.Traits.Contains((Trait)(effect.Tag ?? Trait.None));
  }
  
  internal static bool DefendedAgainst(Creature champion, Creature defender, Creature attacker)
  {
    QEffect? effect = defender.FindQEffect(MQEffectIds.DefendedAgainst);
    return effect != null && effect.Source == champion && attacker.Traits.Contains((Trait)(effect.Tag ?? Trait.None));
  }

  internal static QEffect ChampionReactedAgainst(Creature source)
  {
    return new QEffect { Id = MQEffectIds.ChampionReactedAgainst, Source = source }.WithExpirationAtEndOfSourcesNextTurn(source,
      true);
  }

  internal static void AddLiberationReactionLogic(Feat feat)
  {
    feat.WithOnCreature(champion =>
    {
      QEffect qfChampion = new("Liberating Step {icon:Reaction}",
        "Whenever an enemy damages or grapples your ally, and both are within 15 feet of you, you can spend {icon:Reaction} a reaction. If you do, if this reaction was triggered by an ally taking damage, prevent an amount of that damage equal to 2 plus your level. Either way, if the ally is grappled, it can then attempt to Escape as a free action. Finally, the ally can take a Step as a free action.");
      AddChampionsReactionStateCheck(qfChampion, technicalQEffect =>
      {
        technicalQEffect.YouAreDealtDamageReaction = (qfAlly, damageEvent) =>
        {
          Creature? attacker = damageEvent.Source;
          int num = DefendedAgainst(champion, qfAlly.Owner,attacker) ? 7 : 2;
          int howMuchDamageToReduce = Math.Min(champion.Level + num, damageEvent.TotalResolvedDamage);
          // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
          if (attacker == null || !attacker.Space.OccupiesSpace || !attacker.EnemyOf(champion) ||
              attacker.DistanceTo(champion) > GetChampionAuraRange(champion) || damageEvent.CombatAction != null &&
              damageEvent.CombatAction is not { IsHostileAction: true })
            return null;
          return ReactionOption.CreateCustom("Liberating Step",
              $"Prevent {S.AllOrNumber(howMuchDamageToReduce, damageEvent.TotalResolvedDamage)} of this damage, and allow {qfAlly.Owner.ToColoredName()} to take a Step.",
              null, champion, (Func<Task>)(async () =>
              {
                champion.Overhead("liberating step!", Color.Lime, champion + " uses liberating step!");
                Steam.CollectAchievement("CHAMPION");
                attacker.AddQEffect(ChampionReactedAgainst(champion));
                qfAlly.Owner.AddQEffect(new QEffect(ExpirationCondition.Never)
                {
                  StateCheckLayer = 1,
                  StateCheckWithVisibleChanges = async qfAfterwards =>
                  {
                    qfAfterwards.StateCheckWithVisibleChanges = null;
                    qfAfterwards.ExpiresAt = ExpirationCondition.Immediately;
                    await PhaseTwoAndThree(attacker, qfAlly.Owner);
                  }
                });
                damageEvent.ReduceBy(howMuchDamageToReduce, "Liberating Step");
              }))
            .WithIsReaction()
            .WithTraits(Trait.ChampionsReaction);
        };
        technicalQEffect.AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfTechnical, grapple) =>
        {
          Creature attacker = grapple.Owner;
          Creature defender = qfTechnical.Owner;
          if (grapple.ActionId != ActionId.Grapple)
          {
          }
          else if (!attacker.Space.OccupiesSpace)
          {
          }
          else if (!attacker.EnemyOf(champion))
          {
          }
          else if (attacker.DistanceTo(champion) > 3)
          {
          }
          else if (!defender.HasEffect(QEffectId.Grappled))
          {
          }
          else if (!defender.QEffects.Any(qff => qff.Id == QEffectId.Grappled && qff.Source == attacker))
          {
          }
          else if (!await champion.Battle.AskToUseReaction(champion,
                     $"{attacker} has grabbed {defender}. Use your champion's reaction to allow them to try and Escape?",
                     [Trait.ChampionsReaction]))
          {
          }
          else
          {
            champion.Overhead("liberating step!", Color.Lime, champion + " uses liberating step!");
            Steam.CollectAchievement("CHAMPION");
            await PhaseTwoAndThree(attacker, defender);
          }
        });
      });
      return qfChampion;

      async Task PhaseTwoAndThree(Creature attacker, Creature defender)
      {
        QEffect? grappled = defender.QEffects.FirstOrDefault(qff => qff.Id == QEffectId.Grappled);
        if (grappled != null)
        {
          CombatAction escape = Possibilities.CreateEscape(defender, grappled);
          escape.WithActionCost(0);
          escape.ChosenTargets = ChosenTargets.CreateSingleTarget(defender);
          await escape.AllExecute();
          defender.Battle.GameLoop.NewStateCheckRequired = true;
          if (champion.HasEffect(QEffectId.DivineSmite) && grappled.Source != null &&
              grappled.Source.HasTrait(Trait.Evil) && champion.Abilities.Charisma >= 1)
          {
            grappled.Source.Overhead("divine smite!", Color.Yellow,
              $"{champion} smote {grappled.Source}, dealing persistent spirit damage.");
            grappled.Source?.AddQEffect(RelentlessSpiritDamage(champion));
          }
        }

        List<Creature> creatureList = [defender];
        if (champion.HasEffect(QEffectId.ChampionExalt))
          creatureList.AddRange(champion.Battle.AllCreatures.Where(cr =>
            cr.FriendOf(champion) && cr != defender && cr.DistanceTo(champion) <= GetChampionAuraRange(champion)));
        foreach (Creature stepper in creatureList)
        {
          QEffect unimpededStep = new("Unimpeded Step",
            "You ignore difficult terrain for the duration of the Liberating Step.")
          {
            Id = QEffectId.IgnoresDifficultTerrain
          };
          if (champion.HasEffect(QEffectId.UnimpededStep)) stepper.AddQEffect(unimpededStep);
          if (await stepper.StepAsync($"Step as {stepper}.", allowPass: true) && OathedAgainst(champion, attacker))
          {
            await stepper.StepAsync($"Step again (as {stepper}.", allowPass: true);
          }

          unimpededStep.ExpiresAt = ExpirationCondition.Immediately;
        }
      }
    });
  }

  internal static void AddRedemptionReactionLogic(Feat feat)
  {
    feat.WithOnCreature(champion =>
    {
      QEffect qfChampion = new("Glimpse of Redemption {icon:Reaction}", "Whenever an enemy damages your ally, and both are within your champion's aura, you can spend {icon:Reaction} a reaction. If you do, the enemy must choose one—either all the damage is prevented; or an amount of damage is prevented equal to 2 plus your level, and the enemy then becomes enfeebled 2 until the end of its next turn.");
      AddChampionsReactionStateCheck(qfChampion, technicalQEffect => technicalQEffect.YouAreDealtDamageReaction = (qfAlly, damageEvent) =>
      {
        Creature attacker = damageEvent.Source;
        Creature defender = qfAlly.Owner;
        int num12 = OathedAgainst(champion, attacker) || DefendedAgainst(champion, qfAlly.Owner, attacker) ? 7 : 2;
        int howMuchDamageToReduce = Math.Min(champion.Level + num12, damageEvent.TotalResolvedDamage);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (attacker == null || !attacker.Space.OccupiesSpace || !attacker.EnemyOf(champion) || attacker.DistanceTo(champion) > GetChampionAuraRange(champion) || damageEvent.CombatAction is not
            {
              IsHostileAction: true
            })
          return null;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new(70, 2);
        interpolatedStringHandler.AppendLiteral("Prevent ");
        ref DefaultInterpolatedStringHandler local = ref interpolatedStringHandler;
        string str = howMuchDamageToReduce != damageEvent.TotalResolvedDamage ? $"at least {{b}}{howMuchDamageToReduce}{{/b}}" : "{b}all{/b}";
        local.AppendFormatted(str);
        interpolatedStringHandler.AppendLiteral(" of this damage, and possibly inflict negative conditions on ");
        interpolatedStringHandler.AppendFormatted(attacker.ToColoredName());
        interpolatedStringHandler.AppendLiteral(".");
        return (ReactionOptions) ReactionOption.CreateCustom("Glimpse of Redemption", interpolatedStringHandler.ToStringAndClear(), null, champion, (Func<Task>) (async () =>
        {
          champion.Overhead("glimpse of redemption!", Color.Cyan, champion + " uses glimpse of redemption!");
          Steam.CollectAchievement("CHAMPION");
          attacker.AddQEffect(ChampionReactedAgainst(champion));
          bool flag3 = await attacker.Battle.AskForConfirmation(attacker, IllustrationName.DesperatePrayer, $"{champion} uses Glimpse of Redemption against your {damageEvent.CombatAction.Name}. Proceed with it anyway, even though it means you'll become enfeebled 2?", "Proceed with " + damageEvent.CombatAction.Name, "Abort");
          if (champion.HasEffect(QEffectId.ChampionExalt))
          {
            foreach (Creature creature in champion.Neighbours.CreaturesWithinRadius(GetChampionAuraRange(champion)).Where(cr => cr.FriendOf(champion) && cr != defender).ToList())
            {
              QEffect qEffect = new QEffect
              {
                YouAreDealtDamage = async (_, a2, ds2, _) => ds2.Power != damageEvent.CombatAction || a2 != attacker ? null : (DamageModification) new ReduceDamageModification(Math.Min(champion.Level + 2, ds2.Amount), "Glimpse of Redemption Exalt reduction")
              }.WithExpirationAtEndOfOwnerTurn();
              creature.AddQEffect(qEffect);
            }
          }
          if (flag3)
          {
            bool flag4 = champion.HasEffect(QEffectId.WeightOfGuilt);
            if (flag4)
              flag4 = !await champion.AskForConfirmation(IllustrationName.DesperatePrayer, $"{attacker} proceeds with {damageEvent.CombatAction.Name} despite your Glimpse of Redemption. Choose how to afflict them with guilt.", "Enfeebled 2", "Stupefied 2");
            QEffect qEffect = !flag4 ? QEffect.Enfeebled(2) : QEffect.Stupefied(2);
            qEffect.WithExpirationAtEndOfOwnerTurn();
            qEffect.CannotExpireThisTurn = true;
            attacker.AddQEffect(qEffect);
            if (champion.HasEffect(QEffectId.DivineSmite) && attacker.HasTrait(Trait.Evil) && champion.Abilities.Charisma >= 1)
            {
              attacker.Overhead("divine smite!", Color.Yellow, $"{champion} smote {attacker}, dealing persistent spirit damage.");
              attacker.AddQEffect(RelentlessSpiritDamage(champion));
            }
            damageEvent.ReduceBy(howMuchDamageToReduce, "Glimpse of Redemption");
          }
          else
            damageEvent.ReduceBy(1000, "Glimpse of Redemption");
        })).WithTraits(Trait.ChampionsReaction).WithIsReaction();
      });
      return qfChampion;
    });
  }

  internal static void AddJusticeReactionLogic(Feat feat)
  {
    feat.WithOnCreature(champion =>
    {
      champion.Traits.Add(Trait.Paladin);
      QEffect qfChampion = new("Retributive Strike {icon:Reaction}", "Whenever an enemy damages your ally, and both are within your champion's aura, you can spend {icon:Reaction} a reaction. If you do, an amount of that damage equal to 2 plus your level is prevented, and if the enemy is within your reach, you make a free melee Strike against it.");
      AddChampionsReactionStateCheck(qfChampion, technicalQEffect => technicalQEffect.YouAreDealtDamageReaction = (qfAlly, damageEvent) =>
      {
        Creature attacker = damageEvent.Source;
        int num = DefendedAgainst(champion, qfAlly.Owner, attacker) ? 7 : 2;
        int howMuchDamageToReduce = Math.Min(champion.Level + num, damageEvent.TotalResolvedDamage);
        Creature defender = qfAlly.Owner;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (attacker != null)
        {
          Space space = attacker.Space;
          if (space is { OccupiesSpace: true } && attacker.EnemyOf(champion) && attacker.DistanceTo(champion) <= GetChampionAuraRange(champion))
          {
            if (damageEvent.CombatAction != null)
            {
              CombatAction combatAction = damageEvent.CombatAction;
              if (combatAction is not { IsHostileAction: true })
                goto label_5;
            }
            bool hasRangedReprisal = champion.HasEffect(QEffectId.RangedReprisal);
            return ReactionOption.CreateCustom("Retributive Strike", $"Prevent {S.AllOrNumber(howMuchDamageToReduce, damageEvent.TotalResolvedDamage)} of this damage and possibly Strike back.", null, champion, (Func<Task>) (async () =>
            {
              champion.Overhead("retributive strike!", Color.Orange, champion + " uses retributive strike!");
              Steam.CollectAchievement("CHAMPION");
              attacker.AddQEffect(ChampionReactedAgainst(champion));
              champion.AddQEffect(new QEffect(ExpirationCondition.Never)
              {
                StateCheckLayer = 1,
                StateCheckWithVisibleChanges = (Func<QEffect, Task>) (async qfStrikeBack =>
                {
                  qfStrikeBack.StateCheckWithVisibleChanges = null;
                  qfStrikeBack.ExpiresAt = ExpirationCondition.Immediately;
                  List<CombatAction> validStrikes = champion.Weapons.Where(wp => wp.HasTrait(Trait.Melee) | hasRangedReprisal).SelectMany(weapon => CreateReactiveAttacksFromWeapon(weapon, hasRangedReprisal)).ToList();
                  if (hasRangedReprisal)
                    await RangedReprisalStep(champion, attacker, validStrikes);
                  validStrikes.RemoveAll(ca => !CommonCombatActions.IsStrikeOk(ca, attacker));
                  CombatAction? selectedStrike;
                  if (validStrikes.Count == 0)
                  {
                    champion.Overhead("no reach", Color.White, $"The attacker is not within your weapon reach so {champion} can't make {champion.HisOrHer} retributive strike.");
                    selectedStrike = null;
                  }
                  else if (validStrikes.Count == 1)
                  {
                    selectedStrike = validStrikes[0];
                  }
                  else
                  {
                    CombatAction? primaryStrike = validStrikes.FirstOrDefault(ca => ca.Item == champion.PrimaryWeaponIncludingRanged);
                    if (primaryStrike != null && CommonCombatActions.ShouldUseStrikeAsPrimary(primaryStrike, champion, attacker))
                    {
                      selectedStrike = primaryStrike;
                    }
                    else
                    {
                      Illustration illustration = IllustrationName.Reaction;
                      var question = $"What weapon to use for the retributive strike against {attacker}?";
                      string[] array = validStrikes.Select(wp => $"{wp.Illustration.IllustrationAsIconString} {wp.ShortName}").ToArray();
                      selectedStrike = validStrikes[(await champion.AskForChoiceAmongButtons(illustration, question, array)).Index];
                    }
                  }
                  if (selectedStrike != null)
                  {
                    QEffect oathBonus = new()
                    {
                      BonusToDamage = (_, _, _) => new Bonus(champion.Proficiencies.Get(selectedStrike.Item!.Traits) >= Proficiency.Master ? 6 : 4, BonusType.Circumstance, "Oath")
                    };
                    if (OathedAgainst(champion, attacker))
                      champion.AddQEffect(oathBonus);
                    if (champion.HasEffect(QEffectId.DivineSmite))
                      selectedStrike.StrikeModifiers.OnEachTarget = Delegates.SmartCombineDelegates(selectedStrike.StrikeModifiers.OnEachTarget, async (attacker2, defender2, result2) =>
                      {
                        if (result2 < CheckResult.Success || attacker2.Abilities.Charisma < 1)
                          return;
                        defender2.Overhead("divine smite!", Color.Yellow, $"{attacker2} smote {defender2}, dealing persistent spirit damage.");
                        defender2.AddQEffect(RelentlessSpiritDamage(attacker2));
                      });
                    CreatureTarget target = (CreatureTarget) selectedStrike.Target;
                    if ((bool) selectedStrike.CanBeginToUse(champion) && (bool) target.IsLegalTarget(champion, attacker))
                    {
                      selectedStrike.ChosenTargets = ChosenTargets.CreateSingleTarget(attacker);
                      await selectedStrike.AllExecute();
                    }
                    oathBonus.ExpiresAt = ExpirationCondition.Immediately;
                  }
                  if (!champion.HasEffect(QEffectId.ChampionExalt))
                  {
                  }
                  else
                  {
                    foreach (Creature creature in champion.Neighbours.CreaturesWithinRadius(GetChampionAuraRange(champion)).Where(cr => cr.FriendOf(champion)).ToList())
                    {
                      Func<Creature, bool> isValidTarget = trg => trg == defender;
                      if (CommonCombatActions.GetStrikePossibilities(creature, true, isValidTarget).Count == 0)
                        continue;
                      if (!await creature.AskToUseReaction(
                            $"Make a Strike against {defender} with a -5 penalty thanks to {champion}'s Retributive Strike?"))
                        continue;
                      QEffect qfLess = new()
                      {
                        BonusToAttackRolls = (_, _, _) => new Bonus(-5, BonusType.Untyped, "Retributive Strike Exalt penalty")
                      };
                      creature.AddQEffect(qfLess);
                      if (!await CommonCombatActions.StrikeCreature(creature, isValidTarget, false, $"Don't strike as {creature}", true))
                        creature.Actions.RefundReaction();
                      creature.RemoveAllQEffects(qff => qff == qfLess);
                    }
                  }
                })
              });
              damageEvent.ReduceBy(howMuchDamageToReduce, "Retributive Strike");
            })).WithIsReaction().WithTraits(Trait.ChampionsReaction);
          }
        }
        label_5:
        return null;
      });
      return qfChampion;

      List<CombatAction> CreateReactiveAttacksFromWeapon(Item weapon, bool hasRangedReprisal)
      {
        List<CombatAction> strikes = [];
        CombatAction attackFromWeapon = champion.CreateStrike(weapon, 0).WithActionCost(0);
        attackFromWeapon.Traits.AddRange([Trait.ReactiveAttack, Trait.ChampionsReaction]);
        strikes.Add(attackFromWeapon);
        if (!hasRangedReprisal || !(weapon.WeaponProperties?.Throwable ?? false)) return strikes;
        CombatAction thrown = StrikeRules.CreateStrike(champion, weapon, RangeKind.Ranged, 0, true).WithActionCost(0);
        thrown.Traits.AddRange([Trait.ReactiveAttack, Trait.ChampionsReaction]);
        strikes.Add(thrown);
        return strikes;
      }
    });
  }
  private static async Task RangedReprisalStep(Creature champion, Creature attacker, List<CombatAction> possibleStrikes)
  {
    champion.Actions.NextStrideIsFree = true;
    champion.RegeneratePossibilities();
    List<ICombatAction> actions = champion.Possibilities.CreateActions(false);
    List<Option> options = [];
    if (champion.Speed > 0)
    {
      Tile topLeftTile = champion.Space.TopLeftTile;
      TBattle battle = champion.Battle;
      foreach (Tile tile in Pathfinding.Floodfill(champion, battle, new PathfindingDescription()
               {
                 Squares = 2,
                 Style = new MovementStyle
                 {
                   Shifting = true,
                   PermitsStep = true,
                   MaximumSquares = 2
                 }
               }))
      {
        if (Equals(tile, champion.Space.TopLeftTile) || !tile.InIteration.Steppable ||
            actions.FirstOrDefault(pw => pw.Action.ActionId == ActionId.Step) is not CombatAction combatAction ||
            !(bool)combatAction.Target.CanBeginToUse(champion)) continue;
        champion.Space.TemporaryTranslateTo(tile);
        if (possibleStrikes.Any(strike => CommonCombatActions.IsStrikeOk(strike, attacker)))
          options.Add(combatAction.CreateUseOptionOn(tile).WithIllustration(combatAction.Illustration));
      }
      options.Add(new PassViaButtonOption("Don't step"));
      champion.Space.TemporaryTranslateTo(topLeftTile);
      Option? option = options.Count switch
      {
        1 => options[0],
        > 1 => (await champion.Battle.SendRequest(
          new AdvancedRequest(champion, "Step towards the triggered enemy for retributive strike.", options)
          {
            IsMainTurn = false,
            DisplacedCreature = champion,
            IsStandardMovementRequest = true,
            TopBarIcon = (Illustration)IllustrationName.WarpStep,
            TopBarText = "Step towards the triggered enemy for retributive strike."
          })).ChosenOption,
        _ => null
      };
      if (option != null)
      {
        await option.Action();
      }
    }
    champion.Battle.MovementConfirmer = null;
    champion.Actions.NextStrideIsFree = false;
  }

  public static QEffect HolyOrUnholyPersistent(QEffect effect, Trait trait, string dice, DamageKind kind)
  {
    effect.EndOfYourTurnDetrimentalEffect = async (qf, self) =>
    {
      CombatAction power = CombatAction.CreateSimple(self.Battle.Pseudocreature, "Persistent damage").WithOrigin(
        new ActionOrigin
        {
          Source = qf.Source,
          QEffect = qf
        });
      CombatAction? sourceAction = qf.SourceAction;
      if ((sourceAction != null ? sourceAction.HasTrait(Trait.IgnoresHardness) ? 1 : 0 : 0) != 0)
        power.Traits.Add(Trait.IgnoresHardness);
      power.Traits.Add(trait);
      await CommonSpellEffects.DealDirectDamage(power, DiceFormula.FromText(dice, "Persistent damage"), self, CheckResult.Failure, kind);
      if (self.DeathScheduledForNextStateCheck ||
          self.Actions.HasDelayedYieldingTo != null && !self.HasTrait(Trait.AnimalCompanion))
        return;
      qf.RollPersistentDamageRecoveryCheck(false);
    };
    return effect;
  }

  public static QEffect RelentlessSpiritDamage(Creature attacker)
  {
    Trait? trait = attacker.Traits.FirstOrDefault(tr => tr == HolyTrait.Holy || tr == UnholyTrait.Unholy);
    trait ??= Trait.None;
    return HolyOrUnholyPersistent(QEffect.PersistentDamage(attacker.Abilities.Charisma.ToString(), DamageSpirit.Spirit), trait.Value, attacker.Abilities.Charisma.ToString(), DamageKind.Spirit);
  }
}