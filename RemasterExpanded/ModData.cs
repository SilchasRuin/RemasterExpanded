using Dawnsbury.Audio;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using SpiritDamage;

namespace RemasterExpanded;

public class ModData
{
    public static bool Remaster { get; } = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Dawnsbury.Mods.Remaster.FeatsDb");
    public static bool MoreSpells { get; } = ModManager.TryParse("PE_ForbiddenThought", out QEffectId _) && ModManager.TryParse("SH_Spellheart", out Trait _);
    public static class MTraits
    {
        public static readonly Trait VikingGuard = ModManager.RegisterTrait("RE_VikingGuard", new TraitProperties("Viking Guard", false));
        public static readonly Trait Garment =  ModManager.RegisterTrait("RE_Garment", new TraitProperties("Garment", false));
        public static readonly Trait UnifiedMagicalTheory = ModManager.RegisterTrait("RE_UnifiedMagicalTheory", new TraitProperties("Unified Magical Theory", false));
        public static readonly Trait BlackJacket = ModManager.RegisterTrait("RE_BlackJacket", new TraitProperties("Blackjacket", false));
        public static readonly Trait Pirate = ModManager.RegisterTrait("RE_Pirate", new TraitProperties("Pirate", false));
        public static readonly Trait Viking = ModManager.RegisterTrait("RE_Viking", new TraitProperties("Viking", false));
        public static readonly Trait Sanctified = ModManager.RegisterTrait("RE_Sanctified", new TraitProperties("Sanctified", true, "If you are good your sanctified actions and spells gain the holy trait, if you are evil your sanctified actions and spells gain the unholy trait,."));
    }

    public static class MFeatNames
    {
        public static readonly FeatName HandOfTheApprentice = ModManager.RegisterFeatName("RE_HandOfTheApprenticeFT", "Hand of the Apprentice");
        public static readonly FeatName MercenaryMotivation = ModManager.RegisterFeatName("RE_MercenaryMotivation", "Mercenary Motivation");
        public static readonly FeatName GuardsFury = ModManager.RegisterFeatName("RE_GuardsFury", "Guard's Fury");
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
    }

    public static class MIllustrations
    {
        public static readonly Illustration FlensingSlice = new ModdedIllustration("PMAssets/FlensingSlice.png");
        public static readonly Illustration GlueBomb = new ModdedIllustration("PMAssets/GlueBomb.png");
        public static readonly Illustration FrostVial = new ModdedIllustration("PMAssets/FrostVial.png");
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

    public static DamageKind SpiritDamage => DamageSpirit.Spirit;
}