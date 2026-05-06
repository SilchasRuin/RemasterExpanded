using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Mods.LoresAndWeaknesses;

namespace RemasterExpanded;

public abstract class RemasterLore
{
    public static Lore MaritimeLore { get; set; } = null!;
    public static Lore DevilLore { get; set; } = null!;
    public static Lore FiendLore { get; set; } = null!;
    public static Lore DemonLore { get; set; } = null!;
    public static void Load()
    {
        MaritimeLore = Lores.GetRegisteredLore("Maritime Lore", null) ?? Lores.RegisterNewLore("Maritime Lore",
            "You have sailed the seas and waterways of the world and have learned of the creatures that call them home." +
            $"\n\nYou can use this skill to {RecallWeakness.GetActionLink()} on aquatic and water-based creatures.",
            (_, target) => target.HasTrait(Trait.Aquatic) || target.HasTrait(Trait.Water) || target.HasTrait(Trait.Amphibious));
        DevilLore = Lores.RegisterNewLore("Devil Lore",
            "You have researched the malignant creatures of the Hells, the fearsome Devils." +
            $"\n\nYou can use this skill to {RecallWeakness.GetActionLink()} on devil creatures.", 
            (_, target) => target.HasTrait(Trait.Devil), isSpecific: true);
        FiendLore = Lores.RegisterNewLore("Fiend Lore",
            "You have delved into nature of monstrous fiends and now know the best ways to defeat them." +
            $"\n\nYou can use this skill to {RecallWeakness.GetActionLink()} on fiend creatures.",
            (_, target) => target.HasTrait(Trait.Fiend));
        DemonLore = Lores.RegisterNewLore("Demon Lore",
            "You have sought out forbidden lore and ancient knowledge, and now know of the Demons, the foul creatures born from the wretched Abyss." +
            $"\n\nYou can use this skill to {RecallWeakness.GetActionLink()} on demon creatures.",
            (_, target) => target.HasTrait(Trait.Demon), isSpecific: true);
    }
}