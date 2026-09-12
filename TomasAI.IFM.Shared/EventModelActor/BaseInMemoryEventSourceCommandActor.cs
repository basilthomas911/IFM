using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Opt-in event-sourced command actor that retains committed state for selected high-rate commands.
/// Selected commands advance working state in mailbox order and receive a reply only after atomic durable persistence.
/// </summary>
public abstract class BaseInMemoryEventSourceCommandActor<TActor, TState>(
    ICommandActorContext<TActor> actorContext,
    ILogger logger,
    InMemoryEventSourceActorOptions? options = null)
    : BaseEventSourceCommandActor<TActor>(actorContext, logger)
    where TActor : IActor
    where TState : class, IEventSourceActorState<TState>
{
    readonly InMemoryEventSourceActorOptions _options =
        (options ?? new InMemoryEventSourceActorOptions()).Validate();
    readonly ConcurrentDictionary<ActorThreadId, ResidentSlot> _resident = new();
    readonly ConcurrentQueue<ActorThreadId> _residentOrder = new();

    protected int ResidentStateCount => _resident.Count;

    protected abstract bool IsResidentCommand(ICommand command);
    protected abstract bool IsResidentMessage(ActorSubject subject);
    protected sealed override bool AuditIsCommittedWithState(ICommand command)
        => _options.OptionTradeLegDataEnabled && IsResidentCommand(command);
    protected abstract ValueTask<TState> LoadStateFromStoreAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command,
        CancellationToken cancellationToken);
    protected abstract ValueTask SaveStateToStoreAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, TState state, ICommand command,
        CancellationToken cancellationToken);
    protected abstract ValueTask PersistResidentEventsAsync(
        ICommandActorContext<TActor> context,
        ICommand command,
        DomainEventCollection events,
        long expectedStreamVersion,
        CancellationToken cancellationToken);
    protected abstract ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command, Exception exception);

    protected virtual ValueTask OnInMemoryStartupAsync(ICommandActorContext<TActor> context)
        => ValueTask.CompletedTask;
    protected virtual ValueTask OnInMemoryShutdownAsync(ICommandActorContext<TActor> context)
        => ValueTask.CompletedTask;

    protected sealed override ValueTask OnStartup(ICommandActorContext<TActor> context)
        => OnInMemoryStartupAsync(context);

    protected sealed override async ValueTask OnShutdown(ICommandActorContext<TActor> context)
    {
        await BarrierAllAsync().ConfigureAwait(false);
        if (!_resident.IsEmpty)
            ActorRuntimeMetrics.ResidentStateCount.Add(-_resident.Count);
        _resident.Clear();
        while (_residentOrder.TryDequeue(out _)) { }
        await OnInMemoryShutdownAsync(context).ConfigureAwait(false);
    }

    public sealed override async ValueTask HandleMessageAsync(
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        if (!_options.OptionTradeLegDataEnabled || !IsResidentMessage(message.Subject))
        {
            await BarrierAsync(threadId).ConfigureAwait(false);
            RemoveResident(threadId);
            await base.HandleMessageAsync(message, threadId, cancellationToken).ConfigureAwait(false);
            return;
        }

        ICommand? command = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { command = ParseMessage(Context, message); }
            finally { message.ReleasePayload(); }
            if (command.CommandId == Guid.Empty)
            {
                await message.ReplyAsync<ServiceResult<GuidResult>>(
                    new ServiceFailed<GuidResult>(command.ErrorCode, $"{command.CommandName}.CommandId is empty"));
                return;
            }

            var validationErrors = GetCommandValidationErrors(command);
            if (validationErrors is { Count: > 0 })
            {
                await message.ReplyAsync<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(
                    command.ErrorCode,
                    string.Join(Environment.NewLine, validationErrors.Select(static error => error.ErrorMessage))));
                return;
            }
            await OnValidateAsync(Context, threadId, command, cancellationToken).ConfigureAwait(false);
            var state = (TState)await OnLoadStateAsync(Context, threadId, command, cancellationToken).ConfigureAwait(false);
            state.Id = threadId;
            var result = await ReceiveAsync(Context, state, command, cancellationToken).ConfigureAwait(false);
            var events = state.DetachChanges();
            var slot = _resident[threadId];

            if (!slot.Warmed)
            {
                try
                {
                    await PersistResidentEventsAsync(
                        Context, command, events, slot.NextExpectedVersion, CancellationToken.None).ConfigureAwait(false);
                    slot.NextExpectedVersion = checked(slot.NextExpectedVersion + events.Count);
                    slot.Warmed = true;
                    ActorRuntimeMetrics.ResidentPersistenceCompleted.Add(1);
                    await message.ReplyAsync(result).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    ActorRuntimeMetrics.ResidentPersistenceFailed.Add(1);
                    RemoveResident(threadId);
                    await ReplyFailureOrDuplicateAsync(message, threadId, command, exception).ConfigureAwait(false);
                }
                finally
                {
                    await OnCommandFinishedAsync(Context, command).ConfigureAwait(false);
                }
                return;
            }

            var expectedVersion = slot.NextExpectedVersion;
            slot.NextExpectedVersion = checked(expectedVersion + events.Count);
            Task persistence;
            try
            {
                persistence = PersistResidentEventsAsync(
                    Context, command, events, expectedVersion, CancellationToken.None).AsTask();
            }
            catch
            {
                slot.NextExpectedVersion = expectedVersion;
                throw;
            }
            int pendingCount;
            lock (slot.Sync)
            {
                slot.Pending.Add(persistence);
                pendingCount = slot.Pending.Count;
            }
            ActorRuntimeMetrics.ResidentPersistencePending.Add(1);
            _ = CompleteResidentCommandAsync(slot, persistence, message, threadId, command, result);

            // This is the one-shot command-count bound. The mailbox cannot admit command 65
            // until all work in the current durable window has a terminal outcome.
            if (pendingCount >= _options.MaximumCommandsPerWindow)
                await AwaitSlotAsync(slot).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (command is null) throw;
            RemoveResident(threadId);
            await ReplyFailureOrDuplicateAsync(message, threadId, command, exception).ConfigureAwait(false);
            await OnCommandFinishedAsync(Context, command).ConfigureAwait(false);
        }
    }

    protected sealed override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command, CancellationToken.None);

    protected sealed override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command,
        CancellationToken cancellationToken)
    {
        if (!_options.OptionTradeLegDataEnabled || !IsResidentCommand(command))
        {
            RemoveResident(threadId);
            return await LoadStateFromStoreAsync(context, threadId, command, cancellationToken)
                .ConfigureAwait(false);
        }

        if (_resident.TryGetValue(threadId, out var existing))
        {
            ActorRuntimeMetrics.ResidentStateHits.Add(1);
            return existing.State;
        }

        ActorRuntimeMetrics.ResidentStateMisses.Add(1);
        var loaded = await LoadStateFromStoreAsync(context, threadId, command, cancellationToken)
            .ConfigureAwait(false);
        var slot = new ResidentSlot(loaded, loaded.CommittedStreamVersion);
        if (_resident.TryAdd(threadId, slot))
        {
            ActorRuntimeMetrics.ResidentStateCount.Add(1);
            _residentOrder.Enqueue(threadId);
            TrimResidentStates(threadId);
            return loaded;
        }
        return _resident[threadId].State;
    }

    protected sealed override ValueTask OnSaveStateAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command, CancellationToken.None);

    protected sealed override async ValueTask OnSaveStateAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, IActorState state, ICommand command,
        CancellationToken cancellationToken)
    {
        var typedState = state as TState
            ?? throw new InvalidOperationException($"Expected state {typeof(TState).Name}.");
        try
        {
            await SaveStateToStoreAsync(context, threadId, typedState, command, cancellationToken)
                .ConfigureAwait(false);
            typedState.AcceptChanges();
        }
        catch
        {
            // Working state may include events that did not commit. Never serve it again.
            RemoveResident(threadId);
            throw;
        }
    }

    protected sealed override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        if (IsResidentCommand(command)) RemoveResident(threadId);
        return await HandleCommandExceptionAsync(context, threadId, command, ex).ConfigureAwait(false);
    }

    void TrimResidentStates(ActorThreadId current)
    {
        while (_resident.Count > _options.MaximumResidentStreams && _residentOrder.TryDequeue(out var oldest))
        {
            if (!oldest.Equals(current) && _resident.TryGetValue(oldest, out var slot) && slot.PendingCount == 0)
                RemoveResident(oldest);
        }
    }

    async Task CompleteResidentCommandAsync(
        ResidentSlot slot,
        Task persistence,
        IActorMessage message,
        ActorThreadId threadId,
        ICommand command,
        ServiceResult<GuidResult> result)
    {
        try
        {
            await persistence.ConfigureAwait(false);
            ActorRuntimeMetrics.ResidentPersistenceCompleted.Add(1);
            await message.ReplyAsync(result).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ActorRuntimeMetrics.ResidentPersistenceFailed.Add(1);
            RemoveResident(threadId, slot);
            await ReplyFailureOrDuplicateAsync(message, threadId, command, exception).ConfigureAwait(false);
        }
        finally
        {
            lock (slot.Sync) slot.Pending.Remove(persistence);
            ActorRuntimeMetrics.ResidentPersistencePending.Add(-1);
            await OnCommandFinishedAsync(Context, command).ConfigureAwait(false);
        }
    }

    async ValueTask ReplyFailureOrDuplicateAsync(
        IActorMessage message,
        ActorThreadId threadId,
        ICommand command,
        Exception exception)
    {
        ServiceResult<GuidResult> reply = IsCommittedDuplicateException(command, exception)
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : await HandleCommandExceptionAsync(Context, threadId, command, exception).ConfigureAwait(false);
        await message.ReplyAsync(reply).ConfigureAwait(false);
    }

    async ValueTask BarrierAsync(ActorThreadId threadId)
    {
        if (_resident.TryGetValue(threadId, out var slot))
            await AwaitSlotAsync(slot).ConfigureAwait(false);
    }

    async ValueTask BarrierAllAsync()
    {
        foreach (var slot in _resident.Values)
            await AwaitSlotAsync(slot).ConfigureAwait(false);
    }

    void RemoveResident(ActorThreadId threadId)
    {
        if (_resident.TryRemove(threadId, out _))
        {
            ActorRuntimeMetrics.ResidentStateCount.Add(-1);
            ActorRuntimeMetrics.ResidentStateEvictions.Add(1);
        }
    }

    void RemoveResident(ActorThreadId threadId, ResidentSlot slot)
    {
        if (_resident.TryRemove(new KeyValuePair<ActorThreadId, ResidentSlot>(threadId, slot)))
        {
            ActorRuntimeMetrics.ResidentStateCount.Add(-1);
            ActorRuntimeMetrics.ResidentStateEvictions.Add(1);
        }
    }

    static async ValueTask AwaitSlotAsync(ResidentSlot slot)
    {
        Task[] pending;
        lock (slot.Sync) pending = slot.Pending.ToArray();
        if (pending.Length == 0) return;
        try { await Task.WhenAll(pending).ConfigureAwait(false); }
        catch { /* Each deferred completion reports and evicts its own failed window. */ }
    }

    sealed class ResidentSlot(TState state, long expectedVersion)
    {
        public TState State { get; } = state;
        public object Sync { get; } = new();
        public List<Task> Pending { get; } = [];
        public long NextExpectedVersion { get; set; } = expectedVersion;
        public bool Warmed { get; set; }
        public int PendingCount { get { lock (Sync) return Pending.Count; } }
    }
}
