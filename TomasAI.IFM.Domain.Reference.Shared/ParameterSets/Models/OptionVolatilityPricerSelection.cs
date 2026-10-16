namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePack.MessagePackObject]
public sealed record OptionVolatilityPricerSelection(
    [property: MessagePack.Key(0)] OptionVolatilityExerciseStyle ExerciseStyle,
    [property: MessagePack.Key(1)] string[] PricerVersions);
