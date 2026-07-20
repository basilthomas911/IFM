using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to stop futures option tick data streaming.
/// </summary>
/// <param name="EntityId">The entity identifier for the streaming session.</param>
/// <param name="ContractId">The contract identifier to stop streaming for.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesOptionTickDataStreamingParameter(FuturesOptionTickEntityId EntityId, string ContractId, int ErrorCode)
    : ICommandParameter;
