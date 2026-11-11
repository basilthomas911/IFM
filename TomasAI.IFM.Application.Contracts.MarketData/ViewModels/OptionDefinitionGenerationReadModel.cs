namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>The active immutable definition generation and its published coverage.</summary>
public sealed record OptionDefinitionGenerationReadModel(Guid Generation, DateOnly CoverageFrom, DateOnly CoverageThrough);
