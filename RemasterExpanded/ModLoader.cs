using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Modding;
using HarmonyLib;
using NewSpells = RemasterExpanded.MySpells.NewSpells;

namespace RemasterExpanded;

public class ModLoader
{
    [DawnsburyDaysModMainMethod]
    public static void LoadMod()
    {
        RemasterLore.Load();
        NewItems.LoadItems();
        ModManager.RegisterBooleanSettingsOption("RE_CritChange", "Remaster Flail and Hammer Critical Specialization", "Changes flails and hammers to use their remastered critical specializations, which requires a saving throw against your Class DC.", false);
        Harmony harmony = new("critSpecChange");
        harmony.PatchAll();
        Inkdrop.AddInkdrop();
        NewSpells.LoadSpells();
        foreach (Feat feat in FeatLoader.LoadFeats())
        {
            ModManager.AddFeat(feat);
        }
        if (ModData.Remaster && ModData.MoreSpells)
        {
            foreach (Feat feat in ModifyRemasterWizard.LoadRemasterWizardFeats())
            {
                ModManager.AddFeat(feat);
            }
            if (AllFeats.GetFeatByFeatName(FeatName.AdvancedSchoolSpell) is TrueFeat advancedFeat)
            {
                advancedFeat.Traits.Clear();
            }
        }
        Feat dangerousSorcery =  AllFeats.GetFeatByFeatName(FeatName.DangerousSorcery);
        dangerousSorcery.WithCustomName("Sorcerous Potency");
        dangerousSorcery.FlavorText = "Because of the magical power inherent in your blood, your spells that hurt or cure are stronger than those of other spellcasters.";
        dangerousSorcery.RulesText = "When you Cast a Spell from your spell slots that either deals damage or restores Hit Points, you gain a status bonus to that spell's damage or healing equal to the spell's rank. This applies only to the initial damage or healing the spell deals when cast.";
        dangerousSorcery.OnCreature = null;
        dangerousSorcery.WithPermanentQEffect("Your damage spells deal extra damage and your healing spells heal for more.", effect =>
        {
            effect.BonusToDamage = (_, spell, _) =>
                spell.HasTrait(Trait.Spell) && !spell.HasTrait(Trait.Cantrip) && !spell.HasTrait(Trait.Focus) &&
                spell.CastFromScroll == null && spell.SpellcastingSource?.Kind != SpellcastingKind.Innate
                    ? new Bonus(spell.SpellLevel, BonusType.Status, "Sorcerous Potency")
                    : null;
            effect.AddGrantingOfTechnical(_ => true, qfTech =>
            {
                qfTech.BonusToSelfHealing = (_, spell) => spell != null && spell.Owner == effect.Owner &&
                    spell.HasTrait(Trait.Spell) && !spell.HasTrait(Trait.Cantrip) && !spell.HasTrait(Trait.Focus) &&
                                                         spell.CastFromScroll == null && spell.SpellcastingSource?.Kind != SpellcastingKind.Innate
                    ? new Bonus(spell.SpellLevel, BonusType.Status, "Sorcerous Potency")
                    : null;
            });
        });
        // if (ModManager.TryParse("DawnniEx", out Trait _))
        // {
        //     DawnniPatch.LoadDawnniPatch();
        // }
        LoadOrder.AtEndOfLoadingSequence += () =>
        {
            Item withRunes = Items.CreateNew(NewItems.AutoloadLeathers).WithModificationRune(ItemName.ArmorPotencyRunestone)
                .WithModificationRune(ItemName.ResilientRunestone);
            Items.ShopItems.Add(withRunes);
            // Items.ShopItems = Items.ShopItems.Select(item =>
            // {
            //     if (item.ItemName != NewItems.GlueBombLesser && item.ItemName != NewItems.GlueBombModerate) return item;
            //     item.Traits.Add(Trait.Bomb);
            //     return item;
            // }).ToList();
            // ModManager.RegisterActionOnEachItem(item =>
            // {
            //     if (item.HasTrait(Trait.Bomb) || item.ItemName != NewItems.GlueBombLesser && item.ItemName != NewItems.GlueBombModerate) return item;
            //     item.Traits.Add(Trait.Bomb);
            //     return item;
            // });
            if (ModData.Remaster && ModData.MoreSpells)
            {
                ModifyRemasterWizard.PatchWizard();
            }
            Feat universal = AllFeats.GetFeatByFeatName(FeatName.UniversalistSchool);
            universal.OnSheet += values =>
            {
                values.GrantFeat(FeatName.HandOfTheApprentice);
            };
            universal.RulesText += $"\n\nYou gain the {AllSpells.CreateSpellLink(SpellId.HandOfTheApprentice, Trait.Wizard)} focus school spell and you gain a focus pool of 1 focus point that recharges after every encounter.";
            Feat sorcerer = AllFeats.GetFeatByFeatName(FeatName.Sorcerer);
            sorcerer.OnSheet +=  values => values.GrantFeat(FeatName.DangerousSorcery);
        };
    }
}