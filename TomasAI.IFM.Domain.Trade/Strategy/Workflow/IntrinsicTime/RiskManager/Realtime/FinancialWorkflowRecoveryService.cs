using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
/// <summary>
/// Recovers lost financial-stage notifications directly from committed workflow snapshots.
/// Normal mapped Commands recheck current workflow identity/revision before redispatch or timeout.
/// No financial mutations, fabricated execution facts or direct actor calls occur here.
/// </summary>
public sealed class FinancialWorkflowRecoveryService(FinancialWorkflowRecoveryJournal journal, IActorService actors, TimeProvider clock, ILogger<FinancialWorkflowRecoveryService> logger) : BackgroundService
{
    long _afterStream;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Contain and log one workflow reconciliation failure so other workflows in the bounded page are still observed; cancellation propagates.")]
    public async Task RunOnceAsync(CancellationToken token)
    {
        // Disabling new ITI triggers must not abandon workflows that already own financial requests.
        var page = await (journal.ReadPageAsync(_afterStream, token)).ConfigureAwait(false);
        if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
            foreach (var stream in page.InvalidStreamIds)
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write48(logger, nameof(FinancialWorkflowRecoveryService), nameof(RunOnceAsync), stream);
        foreach (var snapshot in page.Snapshots)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var command = FinancialWorkflowRecoveryModel.Create(snapshot, clock.GetUtcNow().UtcDateTime);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                switch (command)
                {
                    case RedispatchCurrentStrategyPipelineCommand redispatch:
                        var resumed = await (actors.RequestAsync<RedispatchCurrentStrategyPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(redispatch, deadline.Token)).ConfigureAwait(false);
                        if (!resumed.Success)
                            throw new InvalidOperationException(resumed.ErrorMessage ?? "Workflow redispatch was not accepted.");
                        break;
                    case TimeoutRiskManagementCommand timeout:
                        var expired = await (actors.RequestAsync<TimeoutRiskManagementCommand, IntrinsicTimeStrategyWorkflowEntityId>(timeout, deadline.Token)).ConfigureAwait(false);
                        if (!expired.Success)
                            throw new InvalidOperationException(expired.ErrorMessage ?? "Workflow timeout was not accepted.");
                        break;
                }
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write49(logger, error, nameof(FinancialWorkflowRecoveryService), nameof(RunOnceAsync), snapshot.WorkflowId);
            }
        }

        // A late commit in an earlier stream is visited again on the next pass, including after restart.
        _afterStream = page.StreamsRead < 32 ? 0 : page.NextStreamId;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The hosted recovery loop logs a failed pass and continues; host cancellation is handled separately.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await (RunOnceAsync(stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write31(logger, error, nameof(FinancialWorkflowRecoveryService), nameof(ExecuteAsync));
            }

            try
            {
                await (Task.Delay(TimeSpan.FromSeconds(2), stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
