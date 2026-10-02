using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;

/// <summary>Host-only, nonthrowing bootstrap for the Supervisor actor population.</summary>
public sealed class SupervisorBootstrap(
    IActorSupervisor actorSupervisor,
    IActorRegistry registry,
    IActorFactory factory,
    ActorRuntimeStartupOptions options,
    ILogger<SupervisorBootstrap> logger) : ISupervisorBootstrap
{
    readonly IActorSupervisor _actorSupervisor = actorSupervisor ?? throw new ArgumentNullException(nameof(actorSupervisor));
    readonly IActorRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    readonly IActorFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    readonly ActorRuntimeStartupOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    readonly ILogger<SupervisorBootstrap> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    readonly SemaphoreSlim _gate = new(1, 1);
    IActor[] _actors = [];
    int _running;

    /// <inheritdoc />
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    /// <inheritdoc />
    public IReadOnlyList<ActorType> ActorTypes => _actors.Select(static actor => actor.Id.ActorType).ToArray();

    /// <inheritdoc />
    public async ValueTask<SupervisorActorsStartupResult> StartSupervisorAsync(CancellationToken cancellationToken)
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
                return new(SupervisorOperationOutcome.Succeeded, operationId, _actors.Length, _actors.Length,
                    "AlreadyRunning", null);

            _actorSupervisor.SetReadiness(false);
            stage = "DiscoverSupervisor";
            var descriptors = _registry.ActorTypes.Where(IsSupervisorActor).ToArray();
            expected = descriptors.Length;
            if (expected == 0)
                throw new InvalidOperationException("No Supervisor actors were registered.");

            stage = "ConstructSupervisor";
            var actors = new List<IActor>(expected);
            foreach (var descriptor in descriptors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actor = _factory.GetActor(descriptor)
                    ?? throw new InvalidOperationException($"Unable to construct Supervisor actor {descriptor}.");
                _actorSupervisor.AddActor(actor);
                actors.Add(actor);
                _actors = actors.ToArray();
                _actorSupervisor.AddProducer(actor.Id, _actorSupervisor.Container.Resolve<IActorProducer>());
                if (actor.Id.ActorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
                    _actorSupervisor.AddJSProducer(actor.Id, _actorSupervisor.Container.Resolve<IJSActorProducer>());
            }

            stage = "StartSupervisor";
            foreach (var actor in _actors)
            {
                await _actorSupervisor.StartAsync(actor.Id, cancellationToken).ConfigureAwait(false);
                started++;
            }
            if (started != expected || _actors.Any(static actor => !actor.IsRunning))
                throw new InvalidOperationException(
                    $"Supervisor startup validation failed: expected {expected}, started {started}.");

            Volatile.Write(ref _running, 1);
            return new(SupervisorOperationOutcome.Succeeded, operationId, expected, started, "Completed", null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cleanupFailure = acquired ? await RollbackAsync().ConfigureAwait(false) : null;
            return new(SupervisorOperationOutcome.Cancelled, operationId, expected, started, stage,
                CombineFailure("Supervisor bootstrap was cancelled.", cleanupFailure));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            TryLogFailure(operationId, stage, exception);
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
    public async ValueTask<SupervisorActorsShutdownResult> StopSupervisorAsync(CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var expected = _actors.Length;
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
            List<IActor> remaining = [];
            stage = "StopSupervisor";
            for (var index = _actors.Length - 1; index >= 0; index--)
            {
                var actor = _actors[index];
                try
                {
                    await _actorSupervisor.StopAsync(actor.Id, cancellationToken).AsTask()
                        .WaitAsync(_options.ActorShutdownTimeout, cancellationToken).ConfigureAwait(false);
                    RemoveBindings(actor);
                    stopped++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (TimeoutException)
                {
                    remaining.Add(actor);
                    failures.Add($"Supervisor:{actor.Id}: stop timed out after {_options.ActorShutdownTimeout}");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    remaining.Add(actor);
                    TryLogFailure(operationId, stage, exception);
                    failures.Add($"Supervisor:{actor.Id}: {exception.Message}");
                }
            }
            remaining.Reverse();
            _actors = remaining.ToArray();

            if (_actors.Length == 0)
            {
                stage = "StopRuntime";
                try
                {
                    await _actorSupervisor.ShutdownAsync(cancellationToken).AsTask()
                        .WaitAsync(_options.ActorShutdownTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    TryLogFailure(operationId, stage, exception);
                    failures.Add($"Runtime: {exception.Message}");
                }
            }
            Volatile.Write(ref _running, failures.Count == 0 ? 0 : 1);
            if (failures.Count != 0)
                return new(SupervisorOperationOutcome.PartiallyCompleted, operationId, expected, stopped,
                    "HostRecoveryRequired", Bound(string.Join("; ", failures)));
            return new(SupervisorOperationOutcome.Succeeded, operationId, expected, stopped, "Completed", null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(SupervisorOperationOutcome.Cancelled, operationId, expected, stopped, stage,
                "Supervisor shutdown was cancelled.");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            TryLogFailure(operationId, stage, exception);
            return new(SupervisorOperationOutcome.PartiallyCompleted, operationId, expected, stopped, stage,
                Bound(exception.Message));
        }
        finally
        {
            if (acquired) _gate.Release();
        }
    }

    void RemoveBindings(IActor actor)
    {
        if (actor.Id.ActorType.GetDeliveryType() == ActorDeliveryType.NatsJetStream)
            _actorSupervisor.RemoveJSProducer(actor.Id);
        _actorSupervisor.RemoveProducer(actor.Id);
        _actorSupervisor.RemoveActor(actor);
    }

    async ValueTask<string?> RollbackAsync()
    {
        List<string> failures = [];
        for (var index = _actors.Length - 1; index >= 0; index--)
        {
            var actor = _actors[index];
            try
            {
                if (actor.IsRunning)
                    await _actorSupervisor.StopAsync(actor.Id, CancellationToken.None).ConfigureAwait(false);
                RemoveBindings(actor);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                TryLogFailure(Guid.Empty, "RollbackSupervisor", exception);
                failures.Add($"Supervisor:{actor.Id}: {exception.Message}");
            }
        }
        _actors = [];
        Volatile.Write(ref _running, 0);
        return failures.Count == 0 ? null : string.Join("; ", failures);
    }

    static bool IsSupervisorActor(Type descriptor) =>
        descriptor.IsGenericType
        && string.Equals(descriptor.GetGenericArguments()[0].Assembly.GetName().Name,
            SupervisorSharedAssembly.ActorAssemblyName, StringComparison.Ordinal);

    void TryLogFailure(Guid operationId, string stage, Exception exception)
    {
        try { _logger.LogError(exception, "Supervisor bootstrap {OperationId} failed at {Stage}.", operationId, stage); }
        catch (Exception loggingException) when (IsRecoverable(loggingException)) { }
    }

    static string Bound(string value) => value.Length <= 1024 ? value : value[..1024];

    static string CombineFailure(string primary, string? cleanup) =>
        Bound(cleanup is null ? primary : $"{primary} | Cleanup: {cleanup}");

    static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;
}
