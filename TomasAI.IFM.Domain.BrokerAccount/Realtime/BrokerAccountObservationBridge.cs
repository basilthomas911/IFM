using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Realtime;

/// <summary>Consumes the single account callback stream and routes snapshots through the command mailbox.</summary>
public sealed class BrokerAccountObservationBridge(
    ITradeBroker broker,
    IActorSupervisor supervisor,
    ILogger<BrokerAccountObservationBridge> logger) : IAsyncDisposable, IDisposable
{
    private CancellationTokenSource? _stop;
    private Task? _run;

    /// <summary>Starts the account stream and records one initial resynchronization snapshot.</summary>
    public ValueTask StartAsync()
    {
        if (_run is not null) return ValueTask.CompletedTask;
        _stop = new CancellationTokenSource();
        _run = RunAsync(_stop.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Stops the account stream and awaits a clean reader exit.</summary>
    public async ValueTask StopAsync()
    {
        if (_stop is null || _run is null) return;
        await _stop.CancelAsync().ConfigureAwait(false);
        try { await _run.ConfigureAwait(false); }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        _stop.Dispose();
        _stop = null;
        _run = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!supervisor.IsReady)
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        await RecordAsync(await broker.ResynchronizeAccountAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        await foreach (var observation in broker.ObserveAccountAsync(cancellationToken).ConfigureAwait(false))
        {
            if (observation.Kind is not (BrokerObservationKind.AccountSnapshot or
                BrokerObservationKind.GateChanged or BrokerObservationKind.ConnectionChanged))
                continue;
            try
            {
                await RecordAsync(await broker.GetAccountSnapshotAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.AccountObservationFailed(exception, broker.AccountAlias, observation.ObservationId);
            }
        }
    }

    private async ValueTask RecordAsync(BrokerAccountSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var id = new BrokerAccountId(snapshot.AccountAlias);
        var evidence = BrokerAccountSnapshotEvidence.From(snapshot);
        var eventId = BrokerAccountSnapshotIdentity.Create(broker.Environment, evidence);
        var observed = new BrokerAccountSnapshotObservedEvent
        {
            Id = eventId,
            CommandId = eventId,
            Subject = new(ActorType.Event, BrokerAccountEventActor.ActorName,
                BrokerAccountSnapshotObservedEvent.Verb, id.Format()),
            EntityId = id,
            AggregateId = id.Format(),
            EventSource = nameof(BrokerAccountObservationBridge),
            ReceivedOn = snapshot.AsOfUtc,
            Environment = broker.Environment,
            Snapshot = evidence
        };
        var producer = supervisor.GetJSProducer(new ActorMailboxId(ActorType.Event, BrokerAccountEventActor.ActorName));
        await producer.SendAsync<BrokerAccountSnapshotObservedEvent, BrokerAccountId>(
            observed.Subject, observed, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>Stops and releases the account observation bridge.</summary>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>Stops the account observation bridge during synchronous container disposal.</summary>
    public void Dispose() => StopAsync().AsTask().GetAwaiter().GetResult();
}
