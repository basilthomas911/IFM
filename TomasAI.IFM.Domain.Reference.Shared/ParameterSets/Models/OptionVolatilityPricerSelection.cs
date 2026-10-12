namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
public sealed record OptionVolatilityPricerSelection(
    OptionVolatilityExerciseStyle ExerciseStyle,
    string[] PricerVersions);
