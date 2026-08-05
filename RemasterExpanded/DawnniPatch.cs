using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Specific;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Dawnsbury.Mods.DawnniExpanded;

namespace RemasterExpanded;

public class DawnniPatch
{
    public static void LoadDawnniPatch()
    {
        foreach (Feat feat in AllFeats.All.Where(f => f.Traits.Any(t => t == FeatArchetype.ArchetypeTrait || t == FeatArchetype.DedicationTrait) ||
                                                      (f.Name.Contains("Familiar") && f.Traits.Any(t => t == DawnniExpanded.DETrait))))
        {
            feat.Traits.Clear();
        }
    }

    public static void LoadDawnniPatch2()
    {
        List<Item> shop = Items.ShopItems;
        foreach (Item item in shop.Where(item => item.HasTrait(DawnniExpanded.DETrait) && item.Name.Contains("Mutagen") && !item.Name.Contains("Serene")).ToList())
        {
            shop.Remove(item);
        }
        foreach (Item item in shop.Where(item => item.HasTrait(DawnniExpanded.DETrait) && item.Name.Contains("Serene")))
        {
            item.ProsaicName = item.ProsaicName.Contains("Lesser") ? "lesser serene mutagen" : "moderate serene mutagen";
            item.WithItemGreaterGroup(ItemGreaterGroup.Mutagens);
        }
        Items.ShopItems = shop;
    }

    public static void LoadDawnniPatch3()
    {
        AllFeats.All.Remove(Witch.WitchClass);
    }

    public static void LoadDawnniMedicineFix()
    {
        AllFeats.All.RemoveAll(ft => ft.FeatName == FeatName.BattleMedicine);
                Feat medicine = new TrueFeat(FeatName.BattleMedicine, 1, "You can patch up wounds, even in combat.",
                        $"{{b}}Range{{/b}} touch\r\n{{b}}Requirements{{/b}} You must have a hand free.\r\n\r\nMake a Medicine check against DC 15.{S.FourDegreesOfSuccess("The target regains 4d8 HP.", "The target regains 2d8 HP.", null, "The target takes 1d8 damage.")}\n\nRegardless of your result, the target is then temporarily immune to your Battle Medicine for the rest of the day.\n\nIf you're expert in Medicine, you can choose to make the check against DC 20. If you do, you heal 2d8+10 HP on a success instead (4d8+10 HP on a critical success).\n\nIf you're master, you can choose DC 30 for 2d8+30 HP (4d8+30 on a critical success), and if you're legendary, you can choose DC 40 for 2d8+50 HP (4d8+50 on a critical success).",
                        [Trait.General, Trait.Healing, Trait.Manipulate, Trait.Skill]).WithActionCost(1)
                    .WithPrerequisite(values => values.GetProficiency(Trait.Medicine) >= Proficiency.Trained,
                        "You must be trained in Medicine.")
                    .WithPermanentQEffect("You can heal allies as an 'other action'.", qf =>
                        qf.ProvideActionIntoPossibilitySection = (qfBattleMedicine, section) =>
                        {
                            if (section.PossibilitySectionId != PossibilitySectionId.OtherManeuvers)
                                return null;
                            Creature owner = qfBattleMedicine.Owner;
                            if (owner.PersistentCharacterSheet == null)
                                return new ActionPossibility(
                                    BattleMedicine.CreateBattleMedicineAction(owner, Proficiency.Trained));
                            Proficiency proficiency =
                                owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Medicine);
                            if (proficiency < Proficiency.Expert)
                                return new ActionPossibility(
                                    BattleMedicine.CreateBattleMedicineAction(owner, Proficiency.Trained));
                            var possibilityList = new List<Possibility>
                            {
                                (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(
                                    qfBattleMedicine.Owner, Proficiency.Trained),
                                (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                    Proficiency.Expert)
                            };
                            if (proficiency >= Proficiency.Master)
                                possibilityList.Add(
                                    (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                        Proficiency.Master));
                            if (proficiency >= Proficiency.Legendary)
                                possibilityList.Add(
                                    (ActionPossibility)BattleMedicine.CreateBattleMedicineAction(owner,
                                        Proficiency.Legendary));
                            return new SubmenuPossibility(IllustrationName.HealersTools, "Battle Medicine")
                            {
                                Subsections =
                                {
                                    new PossibilitySection("Battle Medicine")
                                    {
                                        Possibilities = possibilityList
                                    }
                                }
                            };

                        });
                ModManager.AddFeat(medicine);
    }
}