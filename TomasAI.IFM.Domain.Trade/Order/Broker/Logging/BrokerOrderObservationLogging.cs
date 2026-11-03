using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Logging;

internal static partial class BrokerOrderObservationLogging
{

    extension(ILogger logger)
    {
        internal void Routed(string kind, string brokerOrderId, Guid observationId, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
            => RoutedCore(logger, kind, brokerOrderId, observationId, method);

        internal void Failed(Exception exception, string category, string brokerOrderId, Guid observationId, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
            => FailedCore(logger, exception, category, brokerOrderId, observationId, method);
    }

    // Keep source-generated logging on the containing partial class.
    [LoggerMessage(2510301, LogLevel.Information, "Broker observation routed Kind={Kind} BrokerOrderId={BrokerOrderId} ObservationId={ObservationId}; Method={Method}", EventName = nameof(Routed))]
    private static partial void RoutedCore(ILogger logger, string kind, string brokerOrderId, Guid observationId, string method);

    [LoggerMessage(2510399, LogLevel.Error, "Broker observation routing failed Category={Category} BrokerOrderId={BrokerOrderId} ObservationId={ObservationId}; Method={Method}", EventName = nameof(Failed))]
    private static partial void FailedCore(ILogger logger, Exception exception, string category, string brokerOrderId, Guid observationId, string method);
}
