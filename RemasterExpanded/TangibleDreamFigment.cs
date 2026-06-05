using System.Linq;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Modding;
using RemasterExpanded.MySpells;

namespace RemasterExpanded;

public class TangibleDreamFigment
{
    public static readonly SpellId Figment = SpellIds.Figment;
    public static void LoadFigment()
    {
        if (AllFeats.GetFeatByFeatName(FeatName.TheTangibleDream) is not {} dream) return;
        dream.OnSheet += sheet =>
        {
            SpellRepertoire repertoire = sheet.SpellRepertoires[Trait.Psychic];
            Spell psiCantrip2 = AllSpells.CreateModernSpell(Figment, null, sheet.MaximumSpellLevel, false,
                new SpellInformation()
                {
                    ClassOfOrigin = Trait.Psychic,
                    PsychicAmpInformation = new PsychicAmpInformation()
                });
            repertoire.SpellsKnown.Add(psiCantrip2);
        };
        string insert = dream.RulesText.Replace(AllSpells.CreateModernSpellTemplate(SpellId.Shield, Trait.Psychic).ToSpellLink(),
            $"{AllSpells.CreateModernSpellTemplate(SpellId.Shield, Trait.Psychic).ToSpellLink()}, {AllSpells.CreateModernSpellTemplate(Figment, Trait.Psychic).ToSpellLink()}");
        dream.RulesText = insert;

        if (AllFeats.GetFeatByFeatNameOrStringOptional(null, dream.FeatName.ToStringOrTechnical() + "ForArchetype")
            is TrueFeat dreamDedication)
        {
            dreamDedication.OnSheet += values => { values.Tags["PsychicOtherCantrip"] = Figment; };
        }
        Feat tangibleFigment = new Feat(
                ModManager.RegisterFeatName("TangibleDreamFigment", "Tangible Dream - Figment"),
                dream.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(Figment, Trait.Psychic)}",
                [], null)
            .WithIllustration(AllSpells.CreateModernSpellTemplate(Figment, Trait.Psychic).Illustration)
            .WithRulesBlockForSpell(Figment, Trait.Psychic)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? value))
                    return;
                ++values.FocusPointCount;
                value.SpellsKnown.Add(AllSpells.CreateModernSpell(Figment, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = SpellId.ImaginaryWeapon;
                values.Tags["PsychicOtherCantrip"] = SpellId.Shield;
            });
        ModManager.AddFeat(tangibleFigment);
        if (AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Psychic.ToStringOrTechnical() + "Dedication")
            is TrueFeat psycheDedication)
        {
            psycheDedication.Subfeats?.Add(tangibleFigment);
        }
        Spell spell = AllSpells.CreateModernSpellTemplate(Figment, Trait.Psychic);
        Feat parallelFigment = new Feat(ModManager.RegisterFeatName(spell.Name, spell.Name), null,
                $"You can cast {spell.ToSpellLink()}.", [], null)
            .WithRulesBlockForSpell(spell.SpellId, Trait.Psychic).WithIllustration(spell.Illustration)
            .WithPrerequisite(
                values =>
                    !values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire1) ||
                    !spellRepertoire1.SpellsKnown.Any(sk =>
                        sk.SpellId == spell.SpellId && sk.CombatActionSpell.PsychicAmpInformation == null),
                "You have already selected this spell as a non-psi cantrip.\n")
            .WithEquivalent(values =>
                values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire2) &&
                spellRepertoire2.SpellsKnown.Any(sk =>
                    sk.SpellId == spell.SpellId && sk.CombatActionSpell.PsychicAmpInformation != null))
            .WithOnSheet(values => values.SpellRepertoires[Trait.Psychic]
                .SpellsKnown
                .Add(AllSpells.CreateModernSpell(spell.SpellId, null, values.MaximumSpellLevel, false,
                    new SpellInformation()
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    })));
        ModManager.AddFeat(parallelFigment);
        AllFeats.GetFeatByFeatName(FeatName.ParallelBreakthrough).Subfeats?.Add(parallelFigment);
    }
}