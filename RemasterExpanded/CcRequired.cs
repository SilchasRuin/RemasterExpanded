using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using SpellsAndSpellhearts;
using static HereThereBeDragons.ModData.MFeatNames;

namespace RemasterExpanded;

public abstract class CcRequired
{
    public static SpellId BrineDragonBile { get; } = NewSpells.BrineDragonBile;
    public static FeatName DomainDragon => DragonDomain;
}