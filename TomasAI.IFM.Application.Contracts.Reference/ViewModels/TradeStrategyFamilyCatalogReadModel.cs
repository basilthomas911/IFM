namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>The committed version and serialized payload of the legacy strategy family catalog projection.</summary>
/// <param name="Revision">The committed catalog revision.</param>
/// <param name="Payload">The projected catalog document.</param>
public readonly record struct TradeStrategyFamilyCatalogReadModel(long Revision, string Payload);
