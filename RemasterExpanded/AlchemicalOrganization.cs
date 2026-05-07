using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Display.Illustrations;

namespace RemasterExpanded;

public abstract class AlchemicalOrganization
{
    public static void LoadOrganization()
    {
        Items.ShopItems = Items.ShopItems.Select(item =>
        {
            if (!item.HasTrait(Trait.Bomb) || item.ItemGreaterGroup == ItemGreaterGroup.Bombs) return item;
            item.WithItemGreaterGroup(ItemGreaterGroup.Bombs);
            if (item.ItemGroup == null)
                item.WithItemGroup("Lesser bombs");
            return item;
        }).ToList();
        Items.ShopItems = Items.ShopItems.Select(item =>
        {
            if (!item.HasTrait(Trait.Alchemical) || !IsLesserOrGreaterEtc(item.ProsaicName) || item.ItemGreaterGroup == ItemGreaterGroup.Bombs) return item;
            item.WithItemGroup($"{GetIllustration(item).IllustrationAsIconString} " + RemoveLesserOrGreaterEtc(item.ProsaicName));
            return item;
        }).ToList();
    }
    
    public static bool IsLesserOrGreaterEtc(string itemName)
    {
        return itemName.Contains("lesser") || itemName.Contains("greater") || itemName.Contains("moderate") ||  itemName.Contains("true") ||  itemName.Contains("minor") || itemName.Contains("major");
    }

    public static string RemoveLesserOrGreaterEtc(string itemName)
    {
        return itemName.Replace("lesser ", "").Replace("greater ", "").Replace("moderate ", "").Replace("minor ", "").Replace("major ", "").Replace("true ", "");
    }

    public static Illustration GetIllustration(Item item)
    {
        if (item.ProsaicName.Contains("lesser"))
            return item.Illustration;
        return Items.ShopItems.FirstOrDefault(i => i.ProsaicName.Contains("lesser") && i.ProsaicName.Contains(RemoveLesserOrGreaterEtc(item.ProsaicName)))?.Illustration ?? item.Illustration;
    }
}