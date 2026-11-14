using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Wakes expired workflows with a durable ITI backlog, including after a host restart.</summary>
public sealed class IntrinsicTimeSignalQueueRecoveryService(IPostgresEventTransaction transactions, IActorService actors,
    TimeProvider clock, ILogger<IntrinsicTimeSignalQueueRecoveryService> logger) : BackgroundService
{
    internal const string RequestSource = "IntrinsicTimeSignalQueueRecovery";
    string afterEntity = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A failed queue wakeup must not block other entities; the source is retried on the next pass.")]
    internal async Task RunOnceAsync(CancellationToken token)
    {
        var page = await transactions.ExecuteOnceAsync((db, ct) => db.QueryAsync(TradeDbSql.ReadPendingIntrinsicTimeSignalQueues,
            [afterEntity], row => (Entity: row.GetString(0), Payload: row.GetFieldValue<byte[]>(1)), ct), token).ConfigureAwait(false);
        foreach (var row in page)
        {
            token.ThrowIfCancellationRequested();
            afterEntity = row.Entity;
            try
            {
                var view = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(row.Payload);
                var now = clock.GetUtcNow().UtcDateTime;
                if (!ExecuteIntrinsicTimeStrategyWorkflow.CanExpire(view, now))
                    continue;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                var command = new RedispatchCurrentStrategyPipelineCommand
                {
                    CommandId = Guid.NewGuid(), EntityId = view.EntityId, WorkflowId = view.WorkflowId,
                    ExpectedWorkflowRevision = view.WorkflowRevision, ExpectedStage = view.CurrentStage,
                    Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor,
                        RedispatchCurrentStrategyPipelineCommand.Verb, row.Entity),
                    RequestedAtUtc = now, RequestedBy = RequestSource
                };
                var result = await actors.RequestAsync<RedispatchCurrentStrategyPipelineCommand,
                    IntrinsicTimeStrategyWorkflowEntityId>(command, deadline.Token).ConfigureAwait(false);
                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage ?? "ITI queue wakeup was rejected.");
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                logger.LogWarning(error, "ITI signal queue wakeup remains pending for {WorkflowEntityId}.", row.Entity);
            }
        }
        if (page.Count < 64)
            afterEntity = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The hosted wakeup loop retries transient failures on its next pass.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var interval = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try { await RunOnceAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "ITI signal queue recovery will retry."); }
        }
        while (await interval.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
