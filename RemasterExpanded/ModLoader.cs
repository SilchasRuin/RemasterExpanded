using System.Collections.Generic;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Specific;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using HarmonyLib;
using RemasterExpanded.MyArchetypes;
using RemasterExpanded.MySpells;
using static RemasterExpanded.ModData;
using NewSpells = RemasterExpanded.MySpells.NewSpells;

namespace RemasterExpanded;

public class ModLoader
{
    [DawnsburyDaysModMainMethod]
    public static void LoadMod()
    {
        RemasterLore.Load();
        NewItems.LoadItems();
        ModManager.RegisterBooleanSettingsOption("RE_CritChange", "Remaster Expanded: Remaster Flail and Hammer Critical Specialization", "Changes flails and hammers to use their remastered critical specializations, which requires a saving throw against your Class DC.", false);
        ModManager.RegisterBooleanSettingsOption("RE_AlchemicalOrganization", "Remaster Expanded: Organize Alchemical Items", "Organizes alchemical items into item groups according to their type. {b}NOTE:{/b} You must restart the game for this to take place.", true);
        ModManager.RegisterBooleanSettingsOption("RemasterCantrips", "Remaster Expanded: Enforce Remaster Rule Cantrip Damage for Divine Lance", "If this is enabled, Divine Lance will use remaster damage baseline for divine lance (base damage 2d4), if disabled it will use pre-remaster baseline (base damage 1d4 + spellcasting ability).", Remaster);
        ModManager.RegisterBooleanSettingsOption("HideDuplicateWardenSpells", "Remaster Expanded: Hide Duplicate Warden Spells", "Hides the base game feats that are duplicated in the Initiate Warden feat line. {b}NOTE:{/b} You must restart the game for this to take place.", false);
        Harmony harmony = new("critSpecChange");
        harmony.PatchAll();
        Inkdrop.AddInkdrop();
        NewSpells.LoadSpells();
        TangibleDreamFigment.LoadFigment();
        if (RemasterSpells && LoadPsychic)
        {
            OscillatingWaveRemaster.RemasterOscillatingWave();
        }
        NewDeities.LoadDomains();
        foreach (Feat feat in FeatLoader.LoadFeats())
        {
            ModManager.AddFeat(feat);
        }
        if (Remaster && MoreSpells)
        {
            foreach (Feat feat in ModifyRemasterWizard.LoadRemasterWizardFeats())
            {
                ModManager.AddFeat(feat);
            }
            if (AllFeats.GetFeatByFeatName(FeatName.AdvancedSchoolSpell) is TrueFeat advancedFeat)
            {
                advancedFeat.Traits.Clear();
            }
        }
        SorcerousPotency.Load();
        ModifyGunslinger.Load();
        Sanctification.ModifyChampionCleric();
        UpdateItems.Load();
        if (ModManager.TryParse("DawnniEx", out Trait _))
        {
            ModManager.RegisterBooleanSettingsOption("RemoveDawnniFeats", "Remaster Expanded: Remove Obsolete Dawnni Content", "This mod option removes Dawnni Expanded's archetype feats and Familiar feats and mutagens already added by the base game. {b}NOTE:{/b} You must restart the game for this to take place.", false);
            if (PlayerProfile.Instance.IsBooleanOptionEnabled("RemoveDawnniFeats"))
                DawnniPatch.LoadDawnniPatch();
        }
        if (PlayerProfile.Instance.IsBooleanOptionEnabled("HideDuplicateWardenSpells"))
        {
            RangerFeats.HideWardenFeats();
        }
        LoadOrder.AtEndOfLoadingSequence += () =>
        {
            foreach (DeitySelectionFeat? deitySelectionFeat in AllFeats.All.OfType<DeitySelectionFeat>())
            {
                deitySelectionFeat?.OnSheet = null;
                IEnumerable<SpellId>? extraSpells = deitySelectionFeat?.GrantedSpells;
                Skill? divineSkill = deitySelectionFeat?.DivineSkill;
                deitySelectionFeat?.WithOnSheet(values =>
                {
                    ClassSelectionFeat? classSelectionFeat = values.Class;
                    Trait trait = classSelectionFeat?.ClassTrait ?? Trait.None;
                    if (trait == Trait.Cleric)
                    {
                        if (values.PreparedSpells.TryGetValue(Trait.Cleric, out PreparedSpellSlots? preparedSpellSlots))
                            preparedSpellSlots.AdditionalPreparableSpells.AddRange(extraSpells ?? throw new InvalidOperationException());
                        if (deitySelectionFeat.AllowedFonts.Length == 1)
                            values.GrantFeat(deitySelectionFeat.AllowedFonts[0]);
                        else
                            values.AddSelectionOption(new SingleFeatSelectionOption("Font", "Divine font", 1, ft => ft.HasTrait(Trait.DivineFont)));
                    }
                    if (trait is Trait.Cleric or Trait.Champion)
                    {
                        Trait mainTrait = Items.CreateNew(deitySelectionFeat.FavoredWeapon).MainTrait;
                        if (mainTrait == Trait.None)
                            throw new Exception("This favored weapon does not have a main trait.");
                        if (mainTrait == Trait.SteelShield)
                            values.Proficiencies.Set(Trait.Shield, Proficiency.Trained);
                        if (mainTrait == Trait.Shortbow)
                            values.Proficiencies.Set(Trait.CompositeShortbow, Proficiency.Trained);
                        values.Proficiencies.Set(mainTrait, Proficiency.Trained);
                    }
                    if (!divineSkill.HasValue ||
                        (!values.AdditionalClassTraits.Any(tr => tr is Trait.Cleric or Trait.Champion) && trait != Trait.Cleric && trait != Trait.Champion))
                        return;
                    values.TrainInThisOrSubstitute(divineSkill.Value);
                });
            }
            foreach (Feat feat in AllFeats.All.Where(ft => ft is ClassSelectionFeat { ClassTrait: not Trait.Cleric and not Trait.Champion }))
            {
                feat.OnSheet += values =>
                    values.AddSelectionOption(new SingleFeatSelectionOption("RE_DeitySelection", "Deity", -1,
                        ft => ft is DeitySelectionFeat).WithIsOptional());
            }
            
            Item withRunes = Items.CreateNew(NewItems.AutoloadLeathers).WithModificationRune(ItemName.ArmorPotencyRunestone)
                .WithModificationRune(ItemName.ResilientRunestone);
            Items.ShopItems.Add(withRunes);
            if (Remaster && MoreSpells)
            {
                ModifyRemasterWizard.PatchWizard();
            }
            Feat universal = AllFeats.GetFeatByFeatName(FeatName.UniversalistSchool);
            universal.OnSheet += values =>
            {
                values.GrantFeat(FeatName.HandOfTheApprentice);
            };
            universal.RulesText += $"\n\nYou gain the {AllSpells.CreateSpellLink(SpellId.HandOfTheApprentice, Trait.Wizard)} focus school spell and you gain a focus pool of 1 focus point that recharges after every encounter.";
            Feat sorcerer = AllFeats.GetFeatByFeatName(FeatName.Sorcerer);
            sorcerer.OnSheet +=  values => values.GrantFeat(FeatName.DangerousSorcery);
            if (ModManager.TryParse("DawnniEx", out Trait _))
            {
                AllFeats.All.RemoveAll(ft => ft.FeatName == FeatName.BattleMedicine);
                Feat medicine = new TrueFeat(FeatName.BattleMedicine, 1, "You can patch up wounds, even in combat.",
                        $"{{b}}Range{{/b}} touch\r\n{{b}}Requirements{{/b}} You must have a hand free.\r\n\r\nMake a Medicine check against DC 15.{S.FourDegreesOfSuccess("The target regains 4d8 HP.", "The target regains 2d8 HP.", null, "The target takes 1d8 damage.")}\n\nRegardless of your result, the target is then temporarily immune to your Battle Medicine for the rest of the day.\n\nIf you're expert in Medicine, you can choose to make the check against DC 20. If you do, you heal 2d8+10 HP on a success instead (4d8+10 HP on a critical success).\n\nIf you're master, you can choose DC 30 for 2d8+30 HP (4d8+30 on a critical success), and if you're legendary, you can choose DC 40 for 2d8+50 HP (4d8+50 on a critical success).",
                        [Trait.General, Trait.Healing, Trait.Manipulate, Trait.Skill]).WithActionCost(1)
                    .WithPrerequisite(values => values.GetProficiency(Trait.Medicine) >= Proficiency.Trained,
                        "You must be trained in Medicine.")
                    .WithPermanentQEffect("You can heal allies as an 'other action'.", qf =>
                        qf.ProvideActionIntoPossibilitySection = (qfBattleMedicine, section) =>
                        {
                            if (section.PossibilitySectionId != PossibilitySectionId.OtherManeuvers)
                                return null;
                            Creature owner = qfBattleMedicine.Owner;
                            if (owner.PersistentCharacterSheet != null)
                            {
                                Proficiency proficiency =
                                    owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Medicine);
                                if (proficiency >= Proficiency.Expert)
                                {
                                    var possibilityList = new List<Possibility>
                                    {
                                        (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(
                                            qfBattleMedicine.Owner, Proficiency.Trained),
                                        (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                            Proficiency.Expert)
                                    };
                                    if (proficiency >= Proficiency.Master)
                                        possibilityList.Add(
                                            (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                                Proficiency.Master));
                                    if (proficiency >= Proficiency.Legendary)
                                        possibilityList.Add(
                                            (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                                Proficiency.Legendary));
                                    return new SubmenuPossibility(IllustrationName.HealersTools, "Battle Medicine")
                                    {
                                        Subsections =
                                        {
                                            new PossibilitySection("Battle Medicine")
                                            {
                                                Possibilities = possibilityList
                                            }
                                        }
                                    };
                                }
                            }

                            return new ActionPossibility(
                                BattleMedicine.CreateBattleMedicineAction(owner, Proficiency.Trained));
                        });
                ModManager.AddFeat(medicine);
                if (PlayerProfile.Instance.IsBooleanOptionEnabled("RemoveDawnniFeats"))
                    DawnniPatch.LoadDawnniPatch2();
                if (ModManager.TryParse("SympatheticStrike", out Trait _))
                    DawnniPatch.LoadDawnniPatch3();
            }
            if (PlayerProfile.Instance.IsBooleanOptionEnabled("RE_AlchemicalOrganization"))
                AlchemicalOrganization.LoadOrganization();
            
        };
    }
}