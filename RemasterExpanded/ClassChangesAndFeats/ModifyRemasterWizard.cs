using System;
using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Mods.Remaster.FeatsDb;
using Dawnsbury.Mods.Remaster.Spellbook;
using RemasterExpanded.MySpells;
using static PsychicExpanded.ModData;
using NewSpells = SpellsAndSpellhearts.NewSpells;

namespace RemasterExpanded;

public abstract class ModifyRemasterWizard
{

    public static IEnumerable<Feat> LoadRemasterWizardFeats()
    {
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AdvancedSchoolSpell", "Advanced School Spell"), 8, 
            "You gain access to a powerful new school spell depending on your arcane school.", 
            $"You gain 1 focus point, up to a maximum of 3, and you gain a spell based on your arcane school:\r\n• Ars Grammatica — {AllSpells.CreateSpellLink(SpellId.GuidedStrike, Trait.Wizard)}\r\n• Battle Magic — {AllSpells.CreateSpellLink(SpellId.EnergyAbsorption, Trait.Wizard)}\r\n• Civic Wizardry — {AllSpells.CreateSpellLink(SpellIds.CommunityRestoration, Trait.Wizard)}\r\n• Mentalism — {AllSpells.CreateSpellLink(SpellId.InvisibilityCloak, Trait.Wizard)}\r\n• Protean Form — {AllSpells.CreateSpellLink(SpellId.ShiftingForm, Trait.Wizard)}\r\n• Boundary — {AllSpells.CreateSpellLink(SpellIds.SpiralOfHorrors, Trait.Wizard)}\r\n• Unified Magical Theory — {AllSpells.CreateSpellLink(SpellIds.InterdisciplinaryIncantation, Trait.Wizard)}",
            [Trait.Wizard]).WithPrerequisite(values => values.WizardSchool != 0, "You must have an arcane school.")
            .WithOnSheet(values =>
        {
            SpellId spellId1;
            if (values.WizardSchool == RemasterFeats.Trait.ProteanForm)
                spellId1 = SpellId.ShiftingForm;
            else if (values.WizardSchool == RemasterFeats.Trait.TheBoundary)
                spellId1 = SpellIds.SpiralOfHorrors;
            else if (values.WizardSchool == RemasterFeats.Trait.ArsGrammatica)
                spellId1 = SpellId.GuidedStrike;
            else if (values.WizardSchool == RemasterFeats.Trait.Mentalism)
                spellId1 = SpellId.InvisibilityCloak;
            else if (values.WizardSchool == RemasterFeats.Trait.BattleMagic)
                spellId1 = SpellId.EnergyAbsorption;
            else if (values.WizardSchool == RemasterFeats.Trait.CivicWizardry)
                spellId1 = SpellIds.CommunityRestoration;
            else if (values.WizardSchool == ModData.MTraits.UnifiedMagicalTheory)
                spellId1 = SpellIds.InterdisciplinaryIncantation;
            else
                spellId1 = SpellId.EnergyAbsorption;
            SpellId spellId2 = spellId1;
            values.AddFocusSpellAndFocusPoint(Trait.Wizard, Ability.Intelligence, spellId2);
        });
    }
    public static void PatchWizard()
    {
        ModManager.RegisterActionOnEachSpell(AddCurriculumTraits);
        List<Feat>? subfeats = AllFeats.GetFeatByFeatName(FeatName.Wizard).Subfeats?.Where(feat => feat.FeatName is not FeatName.UniversalistSchool).ToList();
        if (subfeats != null)
            foreach (Feat feat in subfeats)
            {
                Trait schoolTrait = FeatToTrait(feat);
                if (schoolTrait == Trait.None) continue;
                SpellId[][] spellOptions = CurriculaSpellOptions[schoolTrait];
                feat.RulesText = feat.RulesText.Remove(feat.RulesText.IndexOf("cantrips", StringComparison.Ordinal));
                feat.RulesText = feat.RulesText.TrimEnd();
                for (var spellRank = 0; spellRank < spellOptions.Length; spellRank++)
                {
                    string rankDescription = spellRank switch
                    {
                        0 => "cantrips",
                        1 => "1st",
                        2 => "2nd",
                        3 => "3rd",
                        _ => spellRank + "th",
                    };
                    IEnumerable<string> validSpellEntries = spellOptions[spellRank].Where(spellId => spellId != SpellId.None).Select(spellId => AllSpells.CreateModernSpellTemplate(spellId, Trait.Wizard).ToSpellLink());
                    List<string> spellEntries = validSpellEntries.ToList();
                    if (spellEntries.Count != 0)
                    {
                        feat.RulesText += "\n" + rankDescription + ": " + string.Join(", ", spellEntries);
                    }
                }
                feat.Illustration = TraitToIllustration(schoolTrait);
                feat.OnSheet += sheet =>
                {
                    string schoolTraitName = TraitExtensions.TraitProperties[schoolTrait].HumanizedName;
                    sheet.AddAtLevel(9, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(5, "Wizard:SchoolSpell5:" + schoolTraitName, schoolTrait, schoolTraitName)));
                    sheet.AddAtLevel(11, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(6, "Wizard:SchoolSpell6:" + schoolTraitName, schoolTrait, schoolTraitName)));
                    sheet.AddAtLevel(13, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(7, "Wizard:SchoolSpell7:" + schoolTraitName, schoolTrait, schoolTraitName)));
                    sheet.AddAtLevel(15, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(8, "Wizard:SchoolSpell8:" + schoolTraitName, schoolTrait, schoolTraitName)));
                    sheet.AddAtLevel(17, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(9, "Wizard:SchoolSpell9:" + schoolTraitName, schoolTrait, schoolTraitName)));
                    sheet.AddAtLevel(19, laterValues => laterValues.PreparedSpells.GetValueOrDefault(Trait.Wizard)?.Slots.Add(new Wizard.CurriculumPreparedSpellSlot(10, "Wizard:SchoolSpell10:" + schoolTraitName, schoolTrait, schoolTraitName)));
                };
                if (feat.FeatName == RemasterFeats.FeatName.ProteanForm)
                    feat.FlavorText =
                        "The uninitiated often think of wizards as cerebral, focused on their studies more than the body, yet your school of magic taught of the relationship between the two. Your magic, whether learned at a storied institution like the Alabaster Academy or someplace more sinister, like the Fleshforges, focuses on the ways that living matter can be convinced into another shape for a time, allowing you to polymorph a seed into a vine, a human into a beast, or a harmless germ into a deadly toxin.";
            }
        if (AllSpells.All.FirstOrDefault(sp => sp.SpellId == RemasterSpells.GetSpellIdByName("ScatterScree")) is
            { } scatter)
            scatter.MinimumSpellLevel = 0;
        if (AllFeats.GetFeatByFeatName(FeatName.UniversalistSchool) is not { } universal) return;
        universal.OnSheet += values =>
        {
            values.WizardSchool = ModData.MTraits.UnifiedMagicalTheory;
        };
        universal.WithCustomName("School of Unified Magical Theory");
        universal.FlavorText =
            "You eschew the idea that magic can be neatly expressed by the teachings of any single school or college, instead directing your self-study to pick up the best of every school of magic. In doing so, you'll find the truths that lie at the intersection of each school, coming closer to the ideal nature of arcane magic. One day, you'll uncover that single elegant theory detailing all magic (perhaps a theory bearing your name?), but until then, your studies continue.";
        universal.Illustration = ModData.MIllustrations.CreateIllustration("Incantation");
    }

    private static void AddCurriculumTraits(CombatAction spellAction)
    {
        List<Trait> schoolTraits = (from entry in CurriculaSpellOptions where entry.Value.Any(spellIds => spellIds.Any(spellId => spellId == spellAction.SpellId)) select entry.Key).ToList();
        if (schoolTraits.Count == 0) return;
        spellAction.Traits.AddRange(schoolTraits);
    }

    private static readonly Dictionary<Trait, SpellId[][]> CurriculaSpellOptions = new()
    {
        {
            RemasterFeats.Trait.ArsGrammatica,
            [
                [MSpellIds.Message, SpellId.Daze],
                [SpellId.Command, RemasterFeats.GetSpellIdByName("RunicBody"), SpellId.MagicWeapon],
                [SpellId.LooseTimesArrow, SpellId.MirrorImage],
                [SpellId.DeflectCriticalHit, SpellId.SeaOfThought],
                [SpellId.SpellImmunity, SpellId.ReboundingBarrier],
                [SpellId.QuickenTime, SpellId.StagnateTime]
            ]
        },
        {
            RemasterFeats.Trait.BattleMagic,
            [
                [SpellId.Shield, SpellId.TelekineticProjectile],
                [SpellId.BurningHands, SpellId.MagicMissile, SpellId.MageArmor],
                [SpellId.ObscuringMist, SpellId.ResistEnergy],
                [SpellId.CrashingWave, SpellId.Fireball],
                [SpellId.WallOfFire, SpellIds.WeaponStorm],
                [SpellIds.HowlingBlizzard, SpellIds.ImpalingSpike]
            ]
        },
        {
            RemasterFeats.Trait.CivicWizardry,
            [
                [SpellId.WarpStep, SpellId.TelekineticProjectile],
                [SpellId.HydraulicPush, SpellId.PummelingRubble, SpellIds.SummonConstruct],
                [SpellId.WaterWalk, RemasterFeats.GetSpellIdByName("RevealingLight")],
                [SpellId.Haste, NewSpells.CaveFangs], 
                [SpellId.FreedomOfMovement, SpellId.Fly],
                [SpellId.Geyser, SpellId.QuickenTime]
            ]
        },
        {
            RemasterFeats.Trait.Mentalism,
            [
                [SpellId.Daze, SpellIds.Figment],
                [SpellId.Fear, RemasterFeats.GetSpellIdByName("DizzyingColors"), RemasterFeats.GetSpellIdByName("SureStrike")],
                [RemasterSpells.GetSpellIdByName("LaughingFit"), RemasterFeats.GetSpellIdByName("Stupefy")],
                [SpellId.RoaringApplause, SpellId.PhantomPrison],
                [SpellId.Sleep, SpellIds.VisionOfDeath],
                [SpellId.CrushingDespair, SpellIds.ShockAndAwe]
            ]
        },
        {
            RemasterFeats.Trait.ProteanForm,
            [
                [RemasterFeats.GetSpellIdByName("TangleVine"), RemasterFeats.GetSpellIdByName("GougingClaw")],
                [SpellId.Jump, SpellId.InsectForm, RemasterFeats.GetSpellIdByName("SpiderSting")],
                [RemasterSpells.GetSpellIdByName("OakenResilience"), SpellIds.Hidebound],
                [SpellId.VampiricTouch, SpellIds.CroakVoice],
                [SpellId.Stoneskin, NewSpells.MercurialStride],
                [SpellId.ElementalForm, SpellId.Cloudkill]
            ]
        },
        {
            RemasterFeats.Trait.TheBoundary,
            [
                [SpellId.OpenDoor, RemasterFeats.GetSpellIdByName("VoidWarp")],
                [RemasterFeats.GetSpellIdByName("Enfeeble"), SpellId.GrimTendrils, SpellId.AnimateDead], 
                [RemasterSpells.GetSpellIdByName("FalseVitality"), RemasterFeats.GetSpellIdByName("SeeTheUnseen")], 
                [SpellId.BindUndead, SpellId.GhostlyWeapon],
                [SpellId.Blink, SpellId.DimensionDoor],
                [SpellId.Banishment, SpellIds.InvokeSpirits]
            ]
        }
    };

    public static Trait FeatToTrait(Feat feat)
    {
        if (feat.FeatName == RemasterFeats.FeatName.ArsGrammatica)
            return RemasterFeats.Trait.ArsGrammatica;
        if (feat.FeatName == RemasterFeats.FeatName.BattleMagic)
            return RemasterFeats.Trait.BattleMagic;
        if (feat.FeatName == RemasterFeats.FeatName.CivicWizardry)
            return RemasterFeats.Trait.CivicWizardry;
        if (feat.FeatName == RemasterFeats.FeatName.Mentalism)
            return RemasterFeats.Trait.Mentalism;
        if (feat.FeatName == RemasterFeats.FeatName.ProteanForm)
            return RemasterFeats.Trait.ProteanForm;
        return feat.FeatName == RemasterFeats.FeatName.TheBoundary ? RemasterFeats.Trait.TheBoundary : Trait.None;
    }

    public static Illustration TraitToIllustration(Trait trait)
    {
        if (trait == RemasterFeats.Trait.ArsGrammatica)
            return IllustrationName.SpellResistance;
        if (trait == RemasterFeats.Trait.BattleMagic)
            return IllustrationName.MagicMissile;
        if (trait == RemasterFeats.Trait.CivicWizardry)
            return IllustrationName.Tremor;
        if (trait == RemasterFeats.Trait.Mentalism)
            return IllustrationName.Fear;
        if (trait == RemasterFeats.Trait.TheBoundary)
            return IllustrationName.Enervation;
        return trait == RemasterFeats.Trait.ProteanForm ? IllustrationName.WildShape : IllustrationName.None;
    }
}