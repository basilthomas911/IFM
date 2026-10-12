using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog;

public static class DownloadLogDelivery
{
    public static async ValueTask<bool> ForwardAsync(MarketDataDownloadOutcome? outcome,
        IEvent terminal, MarketDataDownloadDataset dataset, MarketDataDownloadStatus status,
        IEventActorContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);
        if (outcome is null)
        {
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write1(logger, null, terminal.Id);
            return true;
        }
        try
        {
            var (sourceDate, sourceScope) = terminal switch
            {
                EconomicCalendarsImportedCompleteEvent e => (DateOnly.FromDateTime(e.ImportedDate), MarketDataDownloadOutcome.CanonicalScope(e.CountryCodes)),
                EconomicCalendarsImportedFailEvent e => (DateOnly.FromDateTime(e.ImportedDate), MarketDataDownloadOutcome.CanonicalScope(e.CountryCodes)),
                YieldCurveRatesImportedCompleteEvent e => (DateOnly.FromDateTime(e.ImportDate), "US"),
                YieldCurveRatesImportedFailEvent e => (DateOnly.FromDateTime(e.ImportDate), "US"),
                _ => throw new ArgumentException("Unsupported import terminal event.",nameof(terminal))
            };
            if (outcome.ImportCommandId != terminal.CommandId || outcome.SourceTerminalEventId != terminal.Id
                || outcome.Dataset != dataset || outcome.Status != status
                || outcome.ValueDate != sourceDate || !string.Equals(outcome.Scope,sourceScope,StringComparison.Ordinal))
                throw new ArgumentException("Import terminal and download outcome disagree.", nameof(outcome));
            var command = new InsertMarketDataDownloadLogCommand(outcome);
            var result = await (context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(command)).ConfigureAwait(false);
            if (!result.Success)
            {
                if (IsCommittedOutcomeConflict(result))
                {
                    global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write2(logger, null, outcome.ImportCommandId, result.ErrorCode, result.ErrorMessage);
                    return true;
                }

                throw new InvalidOperationException(
                    $"DownloadLog command rejected: {result.ErrorCode}: {result.ErrorMessage}");
            }
            return true;
        }
        catch (Exception exception)
        {
            // Event transport acknowledges mailbox admission, not command completion.
            // Preserve the exact safe payload for operator redelivery; do not claim
            // that throwing here causes JetStream to redeliver actor processing.
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write3(logger, exception, outcome.ImportCommandId, terminal.Id, System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, exception);
        }
    }

    static bool IsCommittedOutcomeConflict(ServiceResult<GuidResult> result)
        => result.ErrorCode == InsertMarketDataDownloadLogCommand.ErrorId
           && (result.ErrorMessage.Contains(
                   "already associated with a different command payload",
                   StringComparison.Ordinal)
               || result.ErrorMessage.Contains(
                   "A different terminal outcome is already committed",
                   StringComparison.Ordinal));
}
