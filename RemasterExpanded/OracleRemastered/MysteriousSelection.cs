using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Selections.Selected;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Phases.Menus.CharacterBuilderPages;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.OracleRemastered;

public class MysteriousSelection(string key, string name, int level, int maxSpellRank) : AbstractSpellSelectionOption(key, name, level, maxSpellRank)
{
    public override int MaximumNumberOfSpells { get; } = 1;

    public override SpellSelectionPageKind SpellSelectionPageKind => SpellSelectionPageKind.Standard;

    public override bool Eligible(CalculatedCharacterSheetValues values, Spell spell)
    {
        if (values.SpellRepertoires.TryGetValue(MTraits.RemasterOracle, out SpellRepertoire _))
        {
            if (!spell.Traits.ContainsOneOf([
                    Trait.Arcane,
                    Trait.Primal,
                    Trait.Occult
                ]) || spell.HasTrait(Trait.Divine) ||
                spell.HasTrait(Trait.SpellCannotBeChosenInCharacterBuilder)) return false;
            if (MaximumSpellLevel == 0 && spell.HasTrait(Trait.Cantrip))
                return true;
            return MaximumSpellLevel >= 1 && spell.MinimumSpellLevel <= MaximumSpellLevel &&
                   !spell.HasTrait(Trait.Cantrip);
        }
        return false;
    }

    public override bool IsTaken(CalculatedCharacterSheetValues values, Spell spell)
    {
        return values.SpellRepertoires[MTraits.RemasterOracle].SpellsKnown.Any(sp => sp.SameAs(spell));
    }

    public override bool IsComplete(SpellSelectedChoice spellSelectedChoice)
    {
        return spellSelectedChoice.Choices.Count == MaximumNumberOfSpells;
    }

    public override void Apply(
        SpellSelectedChoice spellSelectedChoice,
        CalculatedCharacterSheetValues sheet)
    {
        sheet.SpellRepertoires[MTraits.RemasterOracle].AdditionalSpellsAllowed.Add(spellSelectedChoice.Choices.First().SpellId);
        sheet.SpellRepertoires[MTraits.RemasterOracle].SpellsKnown.AddRange(spellSelectedChoice.Choices.Select(spell => spell.Duplicate(spell.CombatActionSpell.Owner, spell.MinimumSpellLevel, false)));
        if (sheet.Tags["OracleMystery"] is MysteryFeat mystery)
            mystery.DivineAccess.Add(spellSelectedChoice.Choices.First().SpellId);
    }
}