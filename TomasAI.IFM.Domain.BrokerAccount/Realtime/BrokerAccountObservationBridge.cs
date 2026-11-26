using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Realtime;

/// <summary>Consumes the single account callback stream and routes snapshots through the command mailbox.</summary>
public sealed class BrokerAccountObservationBridge : IAsyncDisposable,IDisposable
{
    readonly ITradeBroker broker;
    readonly IActorSupervisor supervisor;
    readonly ILogger<BrokerAccountObservationBridge> logger;
    readonly TimeSpan interval;
    readonly SemaphoreSlim snapshotPublication=new(1,1);
    BrokerAccountSnapshotEvidence? pendingAccount,lastConfirmedAccount;
    public BrokerAccountObservationBridge(ITradeBroker broker,IActorSupervisor supervisor,ILogger<BrokerAccountObservationBridge> logger)
        :this(broker,supervisor,logger,TimeSpan.FromSeconds(1)){}
    internal BrokerAccountObservationBridge(ITradeBroker broker,IActorSupervisor supervisor,ILogger<BrokerAccountObservationBridge> logger,TimeSpan pollInterval)
    {
        this.broker=broker;this.supervisor=supervisor;this.logger=logger;
        interval=pollInterval>TimeSpan.Zero?pollInterval:throw new ArgumentOutOfRangeException(nameof(pollInterval));
    }
    readonly Channel<bool> wakeups=Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {SingleReader=true,FullMode=BoundedChannelFullMode.DropWrite,AllowSynchronousContinuations=false});
    int converged,readerActive;
    string? readerError,publicationError;
    long confirmed;
    public bool IsHealthy=>Volatile.Read(ref converged)==1 && Volatile.Read(ref readerActive)==1 && _run?.IsFaulted!=true;
    public string? LastFailure=>Volatile.Read(ref publicationError)??Volatile.Read(ref readerError);
    public long ConfirmedSnapshots=>Interlocked.Read(ref confirmed);
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
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The account reader completed in response to this stop request.
        }
        finally
        {
            _stop.Dispose();
            _stop = null;
            _run = null;
            Volatile.Write(ref converged,0);Volatile.Write(ref readerActive,0);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while(!supervisor.IsReady)await Task.Delay(TimeSpan.FromMilliseconds(10),cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(ReadSupervisedAsync(cancellationToken),PublishLatestAsync(cancellationToken)).ConfigureAwait(false);
    }

    async Task ReadSupervisedAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            try
            {
                Volatile.Write(ref readerActive,1);
                await foreach(var observation in broker.ObserveAccountAsync(token).ConfigureAwait(false))
                {
                    if(observation.Kind is not (BrokerObservationKind.AccountSnapshot or BrokerObservationKind.GateChanged or BrokerObservationKind.ConnectionChanged))continue;
                    Volatile.Write(ref readerError,null);wakeups.Writer.TryWrite(true);
                }
                throw new IOException("Broker account callback reader ended before shutdown.");
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception error)
            {
                Volatile.Write(ref readerActive,0);Volatile.Write(ref readerError,error.Message);
                logger.AccountObservationFailed(error,broker.AccountAlias,Guid.Empty);wakeups.Writer.TryWrite(true);
                await Task.Delay(interval,token).ConfigureAwait(false);
            }
        }
    }

    async Task PublishLatestAsync(CancellationToken token)
    {
        var resynchronized=false;
        while(!token.IsCancellationRequested)
        {
            try
            {
                await snapshotPublication.WaitAsync(token).ConfigureAwait(false);
                try
                {
                if(pendingAccount is null)
                {
                    var snapshot=resynchronized?await broker.GetAccountSnapshotAsync(token).ConfigureAwait(false):await broker.ResynchronizeAccountAsync(token).ConfigureAwait(false);
                    resynchronized=true;var evidence=BrokerAccountSnapshotEvidence.From(snapshot);
                    if(lastConfirmedAccount is null || !SameState(lastConfirmedAccount,evidence))pendingAccount=evidence;
                }
                if(pendingAccount is not null)
                {
                    await RecordAsync(pendingAccount,token).ConfigureAwait(false);
                    lastConfirmedAccount=pendingAccount;pendingAccount=null;Interlocked.Increment(ref confirmed);
                }
                Volatile.Write(ref publicationError,null);Volatile.Write(ref converged,1);
                }
                finally{snapshotPublication.Release();}
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception error)
            {
                // Keep the exact unconfirmed payload and retry even when no further callback arrives.
                Volatile.Write(ref converged,0);Volatile.Write(ref publicationError,error.Message);
                logger.AccountObservationFailed(error,broker.AccountAlias,Guid.Empty);
            }
            using var wait=CancellationTokenSource.CreateLinkedTokenSource(token);wait.CancelAfter(interval);
            try{await wakeups.Reader.ReadAsync(wait.Token).ConfigureAwait(false);}
            catch(OperationCanceledException) when(!token.IsCancellationRequested){}
        }
    }

    static bool SameState(BrokerAccountSnapshotEvidence x,BrokerAccountSnapshotEvidence y)=>x.AccountAlias==y.AccountAlias && x.Currency==y.Currency
        && x.CashBalance==y.CashBalance && x.AvailableFunds==y.AvailableFunds && x.Complete==y.Complete && x.NewRiskAllowed==y.NewRiskAllowed
        && x.Generation==y.Generation && x.SourceSequence==y.SourceSequence && x.Positions.SequenceEqual(y.Positions);

    /// <summary>Publishes an immutable owner checkpoint through the same authoritative snapshot event route.</summary>
    public ValueTask PublishOwnerCheckpointAsync(BrokerAccountSnapshotEvidence evidence,CancellationToken cancellationToken=default)
        =>PublishOwnerCheckpointAsync(_=>ValueTask.FromResult(evidence),cancellationToken);
    /// <summary>Freezes and commits an administrative owner boundary before any same-revision polling snapshot can publish.</summary>
    public ValueTask PublishOwnerCheckpointAsync(Func<BrokerAccountSnapshotEvidence> freezeOwner,CancellationToken cancellationToken=default)
        =>PublishOwnerCheckpointAsync(_=>ValueTask.FromResult(freezeOwner()),cancellationToken);
    public async ValueTask PublishOwnerCheckpointAsync(Func<CancellationToken,ValueTask<BrokerAccountSnapshotEvidence>> freezeOwner,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(freezeOwner);
        await snapshotPublication.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if(pendingAccount is not null)throw new InvalidOperationException("An earlier account snapshot publication is unsettled; retry after it converges.");
            var evidence=await freezeOwner(cancellationToken).ConfigureAwait(false);ArgumentNullException.ThrowIfNull(evidence);
            if(evidence.AccountAlias!=broker.AccountAlias||evidence.SourceSequence<=0||!evidence.Complete)
                throw new ArgumentException("An exact complete committed account snapshot is required.",nameof(freezeOwner));
            pendingAccount=evidence;
            await RecordAsync(evidence,cancellationToken).ConfigureAwait(false);
            lastConfirmedAccount=evidence;pendingAccount=null;
        }
        finally{snapshotPublication.Release();}
    }
    private async ValueTask RecordAsync(BrokerAccountSnapshotEvidence evidence,
        CancellationToken cancellationToken)
    {
        var id = new BrokerAccountId(evidence.AccountAlias);
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
            ReceivedOn = evidence.AsOfUtc,
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
