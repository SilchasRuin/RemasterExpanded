using Dawnsbury.Core.Mechanics.Targeting.Targets;

namespace RemasterExpanded.Technical;

public class MultipleBurstsTarget(int range, int radius, int maximumBursts, string additionalTargetingText = "") : GeneratorTarget
{
    public int MaximumBursts { get; set; } = maximumBursts;
    public int Range { get; set; } = range;
    public int Radius { get; set; } = radius;
    public string AdditionalTargetingText { get; set; } = additionalTargetingText;
    public override bool IsAreaTarget => true;
    public override GeneratedTargetInSequence? GenerateNextTarget()
    {
        int count = OwnerAction.ChosenTargets.AllChosenPointsOfOrigin.Count;
        if (count == MaximumBursts)
            return null;
        if (count >= 1)
        {
            return new GeneratedTargetInSequence(Burst(Range, Radius),
                AdditionalTargetingText, "All bursts completed");
        }
        return GeneratedTargetInSequence.Mandatory(Burst(Range, Radius), AdditionalTargetingText);
        
    }
}