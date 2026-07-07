using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.AuraAnimations;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Kineticist;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.StatBlocks;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using SpiritDamage;
using static RemasterExpanded.ModData;
using static RemasterExpanded.FeatLoader;

namespace RemasterExpanded.MyArchetypes;

public class SisterOfTheGoldenErinys
{
    public static readonly Trait SisterOfTheErinys = ModManager.RegisterTrait("RE_SisterOfTheErinys", new TraitProperties("Sister of the Golden Erinys", false));
    public static FeatName? MonasticWeaponry { get; } = ModManager.TryParse("RL_MonasticWeaponry", out FeatName featName) ?  featName : null;
    public static IEnumerable<Feat> Load()
    {
        yield return ArchetypeFeats.CreateAgnosticArchetypeDedication(SisterOfTheErinys, "The Sisterhood of the Golden Erinys is a women's monastic order named after a mercenary company of fiendish erinyes who often work under the Queen of the Night, mistress of wrath and vengeance. The sisterhood is a martial order, training in a vicious blend of unarmed combat and esoteric weaponry.\n\n" +
            "Fundamentally, their fighting style is rooted in their understanding of devils.",
            " You gain the Additional Lore general feat for Devil Lore. When you choose this feat, you can choose to become sanctified as {tooltip:unholy}unholy{/tooltip}. You have familiarity with the asp coil and scourge, treating them as simple weapons for the purposes of proficiency. If you're unholy, your Strikes using those weapons and your unarmed Strikes gain the unholy trait.\n\n" +
            "{b}Special{/b} If you have the Monastic Weaponry feat, the asp coil gains the monk trait for you.")
            .WithOnSheet(values =>
            {
                Lores.GrantAdditionalLore(values, RemasterLore.DevilLore);
                values.AddSelectionOption(new SingleFeatSelectionOption("RE_SanctificationSister", "Sanctification", values.CurrentLevel, feat => feat.Tag is "SanctificationCleric" && !feat.Name.Contains("Holy")).WithIsOptional());
                List<Trait> erinysWeapons = [MTraits.AspCoil, MTraits.Scourge];
                foreach (Trait weapon in erinysWeapons)
                {
                    values.Proficiencies.AutoupgradeAlongBestWeaponProficiency(
                        [Trait.Simple, weapon]);
                }
                values.Proficiencies.AddProficiencyAdjustment(
                    traits => traits.Any(erinysWeapons.Contains) && traits.Contains(Trait.Martial),
                    Trait.Simple);
            })
            .WithOnCreature(self =>
            {
                if (!self.HasTrait(UnholyTrait.Unholy))
                    return;
                self.AddQEffect(new QEffect("Unholy Strikes", "Your Unarmed Strikes and Strikes with the asp coil and scourge gain the unholy trait.")
                {
                    ModifyActionPossibility = (_, action) =>
                    {
                        if (action.Item == null || !action.HasTrait(Trait.Strike) || (!action.HasTrait(Trait.Unarmed) && !action.Item.HasTrait(MTraits.AspCoil) && !action.Item.HasTrait(MTraits.Scourge)))
                            return;
                        action.WithExtraTrait(UnholyTrait.Unholy);
                    },
                    Innate = true
                });
            })
            .WithPermanentQEffect(null, qf =>
            {
                qf.StartOfCombat = async effect =>
                {
                    Creature self = effect.Owner;
                    if (MonasticWeaponry != null && self.HasFeat(MonasticWeaponry.Value) && self.AllItems.Any(it => it.HasTrait(MTraits.AspCoil)))
                    {
                        foreach (Item asp in self.AllItems.Where(it => it.HasTrait(MTraits.AspCoil)))
                        {
                            asp.Traits.Add(MTraits.AspCoil);
                        }
                    }
                };
            });
        yield return ArchetypeFeat("Eye for Weakness", 4,
                "Your knowledge of your foes leaves them vulnerable to your attacks.",
                $"When you critically succeed at a check to {RecallWeakness.GetActionLink("Recall Weakness")} about a creature, that creature is {{r:flat-footed}}off-guard{{/r}} to you until the end of your turn.",
                SisterOfTheErinys)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                qf.AfterYouTakeActionAgainstTarget = async (effect, action, target, result) =>
                {
                    if (action.ActionId != RecallWeakness.RWActionId || result != CheckResult.CriticalSuccess)
                        return;
                    target.AddQEffect(new QEffect("Off-guard", $"This creature is off-guard to attacks made by {effect.Owner.Name}.", ExpirationCondition.ExpiresAtEndOfSourcesTurn, effect.Owner, IllustrationName.Flatfooted)
                    {
                        IsFlatFootedTo = (_, creature, _) => creature != effect.Owner ? null : "Eye for Weakness"
                    });
                };
            });
        Illustration goldenStance = MIllustrations.CreateIllustration("GoldenErinys");
        yield return ArchetypeFeat("Golden Erinys Stance", 4,
            "Whether you're a keen student of vengeance or you picked up techniques after being a common target for the sisters' punishment, you've learned to dig painfully into your opponent's flesh.",
            "In this stance, you can make fury's fang unarmed attacks. These deal 1d6 piercing damage; are in the brawling group; and have the agile, backstabber, finesse, forceful, nonlethal, and unarmed traits.\n\n" +
            "While in the stance, if you critically hit with a melee Strike that deals piercing damage, the target is {r}sickened{/r} 1 (with a DC to remove equal to your class DC); this is in addition to any critical specialization effect you might gain from the attack.",
            SisterOfTheErinys, MFeatNames.GoldenErinysStance,Trait.Stance)
            .WithActionCost(1)
            .WithIllustration(goldenStance)
            .WithPermanentQEffect("You can enter Golden Erinys Stance and make fury's fang unarmed attacks.", qf =>
            {
                qf.ProvideMainAction = effect =>
                {
                    Creature self = effect.Owner;
                    Item furyFang = new Item(MIllustrations.CreateIllustration("FuryFang"), "fury's fang",
                            Trait.Brawling, Trait.Agile, Trait.Backstabber, Trait.Finesse, Trait.Forceful,
                            Trait.Nonlethal, Trait.Unarmed)
                        .WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Piercing));
                    CombatAction enterStance = new CombatAction(self, goldenStance, "Golden Erinys Stance",
                            [Trait.Stance, Trait.Basic],
                            "Enter a stance.\n\n" +
                            "In this stance, you can make fury's fang unarmed attacks. These deal 1d6 piercing damage; are in the brawling group; and have the agile, backstabber, finesse, forceful, nonlethal, and unarmed traits.\n\n" +
                            $"While in the stance, if you critically hit with a melee Strike that deals piercing damage, the target is {{r}}sickened{{/r}} 1 (DC {self.ClassDC()} to remove); this is in addition to any critical specialization effect you might gain from the attack.",
                            Target.Self().WithAdditionalRestriction(cr =>
                                cr.HasEffect(MQEffectIds.GoldenStance) ? "You're already in this stance." : null))
                        {
                            ShortDescription = "Enter a stance where you can make fury's fang unarmed attacks. These deal 1d6 piercing damage; are in the brawling group; and have the agile, backstabber, finesse, forceful, nonlethal, and unarmed traits. Critical hits with Strikes that deal piercing damage causes sickened 1 on the target."
                        }.WithActionCost(1)
                        .WithEffectOnSelf(async (_, owner) =>
                        {
                            QEffect enterStance = KineticistCommonEffects.EnterStance(owner, goldenStance,
                                "Golden Erinys Stance",
                                "You can make fury's fang unarmed attacks. These deal 1d6 piercing damage; are in the brawling group; and have the agile, backstabber, finesse, forceful, nonlethal, and unarmed traits. Critical hits with Strikes that deal piercing damage causes sickened 1 on the target.",
                                MQEffectIds.GoldenStance);
                            enterStance.AdditionalUnarmedStrike = furyFang;
                            enterStance.AfterYouDealDamageOfKind = async (creature, action, kind, target) =>
                            {
                                if (action.CheckResult != CheckResult.CriticalSuccess || !action.HasTrait(Trait.Strike) || kind != DamageKind.Piercing)
                                    return;
                                target.AddQEffect(QEffect.Sickened(1, creature.ClassDC()));
                            };
                        });
                    return new ActionPossibility(enterStance).WithPossibilityGroup("Enter a stance");
                };
            });
        yield return ArchetypeFeat("Fiendish Brand", 6,
                "The virtuous suffer your brand.",
                "Make a Strike with a weapon or unarmed attack that deals piercing or slashing damage. If it's successful and deals damage, you carve bleeding words into your target. The target takes 1d6 persistent bleed damage, which has the unholy trait. Anytime the creature casts a holy spell while this bleed damage persists, it must succeed at a DC 5 flat check or the spell is disrupted.",
                SisterOfTheErinys, Trait.Divine, UnholyTrait.Unholy)
            .WithActionCost(2)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideStrikeModifierAsPossibilities = (_, weapon) =>
                {
                    if (weapon.WeaponProperties == null || !weapon.DetermineDamageKinds().Any(kind => kind is DamageKind.Piercing or DamageKind.Slashing))
                        return [];
                    QEffect applyBrand = new(ExpirationCondition.ExpiresAtEndOfYourTurn)
                    {
                        AfterYouDealDamage = async (_, action, target) =>
                        {
                            if (action.ActionId != RActionIds.FiendishBrand ||
                                action.CheckResult < CheckResult.Success)
                                return;
                            QEffect brand = QEffect.PersistentDamage(DiceFormula.FromText("1d6", "Fiendish Brand"),
                                DamageKind.Bleed);
                            brand.FizzleOutgoingActions = async (_, combatAction, builder) =>
                            {
                                if (!combatAction.HasTrait(Trait.Spell) || !combatAction.HasTrait(HolyTrait.Holy))
                                    return false;
                                (CheckResult, string) tuple = Checks.RollFlatCheck(5 + qf.Value);
                                builder.AppendLine("Casting a holy spell while branded: " + tuple.Item2);
                                return tuple.Item1 < CheckResult.Success;
                            };
                            brand.StateCheck += effect =>
                            {
                                effect.Owner.AddQEffect(new QEffect("Fiendish Brand", "Anytime this creature casts a holy spell while bleed damage from Fiendish Brand persists, it must succeed at a DC 5 flat check or the spell is disrupted.", ExpirationCondition.Ephemeral, self, MIllustrations.CreateIllustration("FiendishBrand")));
                            };
                            target.AddQEffect(brand);
                        }
                    };
                    StrikeModifiers strikeModifiers = new()
                    {
                        QEffectForStrike = applyBrand
                    };
                    List<ActionPossibility> strikes = [];
                    foreach (CombatAction strike in EagleKnight.CreateStandardAndThrownStrikes(self, weapon, strikeModifiers))
                    {
                        strike.WithFullRename("Fiendish Brand");
                        strike.WithActionCost(2)
                            .WithActionId(RActionIds.FiendishBrand)
                            .WithExtraTrait(Trait.Divine)
                            .WithExtraTrait(UnholyTrait.Unholy)
                            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(strikeModifiers, additionalSuccessText: "The target takes 1d6 persistent bleed damage, which has the unholy trait. Anytime the creature casts a holy spell while this bleed damage persists, it must succeed at a DC 5 flat check or the spell is disrupted."));
                        strike.Illustration = new SideBySideIllustration(MIllustrations.CreateIllustration("FiendishBrand"), strike.HasTrait(Trait.Thrown) ? IllustrationName.Throw : weapon.Illustration);
                        strikes.Add(strike);
                    }
                    return strikes;
                };
            });
        yield return ArchetypeFeat("Vengeance Strike", 6,
                "You lash out at the foe who dared to attack you.",
                "When an enemy damages you and is within your reach, you can use a reaction {icon:Reaction} to make a melee Strike against the triggering enemy. If you hit and deal damage, the enemy is off-guard to you until the end of your next turn.",
                SisterOfTheErinys)
            .WithActionCost(-2)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.AddGrantingOfTechnical(
                    cr => cr.EnemyOf(self) && cr.DistanceToWith10FeetException(self) <= self.Space.ActualReach,
                    qfTech =>
                    {
                        qfTech.AfterYouDealDamage = async (enemy, provokingAction, target) =>
                        {
                            if (target != self)
                                return;
                            if (!(await CommonCombatActions.OfferAndMakeReactiveStrike(qf.Owner, enemy, $"{{b}}{enemy.Name}{{/b}} dealt damage to you which provokes Vengeance Strike.\nUse your reaction to Strike them?", "*vengeance strike*", 1,
                                    [Trait.ReactiveAttack])).HasValue)
                                return;
                            EagleKnight.StoreRespondedTo(qf, provokingAction);
                        };

                    });
            });
        // Needs functioning grapple weapons
        // yield return ArchetypeFeat("Cruel Piercing", 8,
        //         "",
        //         "",
        //         SisterOfTheErinys)
        //     .WithRulesBlockForCombatAction(self =>
        //     {
        //
        //     })
        //     .WithPrerequisite(MFeatNames.GoldenErinysStance, "Golden Erinys Stance")
        //     .WithPermanentQEffectAndSameRulesText(qf =>
        //     {
        //
        //     });
        yield return ArchetypeFeat("Promise of Pain", 10,
                "You call out to an enemy in distress, promising them a future of unending pain.",
                "Once per minute: choose an enemy with the sickened condition that you can see within 120 feet. That creature takes 10d4 mental damage with a basic Will save against your class DC or spell DC, whichever is higher. At 12th level and every 2 levels thereafter, the damage increases by 2d4.",
                SisterOfTheErinys, Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental,
                Trait.Nonlethal)
            .WithActionCost(1)
            .WithPermanentQEffect(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    int heightenLevel = self.Level % 2 == 0 ? self.Level : self.Level - 1;
                    int classOrSpellDC = self.ClassOrSpellDC();
                    var dice = $"{heightenLevel}d4";
                    var description = $"The target takes {dice} mental damage (DC {classOrSpellDC} basic Will save mitigates). Promise of Pain can only be used once per minute.";
                    CombatAction promise = CombatAction.CreateAction(self,
                            MIllustrations.CreateIllustration("PromisePain"), "Promise of Pain",
                            [Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental],
                            description, Target.Ranged(10)
                                .WithAdditionalConditionOnTargetCreature(
                                    new TargetHasQEffectCreatureTargetingRequirement(QEffectId.Sickened,
                                        "Target is not sickened."))
                                .WithAdditionalConditionOnTargetCreature((owner, _) => owner.HasEffect(MQEffectIds.PromiseOfPain) ? Usability.NotUsable("You can only use Promise of Pain once per minute.") : Usability.Usable)
                                .WithOverriddenTargetLine("1 sickened creature", false),
                            1,
                            SfxName.Mental, new SavingThrow(Defense.Will, classOrSpellDC))
                        .WithActionCost(1)
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,
                                DiceFormula.FromText(dice, spell.Name), DamageKind.Mental);
                            caster.AddQEffect(new QEffect("Promise of Pain Cooldown",
                                "When this effect disappears, Promise of Pain can be used again.",
                                ExpirationCondition.CountsDownAtStartOfSourcesTurn, self, spell.Illustration)
                            {
                                DoNotShowUpOverhead = true,
                                Value = 10,
                                HideValue = true,
                                Id = MQEffectIds.PromiseOfPain
                            });
                        });
                    return new ActionPossibility(promise).WithPossibilityGroup("Abilities");
                };

            });
        yield return ArchetypeFeat("Wrathful Presence", 12,
                "You channel the unending wrath of the erinyes all around you.",
                "You have a 30-foot aura for the rest of the encounter. You and your allies in the aura gain a +3 status bonus to damage with Strikes. Your enemies who end their turn within the aura can't reduce their frightened value below 1. This action can only be used once per encounter.",
                SisterOfTheErinys, Trait.Aura, Trait.Divine, Trait.Emotion, Trait.Fear, Trait.Mental)
            .WithActionCost(2)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction presence = new CombatAction(self,
                            MIllustrations.CreateIllustration("WrathfulPresence"), "Wrathful Presence",
                            [Trait.Aura, Trait.Divine, Trait.Emotion, Trait.Fear, Trait.Mental],
                            "You have a 30-foot aura for the rest of the encounter. You and your allies in the aura gain a +3 status bonus to damage with Strikes. Your enemies who end their turn within the aura can't reduce their frightened value below 1. This action can only be used once per encounter.",
                            Target.Self().WithAdditionalRestriction(cr => cr.HasEffect(MQEffectIds.Wrathful) ? "Wrathful Presence can only be used once per encounter." : null))
                        .WithActionCost(2)
                        .WithSoundEffect(SfxName.Fear)
                        .WithEffectOnSelf(async (spell, caster) =>
                        {
                            QEffect wrath = new("Wrathful Presence",
                                spell.Description.Replace(" This action can only be used once per encounter.", ""),
                                ExpirationCondition.Never, caster, spell.Illustration)
                            {
                                SpawnsAura = _ => new MagicCircleAuraAnimation(IllustrationName.BaneCircle, Color.Crimson, 6),
                                Id = MQEffectIds.Wrathful
                            };
                            wrath.AddGrantingOfTechnical(cr => cr.DistanceTo(caster) <= 6, qfTech =>
                            {
                                qfTech.BonusToDamage = (effect, action, _) =>
                                    effect.Owner.EnemyOf(caster) ? null :
                                    action.HasTrait(Trait.Strike) ? new Bonus(3, BonusType.Status, spell.Name) : null;
                                qfTech.StateCheck = effect =>
                                {
                                    if (effect.Owner.FriendOf(caster))
                                        return;
                                    effect.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                        { Id = QEffectId.DirgeOfDoomFrightenedSustainer });
                                };
                            });
                            caster.AddQEffect(wrath);
                        });
                    return new ActionPossibility(presence).WithPossibilityGroup("Abilities");
                };
            });
    }
    
}