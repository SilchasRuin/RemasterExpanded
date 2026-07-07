using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Selections.Selected;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Display;
using Dawnsbury.Display.Controls.Listbox;
using Dawnsbury.Modding;
using SpiritDamage;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.ClassChangesAndFeats;

public class Sanctification 
{
    public static void ModifyChampionCleric()
    {
        AllFeats.GetFeatByFeatName(FeatName.Champion).WithOnSheet(values =>
        {
            values.GrantFeat(MFeatNames.DeificWeapon);
        });
        AllFeats.GetFeatByFeatName(FeatName.Cleric).WithOnSheet(values =>
        {
            SelectionOption choose = HolyAndUnholySelection(Trait.Cleric);
            values.AddSelectionOption(choose);
            SelectedChoice? choiceForKey = values.Sheet.FindChoiceForKey(choose);
            if (choose.GetCompletionStatus(choiceForKey, values) == FeatCompletionStatus.OptionalSelectionMissing)
            {
                values.Sheet.SelectedFeats[choose.Key] = HolyAndUnholyFeat(values, Trait.Cleric);
            }
        });
        if (AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Cleric.ToStringOrTechnical() + "Dedication") is {} dedication2)
            dedication2.WithOnSheet(values =>
            {
                SelectionOption choose = HolyAndUnholySelection(Trait.Cleric, values.CurrentLevel);
                values.AddSelectionOption(choose);
                SelectedChoice? choiceForKey = values.Sheet.FindChoiceForKey(choose);
                if (choose.GetCompletionStatus(choiceForKey, values) == FeatCompletionStatus.OptionalSelectionMissing)
                {
                    values.Sheet.SelectedFeats[choose.Key] = HolyAndUnholyFeat(values, Trait.Cleric);
                }
            });
    }
    public static IEnumerable<Feat> SanctifyFeats()
    {
        yield return new Feat(MFeatNames.HolyChampion, "You have committed to the holy cause in the struggle over the souls of all mortals.", 
                "You gain the holy trait and add that trait to any Strikes you make.", [], null)
            .WithTag("SanctificationChampion")
            .WithOnCreature(cr => cr.AddQEffect(SanctifiedChampion(HolyTrait.Holy)))
            .WithOnCreature(cr => cr.Traits.Add(HolyTrait.Holy))
            .WithPrerequisite(IsGood, "You must be good aligned.");
        yield return new Feat(MFeatNames.UnholyChampion, "You have committed to the unholy cause in the struggle over the souls of all mortals.", 
                "You gain the unholy trait and add that trait to any Strikes you make.", [], null)
            .WithTag("SanctificationChampion")
            .WithOnCreature(cr => cr.AddQEffect(SanctifiedChampion(UnholyTrait.Unholy)))
            .WithOnCreature(cr => cr.Traits.Add(UnholyTrait.Unholy))
            .WithPrerequisite(IsEvil, "You must be evil aligned.");
        yield return new Feat(MFeatNames.NoneChampion,
                "You have refused to commit to a side in the struggle over the souls of all mortals.",
                "You gain neither benefit nor detriment.",
                [], null)
            .WithTag("SanctificationChampion");
        yield return new Feat(MFeatNames.DeificWeapon,
                "You zealously bear your deity's favored weapon.",
                "If your deity's favored weapon is an advanced weapon, you treat it as a martial weapon for the purposes of proficiency.",
                [], null)
            .WithPermanentQEffectAndSameRulesText(qf => qf.WithExpirationNever())
            .WithOnSheet(values =>
            {
                if (values.Deity == null || !Items.CreateNew(values.Deity.FavoredWeapon).HasTrait(Trait.Advanced)) return;
                values.Proficiencies.AddProficiencyAdjustment(
                    item => item.Contains(Items.CreateNew(values.Deity.FavoredWeapon).MainTrait), Trait.Martial);
            });
        yield return new Feat(MFeatNames.HolyCleric, "You have committed to the holy cause in the struggle over the souls of all mortals.", 
                "You gain the holy trait, allowing you to add that trait to sanctified actions.", [], null)
            .WithTag("SanctificationCleric")
            .WithOnCreature(cr => cr.Traits.Add(HolyTrait.Holy))
            .WithPrerequisite(IsGood, "You must be good aligned.");
        yield return new Feat(MFeatNames.UnholyCleric, "You have committed to the unholy cause in the struggle over the souls of all mortals.", 
                "You gain the unholy trait, allowing you to add that trait to sanctified actions.", [], null)
            .WithTag("SanctificationCleric")
            .WithOnCreature(cr => cr.Traits.Add(UnholyTrait.Unholy))
            .WithPrerequisite(IsEvil, "You must be evil aligned.");
        yield return new Feat(MFeatNames.NoneCleric,
                "You have refused to commit to a side in the struggle over the souls of all mortals.",
                "You gain neither benefit nor detriment.",
                [], null)
            .WithTag("SanctificationCleric");
        yield return new Feat(MFeatNames.ChampionSanctification,
            "You have chosen a side in the cosmic struggle over the fate of souls.",
            "Depending on your deity, their {tooltip:sanctify}sanctification{/tooltip} can make you {tooltip:holy}holy{/tooltip} or {tooltip:unholy}unholy{/tooltip}. Whether you become holy, unholy, or neither will limit your choice of causes, devotion spells, and feats.\n\n" +
            "{b}Holy{/b}: You gain the holy trait and add that trait to any Strikes you make. You must be good aligned." +
            "\n\n{b}Unholy{/b}: You gain the unholy trait and add that trait to any Strikes you make. You must be evil aligned.",
            [], null);
    }
    
    public static QEffect SanctifiedChampion(Trait trait)
    {
        return new QEffect(trait.HumanizeTitleCase2(), $"You gain the {trait.HumanizeLowerCase2()} trait and add that trait to any Strikes you make.")
        {
            ModifyActionPossibility = (_, action) =>
            {
                if (!action.HasTrait(Trait.Strike))
                    return;
                action.WithExtraTrait(trait);
            },
            Innate = true
        };
    }

    public static bool IsGood(CalculatedCharacterSheetValues values)
    {
        return values.NineCornerAlignment is NineCornerAlignment.LawfulGood or NineCornerAlignment.ChaoticGood or NineCornerAlignment.NeutralGood;
    }
    
    public static bool IsEvil(CalculatedCharacterSheetValues values)
    {
        return values.NineCornerAlignment is NineCornerAlignment.LawfulEvil or NineCornerAlignment.ChaoticEvil or NineCornerAlignment.NeutralEvil;
    }

    public static FeatSelectedChoice FeatChoice(FeatName featName, FeatName? subfeatName = null)
    {
        return new FeatSelectedChoice(AllFeats.GetFeatByFeatName(featName), AllFeats.GetFeatByFeatNameOptional(subfeatName));
    }

    public static FeatSelectedChoice? HolyAndUnholyFeat(CalculatedCharacterSheetValues values, Trait trait)
    {
        switch (trait)
        {
            case Trait.Champion:
                if (IsGood(values))
                    return FeatChoice(MFeatNames.HolyChampion);
                return IsEvil(values) ? FeatChoice(MFeatNames.UnholyChampion) : FeatChoice(MFeatNames.NoneChampion);
            case Trait.Cleric:
                if (IsGood(values))
                    return FeatChoice(MFeatNames.HolyCleric);
                return IsEvil(values) ? FeatChoice(MFeatNames.UnholyCleric) : FeatChoice(MFeatNames.NoneCleric);
            default:
                return null;
        }
    }

    public static SelectionOption HolyAndUnholySelection(Trait trait, int level = 0)
    {
        string clericOrChampion = trait.HumanizeTitleCase2();
        SelectionOption choose = new SingleFeatSelectionOption("RE_Sanctification"+clericOrChampion, "Sanctification", level, feat => feat.Tag is "SanctificationCleric").WithIsOptional();
        if (trait == Trait.Champion)
            choose = new SingleFeatSelectionOption("RE_Sanctification"+clericOrChampion, "Sanctification", level, feat => feat.Tag is "SanctificationChampion").WithIsOptional();
        return choose;
    }

    public static void ChampionSanctificationChoiceLogic(Feat feat)
    {
        feat.WithOnSheet(values =>
        {
            SelectionOption choose = HolyAndUnholySelection(Trait.Champion, values.CurrentLevel);
            values.AddSelectionOption(choose);
            SelectedChoice? choiceForKey = values.Sheet.FindChoiceForKey(choose);
            if (choose.GetCompletionStatus(choiceForKey, values) == FeatCompletionStatus.OptionalSelectionMissing)
            {
                values.Sheet.SelectedFeats[choose.Key] = HolyAndUnholyFeat(values, Trait.Champion);
            }
        });
    }
}