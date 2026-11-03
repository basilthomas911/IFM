using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.BrokerAccount.Logging;

/// <summary>Compile-time structured BrokerAccount logs.</summary>
internal static partial class BrokerAccountLogging
{

    extension(ILogger logger)
    {
        internal void AccountObservationFailed(Exception exception,
            string accountAlias, Guid observationId, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
            => AccountObservationFailedCore(logger, exception, accountAlias, observationId, method);
    }

    // LoggerMessage generates partial methods on classes, rather than extension blocks.
    [LoggerMessage(EventId = 25601, EventName = nameof(AccountObservationFailed), Level = LogLevel.Error,
        Message = "Broker account observation failed. AccountAlias={AccountAlias} ObservationId={ObservationId}; Method={Method}")]
    private static partial void AccountObservationFailedCore(ILogger logger, Exception exception,
        string accountAlias, Guid observationId, string method);
}
