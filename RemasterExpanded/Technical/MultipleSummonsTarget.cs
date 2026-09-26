using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;

namespace RemasterExpanded.Technical;

public class MultipleSummonsTarget(int range, int howMany, string additionalTargetingText = "", Action<TileTarget>? modifyTarget = null) : GeneratorTarget
{
    public int Range { get; set; }= range;
    public int HowMany { get; set; } = howMany;
    public string AdditionalTargetingText { get; set; } = additionalTargetingText;

    public override GeneratedTargetInSequence? GenerateNextTarget()
    {
        int count = OwnerAction.ChosenTargets.ChosenTiles.Count;
        TileTarget target = RangedEmptyTileForSummoning(Range);
        modifyTarget?.Invoke(target);
        return count == HowMany ? null : GeneratedTargetInSequence.Mandatory(target, AdditionalTargetingText);
    }
    
}