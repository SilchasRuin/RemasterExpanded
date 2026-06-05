using System;
using System.Linq;
using System.Threading.Tasks;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Modding;

namespace RemasterExpanded;

public class UpdateItems
{
    public static void Load()
    {
        Items.ShopItems = Items.ShopItems.Select(item =>
        {
            switch (item.ItemName)
            {
                case ItemName.DrakeheartMutagenLesser or ItemName.DrakeheartMutagenModerate
                    or ItemName.DrakeheartMutagenMajor or ItemName.DrakeheartMutagenGreater:
                {
                    string replace = item.Description?.Replace("Will saves and Reflex saves", "Will saves, Reflex saves, and all skill checks to Recall Knowledge") ?? "";
                    item.Description = replace;
                    break;
                }
                case ItemName.BestialMutagenLesser or ItemName.BestialMutagenModerate
                    or ItemName.BestialMutagenGreater or ItemName.BestialMutagenMajor:
                {
                    int bonus = item.ItemName switch
                    {
                        ItemName.BestialMutagenLesser => 1,
                        ItemName.BestialMutagenModerate => 2,
                        ItemName.BestialMutagenGreater => 3,
                        ItemName.BestialMutagenMajor => 4,
                        _ => 0
                    };
                    string claws = bonus + "d" + Math.Min(8, 2 * bonus + 2);
                    string jaws = bonus + "d" + Math.Min(10, 2 * bonus + 4);
                    string description = $"{{i}}Benefit{{/i}} — You gain a +{{b}}{bonus}{{/b}} item bonus to Athletics checks and unarmed attack rolls. You gain a jaws unarmed attack ({{b}}{jaws}{{/b}} piercing damage) and a claw unarmed attack ({{b}}{claws}{{/b}} slashing damage, agile). Striking runes don't modify the damage caused by these attacks." +
                                         $"{(bonus >= 4 ? " You gain weapon specialization with the claw and jaws, or greater weapon specialization if you already have weapon specialization with these unarmed attacks." : "")}" +
                                         $"\n\n{{i}}Drawback{{/i}} — You take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks." +
                                         $"\n\nThese effects last for the rest of the encounter.";
                    item.Description = description;

                    item.WhenYouDrink = async (_, creature) => await BestialDrink(creature, item, bonus, jaws, claws);
                    break;
                }
            }

            return item;
        }).ToList();
        ModManager.RegisterActionOnEachItem(item =>
        {
            switch (item.ItemName)
            {
                case ItemName.DrakeheartMutagenLesser or ItemName.DrakeheartMutagenModerate
                    or ItemName.DrakeheartMutagenMajor or ItemName.DrakeheartMutagenGreater:
                {
                    string replace = item.Description?.Replace("Will saves and Reflex saves", "Will saves, Reflex saves, and all skill checks to Recall Knowledge") ?? "";
                    item.Description = replace;
                    break;
                }
                case ItemName.BestialMutagenLesser or ItemName.BestialMutagenModerate
                    or ItemName.BestialMutagenGreater or ItemName.BestialMutagenMajor:
                {
                    int bonus = item.ItemName switch
                    {
                        ItemName.BestialMutagenLesser => 1,
                        ItemName.BestialMutagenModerate => 2,
                        ItemName.BestialMutagenGreater => 3,
                        ItemName.BestialMutagenMajor => 4,
                        _ => 0
                    };
                    string claws = bonus + "d" + Math.Min(8, 2 * bonus + 2);
                    string jaws = bonus + "d" + Math.Min(10, 2 * bonus + 4);
                    string description = $"{{i}}Benefit{{/i}} — You gain a +{{b}}{bonus}{{/b}} item bonus to Athletics checks and unarmed attack rolls. You gain a jaws unarmed attack ({{b}}{jaws}{{/b}} piercing damage) and a claw unarmed attack ({{b}}{claws}{{/b}} slashing damage, agile). Striking runes don't modify the damage caused by these attacks." +
                                         $"{(bonus >= 4 ? " You gain weapon specialization with the claw and jaws, or greater weapon specialization if you already have weapon specialization with these unarmed attacks." : "")}" +
                                         $"\n\n{{i}}Drawback{{/i}} — You take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks." +
                                         $"\n\nThese effects last for the rest of the encounter.";
                    item.Description = description;
                    item.WhenYouDrink = async (_, creature) => await BestialDrink(creature, item, bonus, jaws, claws);
                    break;
                }
            }

            return item;
            
        });
        ModManager.RegisterActionOnEachCreature(creature =>
        {
            creature.AddQEffect(new QEffect
            {
                YouAcquireQEffect = (_, mutagen) =>
                {
                    if (mutagen.Id != QEffectId.DrakeheartMutagen) return mutagen;
                    mutagen.BonusToSkillChecks = (_, action, _) =>
                        action.Name.ToLower() is { } actName
                        && (actName.Contains("recall knowledge")
                            || actName.Contains("recall weakness"))
                            ? new Bonus(-1, BonusType.Untyped, "Untyped (Drakeheart Mutagen)")
                            : null;
                    string? replace = mutagen.Description?.Replace("Will saves and Reflex saves",
                        "Will saves, Reflex saves, and all skill checks to Recall Knowledge");
                    mutagen.Description = replace;
                    return mutagen;
                }
            });
        });
    }
    public static async Task BestialDrink(Creature creature, Item item, int bonus, string jaws, string claws)
    {
        {
            QEffect bestialClaw = new()
            {
                DoNotShowUpOverhead = true,
                AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.DragonClaws, "claw", claws,
                    DamageKind.Slashing, Trait.Agile, Trait.BattleformAttack)
            };
            QEffect bestialJaws = new()
            {
                DoNotShowUpOverhead = true,
                AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.Jaws, "jaws", jaws,
                    DamageKind.Piercing, Trait.BattleformAttack)
            };
            var apply = true;
            QEffect bestial = new(item.Name,
                $"You gain a +{{b}}{bonus}{{/b}} item bonus to Athletics checks and unarmed attack rolls. You gain a jaws unarmed attack ({{b}}{jaws}{{/b}} piercing damage) and a claw unarmed attack ({{b}}{claws}{{/b}} slashing damage, agile). Striking runes don't modify the damage caused by these attacks." +
                $"\n\nYou take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks.",
                ExpirationCondition.Never, creature, item.Illustration)
            {
                StateCheck = qf =>
                {
                    qf.Owner.AddQEffect(bestialClaw.WithExpirationEphemeral());
                    qf.Owner.AddQEffect(bestialJaws.WithExpirationEphemeral());
                    if (bonus < 4)
                        return;
                    if (!apply)
                        return;
                    qf.Owner.AddQEffect(!qf.Owner.HasEffect(QEffectId.WeaponSpecialization)
                        ? BestialSpecialization()
                        : BestialSpecialization(true));
                    apply = false;
                },
                Id = QEffectId.BestialMutagen,
                BonusToDefenses = (_, _, def) =>
                    def is Defense.Reflex ? new Bonus(-2, BonusType.Untyped, "Untyped (Bestial Mutagen)", false) : null,
                BonusToSkills = skill =>
                    skill switch
                    {
                        Skill.Athletics => new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true),
                        Skill.Acrobatics or Skill.Stealth => new Bonus(-2, BonusType.Untyped,
                            "Untyped (Bestial Mutagen)", false),
                        _ => null
                    },
                BonusToAttackRolls = (_, action, _) =>
                    action.Item != null && action.Item.HasTrait(Trait.Unarmed) && action.HasTrait(Trait.Attack)
                        ? new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true)
                        : null
            };
            creature.AddQEffect(bestial);
        }
    }

    public static QEffect BestialSpecialization(bool greater = false)
    {
        {
            int basicIncrease = greater ? 4 : 2;
            int step = greater ? 2 : 1;
            string specializationName = greater ? "Greater weapon specialization" : "Weapon specialization";
            return new QEffect(specializationName, $"You deal an additional {basicIncrease} damage with jaws and claws attacks in which you have expert proficiency. This damage increases to {basicIncrease + step} if you're a master, and {basicIncrease + 2 * step} if you're legendary.")
            {
                Id = greater ? QEffectId.GreaterWeaponSpecialization : QEffectId.WeaponSpecialization,
                BonusToDamage = (_, action, _) =>
                {
                    if (action.Item == null || (!action.Item.Name.Contains("claw") && !action.Item.Name.Contains("jaw")))
                        return null;
                    Proficiency proficiency = action.Owner.Proficiencies.Get(action.Item.Traits);
                    return proficiency >= Proficiency.Expert ? new Bonus(proficiency switch
                    {
                        Proficiency.Expert => 2,
                        Proficiency.Master => 3,
                        Proficiency.Legendary => 4,
                        _ => 0
                    }, BonusType.Untyped, specializationName) : null;
                }
            };
        }
    }
}