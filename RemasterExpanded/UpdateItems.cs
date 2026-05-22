using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
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
                                         $"\n\n{{i}}Drawback{{/i}} — You take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks." +
                                         $"\n\nThese effects last for the rest of the encounter.";
                    item.Description = description;
                
                    item.WhenYouDrink = async (_, creature) =>
                    {
                        QEffect bestialClaw = new()
                        {
                            DoNotShowUpOverhead = true,
                            AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.DragonClaws, "claw", claws, DamageKind.Slashing, Trait.Agile, Trait.BattleformAttack)
                        };
                        QEffect bestialJaws = new()
                        {
                            DoNotShowUpOverhead = true,
                            AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.Jaws, "jaws", jaws, DamageKind.Piercing, Trait.BattleformAttack)
                        };
                        QEffect bestial = new(item.Name, $"You gain a +{{b}}{bonus}{{/b}} item bonus to Athletics checks and unarmed attack rolls. You gain a jaws unarmed attack ({{b}}{jaws}{{/b}} piercing damage) and a claw unarmed attack ({{b}}{claws}{{/b}} slashing damage, agile). Striking runes don't modify the damage caused by these attacks." +
                                                         $"\n\nYou take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks.", ExpirationCondition.Never, creature, item.Illustration)
                        {
                            StateCheck = qf => 
                            { 
                                qf.Owner.AddQEffect(bestialClaw.WithExpirationEphemeral()); 
                                qf.Owner.AddQEffect(bestialJaws.WithExpirationEphemeral());
                            },
                            Id = QEffectId.BestialMutagen,
                            BonusToDefenses = (_,_,def) =>
                                def is Defense.Reflex ? new Bonus(-2, BonusType.Untyped, "Untyped (Bestial Mutagen)", false) : null,
                            BonusToSkills = skill =>
                                skill switch
                                {
                                    Skill.Athletics => new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true),
                                    Skill.Acrobatics or Skill.Stealth => new Bonus(-2, BonusType.Untyped, "Untyped (Bestial Mutagen)", false),
                                    _ => null
                                },
                            BonusToAttackRolls = (_, action, _) => action.Item != null && action.Item.HasTrait(Trait.Unarmed) && action.HasTrait(Trait.Attack) ? new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true) : null
                        };
                        creature.AddQEffect(bestial);

                    };
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
                                         $"\n\n{{i}}Drawback{{/i}} — You take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks." +
                                         $"\n\nThese effects last for the rest of the encounter.";
                    item.Description = description;
                
                    item.WhenYouDrink = async (_, creature) =>
                    {
                        QEffect bestialClaw = new()
                        {
                            DoNotShowUpOverhead = true,
                            AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.DragonClaws, "claw", claws, DamageKind.Slashing, Trait.Agile, Trait.BattleformAttack)
                        };
                        QEffect bestialJaws = new()
                        {
                            DoNotShowUpOverhead = true,
                            AdditionalUnarmedStrike = CommonItems.CreateNaturalWeapon(IllustrationName.Jaws, "jaws", jaws, DamageKind.Piercing, Trait.BattleformAttack)
                        };
                        QEffect bestial = new(item.Name.CapitalizeEachWord(), $"You gain a +{{b}}{bonus}{{/b}} item bonus to Athletics checks and unarmed attack rolls. You gain a jaws unarmed attack ({{b}}{jaws}{{/b}} piercing damage) and a claw unarmed attack ({{b}}{claws}{{/b}} slashing damage, agile). Striking runes don't modify the damage caused by these attacks." +
                                                         $"\n\nYou take a –2 penalty to Reflex saves, Acrobatics checks, and Stealth checks.", ExpirationCondition.Never, creature, item.Illustration)
                        {
                            StateCheck = qf => 
                            { 
                                qf.Owner.AddQEffect(bestialClaw.WithExpirationEphemeral()); 
                                qf.Owner.AddQEffect(bestialJaws.WithExpirationEphemeral());
                            },
                            Id = QEffectId.BestialMutagen,
                            BonusToDefenses = (_,_,def) =>
                                def is Defense.Reflex ? new Bonus(-2, BonusType.Untyped, "Untyped (Bestial Mutagen)", false) : null,
                            BonusToSkills = skill =>
                                skill switch
                                {
                                    Skill.Athletics => new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true),
                                    Skill.Acrobatics or Skill.Stealth => new Bonus(-2, BonusType.Untyped, "Untyped (Bestial Mutagen)", false),
                                    _ => null
                                },
                            BonusToAttackRolls = (_, action, _) => action.Item != null && action.Item.HasTrait(Trait.Unarmed) && action.HasTrait(Trait.Attack) ? new Bonus(bonus, BonusType.Item, "Bestial Mutagen", true) : null
                        };
                        creature.AddQEffect(bestial);
                    };
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
}