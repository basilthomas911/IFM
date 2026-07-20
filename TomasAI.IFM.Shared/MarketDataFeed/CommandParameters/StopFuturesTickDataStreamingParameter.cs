using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to stop futures tick data streaming.
/// </summary>
/// <param name="ContractId">The contract identifier to stop streaming for.</param>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesTickDataStreamingParameter(string ContractId, DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
