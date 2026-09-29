using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Lifecycle;

/// <summary>
/// Authoritative serialized startup and shutdown transaction for Supervisor and managed domain actors.
/// Recoverable failures are returned as typed outcomes and never escape this boundary.
/// </summary>
public sealed class SupervisorManagedActorLifecycle(
    IActorSupervisor actorSupervisor,
    IActorRegistry registry,
    IActorFactory factory,
    ISupervisorBootstrap bootstrap,
    ISupervisorActorMetricsPollingService poller,
    ActorRuntimeStartupOptions options,
    ILogger<SupervisorManagedActorLifecycle> logger) : ISupervisorManagedActorLifecycle
{
    readonly IActorSupervisor _actorSupervisor = actorSupervisor ?? throw new ArgumentNullException(nameof(actorSupervisor));
    readonly IActorRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    readonly IActorFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    readonly ISupervisorBootstrap _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
    readonly ISupervisorActorMetricsPollingService _poller = poller ?? throw new ArgumentNullException(nameof(poller));
    readonly ActorRuntimeStartupOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    readonly ILogger<SupervisorManagedActorLifecycle> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    readonly SemaphoreSlim _gate = new(1, 1);
    IActor[] _managedActors = [];
    int _running;

    /// <inheritdoc />
    public async ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var stage = "Gate";
        var expected = 0;
        var started = 0;
        var acquired = false;
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            if (Volatile.Read(ref _running) != 0)
                return new(SupervisorOperationOutcome.Succeeded, operationId,
                    _managedActors.Length, _managedActors.Length, "AlreadyRunning", null);

            _actorSupervisor.SetReadiness(false);
            stage = "Discover";
            if (!_bootstrap.IsRunning)
                throw new InvalidOperationException("Supervisor actors must be running before managed actor startup.");
            var managedDescriptors = _registry.ActorTypes.Where(type => !IsSupervisorActor(type)).ToArray();
            expected = managedDescriptors.Length;
            stage = "ConstructManaged";
            _managedActors = ConstructAndRegister(managedDescriptors, cancellationToken);
            stage = "RegisterConsumers";
            RegisterConsumers(_bootstrap.ActorTypes.Concat(_managedActors.Select(static actor => actor.Id.ActorType)));

            stage = "StartManaged";
            var startedManaged = 0;
            await Parallel.ForEachAsync(
                _managedActors,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(
                        _options.MaximumConcurrency,
                        Math.Max(1, _managedActors.Length))
                },
                async (actor, token) =>
                {
                    await _actorSupervisor.StartAsync(actor.Id, token).ConfigureAwait(false);
                    Interlocked.Increment(ref startedManaged);
                }).ConfigureAwait(false);
            started += startedManaged;

            stage = "ValidateActors";
            if (started != expected || _managedActors.Any(actor => !actor.IsRunning))
                throw new InvalidOperationException(
                    $"Actor startup validation failed: expected {expected}, started {started}.");

            stage = "StartConsumers";
            await _actorSupervisor.StartConsumersAsync(cancellationToken).ConfigureAwait(false);
            _actorSupervisor.SetReadiness(true);

            stage = "StartMetricsPoller";
            _poller.Start();
            if (_poller.State is not SupervisorPollingServiceState.Running
                and not SupervisorPollingServiceState.Starting)
                throw new InvalidOperationException("The Supervisor actor metrics poller did not start.");

            Volatile.Write(ref _running, 1);
            return new(SupervisorOperationOutcome.Succeeded, operationId, expected, started, "Completed", null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cleanupFailure = acquired ? await RollbackAsync().ConfigureAwait(false) : null;
            return new(SupervisorOperationOutcome.Cancelled, operationId, expected, started, stage,
                CombineFailure("Startup was cancelled.", cleanupFailure));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            TryLogStartupFailure(operationId, stage, exception);
            var cleanupFailure = acquired ? await RollbackAsync().ConfigureAwait(false) : null;
            return new(SupervisorOperationOutcome.Failed, operationId, expected, started, stage,
                CombineFailure(exception.Message, cleanupFailure));
        }
        finally
        {
            if (acquired) _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var expected = _managedActors.Length;
        var stopped = 0;
        var stage = "Gate";
        var acquired = false;
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            _actorSupervisor.SetReadiness(false);
            if (Volatile.Read(ref _running) == 0 && expected == 0)
                return new(SupervisorOperationOutcome.Succeeded, operationId, 0, 0, "AlreadyStopped", null);

            List<string> failures = [];
            stage = "StopMetricsPoller";
            try
            {
                if (!_poller.Stop(TimeSpan.FromSeconds(10)))
                    failures.Add("Poller: did not stop within ten seconds");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RecordRollbackFailure("Poller", exception, failures);
            }

            stage = "StopConsumers";
            try { await _actorSupervisor.StopConsumersAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RecordRollbackFailure("Intake", exception, failures);
            }

            stage = "StopManaged";
            var stopResult = await StopActorsAsync(_managedActors, cancellationToken).ConfigureAwait(false);
            stopped = stopResult.Stopped;
            _managedActors = stopResult.Remaining;
            failures.AddRange(stopResult.Failures);
            Volatile.Write(ref _running, _managedActors.Length == 0 ? 0 : 1);
            if (failures.Count != 0)
                return new(SupervisorOperationOutcome.PartiallyCompleted, operationId, expected, stopped,
                    "HostRecoveryRequired", Bound(string.Join("; ", failures)));
            return new(SupervisorOperationOutcome.Succeeded, operationId, expected, stopped, "Completed", null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _actorSupervisor.SetReadiness(false);
            return new(SupervisorOperationOutcome.Cancelled, operationId, expected, stopped, stage, "Shutdown was cancelled.");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            _actorSupervisor.SetReadiness(false);
            TryLogShutdownFailure(operationId, stage, exception);
            return new(SupervisorOperationOutcome.PartiallyCompleted, operationId, expected, stopped, stage, Bound(exception.Message));
        }
        finally
        {
            if (acquired) _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<SupervisorActorOperationResult> ExecuteAsync(
        SupervisorActorOperationRequest request,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            if (request is null)
                return new SupervisorActorOperationResult(
                    SupervisorOperationOutcome.Rejected, Guid.Empty, default, 0,
                    "Validation", "An operation request is required.");
            if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Requester)
                || string.IsNullOrWhiteSpace(request.Reason) || request.Requester.Length > 128
                || request.Reason.Length > 1024 || request.Timeout <= TimeSpan.Zero
                || request.Timeout > TimeSpan.FromMinutes(10))
                return new(SupervisorOperationOutcome.Rejected, request.OperationId, request.Target,
                    request.ExpectedGeneration, "Validation", "Operation ID, bounded requester/reason, and a timeout of at most ten minutes are required.");
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            bool completed;
            switch (request.Operation)
            {
                case SupervisorActorOperationKind.Pause:
                case SupervisorActorOperationKind.Drain:
                case SupervisorActorOperationKind.Quarantine:
                    completed = await _actorSupervisor.PauseAsync(request.Target, request.Timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case SupervisorActorOperationKind.Resume:
                    if (_actorSupervisor.Children.TryGetValue(request.Target.MailboxId, out var resumeActor)
                        && resumeActor.Mailbox.ThreadQueues.GetGeneration(request.Target) != request.ExpectedGeneration)
                        return new(SupervisorOperationOutcome.Rejected, request.OperationId, request.Target,
                            request.ExpectedGeneration, "GenerationFence", "The entity-mailbox generation changed.");
                    await _actorSupervisor.ResumeAsync(request.Target, cancellationToken).ConfigureAwait(false);
                    completed = true;
                    break;
                case SupervisorActorOperationKind.Restart:
                case SupervisorActorOperationKind.Recycle:
                    completed = await _actorSupervisor.RestartAsync(request.Target, request.ExpectedGeneration,
                        request.Timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case SupervisorActorOperationKind.Retire:
                    completed = await _actorSupervisor.RetireAsync(request.Target, request.ExpectedGeneration,
                        request.Timeout, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return new(SupervisorOperationOutcome.Rejected, request.OperationId, request.Target,
                        request.ExpectedGeneration, "Unsupported", $"{request.Operation} is not safely implemented.");
            }
            return new(completed ? SupervisorOperationOutcome.Succeeded : SupervisorOperationOutcome.Rejected,
                request.OperationId, request.Target, request.ExpectedGeneration,
                completed ? "Completed" : "DrainOrGenerationFence", completed ? null : "The mailbox could not drain safely or its generation changed.");
        }
        catch (OperationCanceledException)
        {
            return new(SupervisorOperationOutcome.Cancelled, request?.OperationId ?? Guid.Empty,
                request?.Target ?? default, request?.ExpectedGeneration ?? 0, "Cancelled", "The operation was cancelled.");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new SupervisorActorOperationResult(
                SupervisorOperationOutcome.EmergencyFallback, request?.OperationId ?? Guid.Empty,
                request?.Target ?? default, request?.ExpectedGeneration ?? 0,
                "FinalContainment", Bound(exception.Message));
        }
        finally { if (acquired) _gate.Release(); }
    }

    IActor[] ConstructAndRegister(Type[] descriptors, CancellationToken cancellationToken)
    {
        var actors = new List<IActor>(descriptors.Length);
        foreach (var descriptor in descriptors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actor = _factory.GetActor(descriptor)
                ?? throw new InvalidOperationException($"Unable to construct actor {descriptor}.");
            _actorSupervisor.AddActor(actor);
            actors.Add(actor);
            _managedActors = actors.ToArray();
            _actorSupervisor.AddProducer(actor.Id, _actorSupervisor.Container.Resolve<IActorProducer>());
            if (actor.Id.ActorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
                _actorSupervisor.AddJSProducer(actor.Id, _actorSupervisor.Container.Resolve<IJSActorProducer>());
        }
        return actors.ToArray();
    }

    void RegisterConsumers(IEnumerable<ActorType> actorTypes)
    {
        var distinctActorTypes = actorTypes.Distinct().ToArray();
        if (distinctActorTypes.Contains(ActorType.Notify))
            throw new InvalidOperationException("Notify subjects cannot be registered as backend actors.");
        foreach (var actorType in distinctActorTypes)
        {
            if (actorType.GetDeliveryType() == ActorDeliveryType.NatsCore)
                _actorSupervisor.AddConsumer(actorType, _actorSupervisor.Container.Resolve<IActorConsumer>());
            else if (actorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
                _actorSupervisor.AddConsumer(actorType, _actorSupervisor.Container.Resolve<IJSActorConsumer>());
            else
                throw new InvalidOperationException($"Actor type {actorType} has no backend delivery type.");
        }
    }

    async ValueTask<(int Stopped, IActor[] Remaining, List<string> Failures)> StopActorsAsync(
        IActor[] actors,
        CancellationToken cancellationToken)
    {
        var stopped = 0;
        List<IActor> remaining = [];
        List<string> failures = [];
        for (var index = actors.Length - 1; index >= 0; index--)
        {
            var actor = actors[index];
            try
            {
                await _actorSupervisor.StopAsync(actor.Id, cancellationToken).AsTask()
                    .WaitAsync(_options.ActorShutdownTimeout, cancellationToken).ConfigureAwait(false);
                _actorSupervisor.RemoveConsumer(actor.Id.ActorType);
                if (actor.Id.ActorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
                    _actorSupervisor.RemoveJSProducer(actor.Id);
                _actorSupervisor.RemoveActor(actor);
                _actorSupervisor.RemoveProducer(actor.Id);
                stopped++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (TimeoutException)
            {
                remaining.Add(actor);
                failures.Add($"Managed:{actor.Id}: stop timed out after {_options.ActorShutdownTimeout}");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                remaining.Add(actor);
                RecordRollbackFailure($"Managed:{actor.Id}", exception, failures);
            }
        }
        remaining.Reverse();
        return (stopped, remaining.ToArray(), failures);
    }

    async ValueTask<string?> RollbackAsync()
    {
        List<string> failures = [];
        try { _actorSupervisor.SetReadiness(false); }
        catch (Exception exception) when (IsRecoverable(exception)) { RecordRollbackFailure("Readiness", exception, failures); }
        try
        {
            if (!_poller.Stop(TimeSpan.FromSeconds(2)))
                failures.Add("Poller: timed out");
        }
        catch (Exception exception) when (IsRecoverable(exception)) { RecordRollbackFailure("Poller", exception, failures); }
        try { await _actorSupervisor.StopConsumersAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception exception) when (IsRecoverable(exception)) { RecordRollbackFailure("Intake", exception, failures); }
        for (var index = _managedActors.Length - 1; index >= 0; index--)
        {
            var actor = _managedActors[index];
            try
            {
                if (actor.IsRunning)
                    await _actorSupervisor.StopAsync(actor.Id, CancellationToken.None).ConfigureAwait(false);
                _actorSupervisor.RemoveConsumer(actor.Id.ActorType);
                if (actor.Id.ActorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
                    _actorSupervisor.RemoveJSProducer(actor.Id);
                _actorSupervisor.RemoveProducer(actor.Id);
                _actorSupervisor.RemoveActor(actor);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RecordRollbackFailure($"Managed:{actor.Id}", exception, failures);
            }
        }
        _managedActors = [];
        Volatile.Write(ref _running, 0);
        return failures.Count == 0 ? null : string.Join("; ", failures);
    }

    void RecordRollbackFailure(string component, Exception exception, List<string> failures)
    {
        failures.Add($"{component}: {exception.Message}");
        TryLogShutdownFailure(Guid.Empty, "Rollback" + component, exception);
    }

    static bool IsSupervisorActor(Type descriptor) =>
        descriptor.IsGenericType
        && descriptor.GetGenericArguments()[0].Assembly == SupervisorActorAssembly.Current;

    void TryLogStartupFailure(Guid operationId, string stage, Exception exception)
    {
        try { _logger.LogError(exception, "Supervisor startup {OperationId} failed at {Stage}.", operationId, stage); }
        catch (Exception loggingException) when (IsRecoverable(loggingException)) { }
    }

    void TryLogShutdownFailure(Guid operationId, string stage, Exception exception)
    {
        try { _logger.LogError(exception, "Supervisor shutdown {OperationId} failed at {Stage}.", operationId, stage); }
        catch (Exception loggingException) when (IsRecoverable(loggingException)) { }
    }

    static string Bound(string value) => value.Length <= 1024 ? value : value[..1024];

    static string CombineFailure(string primary, string? cleanup) =>
        Bound(cleanup is null ? primary : $"{primary} | Cleanup: {cleanup}");

    static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;
}
