using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Mods.DawnniExpanded;

namespace RemasterExpanded;

public class DawnniPatch
{
    public static void LoadDawnniPatch()
    {
        foreach (Feat feat in AllFeats.All.Where(f => f.Traits.Any(t => t == FeatArchetype.ArchetypeTrait || t == FeatArchetype.DedicationTrait) ||
                                                      (f.Name.Contains("Familiar") && f.Traits.Any(t => t == DawnniExpanded.DETrait))))
        {
            feat.Traits.Clear();
        }
    }

    public static void LoadDawnniPatch2()
    {
        List<Item> shop = Items.ShopItems;
        foreach (Item item in shop.Where(item => item.HasTrait(DawnniExpanded.DETrait) && item.Name.Contains("Mutagen") && !item.Name.Contains("Serene")).ToList())
        {
            shop.Remove(item);
        }
        foreach (Item item in shop.Where(item => item.HasTrait(DawnniExpanded.DETrait) && item.Name.Contains("Serene")))
        {
            item.ProsaicName = item.ProsaicName.Contains("Lesser") ? "lesser serene mutagen" : "moderate serene mutagen";
            item.WithItemGreaterGroup(ItemGreaterGroup.Mutagens);
        }
        Items.ShopItems = shop;
    }

    public static void LoadDawnniPatch3()
    {
        AllFeats.All.Remove(Witch.WitchClass);
    }
}