using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Zoning;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using SpiritDamage;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public abstract class NewCantrips : NewSpells
{
    public static void Load()
    {
        Figment = ModManager.RegisterNewSpell("RE_Figment", 0, (_, _, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? psychicAmpInformation.Amped ? 1 : 0 : 0) != 0;
            bool psyche = psychicAmpInformation != null;
            var flavorText = "You weave illusions to create a vision.";
            var rulesText = "You can create an illusion that grants concealment to a creature within that lasts as long as you Sustain the Spell. When you Cast or Sustain the Spell, you can attempt to Create a Diversion with the illusion, gaining a +2 circumstance bonus to your Deception check. If the attempt fails against a creature, that creature disbelieves the figment.";
            if (psyche)
            {
                flavorText = "You weave illusions that shift and dance.";
                if (!amped) rulesText += "\nWhen you Sustain the spell, you can move the apparent vision up to 15 feet. You can then attempt to Create a Diversion as usual. Those creatures who disbelieved the illusion aren't affected by this diversion.";
                rulesText += IfAmped(inCombat, "When you amp the spell, you can create a particularly distracting illusion as part of its normal effects. Choose an unoccupied square within the spell's range. The illusion in that square provides flanking for a single melee attack made before the beginning of your next turn. In addition, it provides lesser cover as if it were a creature instead of applying concealment to a creature within. If you Sustain the spell, the details of the illusion change and shift to keep your enemies unsettled; the flanking illusion's duration extends until the end of your next turn and you can move it to any unoccupied square in the spell's range. The flanking illusion can't provide its benefit against any creature who has disbelieved the figment.");
            }
            Target target = Target.Tile((self, tile) => self.DistanceTo(tile) <= (psyche ? 12 : 6)).WithOverriddenFullTargetLine("{b}Range{/b} " + (psyche ? "60 feet" : "30 feet"));
            if (amped)
            {
                rulesText = "Choose an unoccupied square within range. You create an illusion there that grants flanking for one melee attack made before the start of your next turn and provides lesser cover as if it were a creature." +
                            " This illusion lasts as long as you Sustain the spell." +
                            "\n\nWhen you Cast or Sustain, you can attempt to Create a Diversion with the illusion, gaining a +2 circumstance bonus to your Deception check. If the attempt fails, the target disbelieves the figment." +
                            "\nEach time you Sustain, the illusion lasts until the end of your next turn, and you may move it to another unoccupied square in range. The illusion grants no benefits against creatures that have disbelieved it.";
                target = Target.RangedEmptyTileForSummoning(12);
            }
            return Spells.CreateModern(MIllustrations.CreateIllustration("Figment"), "Figment",
                [Trait.Cantrip, Trait.Concentrate, Trait.Illusion, Trait.Manipulate, Trait.SomaticOnly, Trait.Visual, Trait.Arcane, Trait.Occult, Trait.Level1PsychicCantrip],
                flavorText, rulesText, target, level, null)
                .WithActionCost(2).WithSoundEffect(SfxName.DazzlingFlash)
                .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                {
                    IllustrationName[] illusionArray = [IllustrationName.StatueOfPhantasmalKiller, IllustrationName.StatueOfDeath, IllustrationName.StatueOfLife];
                    await OnSustain(true);
                    if (!amped)
                    {
                        Zone illusion = Zone.Spawn(caster, ZoneAttachment.StableBurst(targets.ChosenTiles));
                        illusion.TileEffectCreator = _ => new TileQEffect
                        {
                            Illustration = new TintedIllustration(illusionArray.GetRandomVisualOnly(),
                                Color.LightCyan),
                            TransformsTileIntoUnenterableTerrainForNonflyingEnemiesOnly = true,
                            Name = "Figment",
                            VisibleDescription = "This is an illusion created by Figment.",
                        };
                        illusion.ControllerQEffect.WhenExpires += effect =>
                        {
                            foreach (Creature creature in effect.Owner.Battle.AllCreatures.Where(cr =>
                                         cr.HasEffect(MQEffectIds.Disbelief)))
                            {
                                if (creature.FindQEffect(MQEffectIds.Disbelief) is { } disbelief &&
                                    disbelief.SourceAction == spell)
                                    disbelief.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        };
                        QEffect cantSee = new()
                        {
                            SightReductionTo = DetectionStrength.ConcealedViaBlur,
                            Id = MQEffectIds.ConcealedBy
                        };
                        QEffect conceal = new(ExpirationCondition.Ephemeral)
                        {
                            YouAreTargeted = (_, action) =>
                            {
                                if (!action.Owner.EnemyOf(caster) || (action.Owner.FindQEffect(MQEffectIds.Disbelief) is {} disbelief && disbelief.SourceAction == spell))
                                    return Task.CompletedTask;
                                action.Owner.AddQEffect(cantSee);
                                return Task.CompletedTask;
                            },
                            AfterYouAreTargeted = (_, action) =>
                            {
                                if (action.Owner.FindQEffect(MQEffectIds.ConcealedBy) is { } qf)
                                    qf.ExpiresAt = ExpirationCondition.Immediately;
                                return Task.CompletedTask;
                            }
                        };
                        illusion.StateCheckOnEachCreatureInZone = (_, creature) => { creature.AddQEffect(conceal); };
                        illusion.ApplySustainment(spell, async _ => await OnSustain(false, illusion),
                            "When you sustain the spell, you may create a diversion." +
                            (psyche ? " You may also move the illusion up to 15 feet." : ""));
                        illusion.Apply();
                        return;
                    }
                    Creature figment = Creature.CreateSimpleCreature("Figment");
                    figment.Traits.Add(Trait.Incorporeal);
                    figment.Traits.Add(Trait.Illusion);
                    figment.Traits.Add(Trait.Indestructible);
                    figment.WithSpawnAsGaiaFriends();
                    figment.WithTactics(Tactic.DoNothing);
                    figment.With(cr => cr.DescriptionFulltext = "This is an illusion created by Amped Figment.");
                    figment.Illustration = new TintedIllustration(illusionArray.GetRandomVisualOnly(), Color.LightCyan);
                    QEffect flankingFigment = FlankingFig(caster, figment, spell);
                    caster.Battle.SpawnCreature(figment, caster.Battle.GaiaFriends, targets.ChosenTiles[0]);
                    await caster.Battle.GameLoop.StateCheck();
                    figment.With(cr => cr.DescriptionFulltext = "This is an illusion created by Amped Figment.");
                    caster.AddQEffect(flankingFigment);
                    QEffect sustainedFigment = new(ExpirationCondition.ExpiresAtEndOfYourTurn)
                    {
                        CannotExpireThisTurn = true,
                        WhenExpires = effect =>
                        {
                            figment.Die();
                            if (caster.FindQEffect(MQEffectIds.FlankingFig) is {} fig)
                                fig.ExpiresAt = ExpirationCondition.Immediately;
                            foreach (Creature creature in effect.Owner.Battle.AllCreatures.Where(cr => cr.HasEffect(MQEffectIds.Disbelief)))
                            {
                                if (creature.FindQEffect(MQEffectIds.Disbelief) is { } disbelief &&
                                    disbelief.SourceAction == spell)
                                    disbelief.ExpiresAt = ExpirationCondition.Immediately;
                            }
                            foreach (Creature ally in caster.Battle.AllCreatures.Where(cr => cr.FindQEffect(MQEffectIds.FlankAdd) is {} flanking && flanking.SourceAction == spell))
                            {
                                if (ally.FindQEffect(MQEffectIds.FlankAdd) is { } flanking &&
                                    flanking.SourceAction == spell)
                                    flanking.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    };
                    caster.AddQEffect(sustainedFigment);
                    caster.AddQEffect(QEffect.Sustaining(spell, sustainedFigment,
                        async _ => await OnSustain(false, null, figment, FlankingFig(caster, figment, spell)), "The flanking illusion's duration extends until the end of your next turn and you can move it to any unoccupied square in the spell's range."));
                    
                    return;
                    async Task OnSustain(bool initialCast, Zone? targetZone = null, Creature? fig = null, QEffect? flanking = null)
                    {
                        CombatAction diversion = CommonStealthActions.CreateCreateADiversion(caster).WithActionCost(0);
                        if (diversion.Target is MultipleCreatureTargetsTarget targeting)
                        {
                            targeting.AdditionalRestrictionsOnEachTarget += (_, _, newC) => !newC.HasEffect(MQEffectIds.Disbelief);
                            diversion.Target = targeting;
                        }
                        diversion.WithPrologueEffectOnChosenTargetsBeforeRolls((_, self, _) =>
                        {
                            self.AddQEffect(new QEffect
                            {
                                BonusToAttackRolls = (_, action, _) =>
                                    action.ActionId != ActionId.CreateADiversion
                                        ? null
                                        : new Bonus(2, BonusType.Circumstance, "Figment"),
                                AfterYouTakeAction = (effect, action) =>
                                {
                                    if (action.ActionId != ActionId.CreateADiversion) return Task.CompletedTask;
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                    return Task.CompletedTask;
                                }
                            });
                            return Task.CompletedTask;
                        });
                        bool ask = await caster.Battle.AskForConfirmation(caster, diversion.Illustration,
                            "Would you like to create a diversion using your figment?", "Yes");
                        if (ask)
                        {
                            await caster.Battle.GameLoop.FullCast(diversion);
                            foreach (Creature creature in diversion.ChosenTargets.ChosenCreatures.Where(cr => HiddenRules.DetermineHidden(cr, caster) != DetectionStrength.Hidden))
                            {
                                creature.AddQEffect(new QEffect("Disbelief", "You disbelieve a figment.", ExpirationCondition.Never, caster, IllustrationName.Seek) { Id = MQEffectIds.Disbelief }.WithSourceAction(spell));
                            }
                        }
                        if (psyche && !initialCast && !amped && targetZone != null)
                        {
                            Tile? newTile = await caster.Battle.AskToChooseATile(caster,
                                caster.Battle.Map.AllTiles.Where(tile =>
                                    tile.DistanceTo(targetZone.AffectedTiles.FirstOrDefault()!) <= 3 && !Equals(tile, targetZone.AffectedTiles.FirstOrDefault())),
                                spell.Illustration, "Choose a square to move the figment to.", "", true, true, (Creature?)null);
                            if (newTile == null) return;
                            targetZone.AddTileAndApplyToIt(newTile);
                            foreach (TileQEffect tqf in targetZone.AffectedTiles.FirstOrDefault()
                                         !.TileQEffects
                                         .Where(tq => tq.Zone == targetZone))
                            {
                                tqf.ExpiresAt = ExpirationCondition.Immediately;
                            }
                            targetZone.ZoneAttachment.AffectedTiles.RemoveAt(0);
                        }
                        if (amped && fig != null && flanking != null)
                        {
                            caster.AddQEffect(flanking);
                            Tile? newTile = await caster.Battle.AskToChooseATile(caster, caster.Battle.Map.AllTiles.Where(tile => caster.DistanceTo(tile) <= 12 && tile.IsTrulyGenuinelyFreeToEveryCreature), spell.Illustration, "Choose a square to move your figment to.", "", true,true, (Creature?)null);
                            if (newTile == null) return;
                            await CommonSpellEffects.Teleport(fig, newTile);
                        }
                        
                    }
                });
        });
        ModManager.RegisterActionOnEachSpell(sp =>
        {
            if (sp.SpellId != SpellId.DivineLance) return;
            sp.EffectOnOneTarget = null;
            sp.Traits.Add(MTraits.Sanctified);
            sp.Traits.Add(SpiritTrait.Spirit);
            sp.EffectOnOneTarget = async (spell, caster, target, result) =>
            {
                if (caster.HasTrait(Trait.Good))
                    spell.Traits.Add(HolyTrait.Holy);
                if (caster.HasTrait(Trait.Evil))
                    spell.Traits.Add(UnholyTrait.Unholy);
                await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result,
                    $"{2 + (spell.SpellLevel - 1)}d4", ModData.SpiritDamage);
            };
        });
    }

    private static QEffect FlankingFig(Creature caster, Creature figment, CombatAction spell)
    {
        return new QEffect(ExpirationCondition.ExpiresAtStartOfSourcesTurn) { Source = caster, Id = MQEffectIds.FlankingFig }
                    .AddGrantingOfTechnical(cr => cr.FriendOf(caster), qfTech =>
                    {
                        qfTech.StateCheck = _ =>
                        {
                            if (qfTech.Owner.HasEffect(MQEffectIds.FlankAdd)) return;
                            qfTech.Owner.AddQEffect(new QEffect
                            {
                                StateCheck = sc =>
                                {
                                    foreach (Creature flanked in sc.Owner.Battle.AllCreatures.Where(enemy =>
                                                 IsFigFlanking(enemy, figment, qfTech.Owner, spell)))
                                    {
                                        QEffect flanking = QEffect.FlankedBy(sc.Owner).WithExpirationEphemeral();
                                        flanking.Id = 0;
                                        flanked.AddQEffect(flanking);
                                    }
                                },
                                Id = MQEffectIds.FlankAdd,
                                SourceAction = spell,
                                AfterYouTakeHostileAction = (_, action) =>
                                {
                                    if ((!action.HasTrait(Trait.Strike) &&
                                         (!action.HasTrait(Trait.Spell) || !action.HasTrait(Trait.Attack))) ||
                                        (!action.HasTrait(Trait.Melee) && !action.HasTrait(Trait.VersatileMelee))) return;
                                    if (!IsFigFlanking(action.ChosenTargets.ChosenCreatures[0], figment, action.Owner, spell))
                                        return;
                                    if (caster.FindQEffect(MQEffectIds.FlankingFig) is not { } fig) return;
                                    fig.ExpiresAt = ExpirationCondition.Immediately;
                                    foreach (Creature ally in caster.Battle.AllCreatures.Where(cr => cr.FindQEffect(MQEffectIds.FlankAdd) is {} flanking && flanking.SourceAction == spell))
                                    {
                                        if (ally.FindQEffect(MQEffectIds.FlankAdd) is { } flanking &&
                                            flanking.SourceAction == spell)
                                            flanking.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                }
                            });
                        };
                    });
    }

    private static bool IsFigFlanking(Creature enemy, Creature figment, Creature flanker, CombatAction spell)
    {
        if (flanker.HasEffect(QEffectId.GangUp))
            return enemy.IsAdjacentTo(figment) && flanker.DistanceTo(enemy) <= 2 && (enemy.FindQEffect(MQEffectIds.Disbelief) is not { } dis || dis.SourceAction != spell);
        return enemy.IsAdjacentTo(figment) && FlankingRules.IsOpposite(figment.Occupies, flanker, enemy) &&
               (enemy.FindQEffect(MQEffectIds.Disbelief) is not { } disbelief || disbelief.SourceAction != spell);
    }
}