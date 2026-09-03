using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>The sole serialized owner of Databento start, stop, poll, rollover and recovery.</summary>
public sealed class DatabentoMarketDataWatchdogService(
    IDatabentoLifecycleRuntime runtime,
    IMarketDataServiceStore store,
    IFuturesMarketSessionAuthority sessionAuthority,
    IDatabentoWatchdogPublisher publisher,
    IMarketDataOperationsRecorder recorder,
    DatabentoWatchdogOptions options,
    DatabentoTerminalFaultSignal terminalFaultSignal,
    TimeProvider timeProvider,
    ILogger<DatabentoMarketDataWatchdogService> logger)
    : BackgroundService, IMarketDataLifecycleRequests
{
    const int MaximumRecoveryAttempts = 3;
    readonly SemaphoreSlim _operations = new(1, 1);
    readonly object _snapshotSync = new();
    DatabentoLifecycleSnapshot _current = NewSnapshot();

    public DatabentoLifecycleSnapshot Current { get { lock (_snapshotSync) return _current; } }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var interval = Task.Delay(options.PollInterval, timeProvider, cycle.Token);
            var terminal = terminalFaultSignal.ReadAsync(cycle.Token).AsTask();
            _ = await Task.WhenAny(interval, terminal).ConfigureAwait(false);
            await cycle.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(interval, terminal).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cycle.IsCancellationRequested) { }
            await ProbeAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    public Task StartAsync(DateOnly valueDate, Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default) => SerializedAsync(async token =>
    {
        Measure(MarketDataOperationStage.DatabentoLifecycle, MarketDataOperationOutcome.Requested, Guid.Empty);
        if (runtime.ActiveValueDate == valueDate && Current.State == DatabentoLifecycleState.Healthy)
            return;
        var correlationId = Guid.CreateVersion7(timeProvider.GetUtcNow());
        Transition(DatabentoLifecycleState.Starting, valueDate, correlationId, 0, "Initial startup.");
        try
        {
            await StartAndQualifyAsync(valueDate, correlationId, DatabentoOperationReason.InitialStartup, 0, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Initial Databento startup qualification failed; entering bounded recovery.");
            if (options.Enabled)
                await RecoverAsync(valueDate, correlationId, DatabentoOperationReason.InitialStartup, token).ConfigureAwait(false);
            else
                Transition(DatabentoLifecycleState.Failed, valueDate, correlationId, 0,
                    $"Recovery disabled: {Bound(exception.Message)}");
            if (Current.State == DatabentoLifecycleState.Failed)
            {
                if (errorMessageHandler is not null)
                    await errorMessageHandler(correlationId, 10208, Current.Reason).ConfigureAwait(false);
                throw new InvalidOperationException(Current.Reason, exception);
            }
        }
    }, cancellationToken);

    public Task StopAsync(DateOnly valueDate, CancellationToken cancellationToken = default) => SerializedAsync(async token =>
    {
        if (runtime.ActiveValueDate is { } active && active != valueDate)
            throw new InvalidOperationException($"Active value date {active:yyyy-MM-dd} does not match stop request {valueDate:yyyy-MM-dd}.");
        await runtime.StopAsync(token).ConfigureAwait(false);
        Transition(DatabentoLifecycleState.ScheduledStopped, null, Guid.Empty, 0, "Requested stop completed.");
        await RecordAsync(DatabentoOperationReason.RequestedStop, DatabentoMajorStatus.Down,
            DatabentoDisplayHealth.Inactive, false, 0, EmptyNative(), Guid.Empty, token).ConfigureAwait(false);
    }, cancellationToken);

    public Task ResetAsync(DateOnly valueDate, Guid correlationId, CancellationToken cancellationToken = default)
        => SerializedAsync(async token =>
        {
            await RecoverAsync(valueDate,
            correlationId == Guid.Empty ? Guid.CreateVersion7(timeProvider.GetUtcNow()) : correlationId,
            DatabentoOperationReason.ManualReset, token).ConfigureAwait(false);
            if (Current.State == DatabentoLifecycleState.Failed)
                throw new InvalidOperationException(Current.Reason);
        }, cancellationToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await SerializedAsync(async token =>
        {
            await runtime.StopAsync(token).ConfigureAwait(false);
            Transition(DatabentoLifecycleState.ScheduledStopped, null, Guid.Empty, 0,
                "Application shutdown completed.");
            await RecordAsync(DatabentoOperationReason.ApplicationShutdown, DatabentoMajorStatus.Down,
                DatabentoDisplayHealth.Inactive, false, 0, EmptyNative(), Guid.Empty, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task ProbeAsync(CancellationToken cancellationToken = default) => SerializedAsync(async token =>
    {
        var session = sessionAuthority.Current;
        if (session.ActiveValueDate is null)
        {
            if (runtime.ActiveValueDate is not null)
                await runtime.StopAsync(token).ConfigureAwait(false);
            Transition(DatabentoLifecycleState.ScheduledStopped, null, Guid.Empty, 0, "Planned market closure.");
            await RecordAsync(DatabentoOperationReason.WatchdogPoll, DatabentoMajorStatus.Down,
                DatabentoDisplayHealth.Inactive, false, 0, EmptyNative(), Guid.Empty, token).ConfigureAwait(false);
            return;
        }

        var valueDate = session.ActiveValueDate.Value;
        if (runtime.ActiveValueDate is { } activeValueDate && activeValueDate != valueDate)
        {
            await RecoverAsync(valueDate, Guid.CreateVersion7(timeProvider.GetUtcNow()),
                DatabentoOperationReason.ValueDateRollover, token).ConfigureAwait(false);
            return;
        }
        if (runtime.ActiveValueDate is null)
        {
            var correlationId = Guid.CreateVersion7(timeProvider.GetUtcNow());
            try
            {
                Transition(DatabentoLifecycleState.Starting, valueDate, correlationId, 0,
                    "Scheduled market-session start.");
                await StartAndQualifyAsync(valueDate, correlationId,
                    DatabentoOperationReason.ScheduledSessionStart, 0, token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Scheduled Databento start failed; entering bounded recovery.");
                if (!options.Enabled)
                {
                    Transition(DatabentoLifecycleState.Failed, valueDate, correlationId, 0,
                        $"Recovery disabled: {Bound(exception.Message)}");
                    return;
                }
                await RecoverAsync(valueDate, correlationId,
                    DatabentoOperationReason.AutomaticRecovery, token).ConfigureAwait(false);
                return;
            }
        }
        var native = await SafeProbeAsync(token).ConfigureAwait(false);
        var evaluation = Evaluate(native, session.IsLiveTrading);
        if (evaluation.CoreReady)
        {
            Transition(evaluation.Health == DatabentoDisplayHealth.Orange
                    ? DatabentoLifecycleState.Degraded : DatabentoLifecycleState.Healthy,
                valueDate, Current.CorrelationId, 0, evaluation.Reason, native.NativeGeneration);
            await RecordAsync(DatabentoOperationReason.WatchdogPoll, evaluation.Major, evaluation.Health,
                true, 0, native, Current.CorrelationId, token).ConfigureAwait(false);
            return;
        }
        await RecordAsync(DatabentoOperationReason.WatchdogPoll, evaluation.Major, evaluation.Health,
            false, 0, native, Current.CorrelationId, token).ConfigureAwait(false);
        if (!options.Enabled)
        {
            Transition(DatabentoLifecycleState.Failed, valueDate, Current.CorrelationId, 0,
                $"Recovery disabled: {evaluation.Reason}", native.NativeGeneration);
            return;
        }
        await RecoverAsync(valueDate, Guid.CreateVersion7(timeProvider.GetUtcNow()),
            DatabentoOperationReason.AutomaticRecovery, token).ConfigureAwait(false);
    }, cancellationToken);

    public async Task RefreshAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        Measure(MarketDataOperationStage.DatabentoRefresh, MarketDataOperationOutcome.Requested, correlationId);
        try
        {
            Measure(MarketDataOperationStage.DatabentoRefresh, MarketDataOperationOutcome.Started, correlationId);
            await ProbeAsync(cancellationToken).ConfigureAwait(false);
            Measure(MarketDataOperationStage.DatabentoRefresh, MarketDataOperationOutcome.Completed, correlationId);
        }
        catch
        {
            Measure(MarketDataOperationStage.DatabentoRefresh, MarketDataOperationOutcome.Failed, correlationId);
            throw;
        }
    }

    async Task RecoverAsync(DateOnly valueDate, Guid correlationId, DatabentoOperationReason reason,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumRecoveryAttempts; attempt++)
        {
            if (attempt == 2)
                await Task.Delay(options.AttemptTwoDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            else if (attempt == 3)
                await Task.Delay(options.AttemptThreeDelay, timeProvider, cancellationToken).ConfigureAwait(false);

            Transition(DatabentoLifecycleState.Resetting, valueDate, correlationId, attempt,
                $"Recovery attempt {attempt} of {MaximumRecoveryAttempts}.", attemptStarted: UtcNow());
            await RecordAsync(reason, DatabentoMajorStatus.Resetting, DatabentoDisplayHealth.Orange,
                false, attempt, await SafeProbeAsync(cancellationToken).ConfigureAwait(false), correlationId,
                cancellationToken).ConfigureAwait(false);
            try
            {
                await runtime.StopAsync(cancellationToken).ConfigureAwait(false);
                await StartAndQualifyAsync(valueDate, correlationId, reason, attempt, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                Measure(MarketDataOperationStage.DatabentoLifecycle, MarketDataOperationOutcome.Failed, correlationId);
                logger.LogWarning(exception,
                    "Databento recovery attempt {Attempt} of {MaximumAttempts} failed. CorrelationId={CorrelationId}",
                    attempt, MaximumRecoveryAttempts, correlationId);
                Transition(DatabentoLifecycleState.Resetting, valueDate, correlationId, attempt,
                    Bound(exception.Message), attemptCompleted: UtcNow());
                await RecordAsync(reason, DatabentoMajorStatus.Resetting, DatabentoDisplayHealth.Orange,
                    false, attempt, await SafeProbeAsync(cancellationToken).ConfigureAwait(false), correlationId,
                    cancellationToken, "Lifecycle", Bound(exception.Message)).ConfigureAwait(false);
            }
        }
        try { await runtime.StopAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { logger.LogWarning(exception, "Final Databento teardown failed."); }
        Transition(DatabentoLifecycleState.Failed, valueDate, correlationId, MaximumRecoveryAttempts,
            "Core recovery exhausted after exactly three attempts.", attemptCompleted: UtcNow());
        await RecordAsync(reason, DatabentoMajorStatus.Down, DatabentoDisplayHealth.Red, false,
            MaximumRecoveryAttempts, await SafeProbeAsync(cancellationToken).ConfigureAwait(false), correlationId,
            cancellationToken, "Recovery", Current.Reason).ConfigureAwait(false);
    }

    async Task StartAndQualifyAsync(DateOnly valueDate, Guid correlationId, DatabentoOperationReason reason,
        int attempt, CancellationToken cancellationToken)
    {
        Measure(MarketDataOperationStage.DatabentoLifecycle, MarketDataOperationOutcome.Started, correlationId);
        await runtime.PrepareContractsAsync(valueDate, cancellationToken).ConfigureAwait(false);
        await runtime.StartAsync(valueDate, cancellationToken).ConfigureAwait(false);
        var native = await SafeProbeAsync(cancellationToken).ConfigureAwait(false);
        var evaluation = Evaluate(native, sessionAuthority.Current.IsLiveTrading);
        if (!evaluation.CoreReady)
            throw new InvalidOperationException(evaluation.Reason);
        var state = evaluation.Health == DatabentoDisplayHealth.Orange
            ? DatabentoLifecycleState.Degraded : DatabentoLifecycleState.Healthy;
        Transition(state, valueDate, correlationId, attempt, evaluation.Reason,
            native.NativeGeneration, attemptCompleted: attempt == 0 ? null : UtcNow());
        await RecordAsync(reason, evaluation.Major, evaluation.Health, true, attempt, native,
            correlationId, cancellationToken).ConfigureAwait(false);
        Measure(MarketDataOperationStage.DatabentoLifecycle, MarketDataOperationOutcome.Completed, correlationId);
    }

    async ValueTask<DatabentoBulkWatchdogSnapshot> SafeProbeAsync(CancellationToken cancellationToken)
    {
        var correlationId = Current.CorrelationId;
        Measure(MarketDataOperationStage.DatabentoNative, MarketDataOperationOutcome.Started, correlationId);
        Measure(MarketDataOperationStage.DatabentoInterop, MarketDataOperationOutcome.Started, correlationId);
        try
        {
            var snapshot = await runtime.GetWatchdogSnapshotAsync(options.ProbeTimeout, cancellationToken).ConfigureAwait(false);
            Measure(MarketDataOperationStage.DatabentoNative,
                snapshot.Complete ? MarketDataOperationOutcome.Completed : MarketDataOperationOutcome.Failed,
                correlationId);
            Measure(MarketDataOperationStage.DatabentoInterop,
                snapshot.Complete ? MarketDataOperationOutcome.Completed : MarketDataOperationOutcome.Failed,
                correlationId);
            Measure(MarketDataOperationStage.DatabentoAggregation, MarketDataOperationOutcome.Started, correlationId);
            Measure(MarketDataOperationStage.DatabentoAggregation,
                snapshot.Feeds.Count != 0 && snapshot.Feeds.All(feed => feed.AggregationWorkerRunning)
                    ? MarketDataOperationOutcome.Completed : MarketDataOperationOutcome.Failed,
                correlationId);
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            Measure(MarketDataOperationStage.DatabentoNative, MarketDataOperationOutcome.Failed, correlationId);
            Measure(MarketDataOperationStage.DatabentoInterop, MarketDataOperationOutcome.Failed, correlationId);
            Measure(MarketDataOperationStage.DatabentoAggregation, MarketDataOperationOutcome.Failed, correlationId);
            return EmptyNative(Bound(exception.Message));
        }
    }

    (bool CoreReady, DatabentoMajorStatus Major, DatabentoDisplayHealth Health, string Reason) Evaluate(
        DatabentoBulkWatchdogSnapshot snapshot, bool enforceFreshness)
    {
        if (!snapshot.Complete)
            return (false, DatabentoMajorStatus.Down, DatabentoDisplayHealth.Red,
                string.IsNullOrWhiteSpace(snapshot.FailureDetail) ? "Native watchdog snapshot is incomplete." : snapshot.FailureDetail);
        var core = snapshot.Feeds.Where(feed => feed.Criticality == DatabentoFeedCriticality.Core).ToArray();
        var representedRoles = core.SelectMany(feed => feed.ContractRoles).Distinct().ToHashSet();
        if (Enum.GetValues<DatabentoContractRole>().Any(role => !representedRoles.Contains(role)))
            return (false, DatabentoMajorStatus.Down, DatabentoDisplayHealth.Red,
                "One or more required ES/VX contract roles are absent from the runtime snapshot.");
        if (core.Length == 0 || core.Any(feed => !Operational(feed)))
            return (false, DatabentoMajorStatus.Down, DatabentoDisplayHealth.Red, "One or more core feeds are not operational.");
        if (snapshot.Feeds.Any(feed => feed.Criticality == DatabentoFeedCriticality.Optional
                && !Operational(feed)))
            return (true, DatabentoMajorStatus.Up, DatabentoDisplayHealth.Orange, "An optional feed is unavailable.");
        if (!enforceFreshness)
            return (true, DatabentoMajorStatus.Up, DatabentoDisplayHealth.Green,
                "All core feeds are operational; freshness is not enforced off-trading.");
        var oldest = core.Max(feed => feed.LastProviderMessageAge);
        if (oldest > options.RedFreshnessAge)
            return (false, DatabentoMajorStatus.Down, DatabentoDisplayHealth.Red, "Core live data is older than the red freshness boundary.");
        if (oldest > options.YellowFreshnessAge)
            return (true, DatabentoMajorStatus.Up, DatabentoDisplayHealth.Yellow, "Core live data is intermittently stale.");
        return (true, DatabentoMajorStatus.Up, DatabentoDisplayHealth.Green, "All core feeds are operational.");
    }

    static bool Operational(DatabentoFeedWatchdogStatus feed) =>
        feed.MajorStatus == DatabentoMajorStatus.Up
        && feed.ProducerAlive
        && feed.AggregationWorkerRunning
        && feed.TransportRunning
        && feed.TerminalStatus == 0
        && feed.ReceivedSubscriptions >= feed.ExpectedSubscriptions;

    async Task RecordAsync(DatabentoOperationReason reason, DatabentoMajorStatus major,
        DatabentoDisplayHealth health, bool coreReady, int attempt, DatabentoBulkWatchdogSnapshot native,
        Guid correlationId, CancellationToken cancellationToken, string failureStage = "", string failureDetail = "")
    {
        var observation = new DatabentoWatchdogObservation
        {
            ObservationId = Guid.CreateVersion7(timeProvider.GetUtcNow()), CorrelationId = correlationId,
            ValueDate = runtime.ActiveValueDate ?? sessionAuthority.Current.OperationalValueDate,
            ObservedOnUtc = UtcNow(), OperationReason = reason, MajorStatus = major,
            DisplayHealth = health, CoreContractsReady = coreReady, RecoveryAttempt = attempt,
            NativeBackend = native.NativeBackend, NativeAbiVersion = native.NativeAbiVersion,
            NativeGeneration = native.NativeGeneration, FailureStage = Bound(failureStage),
            FailureDetail = Bound(failureDetail), FeedStatusDetails = native.Feeds
        };
        DatabentoWatchdogObservation? persisted = null;
        Exception? persistenceFailure = null;
        for (var persistenceAttempt = 1; persistenceAttempt <= MaximumRecoveryAttempts; persistenceAttempt++)
        {
            try
            {
                persisted = await store.AppendObservationAsync(observation, cancellationToken).ConfigureAwait(false);
                lock (_snapshotSync) _current = _current with { LastObservation = persisted };
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                persistenceFailure = exception;
                logger.LogWarning(exception,
                    "Databento watchdog observation persistence attempt {Attempt} of {MaximumAttempts} failed.",
                    persistenceAttempt, MaximumRecoveryAttempts);
                if (persistenceAttempt < MaximumRecoveryAttempts && options.PersistenceRetryDelay > TimeSpan.Zero)
                    await Task.Delay(options.PersistenceRetryDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        if (persisted is null)
        {
            lock (_snapshotSync) _current = _current with
            {
                State = _current.CoreReady ? DatabentoLifecycleState.Degraded : _current.State,
                Reason = $"Watchdog persistence failed after three attempts: {Bound(persistenceFailure?.Message ?? "unknown error")}"
            };
            return;
        }
        try
        {
            await publisher.PublishAsync(persisted, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Databento watchdog observation publication failed.");
            lock (_snapshotSync) _current = _current with
            {
                State = _current.CoreReady ? DatabentoLifecycleState.Degraded : _current.State,
                Reason = $"Watchdog publication failed: {Bound(exception.Message)}"
            };
        }
    }

    async Task SerializedAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await operation(cancellationToken).ConfigureAwait(false); }
        finally { _operations.Release(); }
    }

    void Transition(DatabentoLifecycleState state, DateOnly? valueDate, Guid correlationId, int attempt,
        string reason, Guid? generation = null, DateTime? attemptStarted = null, DateTime? attemptCompleted = null)
    {
        lock (_snapshotSync)
        {
            _current = _current with
            {
                State = state, StateRevision = checked(_current.StateRevision + 1), ValueDate = valueDate,
                CorrelationId = correlationId, NativeGeneration = generation ?? _current.NativeGeneration,
                RecoveryAttempt = attempt, Reason = Bound(reason), ChangedOnUtc = UtcNow(),
                AttemptStartedOnUtc = attemptStarted ?? _current.AttemptStartedOnUtc,
                AttemptCompletedOnUtc = attemptCompleted,
                NextRetryOnUtc = state == DatabentoLifecycleState.Resetting && attempt < MaximumRecoveryAttempts
                    ? UtcNow() + (attempt == 1 ? options.AttemptTwoDelay : options.AttemptThreeDelay) : null
            };
        }
    }

    DatabentoBulkWatchdogSnapshot EmptyNative(string failure = "No active native runtime.") => new()
    {
        Complete = false, NativeBackend = "Unavailable", NativeAbiVersion = 0,
        NativeGeneration = Guid.Empty, ObservedOnUtc = UtcNow(), Feeds = [], FailureDetail = failure
    };

    static DatabentoLifecycleSnapshot NewSnapshot() => new()
    {
        State = DatabentoLifecycleState.ScheduledStopped, StateRevision = 0, ValueDate = null,
        CorrelationId = Guid.Empty, NativeGeneration = Guid.Empty, RecoveryAttempt = 0,
        Reason = "Lifecycle has not started.", ChangedOnUtc = DateTime.UnixEpoch
    };

    DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    void Measure(MarketDataOperationStage stage, MarketDataOperationOutcome outcome, Guid correlationId)
    {
        try
        {
            recorder.Record(new(stage, outcome, MarketOutlookUpdateKind.FeedHealth,
                correlationId == Guid.Empty ? Guid.CreateVersion7(timeProvider.GetUtcNow()) : correlationId, UtcNow()));
        }
        catch { }
    }
    static string Bound(string value) => string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, 512)];
}
