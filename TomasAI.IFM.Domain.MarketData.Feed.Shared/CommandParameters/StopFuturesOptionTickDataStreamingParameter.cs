using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to stop futures option tick data streaming.
/// </summary>
/// <param name="EntityId">The entity identifier for the streaming session.</param>
/// <param name="ContractId">The contract identifier to stop streaming for.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesOptionTickDataStreamingParameter(FuturesOptionTickEntityId EntityId, string ContractId, int ErrorCode)
    : ICommandParameter;
