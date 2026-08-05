using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Modding;
using RemasterExpanded.MySpells;
using SpellsAndSpellhearts;
using static RemasterExpanded.ModData;
using RemasterSpells = Dawnsbury.Mods.Remaster.Spellbook.RemasterSpells;

namespace RemasterExpanded;

public abstract class NewSpellhearts
{
    public static void Load()
    {
        ModManager.RegisterNewItemIntoTheShop("RE_DeathbaneCrescent", name => DeathbaneCrescent(name,18, 3, 50, 2, "1d4"));
        ModManager.RegisterNewItemIntoTheShop("RE_DeathbaneCrescentGreater", name => DeathbaneCrescent(name,24, 8, 450, 5, "1d6", " (greater)", SpellIds.InfuseVitality));
        ModManager.RegisterNewItemIntoTheShop("RE_DeathbaneCrescentMajor", name => DeathbaneCrescent(name,30, 12, 1900, 10, "1d8", " (major)", SpellId.Restoration, SpellIds.VitalBeacon));
    }

    public static QEffect DeathbaneCrescentArmor(int resistance, ItemName name)
    {
        return new QEffect
        {
            AfterYouTakeAction = async (qf, spell) =>
            {
                if (spell.CastFromScroll == null || spell.CastFromScroll.Runes.All(it => it.ItemName != name) || spell.HasTrait(Trait.DoesNotCountAsPrimaryCastingOfSpell))
                    return;
                if (!spell.HasTrait(Trait.Positive))
                    return;
                qf.Owner.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
                {
                    CannotExpireThisTurn = true,
                    StateCheck = _ =>
                    {
                        qf.Owner.WeaknessAndResistance.AddSpecialResistance("Undead", (action, _) => action != null && action.Owner.HasTrait(Trait.Undead), resistance, null);
                    }
                });
            }
        };
    }

    public static Item DeathbaneCrescent(ItemName name, int dc, int level, int price, int resistance, string dice, string suffix = "", SpellId? greater = null, SpellId? major = null)
    {
        return Spellhearts.AdvancedSpellheart(name, "deathbane crescent" + suffix,
            $"After you cast a vitality spell by Activating the crescent, you gain resistance {resistance} to attacks and effects from undead creatures until the end of your next turn.",
            $"After you cast a vitality spell by Activating the crescent, your Strikes with the weapon deal an additional {dice} vitality damage until the end of your next turn.",
            dc - 10, dc, level, price, DeathbaneCrescentArmor(resistance, name), dice, DamageKind.Positive, [Trait.Positive], "deathbane",
            "This crescent moon carved out of bone holds the power to turn back the undead. This spellheart covers any item it's affixed to with spiderwebs that reappear even if damaged or removed.",
            RemasterSpells.GetSpellIdByName("VitalityLash"), greater, major, false, MIllustrations.CreateIllustration("DeathbaneCrescent"));
    }
}