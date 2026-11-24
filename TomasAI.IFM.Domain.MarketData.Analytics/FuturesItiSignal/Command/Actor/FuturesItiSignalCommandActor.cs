using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
/// <summary>
/// Represents an actor responsible for managing futures ITI signal commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref = "FuturesItiSignalCommandActor"/> is a specialized command actor designed to handle operations
/// related to futures ITI signals. It processes commands such as generating signals, setting hold-trade state, and
/// clearing hold-trade state. It validates the commands, and manages the actor's state. This actor relies on an
/// event-sourced repository for state persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="actorContext">The actor's typed services and lifecycle.</param>
/// <param name="residency">The independently configured committed-state cache policy.</param>
public class FuturesItiSignalCommandActor(ICommandActorContext<FuturesItiSignalCommandActor> actorContext,
    ResidentActorPilotOptions? residency = null)
    : BaseInMemoryEventSourceCommandActor<FuturesItiSignalCommandActor, FuturesItiSignalCommandState>(actorContext,
        actorContext.Logger, committedStatePolicy: (residency ?? new()).FuturesItiSignal)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesItiSignalCommandContext ActorContext => IsArgumentNull.Named(Context as IFuturesItiSignalCommandContext, nameof(Context))!;

    readonly TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery.FuturesAtrHotCacheReader atrReader = new();
    readonly bool residencyEnabled = residency?.FuturesItiSignal.Enabled == true;
    readonly bool orderedIntakeEnabled = residency?.FuturesItiOrderedIntakeEnabled == true;
    readonly string journalDirectory = (residency ?? new()).FuturesItiJournalDirectory;
    readonly SemaphoreSlim commandGate = new(1, 1);
    readonly SemaphoreSlim preparationGate = new(1, 1);
    readonly SemaphoreSlim processingGate = new(1, 1);
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Sequence, TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSourceCursor Cursor)> processedSources = new(StringComparer.Ordinal);
    readonly System.Collections.Concurrent.ConcurrentDictionary<(string Stream, Guid Epoch), long> retiredSources = new();
    IFuturesItiResidentRepository ResidentRepository => _repo as IFuturesItiResidentRepository
        ?? throw new InvalidOperationException("ITI requires the durable resident repository.");
    public bool OrderedIntakeEnabled => orderedIntakeEnabled;
    public TomasAI.IFM.Application.Storage.EventSourceDb.FuturesItiResidentStore? ResidentStore => (_repo as IFuturesItiResidentRepository)?.ResidentStore;
    public TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiInputWal? InputWal { get; private set; }

    public const string ActorName = "FuturesItiSignalCommand";
    IEventProjector<FuturesItiSignalCommandActor> EventProjector => ActorContext.EventProjector;

    IEventSourceActorStateRepository<FuturesItiSignalCommandState> _repo = default!;
    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <param name = "context">The <see cref = "ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref = "ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnInMemoryStartupAsync(ICommandActorContext<FuturesItiSignalCommandActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>());
        if (orderedIntakeEnabled && !residencyEnabled) throw new InvalidOperationException("Ordered ITI requires committed residency.");
        if (residencyEnabled && _repo is not IResidentCommandStateRepository<FuturesItiSignalCommandState>)
            throw new InvalidOperationException("Resident ITI requires an authoritative no-change stream guard.");
        await EventProjector.StartAsync(context).ConfigureAwait(false);
        if (orderedIntakeEnabled)
        {
            try
            {
                await ResidentRepository.OpenResidentWriterAsync(context, CancellationToken.None).ConfigureAwait(false);
                InputWal = new(Path.Combine(journalDirectory, ResidentStore!.JournalNamespace));
                _ = await ResidentStore.BindJournalRootAsync(InputWal.RootId).ConfigureAwait(false);
                await ImportWalAsync(CancellationToken.None).ConfigureAwait(false);
                await RecoverInputsAsync(CancellationToken.None).ConfigureAwait(false);
                FuturesItiLocalCommandEndpoint.Register(this);
            }
            catch
            {
                await ResidentRepository.CloseResidentWriterAsync().ConfigureAwait(false);
                if (InputWal is not null) await InputWal.DisposeAsync().ConfigureAwait(false);
                await EventProjector.StopAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    protected override async ValueTask OnInMemoryShutdownAsync(ICommandActorContext<FuturesItiSignalCommandActor> context)
    {
        FuturesItiLocalCommandEndpoint.Unregister(this);
        await EventProjector.StopAsync().ConfigureAwait(false);
        if (orderedIntakeEnabled) await ResidentRepository.CloseResidentWriterAsync().ConfigureAwait(false);
        if (InputWal is not null) await InputWal.DisposeAsync().ConfigureAwait(false);
    }
    protected override ValueTask OnInMemoryStoppingAsync(ICommandActorContext<FuturesItiSignalCommandActor> context)
        => FuturesItiLocalCommandEndpoint.StopIngressAsync(this);
    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <param name = "context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name = "message">The NATS message containing the command data to be parsed.</param>
    /// <returns>An <see cref = "ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref = "InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor.</exception>
    protected override ICommand ParseMessage(ICommandActorContext<FuturesItiSignalCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [GenerateFuturesItiSignalCommand.Verb] = msg => msg.AsCommand<GenerateFuturesItiSignalCommand>()!,
        [ClearFuturesItiSignalHoldTradeCommand.Verb] = msg => msg.AsCommand<ClearFuturesItiSignalHoldTradeCommand>()!,
        [SetFuturesItiSignalHoldTradeCommand.Verb] = msg => msg.AsCommand<SetFuturesItiSignalHoldTradeCommand>()!
    };
    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name = "context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name = "state">The current state of the actor. Must be a valid instance of <see cref = "FuturesItiSignalCommandState"/>. Cannot be null.</param>
    /// <param name = "cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref = "InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesItiSignalCommandActor> context, IActorState state, ICommand command)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var itiSignalState = IsArgumentNull.Set((state as FuturesItiSignalCommandState)!);
        if (command is GenerateFuturesItiSignalCommand batch && IsDurableInput(batch))
        {
            foreach (var input in batch.ResidentBatch!)
            {
                if (!string.IsNullOrEmpty(input.InputUnavailableReason))
                {
                    ActorContext.Telemetry.RecordInputUnavailable(input.InputUnavailableReason);
                    continue;
                }
                var result = input.Execute(itiSignalState, ActorContext.Telemetry, ActorContext.Logger);
                if (!result.Success) return ValueTask.FromResult(result);
            }
            return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(batch.CommandId)));
        }
        if (command is GenerateFuturesItiSignalCommand { TimePeriod: TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily } generate)
        {
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.DailyItiAtrThreshold? input = null;
            if (atrReader.TryGetLatest(generate.ContractId, TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.FiveMinutes, out var atr)
                && atr is { IsWarm: true, Metadata.IsValid: true, PeriodLength: 14, AtrValue: > 0 } && double.IsFinite(atr.AtrValue)
                && atr.Metadata.CalculatedAtUtc.UtcDateTime <= generate.Timestamp
                && atr.Metadata.MarketDataAsOfUtc.UtcDateTime <= generate.Timestamp
                && generate.Timestamp - atr.Metadata.MarketDataAsOfUtc.UtcDateTime <= TimeSpan.FromSeconds(630))
                input = new(generate.ContractId, atr.AtrValue, atr.Metadata.MarketDataAsOfUtc.UtcDateTime,
                    atr.Metadata.CalculatedAtUtc.UtcDateTime, atr.Metadata.SourceSequence);
            command = MessageOrigins.Copy(generate, generate with { RequireDailyAtr = true, DailyAtr = input });
        }
        var receiveFunc = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveFunc.Invoke(command, dispatchContext, itiSignalState));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures ITI signal
    /// command logic on a given state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<FuturesItiSignalCommandActor>, FuturesItiSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<FuturesItiSignalCommandActor>, FuturesItiSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(GenerateFuturesItiSignalCommand)] = (cmd, context, state) =>
        {
            var typed = context as IFuturesItiSignalCommandContext;
            return ((GenerateFuturesItiSignalCommand)cmd).Execute(state, typed?.Telemetry, typed?.Logger);
        },
        [typeof(ClearFuturesItiSignalHoldTradeCommand)] = (cmd, context, state) => (cmd as ClearFuturesItiSignalHoldTradeCommand)!.Execute(state),
        [typeof(SetFuturesItiSignalHoldTradeCommand)] = (cmd, context, state) => (cmd as SetFuturesItiSignalHoldTradeCommand)!.Execute(state)
    };
    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <param name = "context">The context in which the command is being executed.</param>
    /// <param name = "threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name = "cmd">The command to be validated. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesItiSignalCommandActor> context, ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);
    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesItiSignalCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        if (command is GenerateFuturesItiSignalCommand batch && IsDurableInput(batch)) return;
        ArgumentNullException.ThrowIfNull(command);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        _ = command.GetType();
        ValidateMappedCommand(command, _validationMap);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
    {
        [typeof(GenerateFuturesItiSignalCommand)] = cmd =>
        {
            var e = (GenerateFuturesItiSignalCommand)cmd;
            return new List<ValidationError>().ValidateCommandId(e.CommandId, e.CommandName).ValidateEntityId(e.EntityId, e.CommandName).ValidateContractId(e.ContractId, e.CommandName).ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate").ValidateDateOnly(e.TimeFrameStartValueDate, e.CommandName, "ValueDate").ValidateTimePeriod(e.TimePeriod, e.CommandName).ValidateTimestamp(e.Timestamp, e.CommandName).ValidateFuturesPrice(e.FuturesPrice, e.CommandName).ValidateVixFuturesPrice(e.VixFuturesPrice, e.CommandName);
        },
        [typeof(ClearFuturesItiSignalHoldTradeCommand)] = cmd =>
        {
            var e = (ClearFuturesItiSignalHoldTradeCommand)cmd;
            return new List<ValidationError>().ValidateCommandId(e.CommandId, e.CommandName).ValidateEntityId(e.EntityId, e.CommandName).ValidateContractId(e.ContractId, e.CommandName).ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate").ValidateTimePeriod(e.TimePeriod, e.CommandName).ValidateTimestamp(e.Timestamp, e.CommandName);
        },
        [typeof(SetFuturesItiSignalHoldTradeCommand)] = cmd =>
        {
            var e = (SetFuturesItiSignalHoldTradeCommand)cmd;
            return new List<ValidationError>().ValidateCommandId(e.CommandId, e.CommandName).ValidateEntityId(e.EntityId, e.CommandName).ValidateContractId(e.ContractId, e.CommandName).ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate").ValidateTimePeriod(e.TimePeriod, e.CommandName).ValidateTimestamp(e.Timestamp, e.CommandName);
        }
    };
    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name = "context">The context of the command actor.</param>
    /// <param name = "threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name = "command">The command for which state is being loaded. Cannot be null.</param>
    /// <param name = "cancellationToken">Cancels restoration.</param>
    /// <returns>A <see cref = "ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
    protected override ValueTask<FuturesItiSignalCommandState> LoadStateFromStoreAsync(
        ICommandActorContext<FuturesItiSignalCommandActor> context, ActorThreadId threadId,
        ICommand command, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        return _repo.LoadStateAsync(command, cancellationToken);
    }

    protected override async ValueTask SaveStateToStoreAsync(
        ICommandActorContext<FuturesItiSignalCommandActor> context, ActorThreadId threadId,
        FuturesItiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var generated = command is GenerateFuturesItiSignalCommand
            ? state.Events.Count(static item => item is FuturesItiSignalGeneratedEvent) : 0;
        await _repo.SaveStateAsync(context, state, command, cancellationToken).ConfigureAwait(false);
        if (command is GenerateFuturesItiSignalCommand batch && IsDurableInput(batch))
        {
            var last = batch.ResidentBatch![^1];
            processedSources[command.StreamId] = (last.JournalSequence, last.SourceCursor!);
            ResidentRepository.AdvanceProcessed(command.StreamId, last.JournalSequence);
        }
        if (generated > 0 && command is GenerateFuturesItiSignalCommand generatedCommand)
        {
            for (var index = 0; index < generated; index++) ActorContext.Telemetry.RecordEventCommitted();
            if (ActorContext.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
                FuturesItiSignalCommandLogging.EventCommitted(ActorContext.Logger, generatedCommand.CommandId,
                    generatedCommand.EntityId.Format(), generatedCommand.ContractId,
                    generatedCommand.ValueDate, generatedCommand.TimePeriod);
        }
    }

    protected override bool IsResidentCommand(ICommand command) => command is GenerateFuturesItiSignalCommand
        || orderedIntakeEnabled && command is SetFuturesItiSignalHoldTradeCommand or ClearFuturesItiSignalHoldTradeCommand;
    protected override bool IsResidentMessage(ActorSubject subject) => subject.Verb == GenerateFuturesItiSignalCommand.Verb;
    protected override ValueTask ValidateResidentNoChangeAsync(
        ICommandActorContext<FuturesItiSignalCommandActor> context, FuturesItiSignalCommandState state,
        ICommand command, CancellationToken cancellationToken)
        => command is GenerateFuturesItiSignalCommand generate && IsDurableInput(generate)
            ? ValidateOwnedNoChange()
            : _repo is IResidentCommandStateRepository<FuturesItiSignalCommandState> resident
            ? resident.ValidateNoChangeAsync(state, command, cancellationToken)
            : ValueTask.FromException(new InvalidOperationException("Resident ITI requires a guarded repository."));
    ValueTask ValidateOwnedNoChange() { ResidentStore!.AssertOwned(); return ValueTask.CompletedTask; }
    bool IsDurableInput(GenerateFuturesItiSignalCommand command) => orderedIntakeEnabled
        && command.ResidentAdmission is FuturesItiDurableAdmission admission
        && ReferenceEquals(admission.Store, ResidentStore) && admission.Matches(command);
    protected override bool PersistsCommandAuditAtomically(ICommand command)
        => command is GenerateFuturesItiSignalCommand generate && IsDurableInput(generate);
    protected override async ValueTask<IAsyncDisposable?> AcquireCommittedCommandScopeAsync(CancellationToken token)
    {
        if (!orderedIntakeEnabled) return null;
        await commandGate.WaitAsync(token).ConfigureAwait(false);
        try { ResidentStore!.AssertOwned(); }
        catch { commandGate.Release(); throw; }
        return new CommandScope(commandGate);
    }
    sealed class CommandScope(SemaphoreSlim gate) : IAsyncDisposable
    { public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; } }

    /// <summary>Durable intake, outside decision processing. Captures never use current cache values on recovery.</summary>
    public async Task<FuturesItiDurableAdmission> PrepareInputsAsync(GenerateFuturesItiSignalCommand[] inputs, CancellationToken token = default)
    {
        if (!orderedIntakeEnabled || inputs.Length is < 1 or > 256)
            throw new InvalidOperationException("Invalid ITI durable batch or disabled ordered intake.");
        await preparationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var store = ResidentStore!;
            store.AssertOwned();
            var rows = new TomasAI.IFM.Application.Storage.EventSourceDb.FuturesItiResidentStore.Input[inputs.Length];
            for (var index = 0; index < inputs.Length; index++)
            {
                var input = inputs[index];
                if (input.StreamId != inputs[0].StreamId || !input.InputsFrozen || input.SourceCursor is not { Epoch: var epoch, Ordinal: > 0 } cursor
                    || epoch == Guid.Empty || input.ResidentAdmission is not null || input.ResidentBatch is not null)
                    throw new InvalidOperationException("Unqualified ITI source batch.");
                rows[index] = new(input.CommandId, input.StreamId, cursor.Dataset, cursor.PublisherId, cursor.InstrumentId,
                    cursor.Epoch, cursor.Ordinal, cursor.EventAtUtc.Ticks, input.JournalPayload ?? FuturesItiJournalEnvelope.Encode(input));
            }
            var sequences = await store.AppendInputsAsync(rows, token).ConfigureAwait(false);
            await PrimeCommittedStateAsync(inputs[0], token).ConfigureAwait(false);
            var prepared = inputs.Select((input, index) => MessageOrigins.Copy(input, input with { JournalSequence = sequences[index] })).ToArray();
            return new(store, prepared);
        }
        finally { preparationGate.Release(); }
    }

    /// <summary>Processes already durable inputs locally. Warm no-change input performs no database work.</summary>
    public async ValueTask ProcessPreparedAsync(FuturesItiDurableAdmission admission, CancellationToken token = default)
    {
        await processingGate.WaitAsync(token).ConfigureAwait(false);
        try { await ProcessPreparedCoreAsync(admission, token).ConfigureAwait(false); }
        finally { processingGate.Release(); }
    }

    async ValueTask ProcessPreparedCoreAsync(FuturesItiDurableAdmission admission, CancellationToken token)
    {
        if (!ReferenceEquals(admission.Store, ResidentStore)) throw new InvalidOperationException("Stale ITI admission owner.");
        ResidentStore!.AssertOwned();
        var all = admission.Inputs;
        var stream = all[0].StreamId;
        if (processedSources.TryGetValue(stream, out var processed))
        {
            var next = all.Where(input =>
            {
                var cursor = input.SourceCursor!;
                if (retiredSources.TryGetValue((stream, cursor.Epoch), out var retired))
                {
                    if (cursor.Ordinal > retired) throw new InvalidOperationException("An ITI input follows the sealed end of a retired source.");
                    return false;
                }
                return cursor.Epoch != processed.Cursor.Epoch || cursor.Ordinal > processed.Cursor.Ordinal;
            }).ToArray();
            if (next.Length == 0) return;
            if (next.Length != all.Length) admission = new(ResidentStore, next);
        }
        var prior = processedSources.TryGetValue(stream, out processed) ? processed.Cursor : null;
        foreach (var input in admission.Inputs)
        {
            var current = input.SourceCursor!;
            if (prior is not null && prior.Epoch != current.Epoch && input.VerifiedEpochStart && current.Ordinal == 1
                && prior.Dataset == current.Dataset && prior.PublisherId == current.PublisherId && prior.InstrumentId == current.InstrumentId)
            {
                _ = await ResidentStore.BridgeEpochAsync(stream, prior.Epoch, prior.Ordinal, processed.Sequence, current.Epoch, token).ConfigureAwait(false);
                retiredSources[(stream, prior.Epoch)] = prior.Ordinal;
                prior = null;
            }
            if (prior is not null && (prior.Dataset != current.Dataset || prior.PublisherId != current.PublisherId
                || prior.InstrumentId != current.InstrumentId || prior.Epoch != current.Epoch || current.Ordinal != prior.Ordinal + 1))
                throw new InvalidOperationException($"ITI source gap at {stream}: {prior.Epoch}/{prior.Ordinal} -> {current.Epoch}/{current.Ordinal}.");
            prior = current;
        }
        var command = MessageOrigins.Copy(admission.Inputs[^1], admission.Inputs[^1] with
        { ResidentAdmission = admission, ResidentBatch = admission.Inputs });
        var message = new FuturesItiLocalMessage(command);
        await HandleMessageAsync(message, command.Subject.ThreadId, token).ConfigureAwait(false);
        if (message.Result?.Success != true)
            throw new InvalidOperationException("Durable ITI processing stopped for recovery: " + message.Result?.ErrorMessage);
    }

    /// <summary>Recovery/shutdown control barrier. No known failed or unprocessed input may be sealed by intake.</summary>
    public async ValueTask SealProcessedEpochsAsync(CancellationToken token = default)
    {
        await processingGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await commandGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                foreach (var pair in processedSources)
                {
                    var version = await ActorContext.DbEventSource.GetCommittedStreamVersionAsync(pair.Key, token).ConfigureAwait(false);
                    _ = await ResidentStore!.SealAsync(pair.Key, pair.Value.Cursor.Epoch, pair.Value.Cursor.Ordinal,
                        pair.Value.Sequence, version, token).ConfigureAwait(false);
                }
            }
            finally { commandGate.Release(); }
        }
        finally { processingGate.Release(); }
    }

    async Task RecoverInputsAsync(CancellationToken token)
    {
        var store = ResidentStore!;
        foreach (var retired in await store.ReadRetiredEpochsAsync(token).ConfigureAwait(false))
            retiredSources[(retired.Stream, retired.Epoch)] = retired.LastOrdinal;
        var committed = await store.ReadCheckpointInputsAsync(token).ConfigureAwait(false);
        foreach (var row in committed)
        {
            var input = FuturesItiJournalEnvelope.Decode(row.Payload, row.Id);
            processedSources[row.Stream] = (row.Id, input.SourceCursor!);
            ResidentRepository.AdvanceProcessed(row.Stream, row.Id);
        }
        var pending = new List<GenerateFuturesItiSignalCommand>(64);
        await foreach (var row in store.ReadPendingAsync(token).ConfigureAwait(false))
        {
            var input = FuturesItiJournalEnvelope.Decode(row.Payload, row.Id);
            if (pending.Count == 64 || pending.Count > 0 && pending[0].StreamId != input.StreamId)
            {
                await ProcessPreparedAsync(new(store, pending.ToArray()), token).ConfigureAwait(false);
                pending.Clear();
            }
            pending.Add(input);
        }
        if (pending.Count > 0) await ProcessPreparedAsync(new(store, pending.ToArray()), token).ConfigureAwait(false);
    }

    async Task ImportWalAsync(CancellationToken token)
    {
        var pending = new List<GenerateFuturesItiSignalCommand>(64);
        TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiInputWal.Position last = default;
        await foreach (var row in InputWal!.ReplayAsync(token).ConfigureAwait(false))
        {
            var input = FuturesItiJournalEnvelope.Decode(row.Payload, 0);
            if (pending.Count == 64 || pending.Count > 0 && pending[0].StreamId != input.StreamId)
            {
                _ = await PrepareInputsAsync(pending.ToArray(), token).ConfigureAwait(false);
                await InputWal.AcknowledgeAsync(last).ConfigureAwait(false);
                pending.Clear();
            }
            pending.Add(input);
            last = row.Position;
        }
        if (pending.Count > 0)
        {
            _ = await PrepareInputsAsync(pending.ToArray(), token).ConfigureAwait(false);
            await InputWal.AcknowledgeAsync(last).ConfigureAwait(false);
        }
    }

    public bool IsProcessedSource(string stream, TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSourceCursor cursor)
    {
        if (retiredSources.TryGetValue((stream,cursor.Epoch), out var sealedOrdinal)) return cursor.Ordinal <= sealedOrdinal;
        return processedSources.TryGetValue(stream, out var current) && current.Cursor.Epoch == cursor.Epoch
            && cursor.Ordinal <= current.Cursor.Ordinal;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")]
    protected override async ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(ICommandActorContext<FuturesItiSignalCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        (context as IFuturesItiSignalCommandContext)?.Telemetry?.RecordFailure(ex.Message);
        var logValue2 = threadId.ToString();
var logValue1 = ex.GetType().Name;
FuturesItiSignalCommandLogging.CommandFailed(ActorContext.Logger, ex, command?.CommandId ?? Guid.Empty, logValue2, command?.CommandName ?? "Unknown", logValue1);
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context)).ConfigureAwait(false);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write55(Context.Logger, innerEx, ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context)).ConfigureAwait(false);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }
    }
}
