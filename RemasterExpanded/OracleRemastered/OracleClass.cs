using System.Reflection;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Campaign.LongTerm;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations.AuraAnimations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Feats.Features;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Specific;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using RemasterExpanded.ClassChangesAndFeats;
using RemasterExpanded.MyArchetypes;
using RemasterExpanded.Technical;
using static RemasterExpanded.ModData;
using static RemasterExpanded.OracleRemastered.RevelationSpells;

namespace RemasterExpanded.OracleRemastered;

public abstract class OracleClass
{

    public static void Load()
    {
        LongTermEffects.EasyRegister("HarmForetold", LongTermEffectDuration.UntilLongRest, HarmForetold);
        LoadRevelationSpells();
        ModManager.RegisterBooleanSettingsOption("RE_RemoveLegacyOracle", "Remaster Expanded - Remove Legacy Oracle", "If enabled, the base-game Oracle which uses pre-remaster rules will be removed from the game. {b}NOTE:{/b} You must restart the game for this to take place.", false);
        if (AllFeats.GetFeatByFeatName(FeatName.ReachSpell) is TrueFeat reach)
            reach.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.WidenSpell) is TrueFeat widen)
            widen.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.BespellWeapon) is TrueFeat bespell)
        {
            bespell.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
            bespell.WithRulesTextCreator(sheet =>
            {
                const string str = "When you cast your first non-cantrip spell each turn, until the end of your turn, your Strikes deal an extra 1d6 force damage and gains the associated trait if it didn't have it already. If the spell dealt a different type of damage, the Strike deals this type of damage instead (or one type of your choice if the spell could deal multiple types of damage).";
                if (sheet.Class is { ClassTrait: Trait.Oracle } || sheet.Class?.ClassTrait == MTraits.RemasterOracle)
                {
                    return str.Replace("associated", "Divine");
                }
                return sheet.Class switch
                {
                    { ClassTrait: Trait.Sorcerer } => str.Replace("associated",
                        "your bloodline's magical tradition trait"),
                    { ClassTrait: Trait.Wizard } => str.Replace("associated", "Arcane"),
                    _ => str
                };
            });
            bespell.WithCustomName("Bespell Strikes");
            bespell.OnCreature = null;
            bespell.WithPermanentQEffect("When you cast a non-cantrip spell, your Strikes deal additional damage.", qf =>
            {
                qf.AfterYouTakeAction = async (qfSelf, spell) =>
                {
                    if (qfSelf.Owner.HasEffect(QEffectId.BespellWeaponSuppressed) || !spell.HasTrait(Trait.Spell) ||
                        spell.HasTrait(Trait.Cantrip) || spell.HasTrait(Trait.SustainASpell))
                        return;
                    List<DamageKind> damageKinds = [.. MagusRemaster.DetermineDamageKindFromSpell(spell, false)];
                    if (damageKinds.Count == 0)
                    {
                        damageKinds.Add(DamageKind.Force);
                    }
                    DamageKind damageKind = damageKinds[0];
                    if (damageKinds.Count > 1)
                    {
                        ChoiceButtonOption choice = await qfSelf.Owner.AskForChoiceAmongButtons(IllustrationName.Light, "Your most recent spell did multiple damage types. Which type do you wish to use for Bespell Strikes?",
                            [.. damageKinds.Select(kind => kind.HumanizeTitleCase2())]);
                        damageKind = damageKinds[choice.Index];
                    }

                    Trait trait = Trait.None;
                    if (qfSelf.Owner.PersistentCharacterSheet is { Class: {} classFeat })
                    {
                        trait = classFeat.ClassTrait switch
                        {
                            Trait.Sorcerer => qfSelf.Owner.Spellcasting?.PrimarySpellcastingSource
                                ?.SpellcastingTradition ?? Trait.None,
                            Trait.Oracle => Trait.Divine,
                            Trait.Wizard => Trait.Arcane,
                            _ => trait
                        };
                        if (classFeat.ClassTrait == MTraits.RemasterOracle)
                            trait = Trait.Divine;
                    }
                    qfSelf.Owner.AddQEffect(new QEffect("Bespelled Strikes", $"Your Strikes deals 1d6 extra {damageKind.HumanizeTitleCase2()} damage.",
                        ExpirationCondition.ExpiresAtEndOfAnyTurn, qfSelf.Owner, IllustrationName.Light)
                    {
                        AddExtraWeaponDamage = _ => (DiceFormula.FromText("1d6", "Bespell Strikes"), damageKind),
                        AdjustStrikeAction = (_, strike) => strike.Traits.Add(trait)
                    });
                    qfSelf.Owner.AddQEffect(new QEffect
                    {
                        Id = QEffectId.BespellWeaponSuppressed,
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfAnyTurn
                    });
                };
            });
        }
        if (AllFeats.GetFeatByFeatName(FeatName.DivineAegis) is TrueFeat aegis)
            aegis.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.SteadySpellcastingMain) is TrueFeat steady)
            steady.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.QuickenSpell) is TrueFeat quicken)
            quicken.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.ChaoticSpell) is TrueFeat chaotic)
            chaotic.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.DetonatingSpell) is TrueFeat detonating)
            detonating.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (AllFeats.GetFeatByFeatName(FeatName.SurgingMight) is TrueFeat surging)
            surging.WithAllowsForAdditionalClassTrait(MTraits.RemasterOracle);
        if (PlayerProfile.Instance.IsBooleanOptionEnabled("RE_RemoveLegacyOracle") && AllFeats.GetFeatByFeatName(FeatName.Oracle) is {} oracle)
        {
            AllFeats.All.Remove(oracle);
            if (AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Oracle.ToStringOrTechnical() + "Dedication") is
                TrueFeat dedication)
                AllFeats.All.Remove(dedication);
        }
        else if (AllFeats.GetFeatByFeatNameOrStringOptional(null, Trait.Oracle.ToStringOrTechnical() + "Dedication") is TrueFeat dedication)
            dedication.WithPrerequisite(values => !values.HasFeat(MFeatNames.RemasterOracle),
                    $"You must not be {MTraits.RemasterOracle.HumanizeLowerCase2().WithIndefiniteArticle()}.")
                .WithPrerequisite(values => !values.HasFeat(AllFeats.GetFeatByFeatNameOrStringOptional(null, MTraits.RemasterOracle.ToStringOrTechnical() + "Dedication")?.FeatName ?? FeatName.Oracle), $"You cannot select {Trait.Oracle.HumanizeLowerCase2().WithIndefiniteArticle()} dedication twice.");
        ModManager.RegisterActionOnEachItem(ModifyOracularCrown);
        Items.ShopItems = [.. Items.ShopItems.Select(ModifyOracularCrown)];
    }
    public static IEnumerable<Feat> LoadOracle()
    {
        foreach (Feat feat in LoadOracleFeats())
        {
            if (feat is TrueFeat)
                feat.Traits.Add(MTraits.VisualOracle);
            yield return feat;
        }

        foreach (Feat feat in DeitySpellFeats())
        {
            yield return feat;
        }

        foreach (Feat feat in CreateDivineAccessSubfeats())
        {
            yield return feat;
        }
        yield return new ClassSelectionFeat(MFeatNames.RemasterOracle,
            "Your conduit to divine power eschews the traditional channels of prayer and servitude—you instead glean sacred truths and great mysteries embodied in overarching concepts, whether because you perceive the common ground across multiple deities or circumvent their power entirely. You explore one of these mysteries and draw upon its power to cast miraculous spells, but that power comes with a terrible price: a curse that grows stronger the more you draw upon it, which you might uphold as an instrument of the divine or view as punishment from the gods.",
            MTraits.RemasterOracle, new EnforcedAbilityBoost(Ability.Charisma), 8,
            [Trait.Fortitude, Trait.Reflex, Trait.Religion, Trait.UnarmoredDefense, Trait.LightArmor, Trait.Simple, Trait.Unarmed, MTraits.RemasterOracle, Trait.Perception],
            [Trait.Will],
            3,
            $"{{b}}1. Divine spontaneous spellcasting.{{/b}} {S.DescribeSpontaneousSpellcasting(MTraits.RemasterOracle)}" +
            $"\n\n{{b}}2. Mystery.{{/b}} You choose a divine mystery which gives you a curse, an extra cantrip, additional spells based on your level, an extra skill, a focus pool of 1 focus point, a revelation focus spell and a unique cursebound ability." +
            $"\n\n{{b}}3. Oracular curse.{{/b}} You have a curse based on your mystery. You start each encounter with your curse dormant. The first time each encounter you use a cursebound ability, you become cursebound 1. Each time you use a cursebound ability while already cursebound, the value of the condition increases by 1 after the ability resolves. At lower levels your cursebound condition can't increase beyond cursebound 2; as you grow in levels, your cursebound condition can progress to 3 and finally 4. At the end of each encounter you cease to be cursebound."
            ,
            [.. OracleMysteries()])
            .WithEffectiveClassFeatures(features => features.AddSpontaneousSpellcasting("three slots and three spells known")
                .AddFeature(7, WellKnownClassFeature.Resolve)
                .AddFeature(7, WellKnownClassFeature.ExpertInSpellcasting)
                .AddFeature(9, WellKnownClassFeature.ExpertInFortitude)
                .AddFeature(11, WellKnownClassFeature.ExpertInPerception)
                .AddFeature(11, "Expert in simple weapons and unarmed attacks")
                .AddFeature(11, "Divine access", "Choose one deity who grants one of your mystery's granted domains. Add up to three cleric spells of your choice granted by that deity to your spell list, and to your spell repertoire as soon as you can cast spells of the appropriate rank.")
                .AddFeature(11, "Major curse", "The maximum cursebound value you can have increases from 2 to 3")
                .AddFeature(13, WellKnownClassFeature.ExpertInUnarmoredDefenseAndLightArmor)
                .AddFeature(13, WellKnownClassFeature.ExpertInReflex)
                .AddFeature(13, WellKnownClassFeature.WeaponSpecialization)
                .AddFeature(15, WellKnownClassFeature.MasterInSpellcasting)
                .AddFeature(17, "Extreme curse", "The maximum cursebound value you can have increases from 3 to 4")
                .AddFeature(17, WellKnownClassFeature.GreaterResolve)
                .AddFeature(19, WellKnownClassFeature.LegendaryInSpellcasting)
            )
            .WithOnSheet(sheet =>
            {
                sheet.SpellTraditionsKnown.Add(Trait.Divine);
                sheet.SpellRepertoires.Add(MTraits.RemasterOracle, new SpellRepertoire(Ability.Charisma, Trait.Divine));
                sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                sheet.AddSelectionOption(new AddToSpellRepertoireOption("RemasterOracleCantrips", "Oracle cantrips", 1, MTraits.RemasterOracle, Trait.Divine, 0, 5));
                for (var level = 1; level <= 19; ++level)
                {
                    int spellLevel = (level + 1) / 2;
                    int numberOfSpells = level % 2 == 0 ? 1 : 3;
                    sheet.AddNewSpontaneousSpells("RemasterOracleSpells" + level, level, MTraits.RemasterOracle, Trait.Divine, spellLevel, numberOfSpells);
                }
                SpellRepertoire repertoire = sheet.SpellRepertoires[MTraits.RemasterOracle];
                repertoire.SpellSlots[1] = 2;
                for (var atLevel = 2; atLevel <= 18; ++atLevel)
                {
                    int spellLevel = (atLevel + 1) / 2;
                    int numberOfSpells = atLevel % 2 == 0 ? 1 : 3;
                    sheet.AddAtLevel(atLevel, _ => repertoire.SpellSlots[spellLevel] += numberOfSpells);
                }
                sheet.AddAtLevel(19, _ => ++repertoire.SpellSlots[10]);
                CommonCharacterFeatures.AddSignatureSpellsFeature(sheet, MTraits.RemasterOracle, "Oracle");
                sheet.IncreaseProficiency(11, Trait.Simple, Proficiency.Expert);
                sheet.IncreaseProficiency(11, Trait.Unarmed, Proficiency.Expert);
                sheet.AddAtLevel(11, values =>
                {
                    values.AddSelectionOption(new SingleFeatSelectionOption("RE_DivineAccess", "Divine Access", 11, ft => ft.Tag is "DivineAccessDeity"));
                });
            })
            .WithOnCreature((sheet, self) =>
            {
                self.AddQEffect(OracleCurseFunction(sheet, self));
            })
            .WithCustomName(PlayerProfile.Instance.IsBooleanOptionEnabled("RE_RemoveLegacyOracle") ? "Oracle" :"Oracle (Remastered)");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_AdvancedRevelation", "Advanced Revelation"), 6,
            "Divine power reveals greater mysteries to you.",
            "You gain the advanced revelation spell associated with your mystery.",
            [MTraits.RemasterOracle, MTraits.VisualOracle], [.. CreateRevelations(false)])
            .WithPrerequisite(values => values.Class?.ClassTrait == MTraits.RemasterOracle || values.HasFeat(MFeatNames.FirstRevelation), "You must know the initial revelation spell for your mystery.");
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_GreaterRevelation", "Greater Revelation"), 12,
            "You unlock deeper revelations hidden within your mystery.",
            "You gain your mystery's greater revelation spell.",
            [MTraits.RemasterOracle, MTraits.VisualOracle], [.. CreateRevelations(true)])
            .WithPrerequisite(values => values.Class?.ClassTrait == MTraits.RemasterOracle || values.HasFeat(MFeatNames.FirstRevelation), "You must know the initial revelation spell for your mystery.");
        foreach (Feat feat in OracleArchetype.OracleArchetypeFeats())
        {
            yield return feat;
        }
    }

    public static IEnumerable<Feat> OracleMysteries()
    {
        SpellId cantrip = SpellId.Daze;
        List<SpellId> grantedSpells = [SpellId.ColorSpray, SpellId.ObscuringMist, SpellId.CloakOfColors];
        if (ModManager.TryParse("Light", out SpellId light) && ModManager.TryParse("Darkness", out SpellId dark) &&
            ModManager.TryParse("MoonFrenzy", out SpellId moon))
        {
            cantrip = light;
            grantedSpells = [SpellId.ColorSpray, dark, moon];
        }
        yield return new MysteryFeat(MysteryFeatNames.Cosmos,
            "Celestial bodies great and small exert influence on you, giving you sublime cosmic power. Perhaps you see the glittering stars as a divine blessing, or perhaps you feel drawn to the infinitely dark spaces between.",
            SpellIds.SprayOfStars, SpellIds.InterstellarVoid, SpellIds.MoonlitAscent,
            cantrip, grantedSpells, [FeatName.DomainMoon, FeatName.DomainVoid], Skill.Nature,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    self.AddQEffect(QEffect.Enfeebled(curse.Value).WithExpirationEphemeral());
                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        BonusToDefenses = (_, action, _) => action is { ActionId: ActionId.Shove } ||
                                                            (action != null && IsForcedMovement(action))
                            ? new Bonus(-curse.Value, BonusType.Status, "Curse of the Sky's Call")
                            : null
                    });
                };
            },
            "Curse of the Sky's Call",
            "You are enfeebled with a value equal to your cursebound value, and you take a status penalty to saves and DCs against all forms of forced movement equal to your cursebound value.",
            "Your body is drawn toward the heavens, making you lighter and less substantial than you should be. Your eyes glow with starry light, and your hair and clothing float and drift around you.",
            IllustrationName.Friendfetch,
            [Trait.Curse, Trait.Divine, MTraits.VisualOracle],
            MFeatNames.OracularWarning);

        List<FeatName> allowedDomains = [FeatName.DomainDestruction, FeatName.DomainFire, FeatName.DomainVoid];
        if (ModManager.TryParse("PS_Dust", out FeatName dust))
            allowedDomains.Add(dust);
        string curseRules = Describe4StageCurse(
            "You gain weakness 2 to fire damage. Any immunity or resistance you have to fire is suppressed.",
            "Swirling ash imposes a -2 circumstance penalty to ranged attack rolls you make.",
            "Your weakness to fire damage is equal to 5 + your level.",
            "You take a –10-foot status penalty to all your Speeds as your limbs begin to crumble like ash.");
        yield return new MysteryFeat(MysteryFeatNames.Ash, "You see all things in the world as fleeting and temporary, waiting to be purified into their base essence: the ash left behind after a burning fire. While you understand fire as a necessary part of this process, you see it mostly as a tool to achieve final purity, not the true goal. You have much in common with oracles with the flames mystery, but you may consider them to be shortsighted, or at best, simply lacking in understanding of the truths that their burning fires impart.",
            SpellIds.AshenWind,
            SpellIds.IncendiaryAshes,
            SpellIds.AshForm,
            MySpells.SpellIds.Ignition,
            [SpellId.BurningHands, SpellId.ObscuringMist, SpellId.Disintegrate],
            allowedDomains,
            Skill.Occultism,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    if (value >= 1)
                    {
                        self.AddQEffect(QEffect.DamageWeakness(DamageKind.Fire, value >= 3 ? 5 + self.Level : 2));
                        self.WeaknessAndResistance.Resistances.RemoveAll(resist =>
                            resist.DamageKind == DamageKind.Fire);
                        self.WeaknessAndResistance.Immunities.RemoveAll(imm => imm == DamageKind.Fire);
                    }
                    if (value >= 2)
                    {
                        self.AddQEffect(new QEffect
                        {
                            BonusToAttackRolls = (_, action, _) => action.HasTrait(Trait.Attack) && action.HasTrait(Trait.Ranged) ? new Bonus(-2, BonusType.Circumstance, "Curse of Creeping Ashes", false) : null
                        });
                    }
                    if (value >= 4)
                    {
                        self.AddQEffect(new QEffect
                        {
                            BonusToAllSpeeds = _ => new Bonus(-2, BonusType.Status, "Curse of Creeping Ashes", false)
                        });
                    }

                    curse.Description = value switch
                    {
                        < 2 => curseRules.Remove(curseRules.IndexOf("{b}Cursebound 2{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 3 => curseRules.Remove(curseRules.IndexOf("{b}Cursebound 3{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 4 => curseRules.Remove(curseRules.IndexOf("{b}Cursebound 4{/b}", StringComparison.Ordinal))
                            .Trim(),
                        _ => curse.Description
                    };
                };
            },
            "Curse of Creeping Ashes",
            curseRules,
            "Your body is slowly being consumed by the fires of your internal power, purifying you with each passing day. You are occasionally wracked with dry, wheezing coughs, and wherever you go you leave behind a fine trace of ash that falls from your body.",
            IllustrationName.IncendiaryAshes,
            [Trait.Curse, Trait.Divination, MTraits.VisualOracle, Trait.Fire],
            MFeatNames.WhispersOfWeakness
        );
        List<FeatName> domains3 = [FeatName.DomainDestruction, FeatName.DomainMight, FeatName.DomainZeal];
        if (ModManager.TryParse("Protection", out FeatName protection2))
        {
            domains3.Add(protection2);
        }
        else if (ModManager.TryParse("PS_Protection",out FeatName protection))
        {
            domains3.Add(protection);
        }

        string curseRules2 = Describe4StageCurse("Spells have an easier time wounding you. You gain weakness 2 to any damage dealt by a spell. Any immunity or resistance you have to spells is suppressed. This applies only to spells, not other magical abilities.",
            "You take a –1 status penalty to saving throws against spells.",
            "Your weakness to spells is equal to your level.",
            "Your status penalty to saving throws against spells increases to –2.");
        yield return new MysteryFeat(MysteryFeatNames.Battle, 
            "Warlike forces fill you with physical might and tactical knowledge, aiming to have you uphold the glory of combat, fight to improve the world, prepare against the necessity of conflict, or endure the inevitability of war.",
            SpellIds.WeaponTrance, SpellIds.BattlefieldPersistence,
            SpellIds.RevelInRetribution,
            SpellId.Shield, [SpellId.TrueStrike, SpellId.TelekineticManeuver, MySpells.SpellIds.WeaponStorm],
            domains3, Skill.Athletics,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    if (value >= 1)
                    {
                        self.WeaknessAndResistance.AddSpecialWeakness(new SpecialResistance("Spell", (action, _) => action != null && action.HasTrait(Trait.Spell), value >= 3 ? self.Level : 2, null));
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            Id = MQEffectIds.MortalWarrior
                        }.AddGrantingOfTechnical(_ => true, qfTech =>
                        {
                            qfTech.YouDealDamageEvent = async (_, damage) =>
                            {
                                if (damage.CombatAction == null || damage.TargetCreature != self || !damage.CombatAction.HasTrait(Trait.Spell))
                                    return;
                                self.WeaknessAndResistance.Resistances.Clear();
                                self.WeaknessAndResistance.Immunities.Clear();
                            };
                        }));
                        self.RemoveAllQEffects(qff => qff.Id == MQEffectIds.ImmunityToSpell);
                    }
                    if (value >= 2)
                    {
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToDefenses = (_, action, _) => action != null && action.HasTrait(Trait.Spell) ? new Bonus(value == 4 ? 2 : 1, BonusType.Status, "Curse of the Mortal Warrior", false) : null
                        });
                    }

                    curse.Description = value switch
                    {
                        < 2 => curseRules2.Remove(curseRules2.IndexOf("{b}Cursebound 2{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 3 => curseRules2.Remove(curseRules2.IndexOf("{b}Cursebound 3{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 4 => curseRules2.Remove(curseRules2.IndexOf("{b}Cursebound 4{/b}", StringComparison.Ordinal))
                            .Trim(),
                        _ => curse.Description
                    };
                };
            },
            "Curse of the Mortal Warrior",
            curseRules2,
            "You thrive in the thick of battle, but your mystery’s sheer focus on the physical and material leaves your soul weak against the tricks of spellcraft. You smell faintly of steel and blood no matter how you try to remove or mask the scent, you appear more imposing and muscular than you actually are, and you hear the faint clash and clamor of battle in the distance at all times.",
            IllustrationName.BattlefieldPersistence, [Trait.Curse, Trait.Divine, MTraits.VisualOracle],
            MFeatNames.OracularWarning);
        yield return new MysteryFeat(MysteryFeatNames.Ancestors,
            "The voices of generations past speak to you, and you hear their words. You might resent the constant interruption, or you might revere the spirits of those who came before.",
            SpellIds.AncestralTouch, SpellIds.AncestralDefense,
            SpellIds.AncestralForm,
            SpellId.Guidance, [SpellId.IllOmen, MySpells.SpellIds.GhostlyCarrier, MySpells.SpellIds.InvokeSpirits],
            [FeatName.DomainDeath, FeatName.DomainFamily],
            Skill.Society,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    self.AddQEffect(QEffect.Clumsy(value).WithExpirationEphemeral());
                };
            },
            "Curse of Ancestral Meddling",
            "You are clumsy with a value equal to your cursebound value as the spirits of your ancestors temporarily possess you and vie for control in your mind, hindering your movements.",
            "The ancestral spirits you commune with haunt you and meddle with your belongings and actions, either out of a well-intentioned (but ultimately detrimental) attempt to assist you, as punishment for your audacity in circumventing the traditional means of achieving divine power, for their own amusement, or a mixture of the above.",
            IllustrationName.AncestralForm,
            [Trait.Curse, Trait.Divine, MTraits.VisualOracle, Trait.Spirit],
            MFeatNames.WhispersOfWeakness);
        List<FeatName> domains2 = [FeatName.DomainDeath, FeatName.DomainUndeath, MFeatNames.Vigil];
        if (ModManager.TryParse("PS_Decay", out FeatName decay))
            domains2.Add(decay);
        string curseRules3 = Describe4StageCurse("You gain weakness 2 to vitality and void damage. You can be targeted by and are hurt by both vitality and void damage even if one or the other normally has no effect on you (such as being targeted and damaged by heal even if you aren't undead). Any immunity or resistance you have to vitality or void is suppressed.",
            "You take a –1 status penalty to Fortitude saves.",
            "Your weakness to vitality and void damage is equal to 5 + your level.",
            "Your status penalty to Fortitude saving throws increases to –2.");
        yield return new MysteryFeat(MysteryFeatNames.Bones,
            "Your mystery imparts an understanding of death and undeath in all their macabre complexity. You might have had a brush with death yourself—maybe even dying and returning to life—or carry the touch of undeath in your blood.",
            SpellIds.SoulSiphon, SpellIds.ArmorOfBones,
            SpellIds.ClaimUndead,
            MySpells.SpellIds.VoidWarp, [SpellId.GrimTendrils, SpellId.FalseLife, SpellId.GhostlyWeapon],
            domains2, Skill.Medicine,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    if (value >= 1)
                    {
                        self.AddQEffect(QEffect.DamageWeakness(Trait.Positive, value >= 3 ? self.Level + 5 : 2)
                            .WithExpirationEphemeral());
                        self.AddQEffect(QEffect.DamageWeakness(Trait.Negative, value >= 3 ? self.Level + 5 : 2)
                            .WithExpirationEphemeral());
                        self.WeaknessAndResistance.Resistances.RemoveAll(resist =>
                            resist.DamageKind == DamageKind.Positive);
                        self.WeaknessAndResistance.Immunities.RemoveAll(imm => imm == DamageKind.Positive);
                        self.WeaknessAndResistance.Resistances.RemoveAll(resist =>
                            resist.DamageKind == DamageKind.Negative);
                        self.WeaknessAndResistance.Immunities.RemoveAll(imm => imm == DamageKind.Negative);
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            Id = MQEffectIds.LivingDeath,
                            YouAreTargeted = async (_, action) =>
                            {
                                bool living = self.IsLivingCreature;
                                if (action.HasTrait(Trait.Positive) && living)
                                {
                                    self.Traits.Add(Trait.Undead);
                                    self.AddQEffect(new QEffect
                                    {
                                        AfterYouAreTargeted = async (effect, cA) =>
                                        {
                                            if (cA != action)
                                                return;
                                            self.Traits.Remove(Trait.Undead);
                                            effect.ExpiresAt = ExpirationCondition.Immediately;
                                        }
                                    });
                                }

                                if (action.HasTrait(Trait.Negative) && !living)
                                {
                                    self.Traits.Remove(Trait.Undead);
                                    self.AddQEffect(new QEffect
                                    {
                                        AfterYouAreTargeted = async (effect, cA) =>
                                        {
                                            if (cA != action)
                                                return;
                                            self.Traits.Add(Trait.Undead);
                                            effect.ExpiresAt = ExpirationCondition.Immediately;
                                        }
                                    });
                                }
                            },
                            ModifyActionPossibility = (_, action) =>
                            {
                                if (!action.HasTrait(Trait.Positive) && !action.HasTrait(Trait.Negative))
                                    return;
                                if (action.Target is not EmanationTarget { IncludeSelf: true } emanationTarget)
                                    return;
                                FieldInfo? includeSelf = typeof(EmanationTarget).GetField(nameof(emanationTarget.IncludeSelf),  BindingFlags.Instance | BindingFlags.NonPublic);
                                if (includeSelf == null)
                                    return;
                                includeSelf.SetValue(emanationTarget, false);
                            }
                        });
                    }

                    if (value >= 2)
                    {
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToDefenses = (_, _, defense) =>
                                defense == Defense.Fortitude
                                    ? new Bonus(value >= 4 ? -2 : -1, BonusType.Status, "Curse of Living Death", false)
                                    : null
                        });
                    }

                    curse.Description = value switch
                    {
                        < 2 => curseRules3.Remove(curseRules3.IndexOf("{b}Cursebound 2{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 3 => curseRules3.Remove(curseRules3.IndexOf("{b}Cursebound 3{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 4 => curseRules3.Remove(curseRules3.IndexOf("{b}Cursebound 4{/b}", StringComparison.Ordinal))
                            .Trim(),
                        _ => curse.Description
                    };
                };
            },
            "Curse of Living Death",
            curseRules3,
            "Your body is slowly decaying even though you are alive, and using your powers furthers this unnatural living death. You carry a touch of the grave about you, manifesting as bloodless pallor, a faint smell of earth, or deathly cold skin.",
            IllustrationName.ArmorOfBones,
            [Trait.Curse, Trait.Divine, MTraits.VisualOracle, Trait.Positive, Trait.Negative],
            MFeatNames.NudgeTheScales);
        List<FeatName> allowedDomains3 = [FeatName.DomainFire, FeatName.DomainSun];
        if (ModManager.TryParse("PS_Dust", out FeatName dust2))
            allowedDomains3.Add(dust2);
        yield return new MysteryFeat(MysteryFeatNames.Flames,
            "Fire lives at the center of the world, the center of the sun, and the center of civilization.",
            SpellIds.IncendiaryAura, SpellIds.WhirlingFlames, SpellIds.FlamingFusillade,
            MySpells.SpellIds.Ignition, [SpellId.BurningHands, SpellId.ScorchingRay, SpellId.Fireball],
            allowedDomains3, Skill.Acrobatics,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    if (value < 1)
                        return;
                    self.WeaknessAndResistance.Resistances.RemoveAll(resist =>
                        resist.DamageKind == DamageKind.Fire);
                    self.WeaknessAndResistance.Immunities.RemoveAll(imm => imm == DamageKind.Fire);
                    if (self.HasEffect(QEffectId.Unconscious))
                        return;
                    QEffect persistent = QEffect.PersistentDamage($"{value}", DamageKind.Fire).WithExpirationEphemeral();
                    persistent.BansPersistentDamageRecovery = (effect, qEffect, _) => qEffect == effect;
                    self.AddQEffect(persistent);
                };
            },
            "Curse of Engulfing Flames",
            "You catch fire, taking persistent fire damage equal to your cursebound value, and any immunity or resistance you have to fire is suppressed. The flames subside if you fall unconscious, but they resume when you return to consciousness.",
            "Fires flare noticeably (though not dangerously) in your presence, you occasionally smoke slightly, and your body is almost painfully hot to the touch.",
            IllustrationName.IncendiaryAura, [Trait.Curse, Trait.Divine, Trait.Fire, MTraits.VisualOracle],
            MFeatNames.ForetellHarm);
        List<FeatName> allowedDomains4 = [FeatName.DomainDeath, FeatName.DomainHealing];
        if (ModManager.TryParse("PS_Pain", out FeatName pain))
            allowedDomains4.Add(pain);
        yield return new MysteryFeat(MysteryFeatNames.Life,
            "The never-ending flow of life force within living beings is palpable to you. You might uphold the sanctity of life, or perhaps you seek to undermine it.",
            SpellIds.LifeLink, SpellIds.DelayAffliction, SpellIds.LifeGivingForm,
            MySpells.SpellIds.VitalityLash, [SpellId.Soothe, SpellId.FalseLife, MySpells.SpellIds.GrislyGrowths],
            allowedDomains4, Skill.Medicine,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    if (value < 1)
                        return;
                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        BonusToSelfHealing = (_, action) => action != null && action.HasAnyTraits([Trait.Magical, Trait.Arcane, Trait.Divine, Trait.Primal, Trait.Occult]) ? new Bonus(-
                            (value * self.Level), BonusType.Status, "Curse of Outpouring Life", false) : null
                    });
                };
            },
            "Curse of Outpouring Life",
            "Magical effects that restore Hit Points to you take a status penalty equal to your level (minimum 1) times your cursebound value to the number of HP you recover.",
            "Life energy flows outward from you and connects you to all living things, but you expend your vital essence to do so. Your presence comforts the ill and injured, causes scars to fade slightly, spurs new growth in plants, and otherwise infuses your surroundings with vitality. As your life force seeps outward, it becomes more difficult to keep your body functioning.",
            IllustrationName.Heal, [Trait.Curse, Trait.Divine, Trait.Oracle],
            MFeatNames.NudgeTheScales);
        string fourCurse = Describe4StageCurse(
            "You gain electricity weakness 2 and electricity spells or effects that have additional effects for a creature made of metal treat you as though you were made of metal. Any immunity or resistance you have to such spells and effects is suppressed.",
            "Blowing winds impose a -2 circumstance penalty to ranged attack rolls you make.",
            "Your weakness to electricity is equal to 5 + your level.",
            "You take a -10-foot status penalty to your Speed.");
        yield return new MysteryFeat(MysteryFeatNames.Tempest,
            "he fury of the wind and waves pounds in your heart.",
            SpellIds.TempestTouch, SpellIds.ThunderBurst, SpellIds.TempestForm,
            SpellId.ElectricArc, [MySpells.SpellIds.Thunderstrike, SpellId.HydraulicTorrent, SpellId.ChainLightning],
            [FeatName.DomainAir, FeatName.DomainCold, FeatName.DomainLightning, FeatName.DomainWater], Skill.Nature,
            qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                        return;
                    int value = curse.Value;
                    curse.Description = value switch
                    {
                        < 2 => fourCurse.Remove(fourCurse.IndexOf("{b}Cursebound 2{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 3 => fourCurse.Remove(fourCurse.IndexOf("{b}Cursebound 3{/b}", StringComparison.Ordinal))
                            .Trim(),
                        < 4 => fourCurse.Remove(fourCurse.IndexOf("{b}Cursebound 4{/b}", StringComparison.Ordinal))
                            .Trim(),
                        _ => curse.Description
                    };
                    if (value >= 1)
                    {
                        self.WeaknessAndResistance.Resistances.RemoveAll(resist =>
                            resist.DamageKind == DamageKind.Electricity);
                        self.WeaknessAndResistance.Immunities.RemoveAll(imm => imm == DamageKind.Electricity);
                        self.Traits.Add(Trait.Metal);
                        self.AddQEffect(QEffect.DamageWeakness(DamageKind.Electricity,
                            value >= 3 ? 5 + self.Level : 2).WithExpirationEphemeral());
                    }
                    if (value >= 2)
                    {
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToAttackRolls = (_, action, _) => action.HasTrait(Trait.Ranged) && action.HasTrait(Trait.Attack) ? new Bonus(-2, BonusType.Circumstance, "Curse of Inclement Headwinds", false) : null
                        });
                    }
                    if (value >= 4)
                    {
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToAllSpeeds = _ => new Bonus(-2, BonusType.Status, "Curse of Inclement Headwinds", false)
                        });
                    }
                };
            },
            "Curse of Inclement Headwinds",
            fourCurse,
            "The weather seems to always oppose you in ways large and small. Even when you are calm and at rest, your hair and clothing are inconveniently blown about by gentle winds, you are slightly damp from the faintest drizzle, and your touch often comes with a static shock.",
            IllustrationName.PushingGust, [Trait.Curse, Trait.Divine, MTraits.VisualOracle],
            MFeatNames.ForetellHarm);
    }
    public static class MysteryFeatNames
    {
        public static readonly FeatName Cosmos = ModManager.RegisterFeatName("RE_CosmosMystery", "Mystery: Cosmos");
        public static readonly FeatName Ash = ModManager.RegisterFeatName("RE_AshMystery", "Mystery: Ash");
        public static readonly FeatName Battle = ModManager.RegisterFeatName("RE_BattleMystery", "Mystery: Battle");
        public static readonly FeatName Ancestors = ModManager.RegisterFeatName("RE_Ancestors", "Mystery: Ancestors");
        public static readonly FeatName Bones = ModManager.RegisterFeatName("RE_Bones", "Mystery: Bones");
        public static readonly FeatName Flames = ModManager.RegisterFeatName("RE_Flames", "Mystery: Flames");
        public static readonly FeatName Life = ModManager.RegisterFeatName("RE_LifeOracle", "Mystery: Life");
        public static readonly FeatName Tempest = ModManager.RegisterFeatName("RE_Tempest", "Mystery: Tempest");
    }

    public static IEnumerable<Feat> LoadOracleFeats()
    {
        yield return new TrueFeat(MFeatNames.OracularWarning, 1,
                "You have a premonition about impending danger that you use to warn your allies.",
                "{b}Trigger{/b} You are about to roll for initiative." +
                "\n\nEach ally within 20 feet gains a +2 status bonus to their initiative roll and gains temporary Hit Points equal to half your level.",
                [Trait.Auditory, Trait.Cursebound, Trait.Divine, Trait.Emotion, Trait.Mental, MTraits.RemasterOracle])
            .WithActionCost(0)
            .WithPermanentQEffect("Grant your allies a bonus to their initiative and temporary hit points, but increase your cursebound value.", qf =>
            {
                Creature self = qf.Owner;
                qf.StartOfCombatReaction = _ =>
                {
                    CombatAction warning = new CombatAction(self, IllustrationName.SeeInvisibility, "Oracular Warning",
                        [Trait.Cursebound, Trait.Auditory, Trait.Divine, Trait.Emotion, Trait.Mental],
                        "{b}Trigger{/b} You are about to roll for initiative." +
                        "\n\nEach ally within 20 feet gains a +2 status bonus to their initiative roll and gains temporary Hit Points equal to half your level.",
                        Target.Self())
                        .WithActionCost(0)
                        .WithEffectOnSelf(caster =>
                        {
                            foreach (Creature cr in caster.Battle.AllCreatures.Where(cr => cr.FriendOfAndNotSelf(caster) && cr.DistanceTo(caster) <= 4))
                            {
                                cr.AddQEffect(new QEffect
                                {
                                    BonusToInitiative = _ => new Bonus(2, BonusType.Status, "Oracular Warning")
                                });
                                cr.GainTemporaryHP(caster.Level/2);
                                if (!cr.EntersInitiativeOrder) continue;
                                caster.Battle.InitiativeOrder.Remove(cr);
                                cr.EnterInitiativeOrderNow();
                            }
                        });
                    return ReactionOption.WrapFullcast(warning, $"Grant your allies a +2 status bonus to their initiative and {self.Level/2} temporary hit points, but increase your cursebound value.");
                };
            });
        const string rulesText = "{b}Frequency{/b} once per round" +
                                 "\n{b}Requirements{/b} Your previous action was to Cast a non-cantrip Spell that dealt damage" +
                                 "\n\nAt the beginning of your target's next turn, it takes damage equal to twice the triggering spell's rank as a seemingly random and minor misfortune finds it. The damage is of a type matching the spell. The target is then temporarily immune to Foretell Harm until the next day.";
        yield return new TrueFeat(MFeatNames.ForetellHarm, 1,
                "Your magic echoes ominously as you glimpse injury in the target's future.", rulesText,
                [MTraits.RemasterOracle, Trait.Cursebound, Trait.Divine])
            .WithActionCost(0)
            .WithPermanentQEffect("You can deal damage to targets who have taken damage from your spells.", qf =>
            {
                Creature self = qf.Owner;
                qf.YouDealDamageEvent = async (_, damage) =>
                {
                    CombatAction? spell = damage.CombatAction;
                    if (spell == null || !spell.HasTrait(Trait.Spell) || spell.HasTrait(Trait.Cantrip))
                        return;
                    QEffect foretell = new()
                    {
                        ProvideMainAction = effect =>
                        {
                            CombatAction harm = new CombatAction(self, IllustrationName.ForeseenFailure,
                                "Foretell Harm", [Trait.Basic, Trait.Cursebound, Trait.Divine],
                                rulesText,
                                Target.Self().WithAdditionalRestriction(me => me.Actions.ActionHistoryThisTurn.Any(act => act.ActionId == RActionIds.ForetellHarm) ? "Foretell Harm can only be used once per round." : null)
                                    .WithAdditionalRestriction(me => spell.ChosenTargets.ChosenCreatures.Any(cr => !cr.HasEffect(MQEffectIds.ForetellHarm) && cr.EnemyOf(me)) ? null : "All enemies are immune to Foretell Harm."))
                                .WithActionCost(0)
                                .WithActionId(RActionIds.ForetellHarm)
                                .WithEffectOnSelf(async (cA, caster) =>
                                {
                                    QEffect foretold = new("Harm Foretold", $"At the start of your next turn you will take {spell.SpellLevel * 2} damage.", IllustrationName.ForeseenFailure)
                                    {
                                        StartOfYourPrimaryTurn = async (qEffect, creature) =>
                                        {
                                            if (creature.HasEffect(MQEffectIds.ForetellHarm))
                                            {
                                                qEffect.ExpiresAt = ExpirationCondition.Immediately;
                                                return;
                                            }
                                            await CommonSpellEffects.DealDirectDamage(cA, creature, $"{spell.SpellLevel * 2}",
                                                creature.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(
                                                    [.. damage.KindedDamages.Select(kd => kd.DamageKind)]));
                                            creature.AddQEffect(HarmForetold());
                                            qEffect.ExpiresAt = ExpirationCondition.Immediately;
                                        },
                                        Source = caster,
                                        SourceAction = cA
                                    };
                                    foreach (Creature cr in spell.ChosenTargets.ChosenCreatures.Where(cr2 => !cr2.HasEffect(MQEffectIds.ForetellHarm)))
                                    {
                                        cr.AddQEffect(foretold);
                                    }
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                });
                            return new ActionPossibility(harm).WithPossibilityGroup("Cursebound");
                        },
                        Key = "ForetellHarm",
                        YouBeginAction = async (effect, action) =>
                        {
                            if (action.ActionId != RActionIds.ForetellHarm)
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    };
                    self.AddQEffect(foretell);
                };
            });
        yield return new TrueFeat(MFeatNames.NudgeTheScales, 1,
                "You lay a finger on the scales of life and death to heal a creature, regardless of whether it's living or undead.",
                "You restore Hit Points equal to 2 + double your level to one creature within 30 feet.\nIn addition, you can meditate during your daily preparations to place yourself on one side of the scales. Choose life or death. If you align yourself with life, you can be targeted and healed by vitality healing effects, as normal for most living creatures; if you align yourself with death, you gain the void healing ability, and can instead be targeted and healed by void effects and other effects that restore Hit Points to undead creatures.",
                [MTraits.RemasterOracle, Trait.Cursebound, Trait.Divine, Trait.Healing, Trait.Spirit])
            .WithActionCost(1)
            .WithOnSheet(sheet =>
            {
                sheet.AddSelectionOption(new SingleFeatSelectionOption("NudgeTheScales", "Life or Death", SelectionOption.MORNING_PREPARATIONS_LEVEL, ft => ft.Tag is "NudgeTheScales"));
            })
            .WithPermanentQEffect("You restore Hit Points equal to 2 + double your level to one creature within 30 feet.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction nudge = new CombatAction(self, new SideBySideIllustration(IllustrationName.Heal, IllustrationName.Harm),
                            "Nudge the Scales",
                            [Trait.Cursebound, Trait.Divine, Trait.Healing, Trait.Spirit, Trait.Basic],
                            $"The target gains {self.Level * 2 + 2} Hit Points.",
                            Target.RangedCreature(6))
                        .WithActionCost(1)
                        .WithSoundEffect(SfxName.Healing)
                        .WithEffectOnEachTarget(async (spell, caster, target, _) => await target.HealAsync($"{caster.Level * 2 + 2}", spell));
                    return new ActionPossibility(nudge).WithPossibilityGroup("Cursebound");
                };
            });
        yield return new Feat(ModManager.RegisterFeatName("RE_Life", "Life"), null,
                "You can be healed by vitality (positive) effects and harmed by void (negative) effects.", [], null)
            .WithTag("NudgeTheScales")
            .WithOnCreature(cr => cr.Traits.Remove(Trait.Undead));
        yield return new Feat(ModManager.RegisterFeatName("RE_Death", "Death"), null,
                "You can be healed by void (negative) effects and harmed by vitality (positive) effects.", [], null)
            .WithTag("NudgeTheScales")
            .WithOnCreature(cr => cr.Traits.Add(Trait.Undead));
        yield return new TrueFeat(MFeatNames.WhispersOfWeakness, 1,
                "Voices whisper to you how to best lay a creature low.",
                "You target one creature within 60 feet; it takes a -2 circumstance penalty to the next saving throw it makes before the start of your next turn. In addition, you gain a +2 status bonus to your next attack roll (or skill check made as part of an attack action) against that foe before the end of your turn. The target is then temporarily immune for the rest of the encounter.",
                [Trait.Cursebound, Trait.Divine, MTraits.RemasterOracle, Trait.Rebalanced])
            .WithActionCost(1)
            .WithPermanentQEffect(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction whisper = new CombatAction(self,
                            MIllustrations.CreateIllustration("WhispersOfWeakness"),
                            "Whispers of Weakness",
                            [Trait.Cursebound, Trait.Divine],
                            "The target takes a -2 circumstance penalty to the next saving throw it makes before the start of your next turn. In addition, you gain a +2 status bonus to your next attack roll (or skill check made as part of an attack action) against that foe before the end of your turn. The target is then temporarily immune for the rest of the encounter.",
                            Target.Ranged(12))
                        .WithActionCost(1)
                        .WithShortDescription("The target takes a -2 circumstance penalty to its next save and you gain a +2 status bonus to your next attack roll against it.")
                        .WithActionId(RActionIds.WhispersOfWeakness)
                        .WithEffectOnEachTarget(async (spell, caster, target, _) =>
                        {
                            target.AddQEffect(new QEffect("Whispers of Weakness", "You take a -2 circumstance penalty to the next save you make.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, spell.Illustration)
                            {
                                BonusToDefenses = (_, action, defense) => action?.SavingThrow != null && defense.IsSavingThrow() ? new Bonus(-2, BonusType.Circumstance, "Whispers of Weakness") : null,
                                AfterYouMakeSavingThrow = (effect, _, result) =>
                                {
                                    if (result.CheckBreakdown.CheckBonuses != null && result.CheckBreakdown.CheckBonuses.Any(b => b?.BonusSource == "Whispers of Weakness"))
                                        effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                                }
                            });
                            caster.AddQEffect(new QEffect("Whispers of Weakness", $"Your next attack made against {target.Name} gains a +2 status bonus.", ExpirationCondition.ExpiresAtEndOfYourTurn, caster, spell.Illustration)
                            {
                                BonusToAttackRolls = (_, action, creature) => action.HasTrait(Trait.Attack) && creature == target ? new Bonus(2, BonusType.Status, "Whispers of Weakness") : null,
                                AfterYouMakeAttackRoll = (effect, result) =>
                                {
                                    if (result.CheckBreakdown.CheckBonuses != null && result.CheckBreakdown.CheckBonuses.Any(b => b?.BonusSource == "Whispers of Weakness"))
                                        effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                                },
                                DoNotShowUpOverhead = true
                            });
                            target.AddQEffect(QEffect.ImmunityToTargeting(RActionIds.WhispersOfWeakness));
                        });
                    return new ActionPossibility(whisper).WithPossibilityGroup("Cursebound");
                };
            });
        const string rulesText2 = "Until the end of the encounter, you gain a +5-foot status bonus to Speed." +
                                 "\n\n\nIf you become cursebound 2 at any point during this duration, the bonus increases to a +10-foot status bonus. If you become cursebound 3 at any point during this duration, this bonus increases to +15-foot status bonus instead. Finally, if you become cursebound 4 at any point during this duration, this bonus increases to a +20-foot status bonus.";
        yield return new TrueFeat(MFeatNames.TranceOfCelerity, 1,
                "Your feet move faster, almost out of your conscious control.",
                rulesText2,
                [MTraits.RemasterOracle, Trait.Cursebound, Trait.Divine])
            .WithActionCost(1)
            .WithPermanentQEffect("Until the end of the encounter, you gain a +5-foot status bonus to Speed. This bonus increases to +10 at cursebound 2, +15 at cursebound 3, and +20 at cursebound 4.",qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    Illustration celerity = MIllustrations.CreateIllustration("TranceOfCelerity");
                    CombatAction trance = new CombatAction(self, celerity, "Trance of Celerity",
                            [Trait.Cursebound, Trait.Divine, Trait.Basic],
                            rulesText2,
                            Target.Self())
                        .WithActionCost(1)
                        .WithSoundEffect(SfxName.MinorAbjuration)
                        .WithEffectOnSelf(cr => cr.AddQEffect(new QEffect
                        {
                            DoNotShowUpOverhead = true,
                            StateCheck = effect =>
                            {
                                int value = effect.Owner.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                                effect.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                {
                                    BonusToAllSpeeds = _ => new Bonus(value,  BonusType.Status, "Trance of Celerity"),
                                    Description = $"You gain a +{value * 5}-foot status bonus to Speed.",
                                    Name = "Trance of Celerity",
                                    Illustration = celerity
                                });
                            }
                        }));
                    return new ActionPossibility(trance).WithPossibilityGroup("Cursebound");
                };
            });
        const string abundantFlavor = "The wellspring of magic within you coalesces into additional spells.";
        yield return CommonFeatTemplates.CreateAbundantSpontaneousSpellcasting(
            ModManager.RegisterFeatName("RE_AbundantOracle1", "Abundant Spellcasting 1"), 1, MTraits.RemasterOracle,
            abundantFlavor);
        yield return CommonFeatTemplates.CreateAbundantSpontaneousSpellcasting(
            ModManager.RegisterFeatName("RE_AbundantOracle2", "Abundant Spellcasting 2"), 2, MTraits.RemasterOracle,
            abundantFlavor);
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_OracleCantripExpansion", "Cantrip Expansion"), 2, "A greater understanding of your magic broadens your range of simple spells.", "Add two additional cantrips from your spell list to your repertoire.",
        [MTraits.RemasterOracle])
            .WithOnSheet(values => values.AddSelectionOption(new AddToSpellRepertoireOption("CantripExpansionOracle", "Cantrip Expansion cantrips", -1, MTraits.RemasterOracle, Trait.Divine, 0, 2)));
        List<Feat> domainFeats = [];
        foreach (Feat feat in Domains())
        {
            feat.WithPrerequisite(sheet =>
                sheet.Tags["OracleMystery"] is MysteryFeat mystery && mystery.AllowedDomains.Any(domain =>
                    feat.FeatName.ToStringOrTechnical() == "RE_OracleDomain" + domain), "The domain must be associated with your mystery.");
            domainFeats.Add(feat);
        }
        yield return new TrueFeat(MFeatNames.DomainAcumen, 2,
                "Every oracle's mystery touches on a divine domain of the deities that fuel it; you can access that power.",
                "Choose a domain associated with your mystery that you haven't already chosen. You gain the initial domain spell from that domain, which you cast as a focus spell. Increase the amount of focus points in your pool by 1, to a maximum of 3.",
                [MTraits.RemasterOracle], domainFeats)
            .WithMultipleSelection();
        List<Feat> advancedDomains = [];
        foreach (Feat feat in CampfireChronicler.DuplicateAdvancedDomains(MTraits.RemasterOracle))
        {
            feat.WithPrerequisite(sheet =>
                feat.Tag is Feat originalDomain && sheet.Tags["OracleMystery"] is MysteryFeat mystery &&
                mystery.AllowedDomains.Contains(originalDomain.FeatName), "The domain must be associated with your mystery.");
            advancedDomains.Add(feat);
        }
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_DomainFluency", "Domain Fluency"), 12,
                "You command a deep understanding of the domains related to your mystery.",
                "Choose one of the domains associated with your mystery for which you have an initial domain spell. You gain an advanced domain spell from that domain, which you cast as a revelation spell.",
                [MTraits.RemasterOracle], advancedDomains)
            .WithMultipleSelection()
            .WithPrerequisite(MFeatNames.DomainAcumen, "Domain Acumen");
        
        const string rulesText3 = "Roll 1d4 to see what type of spirit is drawn to you. Your next action must be the type of action the spirit prefers, but you also gain the listed benefit for the action as the spirit guides you. If you attempt to use a different action, you must succeed at a DC 6 flat check or the action is lost." +
                                  "\n\n{b}1 Warrior{/b} You must attempt a Strike. You gain a +1 status bonus to your attack roll and a +2 status bonus to damage, or a +6 status bonus to damage if you are at least cursebound 3." +
                                  "\n{b}2 Adept{/b} You must attempt a Perception check or a skill action. You gain a +1 status bonus to the check, or a +2 status bonus if you are cursebound 3." +
                                  "\n{b}3 Sage{/b} You must attempt to Cast a Spell. You gain a status bonus to the spell's damage or healing equal to the spell's rank, or equal to the spell's rank + 3 if you are at least cursebound 3." +
                                  "\n{b}4 Wanderer{/b} You must attempt a Stride action. You gain a +10-foot status bonus to your Speed for the action, or a +20-foot status bonus if you are at least cursebound 3.";
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_MeddlingFutures", "Meddling Futures"), 2,
                "You open yourself to the guidance of whatever spirits or powers deign to help you.",
                rulesText3,
                [Trait.Cursebound, Trait.Divine, MTraits.RemasterOracle])
            .WithActionCost(0)
            .WithPermanentQEffect("You can gain a benefit to a random action type, at the cost of being forced to use that action type as your next action.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction meddle = new CombatAction(self, IllustrationName.PhantasmalOrchestra,
                            "Meddling Futures", [Trait.Cursebound, Trait.Divine, Trait.Basic],
                            rulesText3,
                            Target.Self())
                        .WithActionCost(0)
                        .WithSoundEffect(SfxName.MinorAbjuration)
                        .WithActionId(RActionIds.MeddlingFutures)
                        .WithEffectOnSelf(caster =>
                        {
                            bool greater = caster.FindQEffect(MQEffectIds.OracleCurse)?.Value >= 3;
                            List<QEffect> futures = 
                            [
                                Future("Warrior", $"You must attempt a Strike. You gain a +1 status bonus to your attack roll and a +{(greater ? 6 : 2)} status bonus to damage.", greater ? 6 : 2, Trait.BasicStrike,IllustrationName.MagicWeapon),
                                Future("Adept", $"You must attempt a Perception check or a skill action. You gain a +{(greater ? 2 : 1)} status bonus to the check.", greater ? 2 : 1, Trait.Skill, IllustrationName.EyeOfFortune),
                                Future("Sage", $"You must attempt to Cast a Spell. You gain a status bonus to the spell's damage or healing equal to the spell's rank{(greater ? " + 3" : "")}.", greater ? 3 : 0, Trait.Spell, IllustrationName.CastASpell)
                                    .AddGrantingOfTechnical(_ => true, qfTech =>
                                    {
                                        qfTech.BonusToSelfHealing = (_, action) =>
                                        {
                                            if (action?.Owner != caster || !action.HasTrait(Trait.Spell) ||
                                                action.SpellInformation == null)
                                                return null;
                                            return new Bonus(action.SpellLevel + (greater ? 3 : 0), BonusType.Status,
                                                "Meddling Futures");
                                        };
                                    }),
                                Future("Wanderer", $"You must attempt a Stride action. You gain a +{(greater ? 2 : 1)}0-foot status bonus to your Speed for the action.", greater ? 4 : 2, Trait.Move, IllustrationName.FleetStep)
                            ];
                            int roll = R.Next(1, 5);
                            QEffect chosen = futures[roll - 1];
                            caster.Battle.Log($"You rolled {{b}}{roll}{{/b}}. The {{b}}{chosen.Name}{{/b}} guides your actions.");
                            caster.AddQEffect(chosen);
                        });
                    return new ActionPossibility(meddle).WithPossibilityGroup("Cursebound");
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_KnowledgeOfShapes", "Knowledge of Shapes"), 4,
                "Inspiration lets you surpass your preconceptions of your spells' limits.",
                "If the next action you take is to Cast a Spell modified by Reach Spell or Widen Spell, its action cost will be reduced by 1.",
                [MTraits.RemasterOracle, Trait.Cursebound, Trait.Metamagic])
            .WithActionCost(0)
            .WithPrerequisite(values => values.HasFeat(FeatName.ReachSpell) || values.HasFeat(FeatName.WidenSpell), "You must have the Reach Spell or Widen Spell feat.")
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction shape = new CombatAction(self,
                            new SideBySideIllustration(IllustrationName.MetamagicReach,
                                IllustrationName.MetamagicWiden), "Knowledge of Shapes",
                            [Trait.Cursebound, Trait.Basic],
                            "If the next action you take is to Cast a Spell modified by Reach Spell or Widen Spell, its action cost will be reduced by 1.",
                            Target.Self())
                        .WithActionCost(0)
                        .WithActionId(RActionIds.KnowledgeOfShapes)
                        .WithEffectOnSelf(caster =>
                        {
                            caster.RemoveAllQEffects(qff => qff.Id == MQEffectIds.KnowledgeOfShapes);
                            caster.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                            {
                                ModifyActionPossibility = (_, spell) =>
                                {
                                    if (!spell.HasTrait(Trait.Spell) || spell.HasTrait(MTraits.DoNotReduce) || (!spell.Name.StartsWith("Reach") && !spell.Name.StartsWith("Widened")))
                                        return;
                                    spell.ActionCost -= 1;
                                },
                                AfterYouTakeAction = async (effect, spell) =>
                                {
                                    if (spell.ActionId != RActionIds.KnowledgeOfShapes)
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                },
                                Id = MQEffectIds.KnowledgeOfShapes,
                                StateCheck = _ =>
                                {
                                    if (caster.HasFeat(FeatName.ReachSpell))
                                        caster.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                        {
                                            MetamagicProvider = new MetamagicProvider("Reach Spell (3 Action)",
                                                IllustrationName.MetamagicReach, false,
                                                sp =>
                                                {
                                                    CombatAction metamagicSpell =
                                                        Spell.DuplicateSpell(sp).CombatActionSpell;
                                                    if (metamagicSpell.ActionCost is -2 or 1 or 2 or -3)
                                                        return null;
                                                    if (!IncreaseTargetLine(metamagicSpell.Target))
                                                        return null;
                                                    metamagicSpell.Traits.Add(MTraits.DoNotReduce);
                                                    metamagicSpell.Name = "Reach " + metamagicSpell.Name;
                                                    if (metamagicSpell.ActionCost != 3)
                                                    {
                                                        metamagicSpell.ActionCost = 3;
                                                        metamagicSpell.SpentActions = 3;
                                                    }
                                                    string? description = metamagicSpell.Target.ToDescription();
                                                    int num = description?.Count(c => c == '\n') ?? 0;
                                                    string[] strArray = metamagicSpell.Description.Split('\n', 4 + num);
                                                    if (strArray.Length >= 4)
                                                        metamagicSpell.Description =
                                                            $"{strArray[0]}\n{strArray[1]}\n{{Blue}}{metamagicSpell.Target.ToDescription()}{{/Blue}}\n{strArray[3 + num]}";
                                                    return metamagicSpell;

                                                    bool IncreaseTargetLine(Target? targetLine)
                                                    {
                                                        while (true)
                                                        {
                                                            switch (targetLine)
                                                            {
                                                                case null:
                                                                    return false;
                                                                case CreatureTarget creatureTarget3:
                                                                    return IncreaseTarget(creatureTarget3);
                                                                case MultipleCreatureTargetsTarget creatureTargetsTarget2:
                                                                    bool flag1 = creatureTargetsTarget2.Targets.Aggregate(false, (current, target) => current | IncreaseTarget(target));
                                                                    return flag1;
                                                                case BurstAreaTarget burstAreaTarget2:
                                                                    burstAreaTarget2.Range += 6;
                                                                    return true;
                                                                case DependsOnActionsSpentTarget actionsSpentTarget2:
                                                                    targetLine = actionsSpentTarget2.IfThreeActions;
                                                                    continue;
                                                                case DependsOnSpellVariantTarget spellVariantTarget2:
                                                                    var flag2 = false;
                                                                    foreach (Target target in spellVariantTarget2.Targets)
                                                                    {
                                                                        if (target is CreatureTarget creatureTarget4) flag2 |= IncreaseTarget(creatureTarget4);
                                                                    }
                                                                    return flag2;
                                                                default:
                                                                    return false;
                                                            }
                                                        }
                                                    }

                                                    bool IncreaseTarget(CreatureTarget creatureTarget)
                                                    {
                                                        if (creatureTarget.RangeKind == RangeKind.Melee)
                                                        {
                                                            metamagicSpell.Traits = new Traits(metamagicSpell.Traits
                                                                .Except([Trait.Melee])
                                                                .Concat([Trait.Ranged]), metamagicSpell);
                                                            creatureTarget.RangeKind = RangeKind.Ranged;
                                                            creatureTarget.CreatureTargetingRequirements
                                                                .RemoveAll(ctr =>
                                                                {
                                                                    return ctr switch
                                                                    {
                                                                        AdjacencyCreatureTargetingRequirement _
                                                                            or AdjacentOrSelfTargetingRequirement _
                                                                            or NaturalReachCreatureTargetingRequirement
                                                                            _ => true,
                                                                        _ => ctr is
                                                                            MeleeReachCreatureTargetingRequirement
                                                                    };
                                                                });
                                                            creatureTarget.CreatureTargetingRequirements.Add(
                                                                new MaximumRangeCreatureTargetingRequirement(6));
                                                            creatureTarget.CreatureTargetingRequirements.Add(
                                                                new UnblockedLineOfEffectCreatureTargetingRequirement());
                                                            creatureTarget.OverriddenFullTargetLine = null;
                                                            return true;
                                                        }

                                                        MaximumRangeCreatureTargetingRequirement? targetingRequirement =
                                                            creatureTarget.CreatureTargetingRequirements
                                                                .OfType<MaximumRangeCreatureTargetingRequirement>()
                                                                .FirstOrDefault();
                                                        if (targetingRequirement == null)
                                                            return false;
                                                        targetingRequirement.Range += 6;
                                                        creatureTarget.OverriddenFullTargetLine = null;
                                                        return true;
                                                    }
                                                })
                                        });
                                    if (caster.HasFeat(FeatName.WidenSpell))
                                        caster.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                        {
                                            MetamagicProvider = new MetamagicProvider("Widen Spell (3 Action)", IllustrationName.MetamagicWiden, false, spell =>
                                            {
                                                CombatAction combatActionSpell = Spell.DuplicateSpell(spell).CombatActionSpell;
                                                if (Constants.IsVariableActionCost(combatActionSpell.ActionCost) || combatActionSpell.HasTrait(Trait.SpellWithDuration) || combatActionSpell.ActionCost != 3)
                                                    return null;
                                                switch (combatActionSpell.Target)
                                                {
                                                    case BurstAreaTarget target7:
                                                        ++target7.Radius;
                                                        break;
                                                    case ConeAreaTarget target6:
                                                        target6.ConeLength += target6.ConeLength <= 3 ? 1 : 2;
                                                        break;
                                                    default:
                                                    {
                                                        if (combatActionSpell.Target is not LineAreaTarget target5)
                                                            return null;
                                                        target5.LineLength += 2;
                                                        break;
                                                    }
                                                }
                                                combatActionSpell.Traits.Add(MTraits.DoNotReduce);
                                                combatActionSpell.Name = "Widened " + combatActionSpell.Name;
                                                string? description = combatActionSpell.Target.ToDescription();
                                                int num = description?.Count(c => c == '\n') ?? 0;
                                                string[] strArray = combatActionSpell.Description.Split('\n', 4 + num);
                                                if (strArray.Length >= 4 && combatActionSpell.Target is AreaTarget target8)
                                                    combatActionSpell.Description = $"{strArray[0]}\n{strArray[1]}\n{{Blue}}{target8.ToDescription()}{{/Blue}}\n{strArray[3 + num]}";
                                                return combatActionSpell;
                                            })
                                        });
                                }
                            });
                        });
                    return new ActionPossibility(shape).WithPossibilityGroup("Cursebound");
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_GiftedPower", "Gifted Power"), 6,
                "Your mystery grants you additional magic.",
                "You have an extra spell slot of your highest rank, which you can use only to cast one of your mystery's granted spells, heightened to this rank." +
                "\n\n{b}Special{/b} If you have the divine access class feature or Mysterious Repertoire feat, you can cast spells that you learned from those abilities using the additional spell slot from Gifted Power.",
                [MTraits.RemasterOracle])
            .WithOnSheet(sheet =>
            {
                sheet.SpellRepertoires.Add(MTraits.GiftedPower, new SpellRepertoire(Ability.Charisma, Trait.Divine));
                if (!sheet.SpellRepertoires.TryGetValue(MTraits.GiftedPower, out SpellRepertoire? value) || sheet.Tags["OracleMystery"] is not MysteryFeat mystery)
                    return;
                value.SpellSlots[sheet.MaximumSpellLevel]++;
                value.AdditionalSpellsAllowed.AddRange(mystery.DivineAccess.Where(sp => AllSpells.CreateModernSpellTemplate(sp, MTraits.GiftedPower).MinimumSpellLevel <= sheet.MaximumSpellLevel));
                value.AdditionalSpellsAllowed.AddRange(mystery.GrantedSpells.Where(sp => AllSpells.CreateModernSpellTemplate(sp, MTraits.GiftedPower).MinimumSpellLevel <= sheet.MaximumSpellLevel));
                List<SpellId> spellList =
                [
                    .. mystery.DivineAccess.Where(sp =>
                        AllSpells.CreateModernSpellTemplate(sp, MTraits.GiftedPower).MinimumSpellLevel <=
                        sheet.MaximumSpellLevel),

                    .. mystery.GrantedSpells.Where(sp =>
                        AllSpells.CreateModernSpellTemplate(sp, MTraits.GiftedPower).MinimumSpellLevel <=
                        sheet.MaximumSpellLevel)
                ];
                if (sheet.Class?.ClassTrait != MTraits.RemasterOracle)
                {
                    spellList.RemoveAll(sp => !sheet.SpellRepertoires[MTraits.RemasterOracle].SpellsKnown.Contains(AllSpells.CreateModernSpellTemplate(sp, MTraits.RemasterOracle)));
                }
                value.SpellsKnown.AddRange(spellList.Distinct().Select(id => AllSpells.CreateModernSpellTemplate(id, MTraits.GiftedPower, sheet.MaximumSpellLevel)));
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_DebilitatingDichotomy", "Debilitating Dichotomy"), 8,
                "You reveal a glimpse of the impossible conflicts between the divine anathema behind your curse, forcing you to reckon with another's conflicts as well.",
                "You and one creature within 30 feet each take 9d6 mental damage with a basic Will save, and the target is stunned 1 if it critically fails its save. You get a degree of success one better than you rolled for your saving throw. At 10th level, and every 2 levels thereafter, the damage increases by 3d6.",
                [Trait.Concentrate, Trait.Cursebound, Trait.Divine, Trait.Mental, MTraits.RemasterOracle])
            .WithActionCost(2)
            .WithPermanentQEffect("", qf =>
            {
                Creature self = qf.Owner;
                int heighten = self.Level % 2 == 0 ? self.Level - 8 : self.Level - 9;
                int diceCount = 9 + heighten / 2 * 3;
                qf.Description = $"You and one creature within 30 feet each take {S.HeightenedVariable(diceCount, 9)}d6 mental damage with a basic Will save, and the target is stunned 1 if it critically fails its save. You get a degree of success one better than you rolled for your saving throw.";
                qf.ProvideMainAction = _ =>
                {
                    CombatAction dichotomy = new CombatAction(self, IllustrationName.DebilitatingDichotomy,
                            "Debilitating Dichotomy",
                            [Trait.Concentrate, Trait.Cursebound, Trait.Divine, Trait.Mental, Trait.Basic],
                            $"You and the target each take {S.HeightenedVariable(diceCount, 9)}d6 mental damage with a basic Will save, and the target is stunned 1 if it critically fails its save. You get a degree of success one better than you rolled for your saving throw.",
                            Target.Ranged(6))
                        .WithActionCost(2)
                        .WithSoundEffect(SfxName.Mental)
                        .WithSavingThrow(new SavingThrow(Defense.Will, cr => cr?.Spellcasting?.Sources.MaxBy(src => src.GetSpellSaveDC())?.GetSpellSaveDC() ?? 10))
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            QEffect betterSave = new()
                            {
                                AdjustSavingThrowCheckResult = (_, _, action, check) => action != spell ? check : check.ImproveByOneStep()
                            };
                            caster.AddQEffect(betterSave);
                            CheckResult check = await CommonSpellEffects.RollSavingThrowAsync(caster, spell, Defense.Will, caster.Spellcasting?.Sources.MaxBy(src => src.GetSpellSaveDC())?.GetSpellSaveDC() ?? 10);
                            await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, $"{diceCount}d6", DamageKind.Mental);
                            await CommonSpellEffects.DealBasicDamage(spell, caster, caster, check, $"{diceCount}d6", DamageKind.Mental);
                            betterSave.ExpiresAt = ExpirationCondition.Immediately;
                            if (result == CheckResult.CriticalFailure)
                                target.AddQEffect(QEffect.Stunned(1));
                        });
                    return new ActionPossibility(dichotomy).WithPossibilityGroup("Cursebound");
                };
            });
        const string rules = "Once per encounter: roll 1d4 and use the corresponding result below." +
                             "\n{b}1 Good{/b} You or an ally within 30 feet can roll twice on your next attack roll or skill check, taking the higher result. This is a fortune effect." +
                             "\n{b}2 Bad{/b} One creature you are observing within 30 feet must succeed at a Will save against your spell DC; on a failure, the target must roll twice on their next attack roll or skill check that takes at least one action to perform, taking the lower result. This is a misfortune effect." +
                             "\n{b}3 Mixed{/b} You gain the benefits of rolling both a 1 and a 2." +
                             "\n{b}4 Cursed{/b} Your attempts to meddle in the forces of prophecy bring dire consequences for all. Every creature within 30 feet of you when you perform the augury rolls twice on their next attack roll or skill check that takes at least one action to perform; if the highest number rolled is odd, they take the lower result, and if the highest number rolled is even, they take the higher result. If they took the lower result, this effect has the misfortune trait for them, and if they took the higher result, it has the fortune trait.";
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_RollTheBonesOfFate", "Roll the Bones of Fate"), 10,
                "You roll a handful of bones to learn (or perhaps influence) the future course of events.",
                rules,
                [Trait.Cursebound, Trait.Divine, MTraits.RemasterOracle, Trait.Prediction])
            .WithActionCost(1)
            .WithPrerequisite(values => values.HasFeat(MysteryFeatNames.Bones), "You must have the bones mystery.")
            .WithPermanentQEffect("Once per encounter, you can randomly apply a fortune or misfortune effect.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction fate = new CombatAction(self, IllustrationName.BitOfLuck,
                        "Roll the Bones of Fate", [Trait.Cursebound, Trait.Divine, MTraits.RemasterOracle, Trait.Prediction, Trait.Basic], rules, 
                        Target.Self().WithAdditionalRestriction(cr => cr.HasEffect(MQEffectIds.BonesOfFate) ? "Roll the Bones of Fate can only be used once per encounter." : null))
                        .WithSoundEffect(SfxName.BitOfLuck)
                        .WithActionCost(1)
                        .WithEffectOnSelf(async (_, caster) =>
                        {
                            int roll = R.Next(1, 5);
                            List<string> possibles = ["good", "bad", "mixed", "cursed"];
                            caster.Battle.Log($"You rolled {{b}}{roll}{{/b}}. The fates shall be {{b}}{possibles[roll-1]}{{/b}}!");
                            QEffect good = new("Good Fortune", "You roll twice on your next attack roll or skill check, taking the higher result.", IllustrationName.D20CriticalSuccess)
                            {
                                RerollActiveRoll = async (effect, _, action, _) =>
                                {
                                    if (action.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill == null &&
                                        !action.HasTrait(Trait.Attack)) return RerollDirection.DoNothing;
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                    return RerollDirection.RerollAndKeepBest;
                                }
                            };
                            QEffect bad = new("Bad Fortune", "You roll twice on your next attack roll or skill check that takes at least 1 action, taking the lower result.", IllustrationName.D20CriticalFailure)
                            {
                                RerollActiveRoll = async (effect, _, action, _) =>
                                {
                                    if (action.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill == null &&
                                        !action.HasTrait(Trait.Attack)) return RerollDirection.DoNothing;
                                    if (action.ActionCost < 1)
                                        return RerollDirection.DoNothing;
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                    return RerollDirection.RerollAndKeepWorst;
                                }
                            };
                            CombatAction goodRoll = CombatAction.CreateSimple(caster, "Roll the Bones of Fate (Good)",
                                    Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName,
                                    Trait.DoesNotRequireAttackRollOrSavingThrow)
                                .WithAction(ca =>
                                {
                                    ca.Target = Target.RangedFriend(6);
                                    ca.Illustration = IllustrationName.D20CriticalSuccess;
                                })
                                .WithActionCost(0)
                                .WithEffectOnEachTarget(async (_, _, target, _) => target.AddQEffect(good));
                            CombatAction badRoll = CombatAction.CreateSimple(caster, "Roll the Bones of Fate (Bad)",
                                    Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName,
                                    Trait.DoesNotRequireAttackRollOrSavingThrow)
                                .WithAction(ca =>
                                {
                                    ca.Target = Target.Ranged(6);
                                    ca.Illustration = IllustrationName.D20CriticalFailure;
                                })
                                .WithActionCost(0)
                                .WithSavingThrow(new SavingThrow(Defense.Will,
                                    caster.Spellcasting?.Sources.MaxBy(src => src.GetSpellSaveDC())?.GetSpellSaveDC() ??
                                    10))
                                .WithEffectOnEachTarget(async (_, _, target, result) =>
                                {
                                    if (result > CheckResult.Failure)
                                        return;
                                    target.AddQEffect(bad);
                                });
                            switch (roll)
                            {
                                case 1:
                                    await caster.Battle.GameLoop.FullCast(goodRoll);
                                    break;
                                case 2:
                                    await caster.Battle.GameLoop.FullCast(badRoll);
                                    break;
                                case 3:
                                    await caster.Battle.GameLoop.FullCast(goodRoll);
                                    await caster.Battle.GameLoop.FullCast(badRoll);
                                    break;
                                case 4:
                                    await caster.Battle.GameLoop.FullCast(CombatAction.CreateSimple(caster, "Roll the Bones of Fate (Cursed)",
                                            Trait.DoNotShowInCombatLog, Trait.DoNotShowOverheadOfActionName,
                                            Trait.DoesNotRequireAttackRollOrSavingThrow)
                                        .WithAction(ca =>
                                        {
                                            ca.Target = Target.Emanation(6);
                                            ca.Illustration = IllustrationName.BestowCurse;
                                        })
                                        .WithActionCost(0)
                                        .WithEffectOnEachTarget(async (_, _, target, _) => target.AddQEffect(CursedFate())));
                                    break;
                            }
                            caster.AddQEffect(new QEffect { Id = MQEffectIds.BonesOfFate });
                        });
                    return new ActionPossibility(fate).WithPossibilityGroup("Cursebound");
                };
            });
        const string rules2 = "Two ghostly warriors manifest within a 30-foot emanation of you and each attempt a Strike against an adjacent enemy, using your spell attack modifier. The warriors' Strikes each deal 4d6 spirit damage and the warriors can flank with one another and with you and your allies. If you are cursebound 2 when you use The Dead Walk, you instead summon three warriors, and if you are cursebound 3, you instead summon four warriors. The warriors disappear at the start of your next turn.";
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_TheDeadWalk", "The Dead Walk"), 10,
                "You beseech warrior spirits to come forth and aid you.",
                rules2, [Trait.Cursebound, Trait.Divine, MTraits.RemasterOracle])
            .WithActionCost(2)
            .WithPrerequisite(values => values.HasFeat(MysteryFeatNames.Battle) || values.HasFeat(MysteryFeatNames.Ancestors), "You must have the ancestors or battle mystery.")
            .WithPermanentQEffect("You can summon temporary ghost warriors who Strike your enemies using your spell attack modifier for 4d6 spirit damage.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    int value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                    CombatAction walk = new CombatAction(self, IllustrationName.PhantasmalOrchestra, "The Dead Walk",
                        [Trait.Cursebound, Trait.Divine, Trait.Basic], rules2,
                        new MultipleSummonsTarget(6, value >= 3 ? 4 : value >= 2 ? 3 : 2, "", t => t.WithAdditionalTargetingRequirement((caster, t2) =>
                            caster.Battle.AllCreatures.Any(cr => cr.EnemyOf(caster) && cr.IsAdjacentTo(t2))
                                ? Usability.Usable
                                : Usability.NotUsableOnThisCreature("You must select a tile adjacent to an enemy creature."))))
                        .WithActionCost(2)
                        .WithSoundEffect(SfxName.Necromancy)
                        .WithEffectOnChosenTargets(async (_, caster, targets) =>
                        {
                            List<Creature> summons = [];
                            foreach (Tile tile in targets.ChosenTiles)
                            {
                                Creature summon = GhostWarrior(caster.Level, caster.Spellcasting!.Sources.MaxBy(src => src.GetSpellAttack())!.GetSpellAttack());
                                caster.Battle.SpawnCreature(summon, caster.Battle.You, tile);
                                summons.Add(summon);
                            }
                            foreach (Creature summon in summons)
                            {
                                CombatAction strike = summon.CreateStrike(summon.UnarmedStrike).WithActionCost(0);
                                await summon.Battle.GameLoop.FullCast(strike);
                            }
                            caster.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtStartOfYourTurn)
                            {
                                WhenExpires = _ =>
                                {
                                    foreach (Creature summon in summons)
                                    {
                                        summon.AddQEffect(new QEffect
                                        {
                                            StateCheckWithVisibleChanges = async q =>
                                            {
                                                await summon.DieFastAndWithoutAnimation();
                                                q.ExpiresAt = ExpirationCondition.Immediately;
                                            }
                                        });
                                    }
                                }
                            });
                        });
                    return new ActionPossibility(walk).WithPossibilityGroup("Cursebound");
                };
            });
        const string rules3 = "A rain of blazing bolts begins to fall from the heavens in a 10-foot emanation, centered on you, that deals 2d6 fire damage to all creatures in the emanation at the end of each of your turns (basic Reflex save); you can't exclude yourself from the emanation. The skyfire persists for 10 rounds; while you can't Dismiss it, you can suppress the effect for 1 round with a Sustain action. The rain of fire is suppressed if you fall unconscious. If you become cursebound 3 or 4 at any point during Trial by Skyfire's duration, the emanation increases to 15 feet and the damage increases to 4d6.";
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_TrialBySkyfire", "Trial by Skyfire"), 10,
                "Your lips murmur as you portend a great disaster, one you hope you survive.",
                rules3,
                [Trait.Cursebound, Trait.Divine, Trait.Fire, MTraits.RemasterOracle])
            .WithActionCost(1)
            .WithPrerequisite(
                values => values.HasFeat(MysteryFeatNames.Cosmos) || values.HasFeat(MysteryFeatNames.Flames),
                "You must have the cosmos or flames mystery.")
            .WithPermanentQEffect("You can create a rain of fire around yourself, dealing 2d6 fire damage to all creatures in a 10-foot emanation.", qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    CombatAction skyfire = new CombatAction(self, MIllustrations.CreateIllustration("Skyfire"), "Trial by Skyfire",
                        [Trait.Cursebound, Trait.Divine, Trait.Fire, Trait.Basic],
                        rules3,
                        Target.Self())
                        .WithSoundEffect(SfxName.FieryBurst)
                        .WithActionCost(1)
                        .WithEffectOnSelf(async (spell, caster) =>
                        {
                            int value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                            bool increased = value >= 3;
                            caster.AddQEffect(new QEffect(spell.Name, $"At the end of each of your turns, creatures within a 1{(increased ? 5 : 0)}-foot emanation take {(increased ? 4 : 2)}d6 fire damage (basic Reflex save mitigates).", spell.Illustration)
                            {
                                StateCheckWithVisibleChanges = async effect =>
                                {
                                    value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                                    if (value < 3 || increased)
                                        return;
                                    effect.AssociatedAura?.MoveTo(3);
                                    effect.Description = "At the end of each of your turns, creatures within a 15-foot emanation take 4d6 fire damage (basic Reflex save mitigates).";
                                    increased = true;
                                },
                                EndOfYourTurnDetrimentalEffect = async (_, _) =>
                                {
                                    if (caster.HasEffect(QEffectId.Unconscious) || caster.HasEffect(MQEffectIds.Suppressed))
                                        return;
                                    value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                                    int radius = value >= 3 ? 3 : 2;
                                    foreach (Creature cr in caster.Battle.AllCreatures.Where(cr => cr.DistanceTo(caster) <= radius && cr.AliveOrUnconscious).ToList())
                                    {
                                        CheckResult result = await CommonSpellEffects.RollSavingThrowAsync(cr, spell,
                                            Defense.Reflex,
                                            caster.Spellcasting?.Sources.MaxBy(src => src.GetSpellSaveDC())
                                                ?.GetSpellSaveDC() ?? 10);
                                        await CommonSpellEffects.DealBasicDamage(spell, caster, cr, result,
                                            $"{(value >= 3 ? 4 : 2)}d6", DamageKind.Fire);
                                    }
                                },
                                SpawnsAura = _ => new MagicCircleAuraAnimation(IllustrationName.KineticistAuraCircle, Color.Crimson, value >= 3 ? 3 : 2),
                                ProvideContextualAction = _ =>
                                {
                                    if (caster.HasEffect(MQEffectIds.Suppressed))
                                        return null;
                                    CombatAction suppress = new CombatAction(caster, IllustrationName.DismissAura,
                                            "Suppress Skyfire",
                                            [Trait.Basic, Trait.DoNotShowOverheadOfActionName, Trait.SustainASpell],
                                            "Suppresses the effect of your Trial by Skyfire until the start of your next turn.", 
                                            Target.Self())
                                        .WithSoundEffect(SfxName.AuraDismissal)
                                        .WithActionCost(1)
                                        .WithEffectOnSelf(me =>
                                            me.AddQEffect(new QEffect("Skyfire Suppressed", "The effects of your Trial by Skyfire are suppressed.", ExpirationCondition.ExpiresAtStartOfYourTurn, me, IllustrationName.DismissAura) { Id = MQEffectIds.Suppressed }));
                                    return new ActionPossibility(suppress);
                                }
                            }.WithExpirationAtStartOfSourcesTurn(caster, 10));
                        });
                    return new ActionPossibility(skyfire).WithPossibilityGroup("Cursebound");
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_WatersOfCreation", "Waters of Creation"), 10,
                "Water is the source of life, and you draw upon this primordial force to heal your allies' wounds.",
                "A gentle ring ripples out from you in a 15-foot emanation, restoring 5d6 Hit Points to creatures in the area. At 12th level and every two levels thereafter, the amount restored increases by 1d6. If you are cursebound 3 when you use Waters of Creation, the amount healed increases to d8s.",
                [Trait.Cursebound, Trait.Divine, Trait.Healing, MTraits.RemasterOracle, Trait.Positive, Trait.Water])
            .WithActionCost(2)
            .WithPrerequisite(
                values => values.HasFeat(MysteryFeatNames.Life) || values.HasFeat(MysteryFeatNames.Tempest),
                "You must have the life or tempest mystery.")
            .WithPermanentQEffect("", qf =>
            {
                Creature self = qf.Owner;
                qf.Description = $"Heal each creature in a 15-foot emanation by {S.HeightenedVariable(self.Level / 2, 5)}d6 hit points. If you are cursebound 3, increase the healing to d8s.";
                qf.ProvideMainAction = _ =>
                {
                    int value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 1;
                    bool increased = value >= 3;
                    CombatAction waters = new CombatAction(self, IllustrationName.WaterHealing, "Waters of Creation",
                            [Trait.Cursebound, Trait.Divine, Trait.Healing, Trait.Positive, Trait.Water, Trait.Basic],
                            $"Each creature in the area gains {S.HeightenedVariable(self.Level / 2, 5)}d{(increased ? 8 : 6)} hit points.",
                            Target.Emanation(3))
                        .WithSoundEffect(SfxName.NaturalHealing)
                        .WithActionCost(2)
                        .WithEffectOnEachTarget(async (spell, _, target, _) =>
                        {
                            await target.HealAsync($"{S.HeightenedVariable(self.Level / 2, 5)}d{(increased ? 8 : 6)}",
                                spell);
                        });
                    return new ActionPossibility(waters).WithPossibilityGroup("Cursebound");
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_MysteriousRepertoire", "Mysterious Repertoire"), 14,
                "Your mystery holds unknowable depths of magic not always associated with the divine.",
                "Add one non-divine spell to your spell repertoire.",
                [MTraits.RemasterOracle])
            .WithOnSheet(values => values.AddSelectionOption(new MysteriousSelection("RE_MysteriousRepertoireSpell",
                "Mysterious Repertoire spell", -1, values.MaximumSpellLevel)));
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_ForestallCurse", "Forestall Curse"), 14,
                "You've learned to hold back your curse.",
                "Once per day, if the next action you use is a cursebound ability, your cursebound value doesn't increase.", [MTraits.RemasterOracle, Trait.Concentrate])
            .WithActionCost(0)
            .WithPermanentQEffect(qf =>
            {
                Creature self = qf.Owner;
                qf.ProvideMainAction = _ =>
                {
                    return new ActionPossibility(new CombatAction(self, IllustrationName.CurseOverwhelmed,
                        "Forestall Curse", [Trait.Concentrate],
                        "Once per day, if the next action you use is a cursebound ability, your cursebound value doesn't increase.", Target.Self()
                            .WithAdditionalRestriction(cr => cr.PersistentUsedUpResources.UsedUpActions.Contains("Forestall Curse") ? "Forestall Curse may only be used once per day." : null))
                        .WithActionCost(0)
                        .WithEffectOnSelf(async (spell, caster) =>
                        {
                            caster.AddQEffect(new QEffect
                            {
                                Id = MQEffectIds.Forestalled,
                                AfterYouTakeAction = async (effect, action) =>
                                {
                                    if (action == spell)
                                        return;
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                }
                            });
                            caster.PersistentUsedUpResources.UsedUpActions.Add(spell.Name);
                        }));
                };
            });
        yield return new TrueFeat(MFeatNames.WaterWalker, 8,
                "When in the throes of your curse, your steps take on a supernatural buoyancy.",
                $"While you are cursebound, you gain the effects of {AllSpells.CreateSpellLink(SpellId.WaterWalk, MTraits.RemasterOracle)}.",
                [MTraits.RemasterOracle, Trait.Rebalanced])
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    int? value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value;
                    if (value == null)
                        return;
                    self.AddQEffect(QEffect.Swimming().WithExpirationEphemeral());
                };
            });
        yield return new TrueFeat(ModManager.RegisterFeatName("RE_LighterThanAir", "Lighter than Air"), 14,
                "Your mysterious steps become even lighter, transcending the mortal world altogether.",
                "While you are cursebound, you gain {r}flying{/r}. If you are cursebound 3 or greater, you gain a +10-foot status bonus to your Speed.",
                [MTraits.RemasterOracle])
            .WithPermanentQEffectAndSameRulesText(qf =>
            {
                Creature self = qf.Owner;
                qf.StateCheck = _ =>
                {
                    int? value = self.FindQEffect(MQEffectIds.OracleCurse)?.Value;
                    if (value == null)
                        return;
                    self.AddQEffect(QEffect.Flying().WithExpirationEphemeral());
                    if (value >= 3)
                        self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToAllSpeeds = _ => new Bonus(2, BonusType.Status, "Lighter than Air")
                        });
                };
            })
            .WithPrerequisite(MFeatNames.WaterWalker, "Water Walker");
        
    }
    
    public static QEffect OracleCurse(int value)
    {
        return new QEffect("Cursebound", "Your oracular curse is constricting around you as you receive divine punishment after drawing too deeply on your mystery's powers.")
        {
            Id = MQEffectIds.OracleCurse,
            Value = value,
            Innate = false
        };
    }

    public static QEffect HarmForetold()
    {
        return new QEffect
        {
            Id = MQEffectIds.ForetellHarm,
            EndOfCombat = async (eff, won) =>
            {
                Creature owner = eff.Owner;
                if (!won || owner.PersistentCharacterSheet == null || owner.LongTermEffects is not { } lte)
                    return;
                LongTermEffect? effect = WellKnownLongTermEffects.CreateLongTermEffect("HarmForetold");
                if (effect == null || lte.Effects.Any(lt => lt.Id == effect.Id))
                    return;
                lte.Add(effect);
            }
        };
    }
    public static Feat DuplicateDomain(FeatName domain, SpellId domainSpell)
    {
        Feat duplicateDomain = CommonFeatTemplates.CreateDuplicateFeat(domain,
            ModManager.RegisterFeatName("RE_OracleDomain"+domain, domain.HumanizeTitleCase2()), 0);
        duplicateDomain.LevelIfAny = null;
        duplicateDomain.OnSheet = null;
        duplicateDomain.Traits.Remove(Trait.ClericDomain);
        duplicateDomain.WithOnSheet(sheet =>
        {
            sheet.AddFeatForPurposesOfPrerequisitesOnly(domain);
            sheet.AddFocusSpellAndFocusPoint(MTraits.RemasterOracle, Ability.Charisma, domainSpell);
        });
        return duplicateDomain;
    }

    private static IEnumerable<Feat> Domains()
    {
        List<Feat> domainFeats = [.. AllFeats.All.Where(ft => ft.HasTrait(Trait.ClericDomain))];
        return domainFeats.Select(feat => DuplicateDomain(feat.FeatName, feat.ShowRulesBlockFor));
    }

    public static QEffect Future(string name, string description, int statusValue, Trait trait, Illustration illustration)
    {
        return new QEffect(name, description, illustration)
        {
            FizzleOutgoingActions = async (_, action, builder) =>
            {
                switch (trait)
                {
                    case Trait.Spell when action.HasTrait(trait) && action.SpellInformation != null:
                    case Trait.BasicStrike when action.HasTrait(trait):
                    case Trait.Skill when action.ActiveRollSpecification is { } roll &&
                                          (roll.TaggedDetermineBonus.IsPerception || roll.TaggedDetermineBonus.InvolvedSkill != null):
                    case Trait.Move when action.ActionId == ActionId.Stride:
                        return false;
                    default:
                        (CheckResult, string) tuple = Checks.RollFlatCheck(6);
                        builder.AppendLine("Attempting a disallowed action: " + tuple.Item2);
                        return tuple.Item1 < CheckResult.Success;
                }
            },
            BonusToDamage = (_, action, _) =>
            {
                return trait switch
                {
                    Trait.Spell when action.HasTrait(trait) && action.SpellInformation != null => 
                        new Bonus(action.SpellLevel + statusValue, BonusType.Status, "Meddling Futures"),
                    Trait.BasicStrike when action.HasTrait(trait)  =>
                        new Bonus(statusValue, BonusType.Status, "Meddling Futures"),
                    _ => null
                };
            },
            BonusToAttackRolls = (_, action, _) => trait == Trait.BasicStrike && action.HasTrait(trait) ? new Bonus(1, BonusType.Status, "Meddling Futures") : null,
            BonusToSkillChecks = (_, _, _) => trait != Trait.Skill ? null : new Bonus(statusValue, BonusType.Status, "Meddling Futures"),
            BonusToPerception = _ => trait != Trait.Skill ? null : new Bonus(statusValue, BonusType.Status, "Meddling Futures"),
            BonusToAllSpeeds = _ => trait != Trait.Move ? null : new Bonus(statusValue, BonusType.Status, "Meddling Futures"),
            AfterYouTakeAction = async (effect, action) =>
            {
                if (action.ActionId == RActionIds.MeddlingFutures)
                    return;
                effect.ExpiresAt = ExpirationCondition.Immediately;
            },
            ExpiresAt = ExpirationCondition.ExpiresAtEndOfAnyTurn,
            YouBeginAction = async (effect, action) =>
            {
                if (action.ActionId != ActionId.Stride)
                    effect.BonusToAllSpeeds = null;
            }
        };
    }

    public static bool IsForcedMovement(CombatAction action)
    {
        return action.ActionId == ActionId.Shove || action.Description.ContainsIgnoreCase("pull") || action.Description.ContainsIgnoreCase("push") || action.Description.ContainsIgnoreCase("move the creature");
    }

    public static string Describe4StageCurse(string stage1, string stage2, string stage3, string stage4)
    {
        return $"{{b}}Cursebound 1{{/b}} {stage1}" +
               $"\n{{b}}Cursebound 2{{/b}} {stage2}" +
               $"\n{{b}}Cursebound 3{{/b}} {stage3}" +
               $"\n{{b}}Cursebound 4{{/b}} {stage4}";
    }

    private static List<Feat> CreateDivineAccessSubfeats()
    {
        return
        [
            .. AllFeats.All.OfType<DeitySelectionFeat>().Select(deitySelectionFeat =>
            {
                SpellId[] spells = deitySelectionFeat.GrantedSpells;
                return new Feat(
                        ModManager.RegisterFeatName("RE_DivineAccess" + deitySelectionFeat.ToTechnicalName(),
                            deitySelectionFeat.Name),
                        "Your mystery offers you strange access to spells typically reserved for more conventional worshippers.",
                        "You can add up to three spells of your choice granted by this deity to your spell list, and to your spell repertoire as soon as you can cast spells of the appropriate rank: " +
                        S.ConstructOrList(
                            spells.Select(sp => AllSpells.CreateSpellLink(sp, Trait.Oracle)), "and"),
                        [], null)
                    .WithTag("DivineAccessDeity")
                    .WithPrerequisite(
                        values =>
                            values.Tags["OracleMystery"] is MysteryFeat mystery &&
                            mystery.AllowedDomains.ContainsOneOf(
                                deitySelectionFeat.AllowedDomains),
                        $"Your mystery must allow the {S.ConstructOrList(deitySelectionFeat.AllowedDomains.Select(dm => dm.HumanizeLowerCase2()))} domains.")
                    .WithOnSheet(values =>
                    {
                        if (spells.Length > 3)
                            values.AddSelectionOption(new MultipleFeatSelectionOption("RE_DivineAccessSpells",  "Divine Access Spells", 11, ft => ft.Tag is "DivineAccessSpell" && spells.Contains(ft.ShowRulesBlockFor), 3));
                        else
                        {
                            if (!values.SpellRepertoires.TryGetValue(MTraits.RemasterOracle, out SpellRepertoire? spellRepertoire)) return;
                            spellRepertoire.AdditionalSpellsAllowed.AddRange(spells);
                            foreach (SpellId spell in spells)
                            {
                                Spell template = AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle);
                                values.AddAtLevel(template.SpellLevel * 2 - 1, _ => { spellRepertoire.SpellsKnown.Add(template); });
                                if (values.Tags["OracleMystery"] is MysteryFeat mystery)
                                    mystery.DivineAccess.Add(spell);
                            }
                        }
                    });
            })
        ];
    }

    private static List<Feat> DeitySpellFeats()
    {
        List<Feat> spells = [];
        HashSet<string> names = [];
        foreach (DeitySelectionFeat deity in AllFeats.All.OfType<DeitySelectionFeat>())
        {
            spells.AddRange(from spell in deity.GrantedSpells
                let template = AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle)
                where names.Add(spell.HumanizeTitleCase2())
                select new Feat(ModManager.RegisterFeatName("RE_" + spell, spell.HumanizeTitleCase2()), null, "Select this spell to add it to your spell list and repertoire as soon as you can cast spells of the appropriate rank.", [], null).WithRulesBlockForSpell(spell)
                    .WithIllustration(template.Illustration)
                    .WithTag("DivineAccessSpell")
                    .WithOnSheet(values =>
                    {
                        if (!values.SpellRepertoires.TryGetValue(MTraits.RemasterOracle, out SpellRepertoire? spellRepertoire)) return;
                        spellRepertoire.AdditionalSpellsAllowed.Add(spell);
                        values.AddAtLevel(template.SpellLevel * 2 - 1, _ => { spellRepertoire.SpellsKnown.Add(template); });
                        if (values.Tags["OracleMystery"] is MysteryFeat mystery)
                            mystery.DivineAccess.Add(spell);
                    }));
        }
        return spells;
    }
    
    private static IEnumerable<Feat> CreateRevelations(bool greater)
    {
        foreach (MysteryFeat oracleMystery1 in AllFeats.GetFeatByFeatName(MFeatNames.RemasterOracle).Subfeats!.Cast<MysteryFeat>())
        {
            MysteryFeat oracleMystery = oracleMystery1;
            string str = oracleMystery.FeatName.HumanizeTitleCase2();
            if (str.StartsWith("Mystery: "))
                str = str["Mystery: ".Length..];
            SpellId spell = greater ? oracleMystery.GreaterRevelationSpell : oracleMystery.AdvancedRevelationSpell;
            if (spell == SpellId.None) continue;
            Spell modernSpellTemplate = AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle);
            yield return new Feat(ModManager.RegisterFeatName((greater ? "RE_GreaterRevelation:" : "RE_AdvancedRevelation:") + oracleMystery.FeatName.ToStringOrTechnical(), 
                        (greater ? "Greater Revelation: " : "Advanced Revelation: ") + str), 
                    $"You {(greater ? "further deepen" : "deepen")} your understanding of the {str.ToLower()} mystery.", 
                    $"You gain the {AllSpells.CreateModernSpellTemplate(spell, MTraits.RemasterOracle).ToSpellLink()} spell. Increase the focus points in your pool by 1, to a maximum of 3.",
                [],  null)
                .WithOnSheet(sheet => sheet.AddFocusSpellAndFocusPoint(MTraits.RemasterOracle, Ability.Charisma, spell))
                .WithIllustration(modernSpellTemplate.Illustration)
                .WithRulesBlockForSpell(spell, MTraits.RemasterOracle)
                .WithPrerequisite(values => values.HasFeat(oracleMystery.FeatName), $"You must have the {str} mystery.");
        }
    }

    internal static QEffect OracleCurseFunction(CalculatedCharacterSheetValues sheet, Creature self)
    {
        return new QEffect
        {
            AfterYouTakeAction = async (_, action) =>
            {
                if (!action.HasTrait(Trait.Cursebound) || self.HasEffect(MQEffectIds.Forestalled))
                    return;
                if (self.FindQEffect(MQEffectIds.OracleCurse) is not { } curse)
                {
                    curse = OracleCurse(1);
                    if (sheet.Tags["OracleMystery"] is MysteryFeat mystery)
                    {
                        curse.Illustration = mystery.CurseIllustration;
                        curse.Description = mystery.CurseRules;
                    }

                    self.AddQEffect(curse);
                }
                else
                {
                    curse.Value += 1;
                }
            },
            StateCheck = _ =>
            {
                int maxCurse = self.Level >= 17 ? 4 : self.Level >= 11 ? 3 : 2;
                int curseValue = self.FindQEffect(MQEffectIds.OracleCurse)?.Value ?? 0;
                if (curseValue >= maxCurse)
                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        PreventTakingAction = action =>
                            action.HasTrait(Trait.Cursebound)
                                ? "You are already at your maximum cursebound condition."
                                : null
                    });
            }
        };
    }

    internal static QEffect CursedFate()
    {
        return new QEffect("Cursed Fortune", "You roll twice on your next attack roll or skill check that takes at least 1 action. If the higher roll is odd, take the lower result. If the higher roll is even, take the lower result.", IllustrationName.BestowCurse)
        {
            RerollActiveRoll = async (effect, result, action, _) =>
            {
                if (action.ActiveRollSpecification?.TaggedDetermineBonus.InvolvedSkill == null &&
                    !action.HasTrait(Trait.Attack)) return RerollDirection.DoNothing;
                if (action.ActionCost < 1)
                    return RerollDirection.DoNothing;
                effect.ExpiresAt = ExpirationCondition.Immediately;
                int newRoll = R.NextD20();
                int highest = Math.Max(newRoll, result.D20Roll);
                if (highest % 2 == 0)
                {
                    effect.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        DetermineMainCheckResult = _ => newRoll
                    });
                    return RerollDirection.RerollAndKeepBest;
                }
                effect.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    DetermineMainCheckResult = _ => newRoll
                });
                return RerollDirection.RerollAndKeepWorst;
            }
        };
    }

    public static Creature GhostWarrior(int level, int attackBonus)
    {
        return Creature.CreateIndestructibleObject(IllustrationName.GhostPirateCaptain, "Ghostly Warrior",
            level).With(ca =>
        {
            ca.Traits.Remove(Trait.Object);
            ca.Traits.AddRange([Trait.Incorporeal, Trait.Minion, Trait.Mindless]);
            ca.AddQEffect(new QEffect
            {
                PreventTakingAction = action => !action.HasTrait(Trait.Strike) ? "The Ghostly Warrior can only Strike." : null
            });
            AddNaturalWeapon(ca, "Ghost Strike", IllustrationName.GhostlyWeapon, attackBonus, [], "4d6",
                DamageKind.Spirit);
        });
    }
    
    public static Creature AddNaturalWeapon(Creature creature, string naturalWeaponName, Illustration illustration, int attackBonus, Trait[] traits, string damage, DamageKind damageKind, Action<WeaponProperties>? additionalWeaponPropertyActions = null)
    {
        bool flag = traits.Contains(Trait.Finesse) || traits.Contains(Trait.Ranged);
        int num = creature.Abilities.Strength;
        if (flag)
        {
            num = Math.Max(num, creature.Abilities.Dexterity);
        }

        int proficiencyLevel = creature.ProficiencyLevel;
        if (creature.Proficiencies.Get(Trait.Weapon) == Proficiency.Untrained)
        {
            creature.WithProficiency(Trait.Weapon, (Proficiency)(attackBonus - proficiencyLevel - num));
        }

        MediumDiceFormula mediumDiceFormula = DiceFormula.ParseMediumFormula(damage, naturalWeaponName, naturalWeaponName);
        int additionalFlatBonus = mediumDiceFormula.FlatBonus - creature.Abilities.Strength;
        Item item = new Item(illustration, naturalWeaponName, [.. traits, Trait.Unarmed]).WithWeaponProperties(new WeaponProperties(mediumDiceFormula.DiceCount + "d" + (int)mediumDiceFormula.DieSize, damageKind)
        {
            AdditionalFlatBonus = additionalFlatBonus
        });
        additionalWeaponPropertyActions?.Invoke(item.WeaponProperties!);
        creature.UnarmedStrike = item;
        return creature;
    }

    public static Item ModifyOracularCrown(Item item)
    {
        if (item.ItemName != ItemName.OracularCrown)
            return item;
        item.HasLimitedEffectOnlyFor = null;
        item.WithHasLimitedEffectOnlyFor((values, _) =>
        {
            ClassSelectionFeat? classSelectionFeat = values.Class;
            return
                classSelectionFeat?.ClassTrait != Trait.Oracle &&
                classSelectionFeat?.ClassTrait != MTraits.RemasterOracle &&
                !values.AdditionalClassTraits.Contains(Trait.Oracle) &&
                !values.AdditionalClassTraits.Contains(MTraits.RemasterOracle)
                    ? "You get the +2 item bonus to Religion checks, but you're not an oracle so can't use this item's ability."
                    : null;
        });
        item.OnCreatureWhenWorn = null;
        item.WithOnCreatureWhenWorn((_, self) =>
            self.AddQEffect(new QEffect("Oracular crown",
                "The first oracle focus spell you cast each encounter doesn't cost you a focus point.")
            {
                AfterYouExpendSpellcastingResources = (effect, action) =>
                {
                    if (!action.HasTrait(Trait.Focus) ||
                        (!action.HasTrait(Trait.Cursebound) && action.SpellcastingSource?.ClassOfOrigin != MTraits.RemasterOracle) ||
                        effect.ExpiresAt == ExpirationCondition.Immediately)
                        return;
                    if (action.Owner.Spellcasting != null)
                        ++action.Owner.Spellcasting.FocusPoints;
                    effect.ExpiresAt = ExpirationCondition.Immediately;
                }
            }));
        item.PermanentQEffectActionWhenWorn = null;
        item.WithOncePerDayWhenWornAction((itm, self) =>
        {
            if (self.Damage == 0)
                return null;
            if (self.PersistentCharacterSheet is { } sheet && (sheet.Class?.ClassTrait == MTraits.RemasterOracle ||
                                                               sheet.Calculated.AdditionalClassTraits.Contains(
                                                                   MTraits.RemasterOracle)))
                goto label_12;
            OracleCurseStage? curseLevel = OracleCurses.GetCurseLevel(self);
            if (!curseLevel.HasValue)
                return null;
            int num;
            switch (curseLevel.GetValueOrDefault())
            {
                case OracleCurseStage.Minor:
                    num = 3;
                    goto label_11;
                case OracleCurseStage.Moderate:
                    num = 5;
                    goto label_11;
                case OracleCurseStage.Major:
                    num = 7;
                    goto label_11;
                case OracleCurseStage.Extreme:
                    num = 9;
                    goto label_11;
            }

            label_12:
            if (self.FindQEffect(MQEffectIds.OracleCurse)?.Value is not { } value || value < 1)
                return null;
            switch (value)
            {
                case 1:
                    num = 3;
                    goto label_11;
                case 2:
                    num = 5;
                    goto label_11;
                case 3:
                    num = 7;
                    goto label_11;
                case 4:
                    num = 9;
                    goto label_11;
            }

            num = 1;
            label_11:
            int number = num;
            return new CombatAction(self, itm.Illustration, "Use oracular crown healing", [
                    Trait.Healing,
                    Trait.Concentrate,
                    self.HasTrait(Trait.Undead) ? Trait.Negative : Trait.Positive
                ],
                $"You regain {number}d8 HP. {{i}}(The amount of healing depends on the current strength of your oracular curse.){{/i}}",
                Target.Self()).WithSoundEffect(SfxName.Healing).WithEffectOnEachTarget(async (spell, _, target, _) =>
                await target.HealAsync(number + "d8", spell));
        });
        item.Description =
            "You gain a +2 item bonus to Religion.\r\n\r\nThe first oracle focus spell you cast each encounter doesn't cost you a focus point.\r\n\r\nOnce per day, if you are at least cursebound 1, you can spend {icon:Action}an action. If you do, you heal 3d8 HP (or 5d8 if you're cursebound 2, 7d8 if you're cursebound 3, and 9d8 if you're cursebound 4). If you have negative healing, this is a negative healing effect, otherwise it is a positive healing effect.";
        return item;
    }
}