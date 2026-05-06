using Dawnsbury.Audio;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using static RemasterExpanded.ModData;
using static RemasterExpanded.MySpells.SpellIds;

namespace RemasterExpanded.MySpells;

public abstract class NewSpells3rd : NewSpells
{
    public static void Load()
    {
        CroakVoice = ModManager.RegisterNewSpell("RE_CroakVoice", 3, (_, _, level, inCombat, _) =>
        {
            return Spells.CreateModern(MIllustrations.CreateIllustration("Croak"), "Croak Voice", [Trait.Concentrate, Trait.Curse, Trait.Manipulate, Trait.Morph, Trait.Arcane, Trait.Primal],
                    "You cause the target creature's vocal chords to swell like those of a frog.",
                    "The target must attempt a Fortitude save."+
                    S.FourDegreesOfSuccess("The target is unaffected.", "The target's voice becomes hoarse, and speaking becomes painful. Whenever it uses an action that has the auditory trait or attempts to Cast a Spell that doesn't have the subtle trait, it must succeed at a DC 5 flat check or the action is lost. Once per round, the target can spend an Interact action to massage its throat, attempting a Fortitude save against your spell DC. On a success, the spell ends.",
                        $"As success, but using an action with the auditory trait also deals {S.HeightenedVariable(2 + (level - 3), 2)}d10 mental damage to the target as the sound of its distorted voice grates on its ears.", " As failure, but the damage for using an action with the auditory trait is doubled, and the target can't use an Interact action to attempt a Fortitude save to end the effect early."),
                    Target.Ranged(6), level, SpellSavingThrow.Standard(Defense.Fortitude))
                .WithActionCost(2).WithSoundEffect(MSoundEffects.Croak)
                .WithHeighteningNumerical(level, 3, inCombat, 1, "The damage for using an action with the auditory trait increases by 1d10.")
                .WithProjectileCone(VfxStyle.NoAnimation())
                .WithEffectOnEachTarget((spell, caster, target, result) =>
                {
                    if (result == CheckResult.CriticalSuccess) return Task.CompletedTask;
                    var description = "Whenever you use an action that has the auditory trait or attempt to Cast a Spell that doesn't have the subtle trait, you must succeed at a DC 5 flat check or the action is lost.";
                    switch (result)
                    {
                        case CheckResult.Success:
                            description +=
                                $" Once per round, you can spend an Interact action to massage its throat, attempting a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.";
                            break;
                        case CheckResult.Failure:
                            description += $" You take {S.HeightenedVariable(2 + (level - 3), 2)}d10 mental damage when using an action with the auditory trait.";
                            description +=
                                $" Once per round, you can spend an Interact action to massage its throat, attempting a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.";
                            break;
                        case CheckResult.CriticalFailure:
                            description += $" You take {S.HeightenedVariable(2 * (2 + (level - 3)), 2*2)}d10 mental damage when using an action with the auditory trait.";
                            break;
                        case CheckResult.CriticalSuccess:
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, null);
                    }

                    QEffect croak = new("Croak Voice", description, ExpirationCondition.Never, caster,
                        spell.Illustration)
                    {
                        FizzleOutgoingActions = async (effect, action, builder) =>
                        {
                            if (!action.HasTrait(Trait.Auditory) &&
                                (!action.HasTrait(Trait.Spell) || action.HasTrait(Trait.SomaticOnly))) return false;
                            (CheckResult, string) tuple = Checks.RollFlatCheck(5);
                            if (action.HasTrait(Trait.Spell))
                                builder.AppendLine("Casting a spell while croaking: " + tuple.Item2);
                            else
                            {
                                builder.AppendLine("Using an auditory action while croaking: " + tuple.Item2);
                            }
                            Sfxs.Play(MSoundEffects.Croak);
                            if (result <= CheckResult.Failure && action.HasTrait(Trait.Auditory))
                            {
                                await CommonSpellEffects.DealBasicDamage(spell, caster, effect.Owner, result, $"{2 + (level - 3)}d10",
                                    DamageKind.Mental);
                            }
                            return tuple.Item1 < CheckResult.Success;
                        },
                        ProvideContextualAction = effect =>
                        {
                            if (result == CheckResult.CriticalFailure) return null;
                            return new ActionPossibility(new CombatAction(effect.Owner, spell.Illustration,
                                    "Massage Throat", [Trait.Manipulate],
                                    $"You can take an interact action to attempt a Fortitude save against DC {spell.SpellcastingSource!.GetSpellSaveDC()}. On a success, the spell ends.",
                                    Target.Self().WithAdditionalRestriction(creature =>
                                    {
                                        if (creature.HasEffect(MQEffectIds.Massaged))
                                            return "This action can only be used once per round.";
                                        return creature.HasFreeHand
                                            ? null
                                            : "You must have a free hand to perform an interact action.";
                                    }))
                                .WithGoodness((_, self, _) => self.Possibilities.Filter(ap => ap.CombatAction.HasTrait(Trait.Auditory) ||
                                    (ap.CombatAction.HasTrait(Trait.Spell) &&
                                     !ap.CombatAction.HasTrait(Trait.SomaticOnly))).CreateActions(true).Count >= 1 ? float.MaxValue : float.MinValue)
                                .WithActionCost(1).WithEffectOnChosenTargets(async (_, creature, _) =>
                                {
                                    CheckResult save =
                                        await CommonSpellEffects.RollSpellSavingThrowAsync(creature, spell, Defense.Fortitude);
                                    if (save >= CheckResult.Success)
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                    creature.AddQEffect(new QEffect { Id = MQEffectIds.Massaged }
                                        .WithExpirationAtStartOfOwnerTurn());
                                })
                            );
                        }
                    };
                    target.AddQEffect(croak);
                    return Task.CompletedTask;
                });
        });
    }
}