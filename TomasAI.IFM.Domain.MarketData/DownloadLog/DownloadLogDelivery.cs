using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog;

/// <summary>Carries the original immutable outcome for redelivery without rerunning acquisition.</summary>
public sealed class DownloadLogDeliveryException(MarketDataDownloadOutcome outcome, Exception inner)
    : Exception($"DownloadLog delivery failed for import {outcome.ImportCommandId}; redeliver its terminal outcome, not the import.", inner)
{
    public MarketDataDownloadOutcome Outcome { get; } = outcome;
}

public static class DownloadLogDelivery
{
    public static async ValueTask<bool> ForwardAsync(MarketDataDownloadOutcome? outcome,
        IEvent terminal, MarketDataDownloadDataset dataset, MarketDataDownloadStatus status,
        IEventActorContext context, ILogger logger)
    {
        if (outcome is null)
        {
            logger.LogWarning("Legacy import terminal {EventId} has no download outcome; completion remains unconfirmed.", terminal.Id);
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
                _ => throw new ArgumentException("Unsupported import terminal event.")
            };
            if (outcome.ImportCommandId != terminal.CommandId || outcome.SourceTerminalEventId != terminal.Id
                || outcome.Dataset != dataset || outcome.Status != status
                || outcome.ValueDate != sourceDate || outcome.Scope != sourceScope)
                throw new ArgumentException("Import terminal and download outcome disagree.");
            var command = new InsertMarketDataDownloadLogCommand(outcome);
            var result = await context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(command);
            if (!result.Success) throw new InvalidOperationException($"DownloadLog command rejected: {result.ErrorCode}: {result.ErrorMessage}");
            return true;
        }
        catch (Exception exception)
        {
            // Event transport acknowledges mailbox admission, not command completion.
            // Preserve the exact safe payload for operator redelivery; do not claim
            // that throwing here causes JetStream to redeliver actor processing.
            logger.LogError(exception, "DownloadLog handoff failed for import {ImportCommandId}, terminal {TerminalEventId}. Recovery outcome: {DownloadOutcome}",
                outcome.ImportCommandId, terminal.Id, System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, exception);
        }
    }
}
