using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Mods.LoresAndWeaknesses;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.OracleRemastered;

public class MysteryFeat : Feat
{
    public List<FeatName> AllowedDomains { get; set; }
    public Skill MysterySkill { get; set; }
    public SpellId RevelationSpell { get; set; }
    public  SpellId AdvancedRevelationSpell { get; set; }
    public SpellId GreaterRevelationSpell { get; set; }
    public string CurseRules { get; set; }
    public Illustration CurseIllustration { get; set; }
    public List<SpellId> GrantedSpells { get; set; }
    public List<SpellId> DivineAccess { get; set; }
    public MysteryFeat(FeatName featName,
        string? flavorText,
        SpellId revelationSpell,
        SpellId advancedRevelationSpell,
        SpellId greaterRevelationSpell,
        SpellId cantrip,
        List<SpellId> grantedSpells,
        List<FeatName> allowedDomains,
        Skill mysterySkill,
        Action<QEffect> curse,
        string curseName,
        string curseRules,
        string curseFlavor,
        Illustration curseIllustration,
        List<Trait> curseTraits,
        FeatName curseboundFeat,
        List<Feat>? subfeats = null) : base(featName, flavorText, "", [], subfeats)
    {
        AllowedDomains = allowedDomains;
        MysterySkill = mysterySkill;
        RevelationSpell = revelationSpell;
        AdvancedRevelationSpell = advancedRevelationSpell;
        GreaterRevelationSpell = greaterRevelationSpell;
        GrantedSpells = grantedSpells;
        CurseRules = curseRules;
        CurseIllustration = curseIllustration;
        DivineAccess = [];
        WithRulesTextCreator(sheet =>
        {
            if (sheet.Class?.ClassTrait != MTraits.RemasterOracle)
                return "You gain the following benefits:\n\n" +
                       $"• You're trained in {mysterySkill.HumanizeTitleCase2()}." +
                       $"\n• At level 4, you can take the Basic Mysteries feat in order to take the Domain Acumen feat to learn a focus spell from the following domains: {S.ConstructOrList(allowedDomains.Select(fN => AllFeats.GetFeatByFeatName(fN).ToLink(fN.HumanizeLowerCase2())), "").Replace("  ", " ")}" +
                       $"\n• At level 4, you can take the Initial Revelation feat to learn the {AllSpells.CreateSpellLink(revelationSpell, MTraits.RemasterOracle)} focus spell. At level 12, you can take the Advanced Mysteries feat in order to take the Advanced Revelation feat to also learn {AllSpells.CreateSpellLink(advancedRevelationSpell, MTraits.RemasterOracle)}." +
                       "\n• If you gain Oracle spell slots of the appropriate rank, the following spells can be selected in addition to divine spells: " +
                       $"{S.ConstructOrList(grantedSpells.Select(spId => $"{S.OrdinalAbbreviation(AllSpells.CreateModernSpellTemplate(spId, MTraits.RemasterOracle).SpellLevel)} {AllSpells.CreateSpellLink(spId, MTraits.RemasterOracle)}"), "").Replace(',', ';').Replace("  ", " ")}" +
                       "\n\nWhen under the effects of the cursebound condition, you suffer the following curse:";
            return "You gain the following benefits:\n\n"+
                   $"• You automatically add the following spells to your spell repertoire once you can cast spells of that rank: cantrip: {AllSpells.CreateSpellLink(cantrip, MTraits.RemasterOracle)};  " +
                   $"{S.ConstructOrList(grantedSpells.Select(spId => $"{S.OrdinalAbbreviation(AllSpells.CreateModernSpellTemplate(spId, MTraits.RemasterOracle).SpellLevel)} {AllSpells.CreateSpellLink(spId, MTraits.RemasterOracle)}"), "").Replace(',', ';').Replace("  ", " ")}" +
                   $"\n• You learn the {AllSpells.CreateSpellLink(revelationSpell, MTraits.RemasterOracle)} focus spell. At level 6, you can take the Advanced Revelation feat to also learn {AllSpells.CreateSpellLink(advancedRevelationSpell, MTraits.RemasterOracle)}. At level 12, you can take the Greater Revelation feat to also learn {AllSpells.CreateSpellLink(greaterRevelationSpell, MTraits.RemasterOracle)}." +
                   $"\n• At level 2, you can take the Domain Acumen feat to learn a focus spell from the following domains: {S.ConstructOrList(allowedDomains.Select(fN => AllFeats.GetFeatByFeatName(fN).ToLink(fN.HumanizeLowerCase2())), "").Replace("  ", " ")}" +
                   $"\n• You're trained in {mysterySkill.HumanizeTitleCase2()}." +
                   $"\n• You gain the {AllFeats.GetFeatByFeatName(curseboundFeat).ToLink(curseboundFeat.HumanizeTitleCase2())} cursebound feat." +
                   "\n\nWhen under the effects of the cursebound condition, you suffer the following curse:";
        });
        WithOnSheet(sheet =>
        {
            sheet.TrainInThisOrSubstitute(mysterySkill);
            sheet.Tags.Add("OracleMystery", this);
            SpellRepertoire repertoire = sheet.SpellRepertoires[MTraits.RemasterOracle];
            repertoire.AdditionalSpellsAllowed.AddRange(grantedSpells);
            if (sheet.Class == null || sheet.Class.ClassTrait != MTraits.RemasterOracle)
                return;
            repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(cantrip, MTraits.RemasterOracle));
            foreach (SpellId spell in grantedSpells)
            {
                sheet.AddAtLevel(AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle).SpellLevel * 2 - 1,
                    _ => repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle)));
            }
            sheet.AddFocusSpellAndFocusPoint(MTraits.RemasterOracle, Ability.Charisma, revelationSpell);
            sheet.GrantFeat(curseboundFeat);
        });
        WithPermanentQEffect(null, qf =>
        {
            qf.Name = curseName;
            qf.Description = (curseRules.StartsWith("{b}Cursebound 1{/b}") ? "\n" : "") + curseRules;
            qf.Innate = true;
            curse(qf);
        });
        WithTag(greaterRevelationSpell);
        WithRulesBlockForCombatAction(cr =>
            CombatAction.CreateSimple(cr, curseName, [.. curseTraits])
                .WithDescription(
                    curseFlavor,
                    curseRules)
                .WithActionCost(7).WithIllustration(curseIllustration));
    }
}