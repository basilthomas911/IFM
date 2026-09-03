using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Publishes every persisted watchdog transition through the existing typed System status route.</summary>
public sealed class DatabentoWatchdogStatusConsolePublisher(IStatusConsoleWriter statusConsole)
    : IDatabentoWatchdogPublisher
{
    public async ValueTask PublishAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = $"Databento {observation.DisplayHealth}/{observation.MajorStatus}; "
            + $"reason={observation.OperationReason}; attempt={observation.RecoveryAttempt}; "
            + $"correlation={observation.CorrelationId}; coreReady={observation.CoreContractsReady}.";
        if (observation.DisplayHealth == DatabentoDisplayHealth.Red)
            await statusConsole.WriteConsoleAsync(LogSourceType.System, 10208, message,
                nameof(DatabentoWatchdogObservation), JsonSerializer.Serialize(observation)).ConfigureAwait(false);
        else
            await statusConsole.WriteConsoleAsync(LogSourceType.System, message).ConfigureAwait(false);
    }
}
