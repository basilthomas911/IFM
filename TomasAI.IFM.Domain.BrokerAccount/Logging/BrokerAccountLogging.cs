using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.BrokerAccount.Logging;

/// <summary>Compile-time structured BrokerAccount logs.</summary>
internal static partial class BrokerAccountLogging
{
    [LoggerMessage(EventId = 25601, Level = LogLevel.Error,
        Message = "Broker account observation failed. AccountAlias={AccountAlias} ObservationId={ObservationId}")]
    internal static partial void AccountObservationFailed(this ILogger logger, Exception exception,
        string accountAlias, Guid observationId);
}
