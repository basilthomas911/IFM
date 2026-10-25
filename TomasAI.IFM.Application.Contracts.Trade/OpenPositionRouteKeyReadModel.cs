using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies a persisted route entry for replacement or deletion.</summary>
public sealed record OpenPositionRouteKeyReadModel(string ContractId, Guid TradeLegId);
