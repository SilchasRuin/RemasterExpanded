using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display.Illustrations;
using static RemasterExpanded.ModData;

namespace RemasterExpanded.MySpells;

public class NewSpells
{
    public static void LoadSpells()
    {
        NewCantrips.Load();
        NewFocusSpells.Load();
        NewSpells1st.Load();
        NewSpells2nd.Load();
        NewSpells3rd.Load();
        NewSpells4th.Load();
        NewSpells5th.Load();
    }

    public static bool IsTileAway(int px, int py, int ex, int ey, int maxDistance, Tile tile)
    {
        int dx = ex - px;
        int dy = ey - py;
        int stepX = -Math.Sign(dx);
        int stepY = -Math.Sign(dy);
        var coords = new List<(int, int)>();
        int x = px, y = py;
        for (int i = 1; i <= maxDistance; i++)
        {
            x += stepX;
            y += stepY;
            coords.Add((x, y));
        }
        return coords.Any(tuple => tuple.Item1 == tile.X && tuple.Item2 == tile.Y);
    }

    public static Affliction CreateBlindingPoison(int dc, Creature caster)
    {
        return new Affliction(MQEffectIds.BlindingPoison, "Blinding Poison", dc, "{b}Stage 1{/b} 3d6 poison damage and blinded for 1 round (1 round); {b}Stage 2{/b} 4d6 poison damage and blinded for 1 round (1 round); {b}Stage 3{/b} 5d6 poison damage and blinded for 1 round (1 round); {b}Stage 4{/b} 6d6 poison damage and blinded for 1 minute (1 round)", 4,
            stage =>
            {
                return stage switch
                {
                    1 => "3d6",
                    2 => "4d6",
                    3 => "5d6",
                    4 => "6d6",
                    _ => throw new Exception("Unknown stage.")
                };
            }, qf =>
            {
                QEffect blind = QEffect.Blinded();
                blind.Key = "BlindingPoison";
                blind.DoNotShowUpOverhead = true;
                qf.Owner.AddQEffect(qf.Value < 4 ? blind.WithExpirationEphemeral() : blind.WithExpirationAtStartOfSourcesTurn(caster, 10));
            })
        {
            MaximumDuration = 4
        };
    }
    
    public static async Task ApplyIncapacitationPoison(Affliction affliction, Creature attacker, Creature defender, CheckResult savingThrowResult, int level)
  {
    CombatAction poisonAction = new(attacker.Battle.Pseudocreature.With(creature => creature.Level = level), IllustrationName.DragonClaws, affliction.Name,
    [Trait.Poison, Trait.Incapacitation], "", Target.Self())
    {
        SpellLevel = 0
    };
    if (savingThrowResult > CheckResult.Failure)
      return;
    int num = Math.Min(savingThrowResult == CheckResult.CriticalFailure ? 2 : 1, affliction.MaximumStage);
    await defender.AddAffliction(affliction.MaximumStage, EnterStage, new QEffect(affliction.Name + ", Stage", affliction.StagesDescription, ExpirationCondition.ExpiresAtStartOfSourcesTurn, attacker, (Illustration) IllustrationName.Poisoned)
    {
        Id = affliction.Id,
        Value = num,
        RepresentsPoison = true,
        Affliction = affliction,
        CounteractLevel = attacker.MaximumSpellRank,
        Tag = poisonAction,
        StateCheck = affliction.StateCheck,
        StateCheckWithVisibleChanges = affliction.StateCheckWithVisibleChanges,
        StartOfSourcesTurn = (Func<QEffect, Task>)(async qfVenom =>
        {
            Affliction.AdjustValue(qfVenom,
                await CommonSpellEffects.RollSavingThrowAsync(defender, poisonAction, Defense.Fortitude, affliction.DC),
                affliction.MaximumStage);
            if (qfVenom.Value <= 0)
                return;
            await EnterStage(qfVenom);
        })
    }.WithExpirationAtStartOfSourcesTurn(attacker.Battle.CreatureControllingInitiative ?? attacker,
        affliction.MaximumDuration));
    return;

    async Task EnterStage(QEffect qfVenom)
    {
        string? diceFormula = affliction.PoisonDamage(qfVenom.Value);
        if (diceFormula != null)
        {
            DiceFormula damage = DiceFormula.FromText(diceFormula);
            await CommonSpellEffects.DealDirectDamage(poisonAction, damage, qfVenom.Owner, CheckResult.Failure,
                DamageKind.Poison);
        }

        await affliction.EnterStage.InvokeIfNotNull(qfVenom, poisonAction);
    }
  }

    public static string IntToString(int number)
    {
        return number switch
        {
            1 => "one",
            2 => "two",
            3 => "three",
            4 => "four",
            5 => "five",
            6 => "six",
            7 => "seven",
            8 => "eight",
            9 => "nine",
            _ => "10"
        };
    }
    
    public static string IfAmped(bool inCombat, string description)
    {
        return !inCombat ? $"\n\n{{Blue}}{{b}}Amp{{/b}} {description}{{/Blue}}" : "";
    }
}