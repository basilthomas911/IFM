using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Logging;

internal static partial class BrokerOrderQuoteLogging
{
    [LoggerMessage(EventId = 32320, Level = LogLevel.Information,
        Message = "Emulator quote for ContractId {ContractId} completed {MatchedOrderCount} broker order(s).; Method={Method}")]
    internal static partial void Matched(ILogger logger, string contractId, int matchedOrderCount, [System.Runtime.CompilerServices.CallerMemberName] string method = "");
}
