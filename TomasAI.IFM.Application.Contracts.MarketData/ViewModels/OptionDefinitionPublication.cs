namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>One prepared immutable option definition generation, qualified before database publication.</summary>
public sealed record OptionDefinitionPublication(
    string Symbol, Guid Generation, DateOnly CoverageFrom, DateOnly CoverageThrough,
    DateTime RefreshedAtUtc, IReadOnlyList<CachedOptionContractDefinitionReadModel> Rows);
