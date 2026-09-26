using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes.Multiclass;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Display;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using static RemasterExpanded.ModData;
using static RemasterExpanded.OracleRemastered.OracleClass;

namespace RemasterExpanded.OracleRemastered;

public class OracleArchetype
{
    public static IEnumerable<Feat> OracleArchetypeFeats()
    {
        Feat dedication = ArchetypeFeats.CreateMulticlassDedication(MTraits.RemasterOracle,
                "A mysterious force granted you divine magic and a curse.",
                "Choose a mystery. You become trained in Religion and the mystery's skill; if you were already trained, you become trained in a skill of your choice. You gain the curse associated with your mystery, which follows the normal rules for an oracular curse." +
                "\n\nYou cast spells like an oracle and gain the Cast a Spell activity. You gain a spell repertoire with two cantrips, either common divine cantrips or other divine cantrips you learn or discover. You’re trained in the spell attack modifier and spell DC statistics. Your key spellcasting attribute for oracle archetype spells is Charisma, and they are divine oracle spells.", 
                [.. OracleMysteries()])
            .WithEquivalent(values => values.HasFeat(FeatName.Oracle))
            .WithOnSheet(sheet =>
            {
                sheet.TrainInThisOrSubstitute(Skill.Religion);
                sheet.SpellTraditionsKnown.Add(Trait.Divine);
                sheet.SpellRepertoires.Add(MTraits.RemasterOracle, new SpellRepertoire(Ability.Charisma, Trait.Divine));
                sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                sheet.AddSelectionOption(new AddToSpellRepertoireOption("RemasterOracleCantrips", "Oracle cantrips", sheet.CurrentLevel, MTraits.RemasterOracle, Trait.Divine, 0, 2));
            })
            .WithPrerequisite(values => !values.HasFeat(FeatName.Oracle), $"You must not be {Trait.Oracle.HumanizeLowerCase2().WithIndefiniteArticle()}.")
            .WithPrerequisite(values => !values.HasFeat(AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Oracle.ToStringOrTechnical() + "Dedication")?.FeatName ?? FeatName.Oracle), $"You cannot select {Trait.Oracle.HumanizeLowerCase2().WithIndefiniteArticle()} dedication twice.")
            .WithOnCreature((sheet, self) =>
            {
                self.AddQEffect(OracleCurseFunction(sheet, self));
            })
            .WithCustomName(PlayerProfile.Instance.IsBooleanOptionEnabled("RE_RemoveLegacyOracle") ? "Oracle Dedication" :"Oracle Dedication (Remastered)");
        dedication.Traits.Add(MTraits.VisualOracle);
        yield return dedication;
        foreach (Feat feat in MulticlassArchetypeFeats.CreateSpellcastingFeats(MTraits.RemasterOracle, Trait.Spontaneous, "Mysterious", dedication.FeatName))
        {
            feat.Traits.Add(MTraits.VisualOracle);
            yield return feat;
        }
        foreach (Feat feat in ArchetypeFeats.CreateBasicAndAdvancedMulticlassFeatGrantingArchetypeFeats(MTraits.RemasterOracle, "Mysteries"))
        {
            feat.Traits.Add(MTraits.VisualOracle);
            yield return feat;
        }

        yield return new TrueFeat(MFeatNames.FirstRevelation, 4,
            null,
            "You gain your mystery's initial revelation spell. If you don't have one, you gain a focus pool of 1 Focus Point, otherwise increase the focus points in your pool by 1, to a maximum of 3.",
            [MTraits.VisualOracle], [.. CreateArchetypeRevelations()])
            .WithAvailableAsArchetypeFeat(MTraits.RemasterOracle);
    }
    private static IEnumerable<Feat> CreateArchetypeRevelations()
    {
        foreach (MysteryFeat oracleMystery1 in AllFeats.GetFeatByFeatName(MFeatNames.RemasterOracle).Subfeats!.Cast<MysteryFeat>())
        {
            MysteryFeat oracleMystery = oracleMystery1;
            string str = oracleMystery.FeatName.HumanizeTitleCase2();
            if (str.StartsWith("Mystery: "))
                str = str["Mystery: ".Length..];
            SpellId spell = oracleMystery.RevelationSpell;
            if (spell == SpellId.None) continue;
            Spell modernSpellTemplate = AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle);
            yield return new Feat(ModManager.RegisterFeatName("RE_ArchetypeRevelation:" + oracleMystery.FeatName.ToStringOrTechnical(), 
                        "First Revelation: " + str), 
                    null, 
                    $"You gain the {AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle).ToSpellLink()} spell. If you don't have one, you gain a focus pool of 1 Focus Point, otherwise increase the focus points in your pool by 1, to a maximum of 3.",
                    [],  null)
                .WithOnSheet(sheet => sheet.AddFocusSpellAndFocusPoint(MTraits.RemasterOracle, Ability.Charisma, spell))
                .WithIllustration(modernSpellTemplate.Illustration)
                .WithRulesBlockForSpell(spell, MTraits.RemasterOracle)
                .WithPrerequisite(values => values.HasFeat(oracleMystery.FeatName), $"You must have the {str} mystery.");
        }
    }
}