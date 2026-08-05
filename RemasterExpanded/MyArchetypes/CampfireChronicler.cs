using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using RemasterExpanded.MySpells;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MyArchetypes;

public static class CampfireChronicler
{
    public static IEnumerable<Feat> CampfireFeats()
    {
        yield return ArchetypeFeats.CreateAgnosticArchetypeDedication(MTraits.CampfireChronicler, 
            "You follow in the footsteps of the Chronicler, an ancient god dedicated to protecting and assisting travelers in return for a story of their journey. Though tales of the Chronicler himself are scarce, his gifts are still sometimes given to those who protect travelers and collect their stories.", 
            "You become trained in Religion and Survival; for either of these skills in which you were already trained, you instead become trained in a skill of your choice. You gain the Offer Story action.")
            .WithPermanentQEffect("You can offer a story that grants bonuses.", qf =>
            {
                qf.ProvideMainAction = effect =>
                {
                    CombatAction cBlessing = ChroniclersBlessingAction(effect.Owner).WithName("Chronicler's Blessing");
                    return new SubmenuPossibility(MIllustrations.CreateIllustration("OfferStories"), "Offer Story")
                    {
                        SubmenuId = MSubmenuIds.CampfireChronicler,
                        Subsections = [Stories().WithPossibility(new ActionPossibility(cBlessing))]
                    };
                };
                qf.Name = "Offer Story";
            })
            .WithRulesBlockForCombatAction(ChroniclersBlessingAction)
            .WithOnSheet(values =>
            {
                values.TrainInThisOrSubstitute(Skill.Religion);
                values.TrainInThisOrSubstitute(Skill.Survival);
                values.Tags.TryAdd("CampfireChronicler", new List<QEffect>());
                if (values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                {
                    effectsList.Add(ChroniclersBlessing());
                }
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_RagingStories", "Raging Stories"), 4,
                "You tell a story of wild chances and fighting against all odds.",
                "You can offer this story to gain the following benefits in place of Offer Story's original benefits. Offer Story gains the fire trait when used this way. Your melee Strikes for the duration deal 2 additional fire damage. The creature you share a story with can choose this benefit even if you did not." +
                "\n\nThe fire damage increases to 4 if you are a master of Religion and 6 if you are legendary.", [Trait.Divine])
            .WithAvailableAsArchetypeFeat(MTraits.CampfireChronicler)
            .WithPermanentQEffect(null, qf =>
            {
                qf.ProvideActionIntoPossibilitySection = (effect, possibility) => possibility.PossibilitySectionId != MSectionIds.Stories ? null : new ActionPossibility(RagingStoriesAction(effect.Owner));
            })
            .WithOnSheet(values =>
            {
                if (values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                {
                    effectsList.Add(RagingStories());
                }
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_IlluminatingStories", "Illuminating Stories"), 6,
                "Stories serve as beacons in the night, illuminating valuable knowledge even if the tale itself misses the mark.",
                "You can offer this story to gain the following benefits in place of Offer Story's original benefits. You receive a +1 status bonus on checks to Recall Weakness for the duration and can Recall Weakness about one creature as a free action. The creature you share a story with can choose this benefit even if you did not." +
                "\n\nThe bonus increases to +2 if you are a master of Religion and +3 if you are legendary.",
                [Trait.Divine])
            .WithAvailableAsArchetypeFeat(MTraits.CampfireChronicler)
            .WithPermanentQEffect(null, qf =>
            {
                qf.ProvideActionIntoPossibilitySection = (effect, section) => section.PossibilitySectionId != MSectionIds.Stories ? null : new ActionPossibility(IlluminatingStoriesAction(effect.Owner));
            })
            .WithOnSheet(values =>
            {
                if (values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                {
                    effectsList.Add(IlluminatingStories());
                }
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_FlickeringStories", "Flickering Stories"), 8,
                "Stories, like fires, can create shadows as much as they illuminate.",
                "You can offer this story to gain the following benefits in place of Offer Story's original benefits. Offer Story gains the shadow trait when used this way. You become concealed by shadows for the duration. You can’t use this concealment to Hide. The creature you share a story with can choose this benefit even if you did not.", [Trait.Divine])
            .WithAvailableAsArchetypeFeat(MTraits.CampfireChronicler)
            .WithPermanentQEffect(null, qf =>
            {
                qf.ProvideActionIntoPossibilitySection = (effect, possibility) => possibility.PossibilitySectionId != MSectionIds.Stories ? null : new ActionPossibility(FlickeringStoriesAction(effect.Owner));
            })
            .WithOnSheet(values =>
            {
                if (values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                {
                    effectsList.Add(FlickeringStories());
                }
            });
        
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_ListenersBoon", "Listener's Boon"), 4,
                "The Chronicler has given you a gift to assist you on your journey.",
                $"You gain the Domain Initiate feat for the domain of fire, knowledge,{(ModManager.TryParse("Protection", out FeatName _) || ModManager.TryParse("PS_Protection", out FeatName _) ? " protection," : "")} or travel. You gain the trained proficiency rank in spell attack modifier and spell DC, increasing to expert at 11th level.",
                [], [.. DuplicateDomains()])
            .WithAvailableAsArchetypeFeat(MTraits.CampfireChronicler)
            .WithMultipleSelection()
            .WithOnSheet(values =>
            {
                values.AddFeatForPurposesOfPrerequisitesOnly(FeatName.DomainInitiate);
                values.IncreaseProficiency(11, Trait.Spell, Proficiency.Expert);
            });
        yield return ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.AdvancedDomain, MTraits.CampfireChronicler, 8).WithSubfeats(DuplicateAdvancedDomains().ToList());
        
    }

    public static IEnumerable<Feat> DuplicateDomains()
    {
        yield return DuplicateDomain(FeatName.DomainFire, SpellId.FireRay);
        yield return DuplicateDomain(MFeatNames.Knowledge, SpellIds.ScholarlyRecollection);
        yield return DuplicateDomain(FeatName.DomainTravel, SpellId.AgileFeet);
        if (ModManager.TryParse("PS_Protection", out FeatName protection2) && ModManager.TryParse("PS_ProtectorsSacrifice", out SpellId sacrifice2))
            yield return DuplicateDomain(protection2, sacrifice2);
        else if (ModManager.TryParse("Protection", out FeatName protection) && ModManager.TryParse("ProtectorsSacrifice", out SpellId sacrifice))
            yield return DuplicateDomain(protection, sacrifice);
    }

    public static IEnumerable<Feat> DuplicateAdvancedDomains()
    {
        List<Feat>? enumerable = AllFeats.GetFeatByFeatName(FeatName.Cleric).Subfeats;
        if (enumerable == null) yield break;
        List<FeatName> domains = [];
        foreach (Feat deity in enumerable)
        {
            if (deity is not DeitySelectionFeat aDeity) continue;
            domains.AddRange(aDeity.AllowedDomains);
        }
        foreach (FeatName domain in domains.Distinct())
        {
            yield return NewDeities.CreateAdvancedDomainFeat(MTraits.CampfireChronicler, AllFeats.GetFeatByFeatName(domain));
        }
    }

    public static CombatAction ChroniclersBlessingAction(Creature self)
    {
        return new CombatAction(self, IllustrationName.Bless,
                "Offer Story - Chronicler's Blessing",
                [Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental, Trait.Basic],
                "{i}You share a story of your travels.{/i}" +
                "\n\nYou gain the blessing of the Chronicler as a +1 status bonus to AC and Will saves until the end of your next turn. On their next turn, the creature you shared a story with can take a single action, which has the auditory, concentrate, linguistic, and mental traits, to respond with their own story to gain the same benefits until the end of the following turn.", 
                Target.RangedFriend(6))
            .WithActionCost(1)
            .WithSoundEffect(SfxName.BookOpen)
            .WithEffectOnEachTarget(async (_, caster, target, _) =>
            {
                caster.AddQEffect(ChroniclersBlessing().WithSource(caster));
                if (caster.PersistentCharacterSheet?.Calculated is {} values && values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                    target.AddQEffect(RespondToStory(effectsList, caster));
            });
    }

    public static CombatAction RagingStoriesAction(Creature self)
    {
        int bonus = self.Proficiencies.Get(Trait.Religion) >= Proficiency.Legendary ? 6 :
            self.Proficiencies.Get(Trait.Religion) >= Proficiency.Master ? 4 : 2;
        return new CombatAction(self, IllustrationName.KindleInnerFlames,
                "Raging Stories",
                [Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental, Trait.Fire, Trait.Basic],
                "{i}You tell a story of wild chances and fighting against all odds.{/i}" +
                $"\n\nYou can offer this story to gain the following benefits in place of Offer Story's original benefits. Your melee Strikes for the duration deal {bonus} additional fire damage. The creature you share a story with can choose this benefit even if you did not. On their next turn, the creature you shared a story with can take a single action, which has the auditory, concentrate, linguistic, and mental traits, to respond with their own story to gain the same benefits until the end of the following turn.", 
                Target.RangedFriend(6))
            .WithActionCost(1)
            .WithSoundEffect(SfxName.BookOpen)
            .WithEffectOnEachTarget(async (_, caster, target, _) =>
            {
                caster.AddQEffect(RagingStories().WithSource(caster));
                if (caster.PersistentCharacterSheet?.Calculated is {} values && values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                    target.AddQEffect(RespondToStory(effectsList, caster));
            });
    }
    public static CombatAction IlluminatingStoriesAction(Creature self)
    {
        int bonus = self.Proficiencies.Get(Trait.Religion) >= Proficiency.Legendary ? 3 :
            self.Proficiencies.Get(Trait.Religion) >= Proficiency.Master ? 2 : 1;
        return new CombatAction(self, IllustrationName.NarratorBook,
                "Illuminating Stories",
                [Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental, Trait.Basic],
                "{i}Stories serve as beacons in the night, illuminating valuable knowledge even if the tale itself misses the mark.{/i}" +
                $"\n\nYou can offer this story to gain the following benefits in place of Offer Story's original benefits. You receive a +{bonus} status bonus on checks to Recall Weakness for the duration and can Recall Weakness about one creature as a free action. The creature you share a story with can choose this benefit even if you did not. On their next turn, the creature you shared a story with can take a single action, which has the auditory, concentrate, linguistic, and mental traits, to respond with their own story to gain the same benefits until the end of the following turn.", 
                Target.RangedFriend(6))
            .WithActionCost(1)
            .WithSoundEffect(SfxName.BookOpen)
            .WithEffectOnEachTarget(async (_, caster, target, _) =>
            {
                caster.AddQEffect(IlluminatingStories().WithSource(caster));
                if (caster.PersistentCharacterSheet?.Calculated is {} values && values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                    target.AddQEffect(RespondToStory(effectsList, caster));
            });
    }
    public static CombatAction FlickeringStoriesAction(Creature self)
    {
        return new CombatAction(self, IllustrationName.Blur,
                "Flickering Stories",
                [Trait.Auditory, Trait.Concentrate, Trait.Divine, Trait.Linguistic, Trait.Mental, Trait.Basic, Trait.Shadow],
                "{i}Stories, like fires, can create shadows as much as they illuminate.{/i}" +
                "\n\nYou can offer this story to gain the following benefits in place of Offer Story's original benefits. You become concealed by shadows for the duration. You can’t use this concealment to Hide. On their next turn, the creature you shared a story with can take a single action, which has the auditory, concentrate, linguistic, and mental traits, to respond with their own story to gain the same benefits until the end of the following turn.", 
                Target.RangedFriend(6))
            .WithActionCost(1)
            .WithSoundEffect(SfxName.BookOpen)
            .WithEffectOnEachTarget(async (_, caster, target, _) =>
            {
                caster.AddQEffect(FlickeringStories().WithSource(caster));
                if (caster.PersistentCharacterSheet?.Calculated is {} values && values.Tags.TryGetValue("CampfireChronicler", out object? effects) &&
                    effects is List<QEffect> effectsList)
                    target.AddQEffect(RespondToStory(effectsList, caster));
            });
    }

    public static QEffect RespondToStory(List<QEffect> stories, Creature source)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            ProvideMainAction = effect =>
            {
                List<Possibility> storyAction = [];
                storyAction.AddRange(stories.Select(qf => 
                        new CombatAction(effect.Owner, qf.Illustration ?? IllustrationName.None, qf.Name ?? "placeholder", 
                                [Trait.Auditory, Trait.Concentrate, Trait.Linguistic, Trait.Mental, Trait.Basic, qf.Name switch
                                {
                                    "Raging Stories" => Trait.Fire,
                                    "Flickering Stories" => Trait.Shadow,
                                    _ => Trait.None
                                }], qf.Description?.Replace("have", "gain") ?? "", Target.Self())
                        .WithActionCost(1)
                        .WithSoundEffect(SfxName.BookClosed)
                        .WithEffectOnChosenTargets(async (self, _) =>
                        {
                            self.AddQEffect(qf.WithSource(source));
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                        }))
                    .Select(offer => new ActionPossibility(offer).WithPossibilityGroup("Stories")));
                SubmenuPossibility storyBoard =
                    new(MIllustrations.CreateIllustration("OfferStories"), "Respond to Story")
                    {
                        Subsections = [new PossibilitySection("Stories"){Possibilities = storyAction, PossibilitySectionId = MSectionIds.Stories}]
                    };
                return storyBoard;
            }
        };
    }

    public static QEffect ChroniclersBlessing()
    {
        return new QEffect("Chronicler's Blessing", "You have a +1 status bonus to AC and Will saves until the end of your next turn.", ExpirationCondition.ExpiresAtEndOfYourTurn, null, IllustrationName.Bless)
        {
            BonusToDefenses = (_, _, defense) => defense.IsSavingThrow() && defense is Defense.Will || defense is Defense.AC ? new Bonus(1, BonusType.Status, "Chronicler's Blessing") : null,
            CannotExpireThisTurn = true
        };
    }

    public static QEffect RagingStories()
    {
        return new QEffect("Raging Stories", "Your melee Strikes for the duration deal 2 additional fire damage.", ExpirationCondition.ExpiresAtEndOfYourTurn, null,
            IllustrationName.KindleInnerFlames)
        {
            StateCheck = qf =>
            {
                int bonus = qf.Source?.Proficiencies.Get(Trait.Religion) >= Proficiency.Legendary ? 6 :
                    qf.Source?.Proficiencies.Get(Trait.Religion) >= Proficiency.Master ? 4 : 2;
                qf.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    AddExtraKindedDamageOnStrike = (action, _) => !action.HasTrait(Trait.Melee) ? null : new KindedDamage(DiceFormula.FromText($"{bonus}", "Raging Stories"), DamageKind.Fire)
                });
                if (bonus > 2 && qf.Description != null && !qf.Description.Contains(bonus.ToString()))
                {
                    qf.Description = qf.Description?.Replace("2", $"{{b}}{bonus}{{/b}}");
                }
            },
            CannotExpireThisTurn = true
        };
    }

    public static QEffect IlluminatingStories()
    {
        var applied = false;
        return new QEffect("Illuminating Stories", "You have a +1 status bonus on checks to Recall Weakness for the duration and can Recall Weakness a chosen creature as a free action.", ExpirationCondition.ExpiresAtEndOfYourTurn, null,
            IllustrationName.NarratorBook)
        {
            StateCheck = qf => 
            {
                int bonus = qf.Source?.Proficiencies.Get(Trait.Religion) >= Proficiency.Legendary ? 3 :
                    qf.Source?.Proficiencies.Get(Trait.Religion) >= Proficiency.Master ? 2 : 1;
                qf.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    BonusToSkillChecks = (_, action, _) => action.ActionId == RecallWeakness.RWActionId ? new Bonus(bonus, BonusType.Status, "Illuminating Stories") : null,
                });
                if (bonus > 2 && qf.Description != null && !qf.Description.Contains(bonus.ToString()))
                {
                    qf.Description = qf.Description?.Replace("1", $"{{b}}{bonus}{{/b}}");
                }
                if (applied) return;
                qf.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral) { Id = MQEffectIds.Illuminate });
                applied = true;
            },
            CannotExpireThisTurn = true,
            AfterYouAcquireEffect = async (effect, qf) =>
            {
                if (qf.Id != MQEffectIds.Illuminate)
                    return;
                CombatAction recall = RecallWeakness.CreateRecallWeaknessAction(effect.Owner).WithActionCost(0);
                if (!recall.CanBeginToUse(effect.Owner))
                    return;
                await effect.Owner.Battle.GameLoop.FullCast(recall);
            }
        };
    }

    public static QEffect FlickeringStories()
    {
        return new QEffect("Flickering Stories", "You are concealed by shadows for the duration. You can’t use this concealment to Hide.", ExpirationCondition.ExpiresAtEndOfYourTurn, null, IllustrationName.Blur)
        {
            ThisCreatureCannotBeMoreVisibleThan = DetectionStrength.ConcealedViaBlur,
            CannotExpireThisTurn = true
        };
    }

    public static Feat DuplicateDomain(FeatName domain, SpellId domainSpell)
    {
        Feat duplicateDomain = CommonFeatTemplates.CreateDuplicateFeat(domain,
            ModManager.RegisterFeatName("RE_Domain"+domain, domain.HumanizeTitleCase2()), 0);
        duplicateDomain.LevelIfAny = null;
        duplicateDomain.OnSheet = null;
        duplicateDomain.WithOnSheet(sheet =>
        {
            sheet.AddFeatForPurposesOfPrerequisitesOnly(domain);
            sheet.AddFocusSpellAndFocusPoint(MTraits.CampfireChronicler, Ability.Charisma, domainSpell);
        });
        return duplicateDomain;
    }

    public static PossibilitySection Stories()
    {
        return new PossibilitySection("Stories") { PossibilitySectionId = MSectionIds.Stories };
    }

    public static PossibilitySection WithPossibility(this PossibilitySection possibilitySection,
        Possibility possibility)
    {
        possibilitySection.Possibilities.Add(possibility);
        return possibilitySection;
    }

    public static Feat WithSubfeats(this Feat feat, List<Feat> subFeats)
    {
        feat.Subfeats = subFeats;
        return feat;
    }

    extension(QEffect effect)
    {
        public QEffect WithAdditionalDescription(string description)
        {
            effect.Description += description;
            return effect;
        }

        public QEffect WithSource(Creature source)
        {
            effect.Source = source;
            return effect;
        }
    }
}