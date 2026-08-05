using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using static RemasterExpanded.ModData;

// ReSharper disable RedundantAlwaysMatchSubpattern

namespace RemasterExpanded.MySpells;

public abstract class ConfluxSpells
{
    public static void Load()
    {
        ModManager.RegisterActionOnEachSpell(spell =>
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            bool inCombat = spell.Owner != null && spell.Owner.Battle != TBattle.Pseudobattle;
            if (spell.SpellId == SpellId.DimensionalAssault)
            {
                spell.WithHeightenedAtSpecificLevel(spell.SpellLevel, 5, inCombat, "The range is equal to your speed.");
                if (spell is { Owner: not null}) 
                    spell.Target = Target.TileYouCanSeeAndTeleportTo(spell.SpellLevel >= 5 ? spell.Owner.Speed : spell.Owner.Speed / 2).WithAdditionalTargetingRequirement(TileWithEnemyWithinReach());

                if (spell.SpellLevel >= 5) spell.Description = spell.Description.Replace("half your Speed", "your Speed");
                spell.WithExtraTrait(ModTrait);
            }
            if (spell.SpellId == SpellId.HastedAssault)
            {
                spell.EffectOnOneTarget = null;
                spell.WithEffectOnEachTarget(async (action, caster, _, _) => caster.AddQEffect(Level3Spells.HasteQEffect().WithDispellable(action)));
                spell.Description = spell.Description.Replace("only to Strike", "only to Stride or Strike");
                spell.WithExtraTrait(ModTrait);
            }
        });
        ModManager.ReplaceExistingSpell(SpellId.CascadeCountermeasure, 0, (_, level, inCombat, _) =>
        {
            int heighten = (level - level % 3) / 3;
            return Spells.CreateModern(IllustrationName.SpellImmunity, "Conjurer's Countermeasure", [Trait.Uncommon, Trait.Focus, Trait.Magus, Trait.SomaticOnly, Trait.SpellWithDuration],
                "You quickly create a barrier that protects you against the magic of spellcraft.",
                $"You gain resistance {S.HeightenedVariable(heighten * 5, 5)} against damage from spells until the end of your next turn.\n\nThe resistance from this spell is cumulative with that of Arcane Cascade. If you're in Arcane Cascade stance when the duration of {{i}}conjurer's countermeasure{{/i}} expires, the spell's duration extends until Arcane Cascade ends.",
                Target.Self(), level, null)
                .WithActionCost(1)
                .WithHeighteningNumerical(level, 3, inCombat, 3, "The resistance increases by 5.")
                .WithSoundEffect(SfxName.ShieldSpell)
                .WithEffectOnSelf(async (_, caster) =>
                {
                    int resist = heighten * 5;
                    bool flag = caster.HasEffect(QEffectId.ArcaneCascade);
                    int amount = caster.HasEffect(QEffectId.GreaterWeaponSpecialization) ? 3 :
                        caster.HasEffect(QEffectId.WeaponSpecialization) ? 2 : 1;
                    if (flag)
                    {
                        resist += amount;
                    }
                    QEffect effect = new("Conjurer's Countermeasure", $"You gain resistance {resist} against damage from spells.", ExpirationCondition.ExpiresAtEndOfYourTurn, caster, IllustrationName.SpellImmunity)
                    {
                        StateCheck = qf =>
                        {
                            if (caster.WeaknessAndResistance.Resistances.FirstOrDefault(resistance => resistance is SpecialResistance {Name: "Damage from spells"} && resistance.Value == amount) is not {} cascadeResist)
                            {
                                caster.WeaknessAndResistance.AddSpecialResistance("Damage from spells",
                                    (action, _) => action != null && action.HasTrait(Trait.Spell), resist, null);
                            }
                            else
                            {
                                cascadeResist.Value = resist;
                            }
                            if (!qf.CannotExpireThisTurn)
                            {
                                qf.CannotExpireThisTurn = qf.Owner.HasEffect(QEffectId.ArcaneCascade);
                            }
                        },
                        CannotExpireThisTurn = true,
                        StateCheckLayer = 1
                    };
                    caster.AddQEffect(effect);
                });
        });
    }

    public static Func<Creature, Tile, Usability> TileWithEnemyWithinReach()
    {
        return (creature, tile) =>
            creature.Battle.AllCreatures.Any(cr =>
                cr.EnemyOf(creature) && cr.DistanceToWith10FeetException(tile) <= creature.Space.ActualReach)
                ? Usability.Usable
                : Usability.NotUsableOnThisCreature("No enemy in reach.");
    }
}