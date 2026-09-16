using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Logging;

internal static partial class BrokerOrderObservationLogging
{
    [LoggerMessage(2510301, LogLevel.Information, "Broker observation routed Kind={Kind} BrokerOrderId={BrokerOrderId} ObservationId={ObservationId}")]
    internal static partial void Routed(this ILogger logger, string kind, string brokerOrderId, Guid observationId);

    [LoggerMessage(2510399, LogLevel.Error, "Broker observation routing failed Category={Category} BrokerOrderId={BrokerOrderId} ObservationId={ObservationId}")]
    internal static partial void Failed(this ILogger logger, Exception exception, string category, string brokerOrderId, Guid observationId);
}
