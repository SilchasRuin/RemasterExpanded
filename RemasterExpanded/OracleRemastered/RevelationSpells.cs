using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.ReactiveAttacks;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using RemasterExpanded.Technical;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.NewSpells;

namespace RemasterExpanded.OracleRemastered;

public static class RevelationSpells
{
    public static class SpellIds
    {
        public static SpellId SprayOfStars { get; set; }
        public static SpellId InterstellarVoid { get; set; }
        public static SpellId MoonlitAscent { get; set; }
        public static SpellId AshenWind { get; set; }
        public static SpellId IncendiaryAshes { get; set; }
        public static SpellId AshForm { get; set; }
        public static SpellId WeaponTrance { get; set; }
        public static SpellId BattlefieldPersistence { get; set; }
        public static SpellId RevelInRetribution { get; set; }
        public static SpellId AncestralTouch { get; set; }
        public static SpellId AncestralDefense {get; set;}
        public static SpellId AncestralForm { get; set; }
        public static SpellId SoulSiphon { get; set; }
        public static SpellId ArmorOfBones { get; set; }
        public static SpellId ClaimUndead { get; set; }
        public static SpellId IncendiaryAura {get; set;}
        public static SpellId WhirlingFlames { get; set; }
        public static SpellId FlamingFusillade { get; set; }
        public static SpellId LifeLink { get; set; }
        public static SpellId DelayAffliction { get; set; }
        public static SpellId LifeGivingForm { get; set; }
        public static SpellId TempestTouch {get; set;}
        public static SpellId ThunderBurst { get; set; }
        public static SpellId TempestForm { get; set; }
    }
    public static void LoadRevelationSpells()
    {
        SpellIds.SprayOfStars = FastCreateFocusSpell("RE_SprayOfStars", (level, combat) =>
        {
            return Spells.CreateModern(IllustrationName.SprayOfStars, "Spray of Starsṡ", [Trait.Uncommon, Trait.Focus, MTraits.RemasterOracle, MTraits.VisualOracle, Trait.Fire, Trait.Light],
                "You fling a spray of tiny shooting stars.",
                $"You deal {S.HeightenedVariable(level+1, 2)}d4 fire damage. Each creature in the area must attempt a Reflex save.{S.FourDegreesOfSuccess("The creature is unaffected.", "The creature takes half damage and is dazzled for one round.", "The creature takes full damage and is dazzled for three rounds.", "The creature takes double damage and is dazzled for the rest of the encounter.")}",
                Target.FifteenFootCone(), level, SpellSavingThrow.Standard(Defense.Reflex))
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, $"{level + 1}d4", DamageKind.Fire);
                    switch (result)
                    {
                        case CheckResult.CriticalFailure:
                            target.AddQEffect(QEffect.Dazzled().WithExpirationNever());
                            break;
                        case CheckResult.Failure:
                            target.AddQEffect(QEffect.Dazzled().WithExpirationAtStartOfSourcesTurn(caster, 3));
                            break;
                        case CheckResult.Success:
                            target.AddQEffect(QEffect.Dazzled().WithExpirationOneRoundOrRestOfTheEncounter(caster, false));
                            break;
                    }
                }).WithHeighteningOfDamageEveryLevel(level, 1, combat, "1d4");;
        });
        SpellIds.InterstellarVoid = FastCreateFocusSpell("RE_InterstellarVoid", (level, combat) =>
        {
            return Spells.CreateModern(IllustrationName.InterstellarVoid, "Interstellar Voidṡ",
                    [Trait.Uncommon, Trait.Focus, MTraits.RemasterOracle, MTraits.VisualOracle, Trait.Cold],
                    "You call upon the frigid depths of outer space to bring a chill to your enemy.",
                    $"Deal {S.HeightenedVariable(level, 3)}d6 cold damage (basic Fortitude save mitigates). The target is fatigued for as long as you Sustain this spell. In addition, deal the cold damage again each time you Sustain this spell (a new basic Fortitude save mitigates each time).",
                    Target.Ranged(6), level, SpellSavingThrow.Basic(Defense.Fortitude))
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, level + "d6",
                        DamageKind.Cold);
                    caster.AddQEffect(new QEffect
                    {
                        Name = "Sustaining Interstellar Void",
                        Illustration = IllustrationName.InterstellarVoid,
                        Id = QEffectId.Sustaining,
                        ReferencedSpell = spell,
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                        Tag = spell,
                        CannotExpireThisTurn = true,
                        Description = "You're sustaining a spell. It will expire if you fail to sustain it.",
                        StateCheck = qfSelf =>
                        {
                            if (!target.Alive)
                                qfSelf.ExpiresAt = ExpirationCondition.Immediately;
                            target.AddQEffect(QEffect.Fatigued().WithExpirationEphemeral());
                        },
                        ProvideContextualAction = qfSelf =>
                        {
                            if (qfSelf.CannotExpireThisTurn)
                                return null;
                            return new ActionPossibility(new CombatAction(caster,
                                    IllustrationName.InterstellarVoid, "Sustain Interstellar Void",
                                    [
                                        Trait.SustainASpell,
                                        Trait.Basic,
                                        Trait.DoesNotBreakStealth
                                    ],
                                    $"Extend the duration of Interstellar Void until the end of your next turn.\n\nAlso, deal {S.HeightenedVariable(level, 3)}d6 cold damage to {target.Name}.",
                                    Target.Self()).WithSpellSavingThrow(Defense.Fortitude)
                                .WithReferencedQEffect(qfSelf).WithSoundEffect(SfxName.RayOfFrost)
                                .WithEffectOnChosenTargets(async (_, _, _) =>
                                {
                                    qfSelf.CannotExpireThisTurn = true;
                                    CheckResult checkResult =
                                        await CommonSpellEffects.RollSpellSavingThrowAsync(target, spell,
                                            Defense.Fortitude);
                                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, checkResult,
                                        level + "d6", DamageKind.Cold);
                                })).WithPossibilityGroup("Maintain an activity");
                        }
                    });
                }).WithHeighteningOfDamageEveryLevel(level, 3, combat, "1d6");
        });
        SpellIds.MoonlitAscent = UpdateRevelationSpell(SpellId.MoonlitAscent);
        SpellIds.AshenWind = UpdateRevelationSpell(SpellId.AshenWind);
        SpellIds.IncendiaryAshes = UpdateRevelationSpell(SpellId.IncendiaryAshes);
        SpellIds.AshForm = UpdateRevelationSpell(SpellId.AshForm);
        SpellIds.WeaponTrance = FastCreateFocusSpell("WeaponTrance", (level, _) =>
        {
            return Spells.CreateModern(IllustrationName.MagicWeapon, "Weapon Trance",
                    [Trait.Uncommon, Trait.Focus, MTraits.RemasterOracle, MTraits.VisualOracle, Trait.SpellWithDuration, Trait.VerbalOnly],
                    "The serenity of violence fills your mind, giving you a heightened sense of knowing exactly where your weapons need to be.",
                    "Until the end of the encounter, your proficiency with martial weapons is equal to your proficiency with simple weapons.",
                    Target.Self().WithAdditionalRestriction(cr => cr.HasEffect(MQEffectIds.WeaponTrance) ? "You are already under the effect of this spell." : null), level, null)
                .WithSoundEffect(SfxName.MagicWeapon)
                .WithEffectOnSelf(self =>
                {
                    Proficiency old = self.Proficiencies.Get(Trait.Martial);
                    self.Proficiencies.Set(Trait.Martial, self.Proficiencies.Get(Trait.Simple));
                    self.AddQEffect(new QEffect("Weapon Trance", "Your proficiency with martial weapons is equal to your proficiency with simple weapons.", IllustrationName.MagicWeapon)
                    {
                        WhenExpires = _ =>
                        {
                            self.Proficiencies.SetExactly([Trait.Martial], old);
                        },
                        Id = MQEffectIds.WeaponTrance
                    });
                });
        });
        SpellIds.BattlefieldPersistence = UpdateRevelationSpell(SpellId.BattlefieldPersistence);
        SpellIds.RevelInRetribution = FastCreateFocusSpell("RevelInRetribution", (level, inCombat) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("RevelInRetribution"), "Revel In Retribution",
                    [Trait.Focus, Trait.Uncommon, Trait.Mental, MTraits.VisualOracle, Trait.SpellWithDuration, MTraits.RemasterOracle],
                    "Time seems to slow for you, allowing you to strike your opponents mid-move.",
                    "You gain the {tooltip:ReactiveStrike}Reactive Strike{/tooltip} ability, and you immediately gain a second reaction that you can use only to use Reactive Strike. At the start of each of your subsequent turns when you regain your actions, you gain an additional reaction that can be used only to attempt a Reactive Strike." +
                    $"\n\nLashing out at a defenseless enemy invigorates you with the thrill of combat, granting you {S.HeightenedVariable(level -1, 5)} temporary Hit Points whenever you successfully hit with a Reactive Strike.",
                    Target.Self(), level, null)
                .WithSoundEffect(SfxName.Victory)
                .WithActionCost(1)
                .WithHeighteningNumerical(level, 1, inCombat, 1, "The temporary Hit Points you gain from a successful Reactive Strike increase by 1.")
                .WithEffectOnSelf( async (spell, self) =>
                {
                    QEffect reactiveStrike = AttackOfOpportunityMechanics.AttackOfOpportunity(
                        new AttackOfOpportunityMechanics
                        {
                            Name = "Attack of Opportunity",
                            Description =
                                "When a creature leaves a square within your reach, makes a ranged attack or uses a move or manipulate action, you can Strike it for free. On a critical hit, you also disrupt the manipulate action.",
                            RestrictToOnlyAgainstWhom = null,
                            OverheadName = "*attack of opportunity*",
                            StandStill = false,
                            StrikeAndReactionTraits =
                            [
                                Trait.ReactiveAttack,
                                Trait.AttackOfOpportunity
                            ],
                            NumberOfStrikes = 1
                        }
                    );
                    self.AddQEffect(reactiveStrike);
                    self.AddQEffect(new QEffect("Revel in Retribution", $"You gain the Reactive Strike ability, and you immediately gain a second reaction that you can use only to use Reactive Strike. At the start of each of your subsequent turns when you regain your actions, you gain an additional reaction that can be used only to attempt a Reactive Strike. In addition, you gain {S.HeightenedVariable(level -1, 5)} temporary hit points when you successfully hit with a Reactive Strike.", spell.Illustration)
                    {
                        OfferExtraReaction = (_, _, traits) => traits.Contains(Trait.AttackOfOpportunity) ? "Revel in Retribution" : null,
                        WhenExpires = _ => { self.RemoveAllQEffects(qf => qf == reactiveStrike); },
                        AfterYouTakeAction = async (_, action) =>
                        {
                            if (action.CheckResult <= CheckResult.Failure ||
                                !action.HasTrait(Trait.AttackOfOpportunity))
                                return;
                            self.GainTemporaryHP(level - 1);
                        }
                    });
                });
        });
        SpellIds.AncestralTouch = UpdateRevelationSpell(SpellId.AncestralTouch, sp =>
        {
            sp.EffectOnOneTarget = null;
            sp.EffectOnChosenTargets = null;
            sp.Description = $"The target takes {S.HeightenedVariable(sp.SpellLevel + 1, 2)}d4 mental damage, with results depending on a Will save." + 
                             S.FourDegreesOfSuccess("The target is unaffected.",
                                 "The target takes half damage.",
                                 "The target is frightened 1 and takes full damage.",
                                 "The target is frightened 2 and takes double damage.");
            sp.WithEffectOnEachTarget(async (spell, caster, target, result) =>
            {
                await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, $"{spell.SpellLevel + 1}d4", DamageKind.Mental);
                if (result <= CheckResult.Failure)
                    target.AddQEffect(QEffect.Frightened(result == CheckResult.CriticalFailure ? 2 : 1).WithDispellable(spell));
            });
        });
        SpellIds.AncestralDefense = UpdateRevelationSpell(SpellId.AncestralDefense, sp => sp.Description = "Whenever you need to roll a Will save, you can cast this spell cast as a {icon:Reaction} reaction. If you do, you roll the triggering save twice and take the better result.");
        SpellIds.AncestralForm = UpdateRevelationSpell(SpellId.AncestralForm);
        SpellIds.SoulSiphon = UpdateRevelationSpell(SpellId.SoulSiphon);
        SpellIds.ArmorOfBones = UpdateRevelationSpell(SpellId.ArmorOfBones);
        SpellIds.ClaimUndead = UpdateRevelationSpell(SpellId.ClaimUndead, sp =>
        {
            sp.EffectOnOneTarget = null;
            sp.EffectOnChosenTargets = null;
            sp.Description = "The target must attempt a Will save." +
                             S.FourDegreesOfSuccess("The target is unaffected.",
                                 "The target is {r}stunned 1{/r} and {r}confused{/r} for 1 round as it fights off your commands.",
                                 "You gain control of the target for the rest of the encounter, but it repeats the Will save at the end of each of its turns. On a success, the spell ends, and the creature becomes stunned 1 and confused for 1 round.",
                                 "As failure, but the effect lasts for the rest of the encounter with no additional saves.");
            sp.WithEffectOnEachTarget(async (spell, caster, target, result) =>
            {
                if (result == CheckResult.Success)
                {
                    target.AddQEffect(QEffect.Stunned(1));
                    target.AddQEffect(QEffect.Confused(false, spell).WithExpirationInOneRound(caster));
                }
                if (result > CheckResult.Failure)
                    return;
                Level6Spells.Dominate(spell, target,
                    result == CheckResult.CriticalFailure
                        ? Level6Spells.DominationSaveKind.NoNewSaves
                        : Level6Spells.DominationSaveKind.NewSaveAtEndOfEachTurnOfThrall).WithAction(qf =>
                {
                    qf.WhenExpires = _ =>
                    {
                        target.AddQEffect(QEffect.Stunned(1));
                        target.AddQEffect(QEffect.Confused(false, spell).WithExpirationInOneRound(caster));
                    };
                });
            });
        });
        SpellIds.IncendiaryAura = UpdateRevelationSpell(SpellId.IncendiaryAura);
        SpellIds.WhirlingFlames = UpdateRevelationSpell(SpellId.WhirlingFlames);
        SpellIds.FlamingFusillade = UpdateRevelationSpell(SpellId.FlamingFusillade, sp =>
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            bool inCombat = sp.Owner != null && sp.Owner.Battle != TBattle.Pseudobattle;
            sp.EffectOnChosenTargets = null;
            sp.EffectOnOneTarget = null;
            sp.Description =
                $"You cast {AllSpells.CreateModernSpellTemplate(MySpells.SpellIds.Ignition, MTraits.RemasterOracle)} as part of casting {{i}}flaming fusillade{{/i}}. For the rest of the encounter, {{i}}ignition's{{/i}} casting time is reduced from 2 actions to 1."
                + (sp.SpellLevel >= 9 && inCombat
                    ? "\n\nYou also gain a status bonus to damage dealt by {i}ignition{/i} equal to the spell's level."
                    : "");
            sp.Heightening = null;
            sp.WithHeightenedAtSpecificLevel(sp.SpellLevel, 9, inCombat, "For the duration, you also gain a status bonus to damage dealt by {i}ignition{/i} equal to {i}flaming fusillade's{/i} spell rank.");
            sp.WithEffectOnSelf(async (spell, self) =>
            {
                int level = spell.SpellLevel;
                CombatAction combatAction = AllSpells.CreateSpellInCombat(MySpells.SpellIds.Ignition, self, spell.SpellLevel, MTraits.RemasterOracle).WithActionCost(0);
                combatAction.SpellcastingSource = spell.SpellcastingSource;
                self.AddQEffect(new QEffect("Flaming Fusillade", "The action cost of your {i}ignition{/i} is reduced by 1." + (spell.SpellLevel >= 9 ? "\n\nYou have a status bonus to damage dealt by {i}produce flame{/i} equal to the spell's level." : ""), ExpirationCondition.Never, self, IllustrationName.FlamingFusillade)
                {
                    ModifyActionPossibility = (_, action) =>
                    {
                        if (action.SpellId != MySpells.SpellIds.Ignition)
                            return;
                        action.ActionCost = Math.Max(action.ActionCost - 1, 1);
                    },
                    BonusToDamage = (_, action, _) => level >= 9 && action.SpellId == MySpells.SpellIds.Ignition ? new Bonus(level, BonusType.Status, "Flaming Fusillade") : null
                });
                await self.Battle.GameLoop.FullCast(combatAction);
            });
        });
        SpellIds.LifeLink = UpdateRevelationSpell(SpellId.LifeLink);
        SpellIds.DelayAffliction = UpdateRevelationSpell(SpellId.DestroyAffliction);
        SpellIds.LifeGivingForm = UpdateRevelationSpell(SpellId.LifeGivingForm, sp => sp.Description = sp.Description.Replace("positive", "vitality").Replace("negative", "void"));
        SpellIds.TempestTouch = UpdateRevelationSpell(SpellId.TempestTouch);
        SpellIds.ThunderBurst = UpdateRevelationSpell(SpellId.Thunderburst);
        SpellIds.TempestForm = UpdateRevelationSpell(SpellId.TempestForm);
    }

    public static SpellId UpdateRevelationSpell(SpellId spellToUpdate, Action<CombatAction>? additionalUpdate = null)
    {
        return ModManager.RegisterNewSpell($"RE_{spellToUpdate.ToString()}", 0, (_, caster, level, combat, spellInformation) => Spell.DuplicateSpell(AllSpells.CreateModernSpell(spellToUpdate, caster, level, combat, spellInformation).CombatActionSpell)
            .CombatActionSpell.WithAction(sp =>
            {
                sp.Traits.Remove(Trait.Cursebound);
                sp.Traits.Remove(Trait.Enchantment);
                sp.Traits.Remove(Trait.Evocation);
                sp.Traits.Remove(Trait.Transmutation);
                sp.Traits.Remove(Trait.Necromancy);
                sp.Traits.Remove(Trait.Abjuration);
                sp.Traits.Remove(Trait.Divination);
                sp.Traits.Remove(Trait.Conjuration);
                sp.Traits.Remove(Trait.Oracle);
                sp.Traits.Add(MTraits.RemasterOracle);
                sp.Traits.Add(MTraits.VisualOracle);
                sp.Name += "ṡ";
                additionalUpdate?.Invoke(sp);
            }));
    }
}