using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport.ResidentActors;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>
/// Current Daily ITI actor, including command audit, PostgreSQL restoration, evaluation and signal commit.
/// Synthetic ordered prices isolate storage cost; this is not cash-open/recovery qualification.
/// </summary>
[MemoryDiagnoser, InProcess, InvocationCount(1), WarmupCount(3), IterationCount(12)]
public partial class FuturesItiStorageBaselineBenchmarks
{
    const int BatchSize = 16;
    const string Contract = "ES20261218";
    readonly DateTime epoch = new(2026, 11, 23, 14, 30, 0, DateTimeKind.Utc);
    ResidentPilotDatabase database = null!;
    FuturesItiSignalCommandActor actor = null!;
    FuturesItiSignalRuntimeTelemetry telemetry = null!;
    long ordinal;
    int loads;
    long startEvents;
    IFuturesItiSignalCommandContext context = null!;
    CountedRepository counted = null!;
    bool qualifying;
    long sourceOrdinal;
    Guid sourceEpoch = new("a1111111-1111-1111-1111-111111111111");
    readonly string fixtureJournalDirectory = Path.Combine(Path.GetTempPath(), "ifm-iti-fixture-" + Guid.NewGuid().ToString("N"));
    FuturesItiDurableAdmission prepared = null!;
    CountingAudit audits = null!;
    long decisionTransactions;

    [Params(false, true)] public bool SignalChanges { get; set; }
    [Params("Before", "Guarded", "Fast")] public string Mode { get; set; } = "Before";
    public bool Resident { get => Mode != "Before"; set => Mode = value ? "Guarded" : "Before"; }
    public bool Fast => Mode == "Fast";
    public TimeFrameType Frame { get; set; } = TimeFrameType.Daily;

    [GlobalSetup]
    public async Task Setup()
    {
        database = new();
        await database.InitializeAsync().ConfigureAwait(false);
        database.States.CreateState<FuturesItiSignalCommandState>().Returns(_ => new FuturesItiSignalCommandState());
        context = Substitute.For<IFuturesItiSignalCommandContext>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesItiSignalCommandActor.ActorName));
        context.Container.Returns(database.Container);
        context.DbEventSource.Returns(database.Events);
        context.Logger.Returns(NullLogger<FuturesItiSignalCommandActor>.Instance);
        var projector = Substitute.For<IEventProjector<FuturesItiSignalCommandActor>>();
        context.EventProjector.Returns(projector);
        telemetry = new(TimeProvider.System);
        context.Telemetry.Returns(telemetry);
        var repository = new FuturesItiSignalStateRepository(database.States, database.Events,
            Substitute.For<IActorService>(), database.Databases, projector, NullLogger<FuturesItiSignalStateRepository>.Instance);
        counted = new CountedRepository(repository, () => Interlocked.Increment(ref loads));
        database.Container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(counted);
        audits = new(database.Container.Resolve<ICommandAuditLogger>());
        database.Container.Resolve<ICommandAuditLogger>().Returns(audits);
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesAtrSignalReadModel
        {
            ContractId = Contract, ValueDate = DateOnly.FromDateTime(epoch), TimePeriod = TimeFrameType.FiveMinutes,
            PeriodLength = 14, IsWarm = true, AtrValue = 10, AtrBaseline = 10, AtrRatio = 1,
            Metadata = new MarketAnalyticsSignalMetadata
            {
                ContractId = Contract, ValueDate = DateOnly.FromDateTime(epoch), IsValid = true,
                SignalKey = new(MarketSeriesIdentity.ForContract(Contract), MarketAnalyticsSignalKind.Atr,
                    TimeFrameType.FiveMinutes, "iti-baseline-atr14"),
                MarketDataAsOfUtc = new(epoch.AddSeconds(-1)), CalculatedAtUtc = new(epoch.AddSeconds(-1)), SourceSequence = 1
            }
        });
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        await SendAsync(5000).ConfigureAwait(false);
        startEvents = await database.EventCountAsync(Command(5000).StreamId).ConfigureAwait(false);
        if (startEvents != 1)
            throw new InvalidOperationException("Baseline fixture did not persist its initial Daily signal.");
        loads = 0;
        audits.Calls = 0;
    }

    FuturesItiSignalCommandActor CreateActor() => new(context, new ResidentActorPilotOptions
    { FuturesItiSignal = new() { Enabled = Resident }, FuturesItiOrderedIntakeEnabled = Fast,
        FuturesItiJournalDirectory = fixtureJournalDirectory });

    GenerateFuturesItiSignalCommand Command(double price)
    {
        var command = new GenerateFuturesItiSignalCommand(Contract, DateOnly.FromDateTime(epoch),
            Frame, epoch.AddTicks(++ordinal), price, 20);
        return command with { CommandId = Guid.NewGuid(), RequireDailyAtr = true,
            DailyAtr = new(Contract, 10, epoch.AddSeconds(-1), epoch.AddSeconds(-1), 1), Subject = new(ActorType.Command,
            GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, command.EntityId.Format()) };
    }

    async ValueTask SendAsync(double price)
    {
        var command = Command(price);
        var message = new ResidentPilotMessage(command);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, CancellationToken.None).ConfigureAwait(false);
        if (message.Result?.Success != true)
            throw new InvalidOperationException("Baseline rejected input: " + message.Result?.ErrorMessage);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    public async Task CurrentPostgresActor()
    {
        if (Fast)
        {
            var admission = prepared ?? throw new InvalidOperationException("Fast benchmark input must be prepared before each invocation.");
            prepared = null!;
            var before = actor.ResidentStore!.Transactions;
            await actor.ProcessPreparedAsync(admission).ConfigureAwait(false);
            decisionTransactions += actor.ResidentStore.Transactions - before;
            return;
        }
        for (var index = 0; index < BatchSize; index++)
            await SendAsync(SignalChanges ? (index % 2 == 0 ? 5020 : 4980) : 5000).ConfigureAwait(false);
    }

    [IterationSetup(Target = nameof(CurrentPostgresActor))]
    public void PrepareIteration()
    {
        if (!Fast || prepared is not null) return;
        var inputs = Enumerable.Range(0, BatchSize).Select(index => SourceCommand(
            SignalChanges ? (index % 2 == 0 ? 5020 : 4980) : 5000)).ToArray();
        prepared = actor.PrepareInputsAsync(inputs).GetAwaiter().GetResult();
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task DurableIngressAndActor()
    {
        if (!Fast) { await CurrentPostgresActor().ConfigureAwait(false); return; }
        var inputs = Enumerable.Range(0, BatchSize).Select(index => SourceCommand(
            SignalChanges ? (index % 2 == 0 ? 5020 : 4980) : 5000)).ToArray();
        TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiInputWal.Position position = default;
        foreach (var input in inputs)
            position = await actor.InputWal!.AppendAsync(FuturesItiJournalEnvelope.Encode(input), default).ConfigureAwait(false);
        var admission = await actor.PrepareInputsAsync(inputs).ConfigureAwait(false);
        await actor.InputWal!.AcknowledgeAsync(position).ConfigureAwait(false);
        await actor.ProcessPreparedAsync(admission).ConfigureAwait(false);
    }

    GenerateFuturesItiSignalCommand SourceCommand(double price)
    {
        var command = Command(price);
        return MessageOrigins.Copy(command, command with { InputsFrozen = true,
            SourceCursor = new("GLBX.MDP3", 1, 42, sourceEpoch,
                ++sourceOrdinal, sourceOrdinal, command.Timestamp, command.Timestamp) });
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try
        {
            if (actor is not null)
            {
                var current = await database.EventCountAsync(Command(5000).StreamId).ConfigureAwait(false);
                var snapshot = telemetry.GetSnapshot();
                Console.WriteLine($"ITI baseline checks: mode={Mode}; signalChanges={SignalChanges}; stateLoads={loads}; audits={audits.Calls}; decisionTransactions={decisionTransactions}; " +
                    $"noChangeHeadGuards={counted.Guards}; persistedEvents={current - startEvents}; cache={actor.CommittedCacheStatistics}; " +
                    $"telemetry={System.Text.Json.JsonSerializer.Serialize(snapshot)}");
                if (!qualifying && ((!Resident && loads == 0) || (Resident && loads != 0)
                    || (!SignalChanges && current != startEvents) || (SignalChanges && current <= startEvents)))
                    throw new InvalidOperationException("Measured storage path or signal/no-change parity was not exercised.");
                if (!qualifying && Fast && !SignalChanges && (audits.Calls != 0 || decisionTransactions != 0 || counted.Guards != 0))
                    throw new InvalidOperationException("Warm no-change fast processing accessed storage.");
                await actor.StopAsync().ConfigureAwait(false);
            }
        }
        finally { if (database is not null) await database.DisposeAsync().ConfigureAwait(false); }
    }

    /// <summary>Qualifies real persistence and resident lifecycle separately from measured operations.</summary>
    public async Task VerifyLifecycleAsync()
    {
        qualifying = true;
        var reference = new FuturesItiSignalCommandState();
        Require((Command(5000) with { Timestamp = epoch.AddTicks(1) }).Execute(reference).Success, "Reference seed failed.");
        reference.AcceptChanges();
        counted.Capture = true;
        foreach (var price in new[] { 5000d, 5000, 5002, 5010, 5012, 5008, 4990, 4980, 4990, 5010 })
        {
            var command = Command(price);
            Require(command.Execute(reference).Success, "Reference execution failed.");
            var expected = reference.Events.OfType<TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent>()
                .Select(item => MessagePack.MessagePackSerializer.Serialize(item.FuturesItiSignal)).ToArray();
            reference.AcceptChanges();
            counted.Captured.Clear();
            await DispatchAsync(command).ConfigureAwait(false);
            Require(expected.Length == counted.Captured.Count, "Signal/no-change parity failed.");
            for (var index = 0; index < expected.Length; index++)
                Require(expected[index].AsSpan().SequenceEqual(counted.Captured[index]), "Business signal parity failed.");
        }
        var duplicate = Command(5012);
        await DispatchAsync(duplicate).ConfigureAwait(false);
        var stream = duplicate.StreamId;
        var count = await database.EventCountAsync(stream).ConfigureAwait(false);
        await DispatchAsync(duplicate).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Duplicate appended another event.");

        // A real control command evicts the resident entry, then generation hydrates the control state.
        var control = new SetFuturesItiSignalHoldTradeCommand(Contract, DateOnly.FromDateTime(epoch), Frame, epoch.AddTicks(++ordinal));
        control = control with { CommandId = Guid.NewGuid(), Subject = new(ActorType.Command,
            SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, control.EntityId.Format()) };
        await DispatchAsync(control).ConfigureAwait(false);
        var priorLoads = loads;
        await SendAsync(5012).ConfigureAwait(false);
        Require(loads == priorLoads + 1, "Generation did not reload after control mutation.");

        // Recreate the actor over the same PostgreSQL stream; no price/event timestamp is replaced.
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        priorLoads = loads;
        await SendAsync(5012).ConfigureAwait(false);
        Require(loads == priorLoads + 1, "Restart did not hydrate committed state.");

        // Inject rollback after business writes. Next distinct command must hydrate, never reuse the candidate.
        count = await database.EventCountAsync(stream).ConfigureAwait(false);
        database.Transactions.FailNextCommit = true;
        var failed = await DispatchAsync(Command(4000), false).ConfigureAwait(false);
        Require(failed.Result?.Success == false, "Injected rollback was not reported.");
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Rollback left a source event.");
        priorLoads = loads;
        await SendAsync(3900).ConfigureAwait(false);
        Require(loads == priorLoads + 1, "Failed candidate state was reused.");
        if (Resident)
        {
            var external = new FuturesItiSignalCommandActor(context);
            await external.StartAsync(database.Supervisor).ConfigureAwait(false);
            try
            {
                var change = Command(6500);
                var externalMessage = new ResidentPilotMessage(change);
                await external.HandleMessageAsync(externalMessage, change.Subject.ThreadId, default).ConfigureAwait(false);
                Require(externalMessage.Result?.Success == true, "External writer failed.");
                var stale = await DispatchAsync(Command(3900), false).ConfigureAwait(false);
                Require(stale.Result?.Success == false, "Stale resident no-change acknowledgement escaped its version guard.");
                priorLoads = loads;
                await SendAsync(6600).ConfigureAwait(false);
                Require(loads == priorLoads + 1, "External write did not invalidate cached state.");
            }
            finally { await external.StopAsync().ConfigureAwait(false); }
        }
        Console.WriteLine($"ITI lifecycle verified: frame={Frame}; resident={Resident}; evaluator parity, duplicates, controls, restart, rollback and external-writer guard.");
        await actor.StopAsync().ConfigureAwait(false);
        actor = null!;
    }

    static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    async ValueTask<ResidentPilotMessage> DispatchAsync(ICommand command, bool requireSuccess = true)
    {
        var message = new ResidentPilotMessage(command);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, CancellationToken.None).ConfigureAwait(false);
        if (requireSuccess) Require(message.Result?.Success == true, "Actor rejected input: " + message.Result?.ErrorMessage);
        return message;
    }

    sealed class CountedRepository(FuturesItiSignalStateRepository inner, Action load)
        : IFuturesItiResidentRepository
    {
        public bool Capture { get; set; }
        public int Guards { get; private set; }
        public List<byte[]> Captured { get; } = [];
        public List<TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent> CapturedEvents { get; } = [];
        public ValueTask<FuturesItiSignalCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, default);
        public ValueTask<FuturesItiSignalCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        { load(); return inner.LoadStateAsync(command, cancellationToken); }
        public ValueTask SaveStateAsync(ICommandActorContext context, FuturesItiSignalCommandState state, ICommand command)
            => SaveStateAsync(context, state, command, default);
        public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesItiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
        {
            var pendingEvents = Capture ? state.Events.OfType<TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent>().ToArray() : [];
            var pending = pendingEvents.Select(item => MessagePack.MessagePackSerializer.Serialize(item.FuturesItiSignal)).ToArray();
            await inner.SaveStateAsync(context, state, command, cancellationToken).ConfigureAwait(false);
            Captured.AddRange(pending);
            CapturedEvents.AddRange(pendingEvents);
        }
        public ValueTask ValidateNoChangeAsync(FuturesItiSignalCommandState state, ICommand command, CancellationToken cancellationToken)
        { Guards++; return inner.ValidateNoChangeAsync(state, command, cancellationToken); }
        public TomasAI.IFM.Application.Storage.EventSourceDb.FuturesItiResidentStore? ResidentStore => inner.ResidentStore;
        public ValueTask OpenResidentWriterAsync(ICommandActorContext context, CancellationToken token) => inner.OpenResidentWriterAsync(context, token);
        public ValueTask CloseResidentWriterAsync() => inner.CloseResidentWriterAsync();
        public void AdvanceProcessed(string stream, long sequence) => inner.AdvanceProcessed(stream, sequence);
    }

    sealed class CountingAudit(ICommandAuditLogger inner) : ICommandAuditLogger
    {
        public int Calls;
        public ValueTask<CommandAuditReservation> TryReserveAsync(ICommand command, CancellationToken cancellationToken = default)
        { Calls++; return inner.TryReserveAsync(command, cancellationToken); }
    }
}
