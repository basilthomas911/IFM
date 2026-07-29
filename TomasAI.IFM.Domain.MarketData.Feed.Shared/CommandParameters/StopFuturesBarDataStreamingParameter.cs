using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to stop futures bar data streaming.
/// </summary>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesBarDataStreamingParameter(DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
