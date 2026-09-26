namespace TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

/// <summary>
/// Represents an immutable stored strategy parameter-set version and its lifecycle metadata.
/// </summary>
public sealed record ConfigurationParameterSet(
    StrategyParameterSetKind Kind,
    Guid ParameterSetId,
    int Version,
    short SchemaVersion,
    ConfigurationParameterSetStatus Status,
    DateTime? EffectiveFromUtc,
    DateTime? RetiredAtUtc,
    string PayloadJson,
    string PayloadSha256,
    string Description,
    DateTime CreatedUtc,
    string CreatedBy);
