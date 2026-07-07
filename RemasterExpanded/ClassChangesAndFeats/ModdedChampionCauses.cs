using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Dawnsbury.ThirdParty.SteamApi;
using Microsoft.Xna.Framework;
using static RemasterExpanded.ClassChangesAndFeats.ChampionReactionLogics;
using static RemasterExpanded.ClassChangesAndFeats.ChampionRemaster;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.ClassChangesAndFeats;

public static class ModdedChampionCauses
{
    internal static readonly QEffectId BrilliantFlashId = SafelyRegister<QEffectId>("PS_BrilliantFlash");
    internal static readonly QEffectId EvilChampDamage = SafelyRegister<QEffectId>("PS_EvilChampDamage");
    public static readonly ActionId GruesomeStrikeId = SafelyRegister<ActionId>("PS_GruesomeStrikeId");
    public static readonly QEffectId IronRepercussions = SafelyRegister<QEffectId>("PS_IronRepercussions");
    public static readonly QEffectId OngoingSelfishness = SafelyRegister<QEffectId>("PS_OngoingSelfishness");
    internal static readonly ModdedIllustration IronCommandIllustration = new("Champions of Evil Assets/PS_IronCommand.png");
    internal static void AddGrandeurReactionLogic(Feat feat)
    {
        feat.WithOnCreature(champion =>
        {
              if(champion.PersistentCharacterSheet?.Class?.ClassTrait is not Trait.Champion && !champion.HasFeat(FeatName.ChampionsReaction))
              {
                  return new QEffect();
              }
              QEffect qEffect4 = new("Flash of Grandeur {icon:Reaction}", "Whenever an enemy damages your ally, and both are within your champion's aura, you can spend {icon:Reaction} a reaction. If you do, an amount of that damage equal to 2 plus your level is prevented, and the enemy is dazzled and affected by {i}faerie fire{/i} until the end of your next turn.");
              AddChampionsReactionStateCheck(qEffect4, delegate (QEffect technicalQEffect)
              {
                  technicalQEffect.YouAreDealtDamageReaction = delegate (QEffect qfAlly, DamageEvent damageEvent)
                  {
                      Creature attacker = damageEvent.Source;
                      Creature defender = qfAlly.Owner;
                      int num3 = OathedAgainst(champion, attacker) || DefendedAgainst(champion, qfAlly.Owner, attacker) ? 7 : 2;
                      int howMuchDamageToReduce = Math.Min(champion.Level + num3, damageEvent.TotalResolvedDamage);
                      return attacker is { Space.OccupiesSpace: true } && attacker.EnemyOf(champion) && attacker.DistanceTo(champion) <= GetChampionAuraRange(champion) && damageEvent.CombatAction is
                      {
                          IsHostileAction: true
                      } ? (ReactionOptions)ReactionOption.CreateCustom("Flash of Grandeur", $"Prevent {(howMuchDamageToReduce == damageEvent.TotalResolvedDamage ? "{b}all{/b}" : $"at least {{b}}{howMuchDamageToReduce}{{/b}}")} of this damage, and inflict dazzled and {{i}}faerie fire{{/i}} on {attacker.ToColoredName()}.", null, champion, async delegate
                      {
                          attacker.AddQEffect(ChampionReactedAgainst(champion));
                          champion.Overhead("flash of grandeur!", Color.Cyan, champion + " uses flash of grandeur!");
                          Steam.CollectAchievement("CHAMPION");
                          CombatAction revealingLight = new CombatAction(champion, IllustrationName.FaerieFire, "Revealing Light",
                          [Trait.Evocation, Trait.Light, Trait.DoNotShowInCombatLog, Trait.DoesNotProvoke, Trait.UnaffectedByConcealment, Trait.ExecuteEvenIfCasterCannotTakeActions
                          ], "", Target.Distance(999)).WithActionCost(0).WithEffectOnEachTarget(async delegate (CombatAction _, Creature rlCaster, Creature rlTarget, CheckResult _)
                          {
                              QEffect qEffect = QEffect.FaerieFire("Revealing Light", IllustrationName.FaerieFire).WithExpirationAtEndOfSourcesNextTurn(rlCaster, false);
                              rlTarget.AddQEffect(qEffect);
                              QEffect qEffect2 = QEffect.Dazzled().WithExpirationAtEndOfSourcesNextTurn(rlCaster, false);
                              rlTarget.AddQEffect(qEffect2);
                              if (rlCaster.HasEffect(QEffectId.DivineSmite))
                              {
                                  QEffect qEffect3 = RelentlessSpiritDamage(rlCaster);
                                  rlTarget.AddQEffect(qEffect3);
                                  qEffect.With(qf =>
                                      qf.BansPersistentDamageRecovery = (_, effect, _) => effect == qEffect3);
                              }
                          });
                          revealingLight.ChosenTargets = ChosenTargets.CreateSingleTarget(attacker);
                          await revealingLight.AllExecute();
                          if (champion.HasEffect(BrilliantFlashId))
                          {
                              attacker.AddQEffect(QEffect.FlatFooted("Brilliant Flash").WithExpirationAtStartOfSourcesTurn(champion, 1));
                          }
                          if (champion.HasEffect(QEffectId.ChampionExalt))
                          {
                              foreach (Creature item4 in (from cr in champion.Neighbours.CreaturesWithinRadius(GetChampionAuraRange(champion))
                                           where cr.FriendOf(champion) && cr != defender
                                           select cr).ToList())
                              {
                                  QEffect qEffect = QEffect.FaerieFire("Faerie Fire", IllustrationName.FaerieFire).WithExpirationAtEndOfSourcesNextTurn(champion, false);
                                  item4.AddQEffect(qEffect);
                                  QEffect qEffect2 = QEffect.Dazzled().WithExpirationAtEndOfSourcesNextTurn(champion, false);
                                  item4.AddQEffect(qEffect2);
                              }
                          }
                          damageEvent.ReduceBy(howMuchDamageToReduce, "Flash of Grandeur");
                      }).WithTraits(Trait.ChampionsReaction).WithIsReaction() : null;
                  };
              });
              return qEffect4;
        });
    }

    internal static void AddObedienceReactionLogic(Feat feat)
    {
        feat.WithOnCreature(delegate(Creature champion)
        {
            if (champion.PersistentCharacterSheet?.Class?.ClassTrait is not Trait.Champion && !champion.HasFeat(FeatName.ChampionsReaction))
            {
                return new QEffect();
            }
            int baseAmount = champion.Level >= 16 ? 3 : champion.Level >= 9 ? 2 : 1;
            champion.AddQEffect(new QEffect
            {
                AddExtraKindedDamageOnStrike = (action, defender2) =>
                    defender2.QEffects.Any(q => q.Id == EvilChampDamage && q.Source == champion)
                        ? new KindedDamage(
                            DiceFormula.FromText(
                                (((OathedAgainst(champion, defender2) ? baseAmount : 0) +
                                  baseAmount) * (action.ActionId == GruesomeStrikeId ? 2 : 1))
                                .ToString(), "Iron Command"), DamageKind.Spirit)
                        : null
            });
            EditFeatureDescription(champion);
            return new QEffect("Iron Command {icon:Reaction}",
                "Whenever an enemy within your champion's aura damages you, you can spend {icon:Reaction} a reaction. If you do, the enemy must choose one—either the enemy kneels, dropping prone; or you deal 1d6 mental damage to the enemy. This damage increases by 1d6 every four levels. In addition, your Strikes against the triggering creature deal 1 extra spirit damage until the end of your next turn. This extra damage increases to 2 at 9th level and 3 at 16th level.")
            {
                YouAreDealtDamageReaction = delegate(QEffect _, DamageEvent damageEvent)
                {
                    Creature attacker = damageEvent.Source;
                    int damage = (champion.Level + 3) / 4;
                    Space space = attacker.Space;
                    if (space is not { OccupiesSpace: true } || !attacker.EnemyOf(champion) ||
                        attacker.DistanceTo(champion) > GetChampionAuraRange(champion)) return null;
                    CombatAction? combatAction = damageEvent.CombatAction;
                    if (combatAction is not { IsHostileAction: true })
                    {
                        return null;
                    }
                    return ReactionOption.CreateCustom("Iron Command",
                        "Deal " + damage + "d6 mental damage or cause them to drop prone (their choice).",
                        null, champion, async delegate
                        {
                            attacker.AddQEffect(ChampionReactedAgainst(champion));
                            champion.Overhead("iron command!", Color.Crimson,
                                champion + " uses iron command!");
                            Steam.CollectAchievement("CHAMPION");
                            if (await attacker.Battle.AskForConfirmation(attacker, IronCommandIllustration,
                                    champion +
                                    " uses Iron Command against you. Will you refuse and take " + damage +
                                    "d6 mental damage, or will you kneel and drop prone?", "Refuse",
                                    "Kneel"))
                            {
                                bool flag = champion.HasEffect(IronRepercussions);
                                bool flag2 = flag;
                                if (flag2)
                                {
                                    flag2 = !await champion.AskForConfirmation(IronCommandIllustration,
                                        attacker +
                                        " refuses to kneel to Iron Command. Choose how to punish them.",
                                        "Persistent Damage", "Normal Damage");
                                }

                                if (!flag2)
                                {
                                    attacker.AddQEffect(QEffect.PersistentDamage(damage + "d6",
                                        DamageKind.Mental));
                                }
                                else
                                {
                                    await CommonSpellEffects.DealBasicDamage(
                                        CombatAction.CreateSimple(champion, "Iron Command"), champion,
                                        attacker, CheckResult.Failure, damage + "d6", DamageKind.Mental);
                                }

                                if (champion.HasEffect(QEffectId.DivineSmite) && champion.Abilities.Charisma >= 1)
                                {
                                    attacker.Overhead("divine smite!", Color.Yellow,
                                        champion + " smote " + attacker +
                                        ", dealing persistent spirit damage.");
                                    attacker.AddQEffect(RelentlessSpiritDamage(champion));
                                }

                                attacker.AddQEffect(new QEffect
                                {
                                    Id = EvilChampDamage,
                                    ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn,
                                    Source = champion
                                });
                            }
                            else
                            {
                                await attacker.FallProne();
                            }

                            if (champion.HasEffect(QEffectId.ChampionExalt))
                            {
                                foreach (Creature enemy in
                                         champion.Battle.AllCreatures.Where(cr =>
                                             cr.DistanceTo(champion) <= GetChampionAuraRange(champion) && cr != attacker &&
                                             cr.EnemyOf(champion)))
                                {
                                    if (await enemy.Battle.AskForConfirmation(enemy,
                                            IronCommandIllustration,
                                            champion +
                                            " uses Iron Command. Will you refuse and take " + damage +
                                            " mental damage, or will you kneel and drop prone?", "Refuse",
                                            "Kneel"))
                                    {
                                        await CommonSpellEffects.DealBasicDamage(
                                            CombatAction.CreateSimple(champion, "Iron Command"), champion,
                                            attacker, CheckResult.Failure, damage + "", DamageKind.Mental);
                                    }
                                    else
                                    {
                                        await enemy.FallProne();
                                    }
                                }
                            }
                        }).WithIsReaction().WithTraits(Trait.ChampionsReaction);
                }
            };
        });
    }

    internal static void AddDesecrationReactionLogic(Feat feat)
    {
        feat.WithOnCreature(delegate(Creature champion)
        {
            if (champion.PersistentCharacterSheet?.Class?.ClassTrait is not Trait.Champion &&
                !champion.HasFeat(FeatName.ChampionsReaction))
            {
                return new QEffect();
            }

            EditFeatureDescription(champion);
            int baseAmount = champion.Level >= 16 ? 3 : champion.Level >= 9 ? 2 : 1;
            champion.AddQEffect(new QEffect
            {
                AddExtraKindedDamageOnStrike = (action, defender2) =>
                    defender2.QEffects.Any(q => q.Id == EvilChampDamage && q.Source == champion)
                        ? new KindedDamage(
                            DiceFormula.FromText(
                                (((OathedAgainst(champion, defender2) ? baseAmount : 0) +
                                  baseAmount) * (action.ActionId == GruesomeStrikeId ? 2 : 1))
                                .ToString(), "Selfish Shield"), DamageKind.Spirit)
                        : null
            });
            return new QEffect("Selfish Shield {icon:Reaction}",
                "Whenever an enemy within 15 feet damages you, you can spend {icon:Reaction} a reaction. If you do, you gain resistance against the triggering damage equal to 2 + half your level. In addition, your Strikes against the triggering creature deal 1 extra evil or negative damage until the end of your next turn. This extra damage increases to 2 at 9th level.")
            {
                YouAreDealtDamageReaction = delegate(QEffect _, DamageEvent damageEvent)
                {
                    Creature attacker = damageEvent.Source;
                    int num2 = champion.HasEffect(QEffectId.DivineSmite) && champion.Abilities.Charisma > 2
                        ? champion.Abilities.Charisma
                        : 2;
                    int howMuchDamageToReduce = Math.Min(champion.Level / 2 + num2, damageEvent.TotalResolvedDamage);
                    Space space = attacker.Space;
                    if (space is not { OccupiesSpace: true } || !attacker.EnemyOf(champion) ||
                        attacker.DistanceTo(champion) > GetChampionAuraRange(champion)) return null;
                    CombatAction? combatAction = damageEvent.CombatAction;
                    if (combatAction is not { IsHostileAction: true })
                    {
                        return null;
                    }
                    return ReactionOption.CreateCustom("Selfish Shield",
                        "Prevent " + S.AllOrNumber(howMuchDamageToReduce, damageEvent.TotalResolvedDamage) +
                        " of this damage.", null, champion, async delegate
                        {
                            attacker.AddQEffect(ChampionReactedAgainst(champion));
                            champion.Overhead("selfish shield!", Color.Crimson,
                                champion + " uses selfish shield!");
                            Steam.CollectAchievement("CHAMPION");
                            attacker.AddQEffect(new QEffect
                            {
                                Id = EvilChampDamage,
                                ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn,
                                Source = champion
                            });
                            bool flag = champion.HasEffect(OngoingSelfishness);
                            if (flag)
                            {
                                int howMuchDamageToReduce2 = howMuchDamageToReduce / 2;
                                champion.AddQEffect(new QEffect
                                {
                                    ExpiresAt = ExpirationCondition.ExpiresAtEndOfAnyTurn,
                                    StateCheck = delegate(QEffect self)
                                    {
                                        self.Owner.WeaknessAndResistance.Resistances.Add(
                                            new SpecialResistance(attacker.Name + "'s damage",
                                                (attack, _) =>
                                                    attack != null && attack.Owner.Equals(attacker), howMuchDamageToReduce2, null));
                                    }
                                });
                            }

                            if (champion.HasEffect(QEffectId.ChampionExalt))
                            {
                                foreach (Creature enemy in
                                         champion.Battle.AllCreatures.Where(cr =>
                                             cr.DistanceTo(champion) <= GetChampionAuraRange(champion) && cr.EnemyOf(champion)))
                                {
                                    enemy.AddQEffect(new QEffect("Selfish Shield",
                                        "You takes a –1 status penalty to attack rolls against " +
                                        champion + ".",
                                        ExpirationCondition.ExpiresAtStartOfSourcesTurn, champion,
                                        IllustrationName.ShieldingStrike)
                                    {
                                        BonusToAttackRolls =
                                            (_, attack, de) =>
                                                (attack.HasTrait(Trait.Attack) && de != null &&
                                                 de == champion)
                                                    ? new Bonus(-1, BonusType.Status, "Selfish Shield", false)
                                                    : null
                                    });
                                }
                            }

                            damageEvent.ReduceBy(howMuchDamageToReduce, "Selfish Shield");
                        }).WithIsReaction().WithTraits(Trait.ChampionsReaction);
                }
            };
        });
    }

    internal static void AddIniquityReactionLogic(Feat feat)
    {
        feat.WithOnCreature(delegate(Creature champion)
        {
            if (champion.PersistentCharacterSheet?.Class?.ClassTrait is not Trait.Champion &&
                !champion.HasFeat(FeatName.ChampionsReaction))
                return new QEffect();
            EditFeatureDescription(champion);
            int baseAmount = champion.Level >= 16 ? 3 : champion.Level >= 9 ? 2 : 1;
            champion.AddQEffect(new QEffect
            {
                AddExtraKindedDamageOnStrike = (action, defender2) =>
                    defender2.QEffects.Any(q => q.Id == EvilChampDamage && q.Source == champion)
                        ? new KindedDamage(
                            DiceFormula.FromText(
                                (((OathedAgainst(champion, defender2) ? baseAmount : 0) +
                                  baseAmount * 2) * (action.ActionId == GruesomeStrikeId ? 2 : 1))
                                .ToString(), "Destructive Vengeance"), DamageKind.Spirit)
                        : null
            });
            return new QEffect("Destructive Vengeance {icon:Reaction}",
                "Whenever an enemy within your champion's aura damages you, you can spend {icon:Reaction} a reaction. If you do, you increase the amount of damage you take by 1d6, and you deal 1d6 spirit damage to the triggering enemy. This damage increases to 2d6 at 5th level, and to 3d6 at 9th level. In addition, your Strikes against the triggering creature deal 2 extra spirit damage until the end of your next turn. This extra damage increases to 4 at 9th level.")
            {
                YouAreDealtDamageReaction = delegate(QEffect _, DamageEvent damageEvent)
                {
                    Creature attacker = damageEvent.Source;
                    Creature defender = damageEvent.TargetCreature;
                    string howMuchDamage = (champion.Level + 3) / 4 + "d6";
                    Space space = attacker.Space;
                    if (space is not { OccupiesSpace: true } || !attacker.EnemyOf(champion) ||
                        attacker.DistanceTo(champion) > GetChampionAuraRange(champion)) return null;
                    CombatAction? combatAction = damageEvent.CombatAction;
                    if (combatAction is not { IsHostileAction: true })
                    {
                        return null;
                    }
                    return ReactionOption.CreateCustom("Destructive Vengeance",
                        "Deal " + howMuchDamage + " damage to yourself and your attacker?", null, champion,
                        async delegate
                        {
                            attacker.AddQEffect(ChampionReactedAgainst(champion));
                            champion.Overhead("destructive vengeance!", Color.Crimson,
                                champion + " uses destructive vengeance!");
                            Steam.CollectAchievement("CHAMPION");
                            CombatAction vengeance =
                                CombatAction.CreateSimple(defender, "Destructive Vengeance");
                            QEffect exaltDamage = new()
                            {
                                AfterYouTakeDamage = async delegate(QEffect _, int damage,
                                    DamageKind _, CombatAction? ca, bool _)
                                {
                                    if (ca is { Name: "Destructive Vengeance" })
                                    {
                                        CombatAction vengeance2 = CombatAction.CreateSimple(defender,
                                            "{i}{/i}Destructive Vengeance");
                                        foreach (Creature enemy in
                                                 champion.Battle.AllCreatures.Where(cr =>
                                                     cr.DistanceTo(champion) <= GetChampionAuraRange(champion) && cr != attacker &&
                                                     cr.EnemyOf(champion)))
                                        {
                                            await CommonSpellEffects.DealBasicDamage(vengeance2, defender,
                                                enemy, CheckResult.Failure, damage / 2 + "",
                                                DamageKind.Spirit);
                                        }
                                    }
                                }
                            };
                            if (champion.HasEffect(QEffectId.ChampionExalt))
                            {
                                attacker.AddQEffect(exaltDamage);
                            }

                            QEffect relentlessApplier = new()
                            {
                                AfterYouTakeDamage = async (effect, amount, _, action, _) =>
                                {
                                    if (amount == 0 || action != vengeance)
                                        return;
                                    effect.Owner.AddQEffect(RelentlessSpiritDamage(champion));
                                }
                            };

                            if (champion.HasEffect(QEffectId.DivineSmite))
                            {
                                attacker.AddQEffect(relentlessApplier);
                            }
                            await CommonSpellEffects.DealBasicDamage(vengeance, defender, attacker,
                                CheckResult.Failure, howMuchDamage, DamageKind.Spirit);
                            relentlessApplier.ExpiresAt = ExpirationCondition.Immediately;
                            DiceFormula damageFormula =
                                DiceFormula.FromText(howMuchDamage, "Destructive Vengeance");
                            (int, string) result = damageFormula.Roll();
                            damageEvent.DamageEventDescription.Replace("\r\n\r\n", "\r\n");
                            damageEvent.DamageEventDescription.Append("" + result.Item2 + "\r\n{b}{Crimson}= " +
                                                                      result.Item1 +
                                                                      " Destructive Vengeance{/Crimson}{/b}\r\n\r\n");
                            KindedDamage destructiveVengeance =
                                new(damageFormula, DamageKind.Untyped)
                                {
                                    ResolvedDamage = result.Item1
                                };
                            damageEvent.KindedDamages.Add(destructiveVengeance);

                            exaltDamage.ExpiresAt = ExpirationCondition.Immediately;
                            attacker.AddQEffect(new QEffect
                            {
                                Id = EvilChampDamage,
                                ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn,
                                Source = champion
                            });
                        }).WithIsReaction().WithTraits(Trait.ChampionsReaction);

                }
            };
        });
    }

    public static void EditFeatureDescription(Creature champion)
    {
        champion.AddQEffect(new QEffect
        {
            StateCheck = qf =>
            {
                foreach (QEffect qEffect in qf.Owner.QEffects.Where(q => q.Id == QEffectId.DivineSmite))
                {
                    qEffect.Description = champion.PersistentCharacterSheet?.IdentityChoice?.Alignment ==
                                          NineCornerAlignment.NeutralEvil
                        ? $"Your champion's reaction grants resistance {Math.Max(champion.Abilities.Charisma, 2) + champion.Level / 2} to the triggering damage."
                        : $"Your champion's reaction can cause enemies to take {champion.Abilities.Charisma} persistent spirit damage.";
                }

                foreach (QEffect qEffect in qf.Owner.QEffects.Where(q => q.Id == QEffectId.ChampionExalt))
                {
                    qEffect.Description = "Your champion's reaction has some negative effects also on your enemies.";
                }

                foreach (QEffect qEffect in qf.Owner.QEffects.Where(q =>
                             q.Description ==
                             "Your melee Strikes trigger {tooltip:criteffect}critical specialization effects{/} and count as disrupting."))
                {
                    qEffect.Description =
                        "Your melee Strikes trigger {tooltip:criteffect}critical specialization effects{/} and count as fearsome.";
                    qEffect.AddExtraKindedDamageOnStrike = (_, _) => null;
                    qEffect.AfterYouDealDamage = async delegate(Creature _, CombatAction action, Creature defender)
                    {
                        if (action.HasTrait(Trait.Melee) && action.HasTrait(Trait.Strike) &&
                            action.CheckResult == CheckResult.CriticalSuccess)
                        {
                            if (!defender.IsImmuneTo(Trait.Fear) && !defender.IsImmuneTo(Trait.Mental))
                            {
                                defender.AddQEffect(QEffect.Frightened(1));
                            }
                        }
                    };
                }
            }
        });
    }
}