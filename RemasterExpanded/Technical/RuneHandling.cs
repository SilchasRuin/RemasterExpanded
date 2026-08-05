using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Treasure;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.Technical;

public class RuneHandling
{
    public static QEffect AnimalStrengthRunes()
        {
            var applied = false;
            return new QEffect
            {
                StateCheck = qf =>
                {
                    Creature self = qf.Owner;
                    if (!self.HasEffect(MQEffectIds.AnimalFeatureClaws) || applied)
                        return;
                    applied = true;
                    Item? bestWeapon = DetermineBestWeapon(self);
                    Item? wraps = StrikeRules.GetBestHandwraps(self);
                    if (self.Weapons.FirstOrDefault(wp => wp.HasTrait(MTraits.AnimalWeapon)) is not {} animalWeapon || bestWeapon == null) return;
                    if (wraps != null)
                    {
                        bestWeapon = ReturnBetterItem(bestWeapon, wraps);
                        if (wraps == bestWeapon)
                            return;
                    }
                    ResetWeapons(animalWeapon);
                    HandleRune(bestWeapon, animalWeapon, RuneKind.WeaponPotency);
                    HandleRune(bestWeapon, animalWeapon, RuneKind.WeaponStriking);
                    HandleRune(bestWeapon, animalWeapon, RuneKind.WeaponProperty);
                    int dice = bestWeapon.WeaponProperties!.DamageDieCount;
                    int bonus = bestWeapon.WeaponProperties.ItemBonus;
                    if (dice <= 1 && bonus <= 0) return;
                    animalWeapon.WeaponProperties!.DamageDieCount =
                        Math.Max(animalWeapon.WeaponProperties.DamageDieCount, dice);
                    animalWeapon.WeaponProperties.ItemBonus =
                        Math.Max(animalWeapon.WeaponProperties.ItemBonus, bonus);
                    if (bestWeapon.Runes.Any(rune => rune.RuneProperties is
                            { RuneKind: RuneKind.WeaponProperty }))
                    {
                        foreach (Item rune in bestWeapon.Runes.Where(rune => rune.RuneProperties is
                                     { RuneKind: RuneKind.WeaponProperty }))
                        {
                            animalWeapon.ProsaicName = $"{{Blue}}{rune.RuneProperties!.Prefix}{{/Blue}} " + animalWeapon.Name;
                        }
                    }
                    if (dice > 1)
                    {
                        string? str3 = animalWeapon.WeaponProperties.DamageDieCount switch
                        {
                            2 => "striking",
                            3 => "greater striking",
                            4 => "major striking",
                            _ => null
                        };
                        if (str3 != null) animalWeapon.ProsaicName = $"{{Blue}}{str3}{{/Blue}} {animalWeapon.Name}";
                    }
                    if (bonus > 0)
                    {
                        animalWeapon.ProsaicName = $"{{Blue}}+{animalWeapon.WeaponProperties.ItemBonus}{{/Blue}} " + animalWeapon.Name;
                    }
                }
            };
        }
        public static void ResetWeapons(Item attack)
        {
            List<Trait> itemTraits = Items.TryGetItemTemplate(attack.ItemName, out Item? item) ? item.Traits : attack.Traits;
            attack.Runes.RemoveAll(rune => rune.RuneProperties is {RuneKind: RuneKind.WeaponPotency or RuneKind.WeaponStriking or RuneKind.WeaponProperty});
            attack.WeaponProperties = GenerateDefaultWeaponProperties(attack);
            attack.Traits = itemTraits;
            attack.ProsaicName = attack.BaseHumanName;
        }

        internal static void HandleRune(Item handwraps, Item attack, RuneKind type)
        {
            IEnumerable<Item> runes = handwraps.Runes.Where(rune => rune.RuneProperties != null && rune.RuneProperties.RuneKind == type);
            foreach (Item rune in runes)
            {
                if (rune.RuneProperties?.CanBeAppliedTo?.Invoke(rune, attack) != null) continue;
                attack.Runes.Add(rune);
                rune.RuneProperties?.ApplyRuneOntoItem(rune, attack);
            }
        }
        private static WeaponProperties GenerateDefaultWeaponProperties(Item attack)
        {
            List<Trait> itemTraits = Items.TryGetItemTemplate(attack.ItemName, out Item? item) ? item.Traits : attack.Traits;
            Item baseItem = new Item(attack.Illustration, attack.BaseHumanName, itemTraits.ToArray()).WithWeaponProperties(new WeaponProperties($"1d{attack.WeaponProperties!.DamageDieSize}", attack.WeaponProperties.DamageKind));
            if (attack.WeaponProperties.RangeIncrement > 0)
            {
                baseItem.WeaponProperties?.WithRangeIncrement(attack.WeaponProperties.RangeIncrement);
            }
            return baseItem.WeaponProperties!;
        }

        private static Item? DetermineBestWeapon(Creature self)
        {
            Item? weapon = (self.PersistentCharacterSheet?.Inventory.AllOuterItems.Where(item => item.WeaponProperties != null) ?? self.HeldItems.Where(item => item.WeaponProperties != null)).MaxBy(wp =>
                wp.WeaponProperties?.ItemBonus ?? wp.Level);
            Item? weapon2 = (self.PersistentCharacterSheet?.Inventory.AllOuterItems.Where(item => item.WeaponProperties != null) ?? self.HeldItems.Where(item => item.WeaponProperties != null)).MaxBy(wp =>
                wp.Runes.Count);
            if (weapon != weapon2 && weapon != null && weapon2 != null)
            {
                return ReturnBetterItem(weapon, weapon2);
            }
            return weapon;
        }

        private static Item ReturnBetterItem(Item weapon1, Item weapon2)
        {
            if (weapon1.WeaponProperties == null || weapon2.WeaponProperties == null)
                return weapon1;
            List<Item> weapons = [weapon1, weapon2];
            if (weapon1.WeaponProperties.ItemBonus == weapon2.WeaponProperties.ItemBonus)
                return weapons.MaxBy(wp => wp.Runes.Count) ?? weapon1;
            return weapon1.WeaponProperties.ItemBonus > weapon2.WeaponProperties.ItemBonus ? weapon1 : weapon2;

        }
}