using System.Reflection;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.AuraAnimations;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Feats.Features;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Champion;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes.Multiclass;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Mechanics.Zoning;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Mods.LoresAndWeaknesses;
using Microsoft.Xna.Framework;
using RemasterExpanded.MyArchetypes;
using RemasterExpanded.MySpells;
using RemasterExpanded.Technical;
using SpiritDamage;
using static RemasterExpanded.ClassChangesAndFeats.ChampionReactionLogics;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.ClassChangesAndFeats;

public static class ChampionRemaster
{
    public const string AuraDescription = "You're surrounded by an aura in a 15-foot emanation. It has the aura and divine traits. This aura is used as the range for your champion's reaction and for various other effects.";
    public static readonly string Sanctified = AllFeats.GetFeatByFeatName(MFeatNames.ChampionSanctification).ToLink("sanctified");
    public const string UnholyTooltip = "{tooltip:unholy}unholy{/tooltip}";
    public const string HolyTooltip = "{tooltip:holy}holy{/tooltip}";
    public static void Load()
    {
        ModManager.RegisterInlineTooltip("ChampionAura", AuraDescription);
        Feat champion = AllFeats.GetFeatByFeatName(FeatName.Champion)
            .WithOnCreature(cr => cr.AddQEffect(ChampionAura()));
        champion.RulesText = champion.RulesText.Replace("{b}5. Champion feat.{/b}",
            $"{{b}}5. Champion's aura.{{/b}} {AuraDescription}\n\n" +
            "{b}6. Champion feat.{/b}");
        champion.RulesText = champion.RulesText.Replace("You're trained in your deity's favored weapon",
            "If your deity's favored weapon is an advanced weapon, you treat it as a martial weapon for the purposes of proficiency,");
        if (champion is ClassSelectionFeat { ClassFeatures: not null } championClass)
        {
            championClass.ClassFeatures.ClassFeaturesByLevel[3].RemoveAll(cf => cf.Caption == "Divine ally");
            championClass.ClassFeatures.ClassFeaturesByLevel[3].Add(new ClassFeature("Blessing of the devoted", "Choose whether your divine blessing is blessed armament, blessed shield, or blessed swiftness and gain corresponding advantages."));
            championClass.ClassFeatures.ClassFeaturesByLevel[9].RemoveAll(cf => cf.Caption == "Divine smite");
            championClass.ClassFeatures.ClassFeaturesByLevel[9].Add(new ClassFeature("Relentless reaction", "Your champion's reaction improves."));
        }
        champion.WithOnSheet(sheet =>
        {
            sheet.AddAtLevel(3, values =>
            {
                SelectionOption? divineAlly = values.SelectionOptions.FirstOrDefault(so => so.Key == "divineAllySelection" || so.KeyLegacy == "divineAllySelection" || so.Name == "Divine ally");
                if (divineAlly == null) return;
                FieldInfo? setName = typeof(SelectionOption).GetField("<Name>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (setName == null) return;
                setName.SetValue(divineAlly, "Blessing of the devoted");
            });
        });
        if (AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Champion.ToStringOrTechnical() + "Dedication") is
            { } dedication)
        {
            dedication.WithOnCreature(cr => cr.AddQEffect(ChampionAura()));
            dedication.OnSheet = null;
            dedication.WithOnSheet(values =>
            {
                values.SetProficiency(Trait.Champion, Proficiency.Trained);
                values.AdditionalClassTraits.Add(Trait.Champion);
                values.NumberOfFeatsForDedication.TryAdd(Trait.Champion, 0);
            });
            dedication.WithOnSheet(values =>
            {
                MulticlassArchetypeFeats.SelectArchetypeDeity(values);
                values.TrainInThisOrSubstitute(Skill.Religion);
                if (values.Proficiencies.Get(Trait.MediumArmor) >= Proficiency.Trained)
                {
                    values.SetProficiency(Trait.HeavyArmor, Proficiency.Trained);
                    values.Proficiencies.Autoupgrade([
                        Trait.LightArmor,
                        Trait.MediumArmor,
                        Trait.HeavyArmor,
                        Trait.Armor
                    ], [Trait.LightArmor]);
                    values.Proficiencies.Autoupgrade([
                        Trait.LightArmor,
                        Trait.MediumArmor,
                        Trait.HeavyArmor,
                        Trait.Armor
                    ], [Trait.MediumArmor]);
                    values.Proficiencies.Autoupgrade([
                        Trait.LightArmor,
                        Trait.MediumArmor,
                        Trait.HeavyArmor,
                        Trait.Armor
                    ], [Trait.HeavyArmor]);
                    values.AddAtLevel(13, sheet =>
                    {
                        if (sheet.GetProficiency(Trait.UnarmoredDefense) < Proficiency.Expert ||
                            (sheet.GetProficiency(Trait.LightArmor) >= Proficiency.Expert &&
                             sheet.GetProficiency(Trait.MediumArmor) >= Proficiency.Expert
                             && sheet.GetProficiency(Trait.HeavyArmor) >= Proficiency.Expert)) return;
                        sheet.SetProficiency(Trait.LightArmor, Proficiency.Expert);
                        sheet.SetProficiency(Trait.MediumArmor, Proficiency.Expert);
                        sheet.SetProficiency(Trait.HeavyArmor, Proficiency.Expert);
                    });
                }
                else
                {
                    values.SetProficiency(Trait.LightArmor, Proficiency.Trained);
                    values.SetProficiency(Trait.MediumArmor, Proficiency.Trained);
                    values.Proficiencies.Autoupgrade([
                        Trait.LightArmor,
                        Trait.MediumArmor,
                        Trait.HeavyArmor,
                        Trait.Armor
                    ], [Trait.LightArmor]);
                    values.Proficiencies.Autoupgrade([
                        Trait.LightArmor,
                        Trait.MediumArmor,
                        Trait.HeavyArmor,
                        Trait.Armor
                    ], [Trait.MediumArmor]);
                    values.AddAtLevel(13, sheet =>
                    {
                        if (sheet.GetProficiency(Trait.UnarmoredDefense) < Proficiency.Expert ||
                            (sheet.GetProficiency(Trait.LightArmor) >= Proficiency.Expert &&
                             sheet.GetProficiency(Trait.MediumArmor) >= Proficiency.Expert)) return;
                        sheet.SetProficiency(Trait.LightArmor, Proficiency.Expert);
                        sheet.SetProficiency(Trait.MediumArmor, Proficiency.Expert);
                    });
                }
            });
            dedication.RulesText = dedication.RulesText.Replace("You also become trained in all armor.",
                "You become trained in light armor and medium armor. If you already were trained in light armor and medium armor, you gain training in heavy armor as well. Whenever you gain a class feature that grants you expert or greater proficiency in any type of armor (but not unarmored defense), you also gain that proficiency in the armor types granted to you by this feat. If you have a class feature that grants you expert proficiency in unarmored defense and you're 13th level or higher, you also become an expert in the armor types granted to you by this feat.");
            if (dedication.Subfeats != null)
            {
                foreach (Feat subclass in dedication.Subfeats)
                {
                    switch (subclass.Name)
                    {
                        case "Paladin (Lawful Good)":
                            subclass.WithCustomName("Justice (Lawful Good)");
                            break;
                        case "Redeemer (Neutral Good)":
                            subclass.WithCustomName("Redemption (Neutral Good)");
                            break;
                        case "Liberator (Chaotic Good)":
                            subclass.WithCustomName("Liberation (Chaotic Good)");
                            break;
                        case "Tyrant (Lawful Evil)":
                            subclass.WithCustomName("Obedience (Lawful Evil)");
                            break;
                        case "Desecrator (Neutral Evil)":
                            subclass.WithCustomName("Desecration (Neutral Evil)");
                            break;
                        case "Antipaladin (Chaotic Evil)":
                            subclass.WithCustomName("Iniquity (Chaotic Evil)");
                            break;
                    }
                    subclass.WithModifiedRulesText("by this cause.", $"by this cause. You also become {Sanctified} as required by this cause.");
                }
            }
        }
        List<Feat>? subfeats = AllFeats.GetFeatByFeatName(FeatName.Champion).Subfeats;
        if (subfeats != null)
        {
            foreach (Feat subclass in subfeats)
            {
                switch (subclass.Name)
                {
                    case "Tyrant (Lawful Evil)":
                        subclass.WithCustomName("Obedience (Lawful Evil)");
                        subclass.OnCreature = null;
                        ModdedChampionCauses.AddObedienceReactionLogic(subclass);
                        subclass.WithModifiedRulesText("evil or negative", "spirit")
                            .WithModifiedRulesText("persistent evil", "persistent spirit")
                            .WithModifiedRulesText("You're lawful evil.", "You're lawful evil. You may choose to be "+Sanctified+$" as {UnholyTooltip}.");
                        Sanctification.ChampionSanctificationChoiceLogic(subclass);
                        break;
                    case "Desecrator (Neutral Evil)":
                        subclass.WithCustomName("Desecration (Neutral Evil)");
                        subclass.OnCreature = null;
                        ModdedChampionCauses.AddDesecrationReactionLogic(subclass);
                        subclass.WithModifiedRulesText("evil or negative", "spirit")
                            .WithModifiedRulesText("You're neutral evil.", "You're neutral evil. You are "+Sanctified+$" as {UnholyTooltip}.");
                        subclass.WithOnSheet(values => values.GrantFeat(MFeatNames.UnholyChampion));
                        break;
                    case "Antipaladin (Chaotic Evil)":
                        subclass.WithCustomName("Iniquity (Chaotic Evil)");
                        subclass.OnCreature = null;
                        ModdedChampionCauses.AddIniquityReactionLogic(subclass);
                        subclass.WithModifiedRulesText("either evil or negative", "spirit")
                            .WithModifiedRulesText("evil or negative", "spirit")
                            .WithModifiedRulesText("persistent evil", "persistent spirit")
                            .WithModifiedRulesText("You're chaotic evil.", "You're chaotic evil. You are "+Sanctified+$" as {UnholyTooltip}.");
                        subclass.WithOnSheet(values => values.GrantFeat(MFeatNames.UnholyChampion));
                        break;
                    case "Paladin (Lawful Good)":
                        subclass.OnCreature = null;
                        AddJusticeReactionLogic(subclass);
                        subclass.WithCustomName("Justice (Lawful Good)")
                            .WithModifiedRulesText("You're lawful good.", "You're lawful good. You may choose to be "+Sanctified+$" as {HolyTooltip}.");
                        Sanctification.ChampionSanctificationChoiceLogic(subclass);
                        break;
                    case "Redeemer (Neutral Good)":
                        subclass.WithCustomName("Redemption (Neutral Good)")
                            .WithModifiedRulesText("You're neutral good.", "You're neutral good. You are "+Sanctified+$" as {HolyTooltip}.");
                        subclass.OnCreature = null;
                        AddRedemptionReactionLogic(subclass);
                        subclass.WithOnSheet(values => values.GrantFeat(MFeatNames.HolyChampion));
                        break;
                    case "Liberator (Chaotic Good)":
                        subclass.OnCreature = null;
                        AddLiberationReactionLogic(subclass);
                        subclass.WithCustomName("Liberation (Chaotic Good)")
                            .WithModifiedRulesText("You're chaotic good.", "You're chaotic good. You may choose to be "+Sanctified+$" as {HolyTooltip}.");
                        Sanctification.ChampionSanctificationChoiceLogic(subclass);
                        break;
                    case "Grandeur (Any Good)":
                        subclass.OnCreature = null;
                        ModdedChampionCauses.AddGrandeurReactionLogic(subclass);
                        subclass.WithModifiedRulesText("You're good-aligned.", "You're good-aligned. You are "+Sanctified+$" as {HolyTooltip}.");
                        subclass.WithOnSheet(values => values.GrantFeat(MFeatNames.HolyChampion));
                        break;
                }
                subclass.RulesText = subclass.RulesText.Replace("within 15 feet of you",
                    "within your {tooltip:ChampionAura}champion's aura{/tooltip}");
                subclass.WithModifiedRulesText("within 15 feet",
                    "within your {tooltip:ChampionAura}champion's aura{/tooltip}");
                subclass.WithModifiedRulesText("persistent good", "persistent spirit");
            }
        }
        Feat auraOfCourage = AllFeats.GetFeatByFeatName(Champion.AuraOfCourageFeatName);
        auraOfCourage.RulesText = auraOfCourage.RulesText.Replace("within 15 feet",
            "within your {tooltip:ChampionAura}champion's aura{/tooltip}");
        auraOfCourage.Traits.Add(ModTrait);
        auraOfCourage.OnCreature = null;
        auraOfCourage.WithPermanentQEffect(
            "Anytime you gain {i}frightened{/i}, reduce its value by 1. Also, at the end of your turn, when you reduce your frightened condition value by 1, you also reduce the value by 1 for all allies within your champion's aura.",
            qf =>
            {
                qf.Id = QEffectId.Bravery;
                qf.YouAcquireQEffect = Fighter.YouAcquireFrightenedLessByOne;
                qf.EndOfYourTurnBeneficialEffect = async (_, self) =>
                {
                    if (!self.HasEffect(QEffectId.Frightened))
                        return;
                    foreach (Creature affectedCreature in self.Battle.AllCreatures.Where(
                                 cr => cr.FriendOf(self) && cr.DistanceTo(self) <= GetChampionAuraRange(self)))
                    {
                        QEffect? frightened = affectedCreature.HasEffect(QEffectId.Frightened)
                            ? affectedCreature.QEffects.FirstOrDefault(
                                qff => qff.Id == QEffectId.Frightened)
                            : null;
                        if (frightened != null)
                            Fighter.ReduceFrightenedValueOfFrightened(affectedCreature, frightened);
                    }
                };
            });
        auraOfCourage.Prerequisites.RemoveAll(prq => prq.Description.Contains("good"));
        auraOfCourage.WithPrerequisite(values => values.HasFeat(MFeatNames.HolyChampion), "You must be sanctified as holy.");
        Feat auraOfFaith = AllFeats.GetFeatByFeatName(FeatName.AuraOfFaith);
        auraOfFaith.OnCreature = null;
        auraOfFaith.WithPrerequisite(
            values => values.HasFeat(MFeatNames.UnholyChampion) || values.HasFeat(MFeatNames.HolyChampion),
            "You must be sanctified.");
        auraOfFaith.FlavorText =
            "You radiate an aura of belief that imbues the attacks of nearby allies with divine power.";
        auraOfFaith.RulesText = "";
        auraOfFaith.Traits.Add(ModTrait);
        auraOfFaith.WithRulesTextCreator(values =>
        {
            if (values.Calculated.HasFeat(MFeatNames.UnholyChampion))
                return $"Each ally in your {{tooltip:ChampionAura}}champion's aura{{/tooltip}} adds the {UnholyTooltip} trait to their Strikes.";
            return values.Calculated.HasFeat(MFeatNames.HolyChampion)
                ? $"Each ally in your {{tooltip:ChampionAura}}champion's aura{{/tooltip}} adds the {HolyTooltip} trait to their Strikes."
                : $"Each ally in your {{tooltip:ChampionAura}}champion's aura{{/tooltip}} adds the {HolyTooltip} trait to their Strikes if you're holy or adds the {UnholyTooltip} trait to their Strikes if you're unholy.";
        });
        auraOfFaith.WithPermanentQEffectAndSameRulesText(qf =>
        {
            Creature owner = qf.Owner;
            qf.AddGrantingOfTechnical(
                cr => cr.FriendOfAndNotSelf(owner) && cr.DistanceTo(owner) <= GetChampionAuraRange(owner),
                qfTech =>
                {
                    Trait holyOrUnholy = owner.HasTrait(HolyTrait.Holy) ? HolyTrait.Holy : UnholyTrait.Unholy;
                    qfTech.ModifyActionPossibility = (_, action) =>
                    {
                        if (!action.HasTrait(Trait.Strike))
                            return;
                        action.WithExtraTrait(holyOrUnholy);
                    };
                });
        });
        Feat divineHealth = AllFeats.GetFeatByFeatName(FeatName.DivineHealth);
        divineHealth.RulesText =
            "You gain a +2 status bonus to saves against diseases and poisons. Your flat check to remove persistent poison damage is reduced by 2. Allies in your {tooltip:ChampionAura}champion's aura{/tooltip} get this benefit, but their bonus is +1 and flat check reduction is 1." +
            "\n\nIn addition, if you roll a success on a save against a disease or poison, you get a critical success instead. (Your allies don't share this benefit.) If you have the sacred body class feature, when you roll a critical failure on a save against a disease or poison, you get a failure instead.";
        divineHealth.OnCreature = null;
        divineHealth.Traits.Remove(Trait.Homebrew);
        divineHealth.Traits.Add(ModTrait);
        divineHealth.LevelIfAny = 2;
        divineHealth.Prerequisites.RemoveAll(prerequisite => prerequisite.Description.Contains("level 4"));
        divineHealth.WithPrerequisite(values => values.CurrentLevel >= 2,
            "You can only select this feat at level 2 or later.");
        divineHealth.WithPermanentQEffect(
            "You and allies within your champion aura have improved defenses against poison and disease.", qf =>
            {
                qf.BonusToDefenses = (_, action, defense) => defense.IsSavingThrow() &&
                                                             action != null &&
                                                             (action.HasTrait(Trait.Poison) ||
                                                              action.HasTrait(Trait.Disease))
                    ? new Bonus(2, BonusType.Status, "Divine Health")
                    : null;
                qf.AdjustSavingThrowCheckResult = (effect, _, action, result) =>
                {
                    if (!action.HasTrait(Trait.Poison) && !action.HasTrait(Trait.Disease))
                        return result;
                    return result switch
                    {
                        CheckResult.Success => CheckResult.CriticalSuccess,
                        CheckResult.CriticalFailure when effect.Owner is
                        {
                            Level: >= 9, PersistentCharacterSheet.Class.ClassTrait: Trait.Champion
                        } => CheckResult.Failure,
                        _ => result
                    };
                };
                qf.Id = MQEffectIds.DivineHealth;
                qf.Value = 2;
                qf.HideValue = true;
                qf.AddGrantingOfTechnical(
                    cr => cr.FriendOfAndNotSelf(qf.Owner) &&
                          cr.DistanceTo(qf.Owner) <= GetChampionAuraRange(qf.Owner),
                    qfTech =>
                    {
                        qfTech.BonusToDefenses = (_, action, defense) => defense.IsSavingThrow() &&
                                                                         action != null &&
                                                                         (action.HasTrait(Trait.Poison) ||
                                                                          action.HasTrait(Trait.Disease))
                            ? new Bonus(1, BonusType.Status, "Divine Health")
                            : null;
                        qfTech.Id = MQEffectIds.DivineHealth;
                        qfTech.Value = 1;
                        qf.HideValue = true;
                    });
            });
        LoadOrder.WhenFeatsBecomeLoaded += () =>
        {
            Feat oath = AllFeats.GetFeatByFeatName(FeatName.Oath);
            oath.WithRulesTextCreator(_ =>
                "Choose an oath to swear. Oaths can grant you benefits versus specific types of foes or more general benefits.")
                .With(ft => ft.FlavorText = "You swear an oath, binding you to a particular cause.");
            if (PlayerProfile.Instance.IsBooleanOptionEnabled("HideLegacyFeats"))
            {
                oath.Subfeats = [];
                AllFeats.GetFeatByFeatName(FeatName.AnimalCompanionAlly).Traits.Clear();
                AllFeats.GetFeatByFeatName(FeatName.OathAura).Traits.Clear();
                AllFeats.GetFeatByFeatName(FeatName.SmiteEvil).Traits.Clear();
                AllFeats.GetFeatByFeatName(FeatName.DiverseArmorExpert).Traits.Clear();
                if (ModManager.TryParse("PS_SmiteGood", out FeatName smiteGood) && AllFeats.GetFeatByFeatNameOptional(smiteGood) is {} smiteGoodFeat)
                    smiteGoodFeat.Traits.Clear();
                if (GetModdedFeat("BladeAllyRunestone" + ItemName.FlamingRunestone.ToStringOrTechnical()) is {} flamingRunestone)
                    flamingRunestone.Traits.Clear();
                if (GetModdedFeat("BladeAllyRunestone" + ItemName.AnarchicRunestone.ToStringOrTechnical()) is {} anarchic)
                    anarchic.Traits.Clear();
                if (GetModdedFeat("BladeAllyRunestone" + ItemName.AxiomaticRunestone.ToStringOrTechnical()) is {} axiomatic)
                    axiomatic.Traits.Clear();
            }
            foreach (Feat feat in LoadOaths())
            {
                oath.Subfeats?.Add(feat);
                ModManager.AddFeat(feat);
            }
        };
        const string armament = "Your melee Strikes trigger {tooltip:criteffect}critical specialization effects{/}.\n\n" +
                                "When you make your daily preparations, choose one property rune from the following: disrupting, ghost touch, returning, fearsome, or shifting. All weapons you wield at the start of any encounter that day, as well as all your unarmed Strikes, will gain that property rune as an extra rune for that encounter, even if they already have the maximum possible of property runes.\n\n" +
                                "If you don't make a choice, your weapons will count as disrupting (if you are good aligned) or fearsome (if you are evil aligned).";
        AllFeats.GetFeatByFeatName(Champion.BladeAllyFeatName).WithCustomName("Blessed Armament")
            .With(ft =>
            {
                ft.RulesText = armament;
                if (!ChampionsOfEvil)
                    ft.WithOnSheet(values =>
                    {
                        if (values.NineCornerAlignment.GetTraits().Contains(Trait.Evil))
                        {
                            values.Tags["BLADE_ALLY_RUNESTONE"] = ItemName.FearsomeRunestone;
                        }
                    });
                ft.WithOnSheet(values =>
                {
                    SelectionOption? divineAlly = values.SelectionOptions.FirstOrDefault(so =>
                        so.Key == "BladeAllyPropertyRune" || so.KeyLegacy == "BladeAllyPropertyRune" ||
                        so.Name == "Blade Ally property rune");
                    if (divineAlly == null) return;
                    FieldInfo? setName = typeof(SelectionOption).GetField("<Name>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setName == null) return;
                    setName.SetValue(divineAlly, "Blessed Armament property rune");
                });
            });
        AllFeats.GetFeatByFeatName(Champion.ShieldAllyFeatName).WithCustomName("Blessed Shield");
        AllFeats.GetFeatByFeatName(FeatName.SecondAlly).WithCustomName("Second Blessing")
            .WithOnSheet(values =>
            {
                SelectionOption? divineAlly = values.SelectionOptions.FirstOrDefault(so => so.Key == "SecondAlly" || so.KeyLegacy == "SecondAlly" || so.Name == "Second Ally");
                if (divineAlly != null)
                {
                    FieldInfo? setName = typeof(SelectionOption).GetField("<Name>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setName == null)
                        return;
                    setName.SetValue(divineAlly, "Second Blessing");
                }
            })
            .WithRulesTextCreator(_ => "Choose a second blessing of the devoted (different from your first one) and gain its benefits.")
            .With(ft => ft.FlavorText = "Your continued service grants you another boon.");
        AllFeats.GetFeatByFeatName(FeatName.DivineAlly).WithCustomName("Devout Blessing")
            .WithRulesTextCreator(_ => "You gain a blessing of the devoted of your choice.");
        AllFeats.GetFeatByFeatName(FeatName.ChampionsReaction).WithRulesTextCreator(sheet =>
        {
            if (!sheet.Calculated.Tags.TryGetValue("ChampionOriginalCause", out object? obj) || obj is not Feat feat2)
                return null;
            string str = feat2.RulesText[(feat2.RulesText.IndexOf('\n') + 2)..];
            int length = str.IndexOf("At level 9", StringComparison.InvariantCulture);
            if (length != -1)
                str = str[..length];
            str = str.Trim();
            return str;
        });
        if (GetModdedFeat("PS_AuraOfDespair") is {} auraOfDespair)
        {
            auraOfDespair.Prerequisites.RemoveAll(prq => prq.Description.Contains("Evil"));
            auraOfDespair.WithPrerequisite(values => values.HasFeat(MFeatNames.UnholyChampion),
                "You must be sanctified as unholy");
            auraOfDespair.OnCreature = null;
            auraOfDespair
                .WithModifiedRulesText("within 15 feet of you", "within your {tooltip:ChampionAura}champion's aura{/tooltip}")
                .WithPermanentQEffect(
                "Enemies within your champion's aura take a –1 circumstance penalty to saving throws against fear. In addition, enemies within your champion's aura can't reduce the value of their frightened condition below 1.",
                qfFeat =>
                {
                    qfFeat.WhenYouAcquireThis = qf =>
                    {
                        Zone.Spawn(qf, ZoneAttachment.Aura(GetChampionAuraRange(qf.Owner)))
                            .With(zone =>
                            {
                                zone.StateCheckOnEachCreatureInZone = (_, enemy) =>
                                {
                                    if (!enemy.EnemyOf(qf.Owner)) return;
                                    enemy.AddQEffect(new QEffect("Aura of Despair",
                                        "You take a -1 circumstance penalty to saving throws against fear, and your frightened value can't go below 1.",
                                        ExpirationCondition.Ephemeral, qf.Owner,
                                        new ModdedIllustration("Champions of Evil Assets/PS_AuraOfDespair.png"))
                                    {
                                        CountsAsADebuff = true,
                                        Id = QEffectId.DirgeOfDoomFrightenedSustainer,
                                        BonusToDefenses = (_, attack, defense) =>
                                            attack != null && attack.HasTrait(Trait.Fear) && defense.IsSavingThrow() && attack.SavingThrow != null
                                                ? new Bonus(-1, BonusType.Circumstance, "Aura of Despair", false)
                                                : null
                                    });
                                };
                            });
                    };
                });
        }
        if (GetModdedFeat("BladeAllyRunestone" + ItemName.FearsomeRunestone.ToStringOrTechnical()) is { } fearsome)
        {
            fearsome.Prerequisites.Clear();
        }
        else
        {
            Item itemTemplate = Items.GetItemTemplate(ItemName.FearsomeRunestone);
            RuneProperties? runeProperties = itemTemplate.RuneProperties;
            if (runeProperties != null)
            {
                Feat fearsomeRune = new Feat(ModManager.RegisterFeatName(
                            "BladeAllyRunestone" + ItemName.FearsomeRunestone.ToStringOrTechnical(),
                            runeProperties.Prefix.Capitalize()), runeProperties.FlavorText,
                        $"All weapons you wield at the start of any encounter, as well as all your unarmed Strikes, will have the effect of the {runeProperties.Prefix} property rune in addition to any property runes they actually have for that encounter. This extra effect doesn't count against the number of property runes a weapon may have.\n\n{{b}}{runeProperties.Prefix.Capitalize()}:{{/b}} " +
                        runeProperties.RulesText, itemTemplate.Traits.Append(Trait.BladeAllyBasicPropertyRune).ToList(),
                        null).WithIllustration(itemTemplate.Illustration).WithLevel(itemTemplate.Level)
                    .WithOnSheet(values => { values.Tags["BLADE_ALLY_RUNESTONE"] = ItemName.FearsomeRunestone; });
                ModManager.AddFeat(fearsomeRune);
            }
        }
        if (GetModdedFeat("BladeAllyRunestone" + ItemName.DisruptingRunestone.ToStringOrTechnical()) is {} disrupting)
        {
            disrupting.Prerequisites.Clear();
        }
        if (GetModdedFeat("BladeAllyRunestone" + ItemName.GhostTouchRunestone.ToStringOrTechnical()) is {} ghostTouch)
        {
            ghostTouch.Prerequisites.Clear();
        }

        if (GetModdedFeat("BladeAllyRunestone" + ItemName.HolyRunestone.ToStringOrTechnical()) is {} holyRunestone)
        {
            holyRunestone.WithModifiedRulesText(Items.GetItemTemplate(ItemName.HolyRunestone).RuneProperties!.RulesText,
                $"Strikes made with the weapon gain the {HolyTooltip} trait and deal an extra 1d4 spirit damage, or an extra 2d4 against an {UnholyTooltip} target." +
                $"\n\nOnce per day, on a critical hit against an unholy creature, you can also heal HP equal to double the unholy creature's level as a reaction (concentrate, healing, vitality). If you are unholy yourself, you are enfeebled 2 while wielding this weapon.");
            holyRunestone.Prerequisites.Clear();
            holyRunestone.WithPrerequisite(values => values.HasFeat(MFeatNames.HolyChampion), "You must be sanctified as holy.");
            holyRunestone.Traits.RemoveAll(tr => tr is Trait.Good or Trait.Evocation);
            holyRunestone.Traits.Add(HolyTrait.Holy);
            holyRunestone.Traits.Add(ModTrait);
        }
        if (GetModdedFeat("BladeAllyRunestone" + ItemName.UnholyRunestone.ToStringOrTechnical()) is {} unholyRunestone)
        {
            unholyRunestone.WithModifiedRulesText(Items.GetItemTemplate(ItemName.UnholyRunestone).RuneProperties!.RulesText,
                $"Strikes made with the weapon gain the {UnholyTooltip} trait and deal an extra 1d4 spirit damage, or an extra 2d4 against an {HolyTooltip} target." +
                $"\n\nOnce per day, on a critical hit against a holy creature, you can spend a {{icon:Reaction}} reaction (concentrate) to cause the target to take an additional 1d8 persistent bleeding damage per weapon damage die. If you are holy yourself, you are enfeebled 2 while wielding this weapon.");
            unholyRunestone.Prerequisites.Clear();
            unholyRunestone.WithPrerequisite(values => values.HasFeat(MFeatNames.UnholyChampion), "You must be sanctified as unholy.");
            unholyRunestone.Traits.RemoveAll(tr => tr is Trait.Evil or Trait.Evocation);
            unholyRunestone.Traits.Add(UnholyTrait.Unholy);
            unholyRunestone.Traits.Add(ModTrait);
        }

        AllFeats.GetFeatByFeatName(FeatName.RadiantBladeSpirit)
            .WithCustomName("Radiant Armament")
            .With(ft =>
            {
                bool hideLegacy = PlayerProfile.Instance.IsBooleanOptionEnabled("HideLegacyFeats");
                ft.FlavorText = "Your blessed armament radiates power, further enhancing your chosen weapon.";
                ft.RulesText = $"When you choose the weapon for your blessed armament during your daily preparations, add the {(hideLegacy ? "astral and brilliant" : "astral, flaming, and brilliant")} property runes to the list of effects you can choose from. If you're {HolyTooltip}, also add the holy rune, and if you're {UnholyTooltip}, also add the unholy rune.\n\n" +
                               "In addition, you can change the rune you've selected for the day to a different rune from your list as a precombat preparation. Changing the rune doesn't restore abilities that can be used only a limited number of times, such as Holy Healing for the holy rune.";
            })
            .WithOnSheet(values =>
            {
                SelectionOption? blessedArmament = values.SelectionOptions.FirstOrDefault(so => so.Key == "BladeAllyPropertyRune" || so.KeyLegacy == "BladeAllyPropertyRune" || so.Name == "Blessed Armament property rune" || so.Name == "Blade Ally property rune");
                if (blessedArmament == null) return;
                blessedArmament.OptionLevel = SelectionOption.PRECOMBAT_PREPARATIONS_LEVEL;
            });
        if (!ModManager.TryParse("PS_TouchOfCorruption", out FeatName touchOfCorruption) ||
            AllFeats.GetFeatByFeatNameOptional(touchOfCorruption) is not { } touchOfTheVoid ||
            !ModManager.TryParse("PS_LayOnHands", out FeatName layOnHands) ||
            AllFeats.GetFeatByFeatNameOptional(layOnHands) is not { } layOnHands2) return;
        touchOfTheVoid.WithCustomName("Touch of the Void")
            .With(ft =>
            {
                ft.Prerequisites.RemoveAll(prq => prq.Description.Contains("Evil"));
                ft.WithPrerequisite(
                    values => values.Deity is { } deity && deity.AllowedFonts.Contains(FeatName.HarmfulFont),
                    "Your deity's divine font must allow harm.");
            });
        layOnHands2.With(ft =>
        {
            ft.Prerequisites.RemoveAll(pr => pr.Description.Contains("Good"));
            ft.WithPrerequisite(
                values => values.Deity is { } deity && deity.AllowedFonts.Contains(FeatName.HealingFont),
                "Your deity's divine font must allow heal.");

        });
    }

    public static IEnumerable<Feat> LoadFeats()
    {
        yield return CommonFeatTemplates.CreateDuplicateFeatForDifferentClass(FeatName.AnimalCompanionAlly,
                ModManager.RegisterFeatName("RE_FaithfulSteed", "Faithful Steed"), 1, Trait.Champion)
            .WithRulesTextCreator(_ => "Choose an animal companion. If you have the holy or unholy trait, your companion gains it as well, as do the companion's Strikes." +
                                       "\r\n\r\nAt the beginning of each encounter, the animal companion begins combat next to you. The animal companion can't take actions on its own but you can spend 1 action once per turn to Command an Animal. This will allow the animal companion to spend 2 actions (you will control how the animal companion spends them)." +
                                       "\r\n\r\nIf your animal companion dies, a new animal companion will come to you after your next long rest.")
            .With(ft =>
            {
                ft.Traits.RemoveAll(tr => tr == Trait.DivineAlly);
                ft.FlavorText = "You gain the services of a young animal companion who travels with you on your adventures.";
            })
            .WithPermanentQEffect(null, qf =>
            {
                qf.StartOfYourTurn = async (effect, champion) =>
                {
                    if (Ranger.GetAnimalCompanion(champion) is {} steed)
                    {
                        if (champion.HasTrait(HolyTrait.Holy))
                        {
                            steed.AddQEffect(Sanctification.SanctifiedChampion(HolyTrait.Holy));
                            steed.WithExtraTrait(HolyTrait.Holy);
                        }
                        if (champion.HasTrait(UnholyTrait.Unholy))
                        {
                            steed.AddQEffect(Sanctification.SanctifiedChampion(UnholyTrait.Unholy));
                            steed.WithExtraTrait(UnholyTrait.Unholy);
                        }
                    }
                    effect.ExpiresAt = ExpirationCondition.Immediately;
                };
            });
        yield return new TrueFeat(MFeatNames.ExpandAura, 6,
            "You focus your divine power to extend your influence and protection.",
            "Expand the radius of your champion's aura to 30 feet until the start of your next turn. At 10th level, the expansion lasts until the end of the encounter, and at 16th level it is permanent.",
            [Trait.Champion, Trait.Concentrate])
            .WithActionCost(1)
            .WithPermanentQEffect("",qf =>
            {
                Creature owner = qf.Owner;
                if (owner.Level >= 16)
                {
                    QEffect? aura = owner.FindQEffect(MQEffectIds.ChampionAura);
                    if (aura?.AssociatedAura == null)
                        return;
                    aura.AssociatedAura.MoveTo(6);
                    qf.Name = "Expand Aura";
                    qf.Description = "Your champion's aura has a radius of 30 feet.";
                    qf.Id = MQEffectIds.ExpandAura;
                    return;
                }
                qf.Description = $"Expand the radius of your champion's aura to 30 feet until {(owner.Level >= 10 ? "the end of the encounter" : "the start of your next turn")}.";
                qf.ProvideMainAction = _ =>
                {
                    CombatAction expandAura = CombatAction.CreateAction(owner, IllustrationName.CircleOfProtection,
                            "Expand Aura",
                            [Trait.Champion, Trait.Concentrate, Trait.Basic],
                            $"Expand the radius of your champion's aura to 30 feet until {(owner.Level >= 10 ? "the end of the encounter" : "the start of your next turn")}.",
                            Target.Self().WithAdditionalRestriction(self =>
                                self.HasEffect(MQEffectIds.ExpandAura) ? "Your aura is already expanded." : null), 1,
                            SfxName.AuraExpansion, null)
                        .WithEffectOnSelf(async (_, self) => self.AddQEffect(new QEffect
                        {
                            WhenYouAcquireThis = _ =>
                            {
                                QEffect? aura = self.FindQEffect(MQEffectIds.ChampionAura);
                                if (aura?.AssociatedAura == null)
                                    return;
                                aura.AssociatedAura.MoveTo(6);
                            },
                            WhenExpires = _ =>
                            {
                                QEffect? aura = self.FindQEffect(MQEffectIds.ChampionAura);
                                if (aura?.AssociatedAura == null)
                                    return;
                                aura.AssociatedAura.MoveTo(3);
                                Sfxs.Play(SfxName.AuraDismissal);
                            },
                            Id = MQEffectIds.ExpandAura
                        }.WithExpirationOneRoundOrRestOfTheEncounter(self, self.Level >= 10)));
                    
                    return new ActionPossibility(expandAura).WithPossibilityGroup("Abilities");
                };
            });
        yield return new Feat(ModManager.RegisterFeatName("RE_ExpandAuraExploration", "Maintain Expand Aura"),
            "",
            $"You gain the effects of {AllFeats.GetFeatByFeatName(MFeatNames.ExpandAura).ToLink("Expand Aura {icon:Action}")} at the start of an encounter.", 
            [ExplorationActivities.ModData.Traits.ExplorationActivity], null)
            .WithPrerequisite(MFeatNames.ExpandAura, "Expand Aura")
            .WithPrerequisite(values => values.CurrentLevel < 16, "Your champion's aura is permanently expanded.")
            .WithPermanentQEffect("You gain the effect of Expand Aura at the start of an encounter.", qf =>
            {
                Creature self = qf.Owner;
                qf.StartOfCombatReaction = _ =>
                {
                    return ReactionOption.CreateCustom("Expand Aura",
                        $"Expand the radius of your champion's aura to 30 feet until {(self.Level >= 10 ? "the end of the encounter" : "the start of your next turn")}.",
                        IllustrationName.CircleOfProtection, self, async () => self.AddQEffect(new QEffect
                        {
                            WhenYouAcquireThis = _ =>
                            {
                                QEffect? aura = self.FindQEffect(MQEffectIds.ChampionAura);
                                if (aura?.AssociatedAura == null)
                                    return;
                                aura.AssociatedAura.MoveTo(6);
                            },
                            WhenExpires = _ =>
                            {
                                QEffect? aura = self.FindQEffect(MQEffectIds.ChampionAura);
                                if (aura?.AssociatedAura == null)
                                    return;
                                aura.AssociatedAura.MoveTo(3);
                                Sfxs.Play(SfxName.AuraDismissal);
                            },
                            Id = MQEffectIds.ExpandAura
                        }.WithExpirationOneRoundOrRestOfTheEncounter(self, self.Level >= 10)));
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_Smite", "Smite"), 6,
                "You single out one enemy to destroy in your deity's name.",
                $"Designate one enemy you can see. Until the start of your next turn, your Strikes against that enemy gain a +3 status bonus to damage, increasing to +4 if you have master proficiency with the weapon or unarmed attack you're using for the Strike. If you're {HolyTooltip} or {UnholyTooltip} and the target has the opposite trait, the bonus is +4 (or +6 if you're a master).\n\n" +
                "If the target takes a hostile action against you or one of your allies before the start of your next turn, the duration extends to the end of that enemy's next turn. If the enemy continues to take these hostile actions each turn, the duration continues to extend.\n\n" +
                "Your current Smite ends if you use the Smite action again.",
                [Trait.Champion, Trait.Concentrate])
            .WithActionCost(1)
            .WithPermanentQEffect("You can empower your Strikes to gain a status bonus to damage against a creature.",
                qf =>
            {
                Creature owner = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction smite = CombatAction.CreateAction(owner, IllustrationName.SmiteEvil, "Smite",
                            [Trait.Concentrate, Trait.Basic, Trait.AlwaysHits, Trait.IsNotHostile],
                            "Designate one enemy you can see. Until the start of your next turn, your Strikes against that enemy gain a +3 status bonus to damage, increasing to +4 if you have master proficiency with the weapon or unarmed attack you're using for the Strike. If you're holy or unholy and the target has the opposite trait, the bonus is +4 (or +6 if you're a master).\n\n" +
                            "If the target takes a hostile action against you or one of your allies before the start of your next turn, the duration extends to the end of that enemy's next turn. If the enemy continues to take these hostile actions each turn, the duration continues to extend.\n\n" +
                            "Your current Smite ends if you use the Smite action again.",
                            Target.Ranged(100).WithAdditionalConditionOnTargetCreature((creature, creature1) => creature1.FindQEffect(QEffectId.SmiteEvil)?.Source == creature ? Usability.NotUsableOnThisCreature("Already smitten.") : Usability.Usable), 1, SfxName.DivineLance, null)
                        .WithTargetingTooltip((action, creature, _) =>
                        {
                            const string improvedSmite = "Designate one enemy you can see. Until the start of your next turn, your Strikes against that enemy gain a {b}+4{/b} status bonus to damage, increasing to {b}+6{/b} if you have master proficiency with the weapon or unarmed attack you're using for the Strike.\n\n" +
                                                         "If the target takes a hostile action against you or one of your allies before the start of your next turn, the duration extends to the end of that enemy's next turn. If the enemy continues to take these hostile actions each turn, the duration continues to extend.\n\n" +
                                                         "Your current Smite ends if you use the Smite action again.";
                            if (action.Owner.HasTrait(UnholyTrait.Unholy) && creature.HasTrait(HolyTrait.Holy))
                                return improvedSmite;
                            if (action.Owner.HasTrait(HolyTrait.Holy) && creature.HasTrait(UnholyTrait.Unholy))
                                return improvedSmite;
                            return action.Description.Replace(" If you're holy or unholy and the target has the opposite trait, the bonus is +4 (or +6 if you're a master).", "");
                        })
                        .WithEffectOnEachTarget(async (_, caster, target, _) =>
                        {
                            foreach (Creature creature in caster.Battle.AllCreatures.Where(cr => cr.FindQEffect(QEffectId.SmiteEvil)?.Source == caster))
                            {
                                creature.RemoveAllQEffects(qff => qff.Id == QEffectId.SmiteEvil && qff.Source == caster);
                            }
                            bool opposingValues =
                                (caster.HasTrait(UnholyTrait.Unholy) && target.HasTrait(HolyTrait.Holy)) ||
                                (caster.HasTrait(HolyTrait.Holy) && target.HasTrait(UnholyTrait.Unholy));
                            QEffect smite = new($"Smite ({caster.Name})", $"{caster.Name}'s Strikes made against this creature gain a {(opposingValues ? "{b}+4{/b}" : "+3")} status bonus to damage, increasing to {(opposingValues ? "{b}+6{/b}" : "+4")} if they have master proficiency with the weapon used.\n\n" +
                                                                          $"If this creature takes a hostile action against {caster.Name} or one of their allies before the start of their next turn, the duration extends to the end of this creature's next turn. If this creature continues to take hostile actions each turn, the duration continues to extend.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, IllustrationName.SmiteEvil)
                            {
                                Id = QEffectId.SmiteEvil,
                                AfterYouTakeActionAgainstTarget = async (effect, action, ally, _) =>
                                {
                                    if (action is { IsHostileAction: false, WillBecomeHostileAction: false } || !ally.FriendOf(caster))
                                        return;
                                    effect.ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn;
                                    effect.CannotExpireThisTurn = true;
                                }
                            };
                            smite.AddGrantingOfTechnical(cr => cr == caster, qfTech =>
                            {
                                qfTech.BonusToDamage = (effect, action, targeted) =>
                                {
                                    if (targeted != target || action.Item is not {} weapon || !action.HasTrait(Trait.Strike))
                                        return null;
                                    bool master = effect.Owner.Proficiencies.Get(weapon.Traits) >= Proficiency.Master;
                                    int amount = master && opposingValues ? 6 : master || opposingValues ? 4 : 3;
                                    return new Bonus(amount, BonusType.Status, "Smite");
                                };
                            });
                            target.AddQEffect(smite);
                        });
                    return new ActionPossibility(smite).WithPossibilityGroup("Abilities");
                };
            });
        foreach (Feat feat in LoadOathCreatures())
        {
            yield return feat;
        }
        yield return new TrueFeat(MFeatNames.BlessedCounterstrike, 12,
                "You call upon divine power to strike back at those who would dare threaten your allies.",
                "{b}Requirements{/b} An enemy triggered your champion's reaction since the end of your last turn." +
                "\n\nYou make a weapon or unarmed Strike against the enemy who triggered your champion's reaction. The Strike deals one extra weapon damage die. If this Strike hits, until the start of your next turn, the target gains weakness equal to half your level to all Strikes made by you and your allies.",
                [Trait.Champion, Trait.Flourish])
            .WithPermanentQEffect("You can make a strike against enemies that trigger your champion's reaction that deals additional damage and inflicts weakness.", 
                qf =>
                {
                    Creature champion = qf.Owner;
                    qf.ProvideStrikeModifierAsPossibilities = (_, item) =>
                    {
                        if (item.WeaponProperties == null)
                            return [];
                        StrikeModifiers blessed = new()
                        {
                            AdditionalWeaponDamageDice = 1,
                            OnEachTarget = async (self, target, result) =>
                            {
                                if (result < CheckResult.Success || target.DeathScheduledForNextStateCheck)
                                    return;
                                int amount = self.Level / 2;
                                target.AddQEffect(new QEffect
                                {
                                    StateCheck = effect =>
                                    {
                                        effect.Owner.WeaknessAndResistance.AddSpecialWeakness(new SpecialResistance($"strikes made by {self} and allies", (action, _) => action != null && action.HasTrait(Trait.Strike) && action.Owner.FriendOf(self), amount, null));
                                    }
                                }.WithExpirationAtStartOfSourcesTurn(self, 1));
                            }
                        };
                        List<CombatAction> strikes = EagleKnight.CreateStandardAndThrownStrikes(champion, item, blessed);
                        List<ActionPossibility> possibilities = [];
                        foreach (CombatAction strike in strikes)
                        {
                            strike.WithFullRename("Blessed Counterstrike");
                            strike.WithExtraTrait(Trait.Flourish);
                            strike.WithExtraTrait(Trait.Basic);
                            strike.WithDescription(StrikeRules.CreateBasicStrikeDescription2(strike.StrikeModifiers,
                                additionalSuccessText:
                                "Until the start of your next turn, the target gains weakness equal to half your level to all Strikes made by you and your allies."));
                            strike.Illustration = new SideBySideIllustration(strike.HasTrait(Trait.Thrown) ? IllustrationName.Throw : item.Illustration, MIllustrations.CreateIllustration("BlessedCounterstrike"));
                            if (strike.Target is CreatureTarget creatureTarget)
                                strike.Target = creatureTarget.WithAdditionalConditionOnTargetCreature((self, enemy) => enemy.FindQEffect(MQEffectIds.ChampionReactedAgainst)?.Source == self ? Usability.Usable : Usability.NotUsableOnThisCreature("This creature has not triggered your champion's reaction in the last turn."));
                            possibilities.Add(strike);
                        }
                        return possibilities;
                    };
                })
            .WithPrerequisite(values => values.NineCornerAlignment.IsGood(), "You must have a champion's reaction that grants an ally resistance to an enemy's damage (including the grandeur, justice, liberation, and redemption causes).");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AuraOfRighteousness", "Aura of Righteousness"), 14,
                "Your righteous aura dampens evil's might and prevents the unholy from escaping you.",
                " You and all allies in your champion's aura gain resistance 5 to unholy spells, unholy Strikes, and other unholy effects. Unholy creatures can't teleport while within your champion's aura.",
                [Trait.Champion])
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature champion = qf.Owner;
                qf.AddGrantingOfTechnical(cr => cr.FriendOf(champion) && cr.DistanceTo(champion) <= GetChampionAuraRange(champion), 
                    qfTech =>
                    {
                        qfTech.StateCheck = effect =>
                        {
                            effect.Owner.WeaknessAndResistance.AddSpecialResistance(new SpecialResistance("unholy",
                                (action, _) => action != null &&
                                                  (action.Owner.HasTrait(UnholyTrait.Unholy) ||
                                                   action.HasTrait(UnholyTrait.Unholy)), 5, null));
                        };
                    });
                qf.AddGrantingOfTechnical(
                    cr => cr.HasTrait(UnholyTrait.Unholy) && cr.DistanceTo(champion) <= GetChampionAuraRange(champion),
                    qfTech =>
                    {
                        qfTech.StateCheck += sc =>
                            sc.Owner.AddQEffect(DimensionalTravelRules.DimensionalAnchor().WithExpirationEphemeral());
                    });

            })
            .WithPrerequisite(values => values.HasFeat(MFeatNames.HolyChampion), "You must be holy.");
        yield return new Feat(MFeatNames.BlessedSwiftness,
                "Spirits grant a wind at your back and protection to your allies.",
                "You gain a +5-foot status bonus to Speed. In addition, when the movement of one of your allies triggers an enemy's reaction while the ally is in your champion's aura, the ally gains a +2 status bonus to all defenses against that reaction.",
                [Trait.DivineAlly], null)
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                qf.BonusToAllSpeeds = _ => new Bonus(1, BonusType.Status, "Blessed Swiftness");
                qf.AddGrantingOfTechnical(
                    cr => cr.FriendOfAndNotSelf(qf.Owner) && cr.DistanceTo(qf.Owner) <= GetChampionAuraRange(qf.Owner),
                    qfTech =>
                    {
                        qfTech.AddGrantingOfTechnical(cr => cr.EnemyOf(qfTech.Owner) && cr.QEffects.Any(qff => qff.WhenProvoked != null), qfInner =>
                        {
                            var applied = false;
                            qfInner.StateCheck += _ =>
                            {
                                if (applied)
                                    return;
                                foreach (QEffect opportunity in qfInner.Owner.QEffects.Where(qff => qff.WhenProvoked != null))
                                {
                                    Func<QEffect, CombatAction, Task>? provoke = opportunity.WhenProvoked;
                                    if (provoke == null)
                                        return;
                                    opportunity.WhenProvoked = Delegates.SmartCombineDelegates(
                                        async (_, provokingAction) =>
                                        {
                                            if (provokingAction.HasTrait(Trait.Move) &&
                                                provokingAction.Owner == qfTech.Owner)
                                                provokingAction.Owner.AddQEffect(
                                                    new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                                                    {
                                                        BonusToDefenses = (effect, _, _) => effect.Owner.DistanceTo(qf.Owner) <= GetChampionAuraRange(qf.Owner) ?
                                                            new Bonus(2, BonusType.Status, "Blessed Swiftness") : null,
                                                        AfterYouTakeAction = async (effect, action) =>
                                                        {
                                                            if (action != provokingAction)
                                                                return;
                                                            effect.ExpiresAt = ExpirationCondition.Immediately;
                                                        }
                                                    });
                                        }, provoke);
                                }
                                applied = true;
                            };
                        });
                    });
            });
        Spell template = AllSpells.CreateModernSpellTemplate(SpellIds.SpectralAdvance, Trait.Champion);
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_SpectralAdvance", "Spectral Advance"), 10,
            "You move to an enemy, bypassing all hindrances.",
            "You gain the {i}spectral advance{/i} devotion spell and an additional Focus Point.",
            [Trait.Champion, Trait.Concentrate, Trait.Divine, Trait.Teleportation])
            .WithRulesBlockForSpell(SpellIds.SpectralAdvance, Trait.Champion)
            .WithIllustration(template.Illustration)
            .WithOnSheet(sheet =>
            {
                sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                sheet.AddFocusSpellAndFocusPoint(Trait.Champion, Ability.Charisma, SpellIds.SpectralAdvance);
            })
            .WithPrerequisite(MFeatNames.BlessedSwiftness, "Blessed Swiftness");
        Item itemTemplate = Items.CreateNew(NewItems.AstralRune);
        RuneProperties? runeProperties = itemTemplate.RuneProperties;
        if (runeProperties != null)
        {
            yield return new Feat(
                    ModManager.RegisterFeatName("BladeAllyRunestone" + NewItems.AstralRune.ToStringOrTechnical(),
                        runeProperties.Prefix.Capitalize()), runeProperties.FlavorText,
                    $"All weapons you wield at the start of any encounter, as well as all your unarmed Strikes, will have the effect of the {runeProperties.Prefix} property rune in addition to any property runes they actually have for that encounter. This extra effect doesn't count against the number of property runes a weapon may have.\n\n{{b}}{runeProperties.Prefix.Capitalize()}:{{/b}} " +
                    runeProperties.RulesText,
                    itemTemplate.Traits.Append(Trait.BladeAllyAdvancedPropertyRune).ToList(), null)
                .WithIllustration(itemTemplate.Illustration)
                .WithLevel(itemTemplate.Level)
                .WithOnSheet(values => values.Tags["BLADE_ALLY_RUNESTONE"] = NewItems.AstralRune);
        }

        Item itemTemplate2 = Items.GetItemTemplate(ItemName.BrilliantRunestone);
        RuneProperties? runeProperties2 = itemTemplate2.RuneProperties;
        if (runeProperties2 != null)
        {
            yield return new Feat(ModManager.RegisterFeatName("BladeAllyRunestone" + ItemName.BrilliantRunestone.ToStringOrTechnical(), runeProperties2.Prefix.Capitalize()), runeProperties2.FlavorText,
                    $"All weapons you wield at the start of any encounter, as well as all your unarmed Strikes, will have the effect of the {runeProperties2.Prefix} property rune in addition to any property runes they actually have for that encounter. This extra effect doesn't count against the number of property runes a weapon may have.\n\n{{b}}{runeProperties2.Prefix.Capitalize()}:{{/b}} " + runeProperties2.RulesText,
                    itemTemplate2.Traits.Append(Trait.BladeAllyAdvancedPropertyRune).ToList(), null)
                .WithIllustration(itemTemplate2.Illustration)
                .WithLevel(itemTemplate2.Level)
                .WithOnSheet(values => values.Tags["BLADE_ALLY_RUNESTONE"] = ItemName.BrilliantRunestone);
        }
    }

    public static IEnumerable<Feat> LoadOaths()
    {
        yield return new Feat(ModManager.RegisterFeatName("RE_OathOfTheSlayer", "Oath of the Slayer"),
                "Each day, you swear an oath to defeat, topple, or destroy a certain kind of enemy during your deeds that day.",
                "During your daily preparations, choose aberrations, celestials, dragons, fiends, or undead. You can't choose celestials if you're {tooltip:holy}holy{/tooltip}, nor can you choose fiends if you're {tooltip:unholy}unholy{/tooltip}." +
                "\n\nYour Strikes and devotion spells that deal damage do an additional 1 spirit damage against a creature with the chosen trait. This damage increases to 2 at 7th level and 3 at 14th level. If a creature with the chosen trait triggers your champion's reaction, this additional damage doubles until the end of your next turn.",
                [], null)
            .WithOnSheet(values => values.AddSelectionOption(new SingleFeatSelectionOption("SlayerChoices", "Oath of the Slayer creature type", SelectionOption.MORNING_PREPARATIONS_LEVEL, feat => feat.Tag is "SlayerEnemy").WithIsOptional()));
        yield return new Feat(ModManager.RegisterFeatName("RE_OathOfTheDefender", "Oath of the Defender"),
            "Each day, you swear an oath to defend against a certain kind of enemy during your deeds that day.",
            "During your daily preparations, choose aberrations, celestials, dragons, fiends, or undead. You can't choose celestials if you're {tooltip:holy}holy{/tooltip}, nor can you choose fiends if you're {tooltip:unholy}unholy{/tooltip}." +
            "\n\nAllies in your champion's aura, not including you, get resistance 2 to damage dealt by creatures with the chosen trait. If such a creature deals more than one damage type at once, apply this resistance only to the highest amount of damage. The resistance increases to 3 at 7th level, 4 at 12th level, and 5 at 17th level. If a creature with the chosen trait triggers your champion's reaction and your champion's reaction grants resistance against the triggering damage, the resistance increases by 5.",
            [], null)
            .WithOnSheet(values => values.AddSelectionOption(new SingleFeatSelectionOption("DefenderChoices", "Oath of the Defender creature type", SelectionOption.MORNING_PREPARATIONS_LEVEL, feat => feat.Tag is "DefenderEnemy").WithIsOptional()));
        yield return new Feat(ModManager.RegisterFeatName("RE_OathOfTheAvenger", "Oath of the Avenger"),
            "You've sworn an oath to punish wicked acts you witness. You gain the edict “hunt down those who have harmed innocents or committed heinous atrocities.”",
            "When you see an enemy harm an ally, an innocent, or a noncombatant, you invoke your oath against that enemy (no action required)." +
            $"\n\nYou can use {AllSpells.CreateSpellLink(ChampionFocusSpells.LayOnHands, Trait.Champion)} to damage a creature you have invoked your oath against as if it were undead; in this case, {{i}}lay on hands{{/i}} deals spirit damage instead of vitality damage, gains the spirit trait, and loses the vitality trait.",
            [], null)
            .WithPrerequisite(values => values.FocusSpells.Any(pair => pair.Value.Spells.Any(sp => sp.SpellId == ChampionFocusSpells.LayOnHands)), "You must be able to cast {i}lay on hands{/i}.")
            .WithPrerequisite(values => values.NineCornerAlignment.IsGood(), "You must be good-aligned.")
            .WithPermanentQEffect("You can use {i}lay on hands{/i} on living creatures who you have invoked your oath against as if they were undead.", qf =>
            {
                Creature champion = qf.Owner;
                qf.AddGrantingOfTechnical(cr => cr.EnemyOf(champion) && !cr.HasTrait(Trait.Undead), qfTech =>
                {
                    qfTech.AfterYouTakeActionAgainstTarget = async (_, action, defender, _) =>
                    {
                        if (!defender.FriendOfAndNotSelf(champion) && !defender.OwningFaction.IsGaia)
                            return;
                        if (action is { IsHostileAction: false, WillBecomeHostileAction: false })
                            return;
                        action.Owner.AddQEffect(new QEffect("Oath Invoked", $"{champion.Name} has invoked their oath of vengeance against this creature and may use {{i}}lay on hands{{/i}} on them as if they were undead.", ExpirationCondition.Never, champion, IllustrationName.DivineDecree)
                        {
                            Id = MQEffectIds.InvokedOath,
                            Key = "InvokedOath"
                        });
                    };
                });
                bool acceleratingTouch =  champion.HasEffect(QEffectId.AcceleratingTouch);
                bool companionHealing = champion.HasEffect(QEffectId.CompanionHealing);
                int level = champion.Level;
                qf.ModifyActionPossibility = (_, action) =>
                {
                    if (action.SpellId != ChampionFocusSpells.LayOnHands)
                        return;
                    action.Target = TouchIncludingFriend().WithAdditionalConditionOnTargetCreature((a, d) =>
                        (!d.IsLivingCreature || !d.FriendOf(a)) && (!d.EnemyOf(a) ||
                                                                    (!d.HasTrait(Trait.Undead) && d.FindQEffect(MQEffectIds.InvokedOath)?.Source != a))
                            ? Usability.NotUsableOnThisCreature("This is not your ally, an undead, or a creature you have invoked your oath against.")
                            : Usability.Usable);
                    action.EffectOnOneTarget = async (spell, caster, target, result) =>
                    {
                        bool invokedOath = target.FindQEffect(MQEffectIds.InvokedOath)?.Source == caster;
                        if (target.IsLivingCreature && !invokedOath)
                        {
                            if (target.Damage > 0 & acceleratingTouch)
                                target.AddQEffect(new QEffect("Accelerating Touch", "You have a +10-foot bonus to Speed.", ExpirationCondition.ExpiresAtEndOfYourTurn, caster, (Illustration) IllustrationName.FleetStep)
                                {
                                    BonusToAllSpeeds = _ => new Bonus(2, BonusType.Status, "Accelerating Touch"),
                                    CannotExpireThisTurn = true
                                });
                            await target.HealAsync((spell.SpellLevel * (!companionHealing || target.FindQEffect(QEffectId.RangersCompanion)?.Source != caster ? 6 : 10)).ToString(), spell);
                            if (target == caster)
                                return;
                            target.AddQEffect(new QEffect("Lay on Hands", "You have +2 to AC.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, (Illustration) IllustrationName.LayOnHands)
                            {
                                DoNotShowUpOverhead = true,
                                BonusToDefenses = (_, _, defense) => defense != Defense.AC ? null : new Bonus(2, BonusType.Status, "Lay on Hands")
                            });
                        }
                        else
                        {
                            if (!target.HasTrait(Trait.Undead) && !invokedOath)
                                return;
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, level + "d6", !target.HasTrait(Trait.Undead) ? DamageSpirit.Spirit : DamageKind.Positive);
                            if (result > CheckResult.Failure)
                                return;
                            target.AddQEffect(new QEffect("Lay on Hands", "You have -2 to AC.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, (Illustration) IllustrationName.LayOnHands)
                            {
                                BonusToDefenses = (_, _, defense) => defense != Defense.AC ? null : new Bonus(-2, BonusType.Status, "Lay on Hands")
                            });
                        }
                    };
                };
                qf.YouBeginAction = async (_, action) =>
                {
                    if (action.SpellId != ChampionFocusSpells.LayOnHands || action.ChosenTargets.ChosenCreature?.FindQEffect(MQEffectIds.InvokedOath)?.Source != champion)
                        return;
                    action.Traits.Remove(Trait.Positive);
                    action.Traits.Add(SpiritTrait.Spirit);
                };
            });
    }
 
    public static IEnumerable<Feat> LoadOathCreatures()
    {
        List<Trait> toSlay = [Trait.Aberration, Trait.Celestial, Trait.Dragon, Trait.Fiend, Trait.Undead];
        foreach (Trait trait in toSlay)
        {
            Feat slayerEnemy = new Feat(
                    ModManager.RegisterFeatName("RE_SlayerOath" + trait, trait.HumanizeTitleCase2()),
                    $"You've swore an oath to banish or slay{(trait == Trait.Undead ? " the" : "")} {trait.HumanizeLowerCase2()}{(trait == Trait.Undead ? "" : "s")}.",
                    $"You've chosen {trait.HumanizeLowerCase2()} as your mortal foe.\n\n" +
                    $"Your Strikes and devotion spells that deal damage do an additional 1 spirit damage against a creature with the {trait.HumanizeLowerCase2()} trait. This damage increases to 2 at 7th level and 3 at 14th level. If a creature with the chosen trait triggers your champion's reaction, this additional damage doubles until the end of your next turn.",
                    [], null)
                .WithTag("SlayerEnemy")
                .WithPermanentQEffect($"You deal additional spirit damage to {(trait == Trait.Undead ? " the" : "")} {trait.HumanizeLowerCase2()}{(trait == Trait.Undead ? "" : "s")}.", qf =>
                {
                    Creature champion = qf.Owner;
                    int amount = champion.Level >= 14 ? 3 : champion.Level >= 7 ? 2 : 1;
                    qf.Name = "Oath of the Slayer";
                    qf.AddExtraKindedDamageOnStrike = (_, creature) =>
                    {
                        if (!creature.HasTrait(trait))
                            return null;
                        var dice = amount.ToString();
                        if (creature.FindQEffect(MQEffectIds.ChampionReactedAgainst)?.Source == champion)
                        {
                            dice = (amount * 2).ToString();
                        }
                        return new KindedDamage(DiceFormula.FromText(dice, "Oath of the Slayer"), DamageSpirit.Spirit);
                    };
                    qf.YouDealDamageEvent = async (_, damage) =>
                    {
                        if (!damage.TargetCreature.HasTrait(trait) || damage.CombatAction is not {} devotion || !devotion.HasTrait(Trait.Spell) || !devotion.HasTrait(Trait.Champion) || devotion.HasTrait(Trait.Strike))
                            return;
                        var dice = amount.ToString();
                        if (damage.TargetCreature.FindQEffect(MQEffectIds.ChampionReactedAgainst)?.Source == champion)
                        {
                            dice = (amount * 2).ToString();
                        }
                        damage.KindedDamages.Add(new KindedDamage(DiceFormula.FromText(dice, "Oath of the Slayer"),
                            DamageSpirit.Spirit));
                    };
                });
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (trait)
            {
                case Trait.Fiend:
                    slayerEnemy.WithPrerequisite(values => !values.HasFeat(MFeatNames.UnholyChampion),
                        "You can't choose fiends because you are unholy.");
                    break;
                case Trait.Celestial:
                    slayerEnemy.WithPrerequisite(values => !values.HasFeat(MFeatNames.HolyChampion),
                        "You can't choose celestials because you are holy.");
                    break;
            }
            yield return slayerEnemy;
            
            Feat defenderEnemy = new Feat(
                    ModManager.RegisterFeatName("RE_DefenderOath" + trait, trait.HumanizeTitleCase2()),
                    $"You've swore an oath to banish or slay{(trait == Trait.Undead ? " the" : "")} {trait.HumanizeLowerCase2()}{(trait == Trait.Undead ? "" : "s")}.",
                    $"You've chosen {trait.HumanizeLowerCase2()} as your mortal foe.\n\n" +
                    $"Allies in your champion's aura, not including you, get resistance 2 to damage dealt by creatures with the {trait.HumanizeLowerCase2()} trait. If such a creature deals more than one damage type at once, apply this resistance only to the highest amount of damage. The resistance increases to 3 at 7th level, 4 at 12th level, and 5 at 17th level. If a creature with the {trait.HumanizeLowerCase2()} trait triggers your champion's reaction and your champion's reaction grants resistance against the triggering damage, the resistance increases by 5.",
                    [], null)
                .WithTag("DefenderEnemy")
                .WithPermanentQEffect($"Allies in your champion aura gain resistance to attacks made by {(trait == Trait.Undead ? " the" : "")} {trait.HumanizeLowerCase2()}{(trait == Trait.Undead ? "" : "s")}.", qf =>
                {
                    Creature champion = qf.Owner;
                    int amount = champion.Level >= 17 ? 5 : champion.Level >= 12 ? 4 : champion.Level >= 7 ? 3 : 2;
                    qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(champion) && cr.DistanceTo(champion) <= GetChampionAuraRange(champion), qfTech =>
                    {
                        qfTech.StateCheck = effect =>
                        {
                            effect.Owner.WeaknessAndResistance.AddSpecialResistance(new SpecialResistance($"{trait.HumanizeLowerCase2()}", (_, _) => false, amount, null));
                        };
                        qfTech.Id = MQEffectIds.DefendedAgainst;
                        qfTech.Tag = trait;
                        qfTech.YouAreDealtDamageEvent = async (effect, damage) =>
                        {
                            if (!damage.Source.HasTrait(trait))
                                return;
                            Creature owner = effect.Owner;
                            CombatAction? cAction = damage.CombatAction;
                            KindedDamage? highestDamage = damage.KindedDamages.MaxBy(kd => kd.OriginalResolvedDamage);
                            if (highestDamage == null)
                                return;
                            int appliedWeakness = owner.WeaknessAndResistance.GetAndApplyResistance(cAction, highestDamage, highestDamage.OriginalResolvedDamage, damage);
                            if (appliedWeakness > amount)
                                return;
                            int num = amount - appliedWeakness;
                            highestDamage.ResolvedDamage -= Math.Min(highestDamage.ResolvedDamage, num);
                            damage.DamageEventDescription.AppendLine($"{{b}}-{num.ToString()}{{/b}} Oath of the Defender");
                        };
                    });
                });
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (trait)
            {
                case Trait.Fiend:
                    defenderEnemy.WithPrerequisite(values => !values.HasFeat(MFeatNames.UnholyChampion),
                        "You can't choose fiends because you are unholy.");
                    break;
                case Trait.Celestial:
                    defenderEnemy.WithPrerequisite(values => !values.HasFeat(MFeatNames.HolyChampion),
                        "You can't choose celestials because you are holy.");
                    break;
            }
            yield return defenderEnemy;
        }
    }

    public static QEffect ChampionAura()
    {
        return new QEffect("Champion's Aura", AuraDescription)
        {
            Id = MQEffectIds.ChampionAura,
            SpawnsAura = effect => new MagicCircleAuraAnimation(IllustrationName.AngelicHaloCircleWhite,
                effect.Owner.PersistentCharacterSheet?.Calculated.NineCornerAlignment.IsGood() ?? true
                    ? Color.DarkGoldenrod
                    : Color.Crimson, 3)
        };
    }
    public static int GetChampionAuraRange(Creature champion)
    {
        return champion.HasEffect(MQEffectIds.ExpandAura) ? 6 : 3;
    }

    public static CreatureTarget TouchIncludingFriend()
    {
        return new CreatureTarget(RangeKind.Melee, new NaturalReachCreatureTargetingRequirement(),
            (_, _, _) => int.MinValue);
    }
}