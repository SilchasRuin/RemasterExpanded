using System;
using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Display;
using Dawnsbury.Modding;
using RemasterExpanded.MySpells;
using static RemasterExpanded.ModData;

namespace RemasterExpanded;

public static class NewDeities
{
    public static IEnumerable<Feat> LoadDeities()
    {
        Feat zarazrael = new DeitySelectionFeat(ModManager.RegisterFeatName("RE_Zarazrael", "Deity: Zarazrael"), 
            "Zarazrael, the Burning Blade, is the archangel of justice, duty and righteous vengeance. He is the archetypal paladin, valuing strength of arms, keeping oaths, the righting of wrongs, and destruction of the wicked." +
            "\n\nBorn of an archdevil and an elemental lord of flame, Zarazrael knows of the struggle for acceptance and to rise above one's nature. Followers of Zarazrael are to lead by example, destroying the wicked without remorse and setting an example of duty and courage for those who can be shown a better path.",
            "{b}• Edicts{/b} Punish the wicked, avenge the innocent, stand strong in the face of adversity." +
            "\n{b}• Anathema{/b} Break one's oaths, abandon one's comrades, allow evil to escape just punishment.", 
            [NineCornerAlignment.LawfulGood], 
            [FeatName.HealingFont, FeatName.HarmfulFont], [FeatName.DomainFire, FeatName.DomainZeal, FeatName.DomainMight, FeatName.DomainDestruction], ItemName.BastardSword,
            [SpellId.TrueStrike, SpellId.Haste, SpellId.FireShield], Skill.Intimidation);
        AllFeats.GetFeatByFeatName(FeatName.Cleric).Subfeats?.Add(zarazrael);
        yield return  zarazrael;
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "HereThereBeDragons") && AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "SpellsAndSpellhearts"))
        {
            Feat theWorldSerpent = new DeitySelectionFeat(
                ModManager.RegisterFeatName("RE_WorldSerpent", "Deity: The World Serpent"),
                "Braggadocios and fierce, the World Serpent is an immense serpentine draconic entity who is the patron deity of sea serpents and linnorms. His immense length and vast strength is said to be so great that he could crush a mountain by coiling around it. Arrogant, but lazy, he waits and watches in the depths of the Endless Oceans, occasionally answering the prayers of those willing to subjugate themselves." +
                "\n\nFollowers of the World Serpent know that one must never demand from him, and always deliver all due honorifics, for his wrath at being slighted is great. Despite this, the World Serpent's ancient wisdom is vast, containing knowledge otherwise lost to time.",
                "{b}• Edicts:{/b} Seek ancient knowledge, plumb seldom seen depths, punish those who slight you" +
                "\n{b}• Anathema{/b} Destroy lost knowledge, seek out and slay sea serpents or linnorms without cause",
                [NineCornerAlignment.ChaoticNeutral, NineCornerAlignment.ChaoticEvil, NineCornerAlignment.NeutralEvil],
                [FeatName.HealingFont, FeatName.HarmfulFont],
                [FeatName.DomainDestruction, CcRequired.DomainDragon, FeatName.DomainWater, MFeatNames.Vigil], ItemName.Whip,
                [SpellId.HydraulicPush, CcRequired.BrineDragonBile, SpellIds.FeetToFins], Skill.Occultism);
            AllFeats.GetFeatByFeatName(FeatName.Cleric).Subfeats?.Add(theWorldSerpent);
            yield return  theWorldSerpent;
        }
        if (ModManager.TryParse("PS_Sorrow", out FeatName sorrow) && ModManager.TryParse("PS_Time", out FeatName time))
        {
            Feat theLostLord = CreateDeity("The Lost Lord",
                "Archfey of the lost, fallen, and forgotten, the Lost Lord withers away in his forgotten keep beyond time. The mysteries of his origin are lost in the distant past, though whispers suggest he did not originate within the lands of the fey." +
                "\nProne to descending into deep bouts of despondency and sorrow, the Lost Lord rarely speaks and never involves himself in the schemes of the other archfey. In spite of his melancholic nature, the Lost Lord offers respite and sympathy for others suffering from loss and grief and demands his followers do the same.",
                "Help the depressed, be subdued in appearance, avoid entanglement in schemes, remember history",
                "Abandon those who have no-one else, be prideful and boast of good deeds",
                [NineCornerAlignment.NeutralGood, NineCornerAlignment.TrueNeutral],
                [FeatName.HarmfulFont, FeatName.HealingFont],
                [MFeatNames.Vigil, MFeatNames.Knowledge, time, sorrow], [SpellId.Soothe, SpellId.Sleep, SpellId.CrushingDespair],
                ItemName.Staff, Skill.Society);
            yield return theLostLord;
        }

        Feat theTraveler = CreateDeity("The Traveler",
            "The Traveler walks the paths of the dead, the roads between the planes and the mortal realm where detritus of the fallen come to rest. The Traveler walks these roads so that others might follow. They lead the souls of the lost who have fallen far from home back to their spiritual abode. They are a patron of travelers and vigils and their followers seek to follow in the Traveler's example.", 
            "Tend to roadside graves, find missing persons or lost possessions, help travelers and souls returned from the grave",
            "Live an unchanging life, prevent others from growing and changing",
            [NineCornerAlignment.NeutralGood, NineCornerAlignment.TrueNeutral, NineCornerAlignment.ChaoticNeutral, NineCornerAlignment.ChaoticGood],
            [FeatName.HealingFont],
            [FeatName.DomainDeath, FeatName.DomainTravel, MFeatNames.Vigil, MFeatNames.Knowledge],
            [SpellIds.Tailwind, SpellId.LooseTimesArrow, SpellId.DimensionDoor],
            ItemName.Club, Skill.Survival);
        yield return theTraveler;
        Feat theWatcher = CreateDeity("The Watcher",
            "The Watcher is an eternal sentry and the patron of guardians and enforcers. He enforces the laws of the heavens with rigid absolutism, never wavering from his given task. Those who choose to follow him know they must adhere to absolute standards of loyalty and duty." +
            "\n\nThe Watcher is an ancient deity, none know his origins. Always encased head to toe in armor, none have ever seen his true face. The Watcher is impartial in his tasks, never swayed by emotion.",
            "Guard your charges, obey legitimate authority, follow orders",
            "Falter in your watch, fail to stand in defense of those you are sworn to, allow your judgment to be swayed by sentiment",
            [NineCornerAlignment.LawfulGood, NineCornerAlignment.LawfulNeutral],
            [FeatName.HarmfulFont, FeatName.HealingFont],
            [FeatName.DomainDeath, MFeatNames.Vigil, FeatName.DomainZeal, FeatName.DomainConfidence],
            [SpellId.TrueStrike, SpellId.Paralyze, SpellId.Stoneskin], ItemName.Greataxe, Skill.Athletics);
        yield return theWatcher;
        if (ModManager.TryParse("PS_Change", out FeatName change) && ModManager.TryParse("PS_Trickery", out FeatName trickery) && ModManager.TryParse("Nightmares", out FeatName nightmare))
        {
            Feat thePranksters = CreateDeity("The Pranksters",
                "Some people just want to delight in anarchy and the Pranksters are here to help. The Pranksters are not a singular entity, but rather a covenant of powerful fey and chaotic spirits who delight in a world made less predictable." +
                "\n\nThose who follow the Pranksters run the gamut, from well meaning jokesters to cruel maniacs. There is no singular set of rituals or prayers common to the adherents of the Pranksters, and they'd be insulted if anyone tried to create one!",
                "Upset the established order, be unpredictable, prank those who expect it least",
                "Be boring, wear drab colors, undo anarchy",
                [NineCornerAlignment.ChaoticNeutral, NineCornerAlignment.ChaoticEvil, NineCornerAlignment.ChaoticGood],
                [FeatName.HarmfulFont, FeatName.HealingFont],
                [change, trickery, nightmare, FeatName.DomainLuck],
                [SpellIds.Befuddle, SpellId.HideousLaughter, SpellId.CloakOfColors],
                ItemName.GnomeFlickmace, Skill.Deception);
            yield return thePranksters;
        } 

        if (ModManager.TryParse("PS_Metal", out FeatName metal) && ModManager.TryParse("Falcata", out ItemName falcata) && ModManager.TryParse("PS_Creation", out FeatName creation) && ModManager.TryParse("SH_Forge", out SpellId forge))
        {
            Feat theSovereignSword = CreateDeity("The Sovereign Sword",
                "A magic blade once wielded by a long lost deity, the Sovereign Sword gained sentience and a spark of divinity when retrieved by adventurers after their wielder was devoured by the Beast of Ages. How and why this happened has remained a mystery to scholars since, but the Sovereign Sword has taken a place amongst the pantheon." +
                "\n\nThe Sovereign Sword's creed focuses on preparation for future dangers and the creation of magical artifacts. They are the patron of other intelligent items and command that those who would take up these items treat them with respect.",
                "Craft masterworks, respect and cherish items, prepare for myriad futures",
                "Disrespect or destroy intelligent items",
                NineCornerAlignmentExtensions.All(),
                [FeatName.HarmfulFont, FeatName.HealingFont],
                [metal, creation, MFeatNames.Knowledge, FeatName.DomainHealing],
                [forge, SpellIds.AnimatedAssault, SpellIds.WeaponStorm],
                falcata, Skill.Crafting);
            yield return theSovereignSword;
        }
    }

    public static void LoadDomains()
    {
        CreateDomainFeats(MFeatNames.Vigil, "You watch over those long passed and guard their secrets.", SpellIds.ObjectMemory,
            SpellIds.RememberTheLost);
        CreateDomainFeats(MFeatNames.Knowledge, "You receive divine insights.", SpellIds.ScholarlyRecollection, SpellIds.KnowTheEnemy);
    }

    private static void CreateDomainFeats(FeatName featName, string flavorText, SpellId basicDomain, SpellId advancedDomain)
    {
        Feat domainFeat = ClericClassFeatures.CreateDomain(featName, flavorText, basicDomain, advancedDomain);
        ClericClassFeatures.AllDomainFeats.Add(domainFeat);
        ModManager.AddFeat(domainFeat);
        Feat clericDomain = CreateAdvancedDomainFeat(Trait.Cleric, domainFeat);
        ModManager.AddFeat(clericDomain);
        AllFeats.GetFeatByFeatName(FeatName.AdvancedDomain).Subfeats?.Add(clericDomain);
        Feat championDomain = CreateAdvancedDomainFeat(Trait.Champion, domainFeat);
        ModManager.AddFeat(championDomain);
        AllFeats.GetFeatByFeatName(FeatName.AdvancedDeitysDomain).Subfeats?.Add(championDomain);
        Feat oracleDomain = CreateAdvancedDomainFeat(Trait.Oracle, domainFeat);
        ModManager.AddFeat(oracleDomain);
        AllFeats.GetFeatByFeatName(FeatName.DomainFluency).Subfeats?.Add(oracleDomain);
    }
    
    internal static Feat CreateAdvancedDomainFeat(Trait forClass, Feat domainFeat) 
    {
        string name = domainFeat.Name;
        SpellId advancedSpell = (SpellId)domainFeat.Tag!;
        Spell spell = AllSpells.CreateModernSpellTemplate(advancedSpell, forClass);
        Feat advancedDomain = new Feat(ModManager.TryParse("AdvancedDomain:" + forClass.HumanizeTitleCase2() + ":" + name, out FeatName featName) ? featName : ModManager.RegisterFeatName("AdvancedDomain:" + forClass.HumanizeTitleCase2() + ":" + name, name + ": " + spell.Name), "Your studies or prayers have unlocked deeper secrets of the " + name.ToLower() + " domain.",
                $"You learn the {forClass.HumanizeTitleCase2().ToLower()} focus spell " + AllSpells.CreateSpellLink(advancedSpell, forClass) + ", and you gain 1 focus point, up to a maximum 3.", [], null)
            .WithIllustration(spell.Illustration)
            .WithRulesBlockForSpell(advancedSpell, forClass)
            .WithPrerequisite(values => values.HasFeat(domainFeat.FeatName), "You must have the " + name + " domain.")
            .WithOnSheet(sheet =>
            {
                switch (sheet.Sheet.Class?.ClassTrait)
                {
                    case Trait.Cleric:
                        sheet.AddFocusSpellAndFocusPoint(Trait.Cleric, Ability.Wisdom, advancedSpell);
                        break;
                    case Trait.Oracle:
                        sheet.AddFocusSpellAndFocusPoint(Trait.Oracle, Ability.Charisma, advancedSpell);
                        break;
                    case Trait.Champion:
                        sheet.AddFocusSpellAndFocusPoint(Trait.Champion, Ability.Charisma, advancedSpell);
                        break;
                    default:
                        sheet.AddFocusSpellAndFocusPoint(MTraits.CampfireChronicler, Ability.Charisma, advancedSpell);
                        break;
                }
            });
        return advancedDomain;
    }

    private static Feat CreateDeity(string deityName, string flavorText, string edicts, string anathema, NineCornerAlignment[] alignments, FeatName[] allowedFonts, FeatName[] domains, SpellId[] extraSpells,ItemName favoredWeapon, Skill favoredSkill)
    {
        Feat deity = new DeitySelectionFeat(ModManager.RegisterFeatName("RE_"+deityName.Replace(" ", ""), "Deity: "+deityName), flavorText,
            "{b}• Edicts{/b} "+edicts+"\n{b}• Anathema{/b} "+anathema, alignments, allowedFonts, domains, favoredWeapon, extraSpells, favoredSkill);
        AllFeats.GetFeatByFeatName(FeatName.Cleric).Subfeats?.Add(deity);
        return deity;
    }
}