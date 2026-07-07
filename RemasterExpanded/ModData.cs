using Dawnsbury.Audio;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using SpiritDamage;

namespace RemasterExpanded;

public static class ModData
{
    public static bool Remaster { get; } = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Dawnsbury.Mods.Remaster.FeatsDb");
    public static bool RemasterSpells { get; } = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Dawnsbury.Mods.Remaster.Spellbook");
    public static bool LoadPsychic { get; } = ModManager.TryParse("OscillatingWave", out FeatName _);
    public static bool MoreSpells { get; } = ModManager.TryParse("OscillatingWave", out FeatName _) && ModManager.TryParse("SH_Spellheart", out Trait _);
    public static bool ChampionsOfEvil { get; } = ModManager.TryParse("PS_TyrantChampionSubclass", out FeatName _);
    public static bool ShieldsOfSpirit { get; } = ModManager.TryParse("PS_GrandeurChampionSubclass", out FeatName _);
    public static readonly Trait ModTrait = ModManager.ModBeingLoadedTrait ?? Trait.None;

    public static T SafelyRegister<T>(string technicalName, string? displayName = null) where T : struct, Enum
    {
        return (T)(ModManager.TryParse(technicalName, out T alreadyRegistered)
            ? alreadyRegistered
            : typeof(T) == typeof(FeatName)
                ? (Enum)ModManager.RegisterFeatName(technicalName, displayName)
                : ModManager.RegisterEnumMember<T>(technicalName));
    }

    public static Feat? GetModdedFeat(string technicalName)
    {
        return ModManager.TryParse(technicalName, out FeatName featName)
            ? AllFeats.GetFeatByFeatNameOptional(featName)
            : null;
    }
    
    public static class RActionIds
    {
        public static readonly ActionId DesignateAlly = ModManager.RegisterEnumMember<ActionId>("RE_DesignateAlly");
        public static readonly ActionId ReArm = ModManager.RegisterEnumMember<ActionId>("RE_Rearm");
        public static readonly ActionId PackBreaker = ModManager.RegisterEnumMember<ActionId>("RE_PackBreaker");
        public static readonly ActionId CommitmentToJustice = ModManager.RegisterEnumMember<ActionId>("RE_CommitmentToJustice");
        public static readonly ActionId CommitmentToEquality = ModManager.RegisterEnumMember<ActionId>("RE_CommitmentToEquality");
        public static readonly ActionId EvenTheOdds = ModManager.RegisterEnumMember<ActionId>("RE_EvenTheOdds");
        public static readonly ActionId ImproviseStrategy = ModManager.RegisterEnumMember<ActionId>("RE_ImproviseStrategy");
        public static readonly ActionId SeasonedCommand =  ModManager.RegisterEnumMember<ActionId>("RE_SeasonedCommand");
        public static readonly ActionId FiendishBrand = ModManager.RegisterEnumMember<ActionId>("RE_FiendishBrand");
    }
    public static class MTraits
    {
        public static readonly Trait VikingGuard = ModManager.RegisterTrait("RE_VikingGuard", new TraitProperties("Viking Guard", false));
        public static readonly Trait Garment =  ModManager.RegisterTrait("RE_Garment", new TraitProperties("Garment", false));
        public static readonly Trait UnifiedMagicalTheory = ModManager.RegisterTrait("RE_UnifiedMagicalTheory", new TraitProperties("Unified Magical Theory", false));
        public static readonly Trait BlackJacket = ModManager.RegisterTrait("RE_BlackJacket", new TraitProperties("Blackjacket", false));
        public static readonly Trait Pirate = ModManager.RegisterTrait("RE_Pirate", new TraitProperties("Pirate", false));
        public static readonly Trait Viking = ModManager.RegisterTrait("RE_Viking", new TraitProperties("Viking", false));
        public static readonly Trait Sanctified = ModManager.RegisterTrait("RE_Sanctified", new TraitProperties("Sanctified", true, "If you are good your sanctified actions and spells gain the holy trait, if you are evil your sanctified actions and spells gain the unholy trait."));
        public static readonly Trait CampfireChronicler = ModManager.RegisterTrait("RE_CampfireChronicler", new TraitProperties("Campfire Chronicler", false){ IsClassTrait = true });
        public static readonly Trait FaithSymbol = ModManager.RegisterTrait("RE_FaithSymbol", new TraitProperties("Faith Symbol", false));
        public static readonly Trait AnimalWeapon = ModManager.RegisterTrait("RE_AnimalNaturalWeapon", new TraitProperties("Animal Natural Weapon", false));
        public static readonly Trait AspCoil = ModManager.RegisterTrait("RE_AspCoil", new TraitProperties("Asp Coil", false));
        public static readonly Trait Scourge = ModManager.RegisterTrait("RE_Scourge", new TraitProperties("Scourge", false));
    }

    public static class MFeatNames
    {
        public static readonly FeatName MercenaryMotivation = ModManager.RegisterFeatName("RE_MercenaryMotivation", "Mercenary Motivation");
        public static readonly FeatName GuardsFury = ModManager.RegisterFeatName("RE_GuardsFury", "Guard's Fury");
        public static readonly FeatName Vigil = ModManager.RegisterFeatName("Vigil", "Vigil");
        public static readonly FeatName Knowledge = ModManager.RegisterFeatName("RE_Knowledge", "Knowledge");
        public static readonly FeatName DeificWeapon = ModManager.RegisterFeatName("DeificWeapon", "Deific Weapon");
        public static readonly FeatName ChampionSanctification = ModManager.RegisterFeatName("RE_ChampionSanctification", "Sanctification");
        public static readonly FeatName HolyChampion = ModManager.RegisterFeatName("RE_ChampionHoly", "Holy");
        public static readonly FeatName UnholyChampion = ModManager.RegisterFeatName("RE_ChampionUnholy", "Unholy");
        public static readonly FeatName NoneChampion =  ModManager.RegisterFeatName("RE_ChampionNone", "None");
        public static readonly FeatName HolyCleric = ModManager.RegisterFeatName("RE_ClericHoly", "Holy");
        public static readonly FeatName UnholyCleric = ModManager.RegisterFeatName("RE_ClericUnholy", "Unholy");
        public static readonly FeatName NoneCleric = ModManager.RegisterFeatName("RE_ClericNone", "None");
        public static readonly FeatName InitiateWarden = ModManager.RegisterFeatName("RE_InitiateWarden", "Initiate Warden");
        public static readonly FeatName MonsterHunter = ModManager.RegisterFeatName("RE_MonsterHunter", "Monster Hunter");
        public static readonly FeatName MasterMonsterHunter = ModManager.RegisterFeatName("RE_MasterMonsterHunter", "Master Monster Hunter");
        public static readonly FeatName AnimalFeature = ModManager.RegisterFeatName("RE_AnimalFeature", "Animal Feature");
        public static readonly FeatName ExperiencedTracker = ModManager.RegisterFeatName("RE_ExperiencedTracker", "Experienced Tracker");
        public static readonly FeatName CommitmentToJustice = ModManager.RegisterFeatName("RE_CommitmentToJustice", "Commitment to Justice");
        public static readonly FeatName CommitmentToEquality = ModManager.RegisterFeatName("RE_CommitmentToEquality", "Commitment to Equality");
        public static readonly FeatName GoldenErinysStance = ModManager.RegisterFeatName("RE_GoldenErinysStance", "Golden Erinys Stance");
        public static readonly FeatName BlessedCounterstrike = ModManager.RegisterFeatName("RE_BlessedCounterstrike", "Blessed Counterstrike");
        public static readonly FeatName ExpandAura = ModManager.RegisterFeatName("RE_ExpandAura", "Expand Aura");
        public static readonly FeatName BlessedSwiftness = ModManager.RegisterFeatName("RE_BlessedSwiftness", "Blessed Swiftness");
    }

    public static class MQEffectIds
    {
        public static QEffectId BlindingPoison { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_BlindingPoison");
        public static QEffectId Massaged { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Massaged");
        public static QEffectId CantTrigger { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_CantTrigger");
        public static QEffectId Disbelief { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Disbelief");
        public static QEffectId ConcealedBy { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_ConcealedBy");
        public static QEffectId FlankingFig { get; } =  ModManager.RegisterEnumMember<QEffectId>("RE_FlankingFig");
        public static QEffectId FlankAdd { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_FlankAdd");
        public static QEffectId DesignatedAlly { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_DesignatedAlly");
        public static QEffectId GuardedMindUsed { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_GuardedMindUsed");
        public static QEffectId PlankWalked { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_PlankWalked");
        public static QEffectId NothingPersonal { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_NothingPersonal");
        public static QEffectId Counter { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Counter");
        public static QEffectId Illuminate { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Illuminate");
        public static QEffectId IncreasedReach { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_IncreasedReach");
        public static QEffectId LungingReach { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_LungingReach");
        public static QEffectId TacticianCharges { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_TacticianCharges");
        public static QEffectId TacticianUsed { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_TacticianUsed");
        public static QEffectId MasterMonsterHunter { get; } =  ModManager.RegisterEnumMember<QEffectId>("RE_MasterMonsterHunter");
        public static QEffectId LegendaryMonsterHunter { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_MonsterHunterLegendary");
        public static QEffectId MonsterWarden { get; } =  ModManager.RegisterEnumMember<QEffectId>("RE_MonsterWarden");
        public static QEffectId MonsterHunterUsed { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_MonsterHunterUsed");
        public static QEffectId AnimalStrength  { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_AnimalStrength");
        public static QEffectId AnimalFeatureClaws { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_AnimalFeatureClaws");
        public static QEffectId CommitmentToJustice { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_CommitmentToJustice");
        public static QEffectId CommitmentToEquality { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_CommitmentToEquality");
        public static QEffectId Talmandor { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Talmandor");
        public static QEffectId Cassisian { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Cassisian");
        public static QEffectId GoldenStance { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_GoldenStance");
        public static QEffectId PromiseOfPain { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_PromiseOfPain");
        public static QEffectId Wrathful { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_Wrathful");
        public static QEffectId ChampionAura { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_ChampionAura");
        public static QEffectId ExpandAura { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_ExpandAura");
        public static QEffectId DivineHealth { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_DivineHealth");
        public static QEffectId ChampionReactedAgainst { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_ChampionReactedAgainst");
        public static QEffectId DefendedAgainst { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_DefendedAgainst");
        public static QEffectId InvokedOath { get; } = ModManager.RegisterEnumMember<QEffectId>("RE_InvokedOath");
    }

    public static class MIllustrations
    {
        public static readonly Illustration AlbatrossCurse = new ModdedIllustration("PMAssets/AlbatrossCurse.png");
        public static readonly Illustration Hidebound = new ModdedIllustration("PMAssets/Hidebound.png");
        public static readonly Illustration LiberatingCommand = new ModdedIllustration("PMAssets/LiberatingCommand.png");
        public static readonly Illustration PyrefowlRebuke = new ModdedIllustration("PMAssets/PyrefowlRebuke.png");
        public static readonly Illustration StickyFire = new ModdedIllustration("PMAssets/StickyFire.png");
        public static readonly Illustration CurseOfRecoil =  new ModdedIllustration("PMAssets/CurseOfRecoil.png");
        public static Illustration CreateIllustration(string illustrationName)
        {
            return new ModdedIllustration($"PMAssets/{illustrationName}.png");
        }
    }

    public static class MSoundEffects
    {
        public static readonly SfxName Croak = ModManager.RegisterNewSoundEffect("PMAssets/FrogCroak.mp3", 2);
    }
    
    public static class MSubmenuIds
    {
        public static readonly SubmenuId CampfireChronicler = ModManager.RegisterEnumMember<SubmenuId>("RE_CampfireChronicler");
        public static readonly SubmenuId FaithSymbol = ModManager.RegisterEnumMember<SubmenuId>("RE_FaithSymbol");
    }

    public static class MSectionIds
    {
        public static readonly PossibilitySectionId Stories = ModManager.RegisterEnumMember<PossibilitySectionId>("RE_Stories");
    }

    public static class MActionIds
    {
        public static readonly ActionId PsychicIgnition = ModManager.RegisterEnumMember<ActionId>("RE_PsychicIgnition"); 
    }

    public static DamageKind SpiritDamage => DamageSpirit.Spirit;

    extension(DamageKind)
    {
        public static DamageKind Spirit => SpiritDamage;
    }
    
}