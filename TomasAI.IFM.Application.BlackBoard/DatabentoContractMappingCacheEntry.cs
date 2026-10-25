using System.Globalization;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public sealed record DatabentoContractMappingCacheEntry
{
    public required string Dataset { get; init; }
    public required string DefinitionDate { get; init; }
    public required string ContractId { get; init; }
    public uint InstrumentId { get; init; }
    public ushort PublisherId { get; init; }
    public AssetTypeId AssetTypeId { get; init; }
    public DateTimeOffset AbsoluteExpirationUtc { get; init; }
}
