using System;
using System.Linq;
using System.Threading.Tasks;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Roller;
using Dawnsbury.Modding;
using RemasterExpanded.ClassChangesAndFeats;
using SpiritDamage;

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
                case ItemName.HolyRunestone:
                    ModifyHolyRune(item);
                    break;
                case ItemName.UnholyRunestone:
                    ModifyUnholyRune(item);
                    break;
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
                case ItemName.HolyRunestone:
                    ModifyHolyRune(item);
                    break;
                case ItemName.UnholyRunestone:
                    ModifyUnholyRune(item);
                    break;
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
                        : null,
                Traits = [Trait.Polymorph]
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

    public static void ModifyHolyRune(Item rune)
    {
        rune.Traits.RemoveAll(tr => tr is Trait.Good or Trait.Evocation);
        rune.Traits.Add(HolyTrait.Holy);
        rune.Traits.Add(ModData.ModTrait);
        rune.WithRuneProperties(new RuneProperties("holy", RuneKind.WeaponProperty, "The enchanted weapon commands powerful celestial energy.", 
            $"Strikes made with the weapon gain the {ChampionRemaster.HolyTooltip} trait and deal an extra 1d4 spirit damage, or an extra 2d4 against an {ChampionRemaster.UnholyTooltip} target." +
            $"\n\nOnce per day, on a critical hit against an unholy creature, you can also heal HP equal to double the unholy creature's level as a reaction (concentrate, healing, vitality). If you are unholy yourself, you are enfeebled 2 while wielding this weapon.", 
            weapon =>
        {
            weapon.WeaponProperties?.WithOnTarget(async (_, caster, target, result) =>
            {
                if (result != CheckResult.CriticalSuccess || !target.HasTrait(UnholyTrait.Unholy))
                    return;
                int num = Math.Min(2 * target.Level, caster.Damage);
                CombatAction fake = CombatAction
                    .CreateSimple(caster, "Holy weapon", Trait.Positive, Trait.Healing, Trait.Concentrate)
                    .WithActionCost(0);
                if (num <= 0 || caster.PersistentUsedUpResources.UsedUpActions.Contains("HolyRuneHealing") || !fake.CanBeginToUse(caster))
                    return;
                if (!await caster.AskToUseReaction($"You scored a critical hit against an unholy creature with a holy weapon. Heal {num} HP as a reaction?"))
                    return;
                caster.PersistentUsedUpResources.UsedUpActions.Add("HolyRuneHealing");
                await caster.HealAsync(DiceFormula.FromText((2 * target.Level).ToString(), "Holy weapon"), fake);
            });
            weapon.WithOnStateCheckWhenWielded((wielder, item) =>
            {
                wielder.AddQEffect(new QEffect
                {
                    AddExtraKindedDamageOnStrike = (action, target) => action.Item != item
                        ? null
                        :
                        target.HasTrait(UnholyTrait.Unholy)
                            ?
                            new KindedDamage(DiceFormula.FromText("2d4", item.Name), DamageKind.Spirit)
                            : new KindedDamage(DiceFormula.FromText("1d4", item.Name), DamageKind.Spirit),
                    AdjustStrikeAction = (_, action) =>
                    {
                        if (action.Item == item)
                            action.WithExtraTrait(HolyTrait.Holy);
                    }
                }.WithExpirationEphemeral());
                if (!wielder.HasTrait(UnholyTrait.Unholy))
                    return;
                QEffect qEffect = QEffect.Enfeebled(2).WithExpirationEphemeral();
                qEffect.Description += " You're enfeebled because you're unholy and you're wielding a holy weapon.";
                wielder.AddQEffect(qEffect);
            });
        })
            .WithCanBeAppliedTo((_, baseItem) =>
            {
                if (baseItem.Runes.All(rune4 => rune4.ItemName != ItemName.UnholyRunestone)) return null;
                WeaponProperties? weaponProperties = baseItem.WeaponProperties;
                return (weaponProperties != null ? weaponProperties.ItemBonus >= 2 ? 1 : 0 : 0) != 0 ? "You cannot enchant a weapon with a holy rune if it is unholy." : null;
            }));
    }

    public static void ModifyUnholyRune(Item rune)
    {
        rune.Traits.RemoveAll(tr => tr is Trait.Evil or Trait.Evocation);
        rune.Traits.Add(UnholyTrait.Unholy);
        rune.Traits.Add(ModData.ModTrait);
        rune.WithRuneProperties(new RuneProperties("unholy", RuneKind.WeaponProperty, "The enchanted weapon commands powerful demonic energy.", 
            $"Strikes made with the weapon gain the {ChampionRemaster.UnholyTooltip} trait and deal an extra 1d4 spirit damage, or an extra 2d4 against an {ChampionRemaster.HolyTooltip} target." +
            $"\n\nOnce per day, on a critical hit against a holy creature, you can spend a {{icon:Reaction}} reaction (concentrate) to cause the target to take an additional 1d8 persistent bleeding damage per weapon damage die. If you are holy yourself, you are enfeebled 2 while wielding this weapon.", 
            weapon =>
        {
            weapon.WeaponProperties?.WithOnTarget(async (spell, caster, target, result) =>
            {
                CombatAction fake = CombatAction.CreateSimple(caster, "unholy", Trait.Concentrate).WithActionCost(0);
                if (result != CheckResult.CriticalSuccess || !target.HasTrait(HolyTrait.Holy) || caster.PersistentUsedUpResources.UsedUpActions.Contains("UnholyRuneBleeding") || !fake.CanBeginToUse(caster))
                    return;
                if (!await caster.AskToUseReaction($"You scored a critical hit against a holy creature with an unholy weapon. Deal an additional {spell.Item?.WeaponProperties?.DamageDieCount ?? 1}d8 persistent bleeding damage as a reaction?"))
                    return;
                caster.PersistentUsedUpResources.UsedUpActions.Add("UnholyRuneBleeding");
                target.AddQEffect(QEffect.PersistentDamage((spell.Item?.WeaponProperties?.DamageDieCount ?? 1) + "d8", DamageKind.Bleed));
            });
            weapon.WithOnStateCheckWhenWielded((wielder, item) =>
            {
                wielder.AddQEffect(new QEffect
                {
                    AddExtraKindedDamageOnStrike = (action, target) => action.Item != item
                        ? null
                        :
                        target.HasTrait(HolyTrait.Holy)
                            ?
                            new KindedDamage(DiceFormula.FromText("2d4", item.Name), DamageKind.Spirit)
                            : new KindedDamage(DiceFormula.FromText("1d4", item.Name), DamageKind.Spirit),
                    AdjustStrikeAction = (_, action) =>
                    {
                        if (action.Item == item)
                            action.WithExtraTrait(UnholyTrait.Unholy);
                    }
                }.WithExpirationEphemeral());
                if (!wielder.HasTrait(HolyTrait.Holy))
                    return;
                QEffect qEffect = QEffect.Enfeebled(2).WithExpirationEphemeral();
                qEffect.Description += " You're enfeebled because you're holy and you're wielding a unholy weapon.";
                wielder.AddQEffect(qEffect);
            });
        })
            .WithCanBeAppliedTo((_, baseItem) =>
            {
                if (baseItem.Runes.All(rune4 => rune4.ItemName != ItemName.HolyRunestone)) return null;
                WeaponProperties? weaponProperties = baseItem.WeaponProperties;
                return (weaponProperties != null ? weaponProperties.ItemBonus >= 2 ? 1 : 0 : 0) != 0 ? "You cannot enchant a weapon with a unholy rune if it is holy." : null;
            }));
    }
}