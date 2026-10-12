using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Describes one immutable file produced by a provider batch job.</summary>
/// <param name="ProviderFileId">The provider file id.</param>
/// <param name="FileName">The file name.</param>
/// <param name="Schema">The provider schema or recovery baseline required for the operation.</param>
/// <param name="SizeBytes">The size bytes.</param>
/// <param name="Sha256">The sha256.</param>
public sealed record HistoricalProviderFile(
    string ProviderFileId,
    string FileName,
    HistoricalDataSchema Schema,
    long SizeBytes,
    string Sha256);
