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
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Controls.Statblocks;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using HarmonyLib;
using RemasterExpanded.ClassChangesAndFeats;
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
        ModManager.RegisterBooleanSettingsOption("HideLegacyFeats", "Remaster Expanded: Hide Legacy Feats", "Feats which have been replaced in the remaster (and which this mod includes the updated version of) will be hidden. {b}NOTE:{/b} You must restart the game for this to take place.", false);
        ModManager.RegisterBooleanSettingsOption("RE_UseDefenseBlock", "Remaster Expanded - Use Defense Block", "With this option enabled, certain feats will add their ability description into the defense section of the stat block instead of the abilities section.", false);
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
        ChampionRemaster.Load();
        Sanctification.ModifyChampionCleric();
        MagusRemaster.Load();
        // FighterFeats.Load();
        UpdateItems.Load();
        FeatLoader.ModifyFeats();
        if (ModManager.TryParse("DawnniEx", out Trait _))
        {
            ModManager.RegisterBooleanSettingsOption("RemoveDawnniFeats", "Remaster Expanded: Remove Obsolete Dawnni Content", "This mod option removes Dawnni Expanded's archetype feats and Familiar feats and mutagens already added by the base game. {b}NOTE:{/b} You must restart the game for this to take place.", false);
            if (PlayerProfile.Instance.IsBooleanOptionEnabled("RemoveDawnniFeats"))
                DawnniPatch.LoadDawnniPatch();
            DawnniPatch.LoadDawnniMedicineFix();
        }
        if (PlayerProfile.Instance.IsBooleanOptionEnabled("HideDuplicateWardenSpells"))
        {
            RangerFeats.HideWardenFeats();
        }

        if (RemasterSpells && SpellHearts)
        {
            NewSpellhearts.Load();
        }
        else
        {
            if (!SpellHearts)
                GeneralLog.Log("Spellhearts not loaded.");
            if (!RemasterSpells)
                GeneralLog.Log("Remaster Spells not loaded.");
        }
        LoadOrder.AtEndOfLoadingSequence += () =>
        {
            foreach (DeitySelectionFeat deitySelectionFeat in AllFeats.All.OfType<DeitySelectionFeat>())
            {
                deitySelectionFeat.OnSheet = null;
                IEnumerable<SpellId>? extraSpells = deitySelectionFeat.GrantedSpells;
                Skill? divineSkill = deitySelectionFeat.DivineSkill;
                deitySelectionFeat.WithOnSheet(values =>
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
                    if (trait is Trait.Cleric or Trait.Champion || 
                        (ModManager.TryParse("Avenger", out Trait avenger) && values.AdditionalClassTraits.Any(tr => tr == avenger)) || 
                        (ModManager.TryParse("Vindicator", out Trait vindicator) && values.AdditionalClassTraits.Any(tr => tr == vindicator)))
                    {
                        Trait mainTrait = Items.CreateNew(deitySelectionFeat.FavoredWeapon).MainTrait;
                        switch (mainTrait)
                        {
                            case Trait.None:
                                throw new Exception("This favored weapon does not have a main trait.");
                            case Trait.SteelShield:
                                values.Proficiencies.Set(Trait.Shield, Proficiency.Trained);
                                break;
                            case Trait.Shortbow:
                                values.Proficiencies.Set(Trait.CompositeShortbow, Proficiency.Trained);
                                break;
                        }
                        values.Proficiencies.Set(mainTrait, Proficiency.Trained);
                    }
                    if (!values.AdditionalClassTraits.Any(tr => tr is Trait.Cleric or Trait.Champion ||
                                                                (ModManager.TryParse("Avenger",
                                                                    out Trait avengerTrait) && tr == avengerTrait)) && trait != Trait.Cleric && trait != Trait.Champion)
                        return;
                    values.TrainInThisOrSubstitute(divineSkill.Value);
                });
            }
            foreach (Feat feat in AllFeats.All.Where(ft => ft is ClassSelectionFeat { ClassTrait: not Trait.Cleric and not Trait.Champion }))
            {
                feat.OnSheet += values =>
                {
                    if (values.Deity == null)
                        values.AddSelectionOptionRightNow(new SingleFeatSelectionOption("RE_DeitySelection", "Deity",
                            -1, ft => ft is DeitySelectionFeat).WithIsOptional());
                };
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