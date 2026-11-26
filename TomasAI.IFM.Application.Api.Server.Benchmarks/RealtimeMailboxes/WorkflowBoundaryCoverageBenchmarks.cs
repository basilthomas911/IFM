using BenchmarkDotNet.Attributes;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Actual v2 output boundary; equivalent direct isolated receiver is a weaker-ownership baseline.
/// Saved real source sizes, explicitly synthetic typed NoTrade completions. Not owner PostgreSQL or financial commit timings.</summary>
[MemoryDiagnoser]
public class WorkflowBoundaryCoverageBenchmarks
{
    [Params("Regime", "Selection", "Composition", "FinancialRequest")] public string Shape { get; set; } = "Regime";
    [Params("Capture", "AcceptedReplay")] public string Mode { get; set; } = "Capture";
    WorkflowStrategyStateUpdatedEvent source = null!;
    ICommand<IntrinsicTimeStrategyWorkflowEntityId> command = null!;
    ReserveFundOrderCompositionRequest request = null!;
    byte[] commandBytes = [], requestBytes = [];
    WorkflowPublicationJournal journal = null!;
    Receiver before = null!, after = null!;
    string root = "";
    object? retained;
    Guid Key => WorkflowPublicationDelivery.Key(source.State);
    string Digest => WorkflowPublicationDelivery.Digest(source.State);
    bool Financial => Shape == "FinancialRequest";

    [GlobalSetup]
    public void LoadAndVerify()
    {
        var name = Shape switch { "Regime" => "20261125-safety/workflow-regime-completion", "Selection" => "20261124-safety/workflow-selection-function", _ => "20261124-safety/workflow-composition-function" };
        var path = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes", name + "-real-source.msgpack");
        var bytes = File.ReadAllBytes(path);
        var expected = Shape switch { "Regime" => 2044, "Selection" => 47134, _ => 407570 };
        if (bytes.Length != expected) throw new InvalidDataException("Qualified source fixture size changed.");
        source = MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(bytes);
        var view = source.State;
        var at = view.UpdatedAtUtc;
        var id = Guid.NewGuid();
        command = Shape switch
        {
            "Regime" => new CompleteRegimeDiscoveryCommand { CommandId = id, EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = source.Id, CausationId = source.Id, CompletedAtUtc = at,
                Subject = Subject(CompleteRegimeDiscoveryCommand.Verb), ParameterSet = view.RegimeDiscoveryParameterSet, ParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256,
                Result = StrategyStageResultEnvelope.CreateRegime(new RegimeDiscoveryResult { ResultId = Guid.NewGuid(), EntityId = view.EntityId, WorkflowId = view.WorkflowId, ProducedAtUtc = at, MarketDataAsOfUtc = at }) },
            "Selection" => new CompleteTradeSelectionCommand { CommandId = id, EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = source.Id, CausationId = source.Id, CompletedAtUtc = at, Subject = Subject(CompleteTradeSelectionCommand.Verb),
                Result = StrategyStageResultEnvelope.CreateSelection(new TradeSelectionResult { SchemaVersion = 1, ResultId = Guid.NewGuid(), EntityId = view.EntityId, WorkflowId = view.WorkflowId,
                    InputWorkflowRevision = view.WorkflowRevision, ProducedAtUtc = at, EvaluatedAtUtc = at, ValidUntilUtc = view.ExpiresAtUtc,
                    Outcome = SelectionOutcome.NoTrade, PrimaryReasonCode = "BENCHMARK.NO_TRADE",
                    DecisionContext = new() { RegimeResultEnvelope = view.RegimeDiscovery.Result ?? new(), AssessmentResultEnvelope = view.MarketCondition.Result ?? new(), SelectionBinding = view.SelectionBinding } }) },
            _ => new CompleteOrderCompositionCommand { CommandId = id, EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = source.Id, CausationId = source.Id, CompletedAtUtc = at, Subject = Subject(CompleteOrderCompositionCommand.Verb),
                Result = StrategyStageResultEnvelope.CreateComposition(new OrderCompositionResult { ResultId = Guid.NewGuid(), EntityId = view.EntityId, WorkflowId = view.WorkflowId,
                    InputWorkflowRevision = view.WorkflowRevision, ProducedAtUtc = at, EvaluatedAtUtc = at, Outcome = CompositionOutcome.NoCandidate, SummaryText = "Benchmark typed NoCandidate; no strategy calculation timed." }) }
        };
        commandBytes = Serialize(command);
        request = new ReserveFundOrderCompositionRequest { WorkflowId = view.WorkflowId.Value, WorkflowRevision = view.WorkflowRevision, TradeSelectionInvocationId = Guid.NewGuid(), TradeSelectionResultId = Guid.NewGuid(),
            TradeSelectionResultSha256 = new string('A',64), PortfolioId = 1, PortfolioVersion = 1, FundId = 701, FundMandateVersion = 1, TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 1,
            OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1, UnderlyingRoot = "ES", DecisionHorizon = "Monthly", RequestedTradeDate = new(2026,11,20), RequestedMaturityDate = new(2027,1,1),
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = at, ExpiresAtUtc = at.AddMinutes(5), PortfolioFundStrategySnapshotSha256 = new string('B',64),
            TradeInstructions = [new() { TradeFamily = "IronCondor", DirectionOrBias = "Balanced", TradeAction = "Open", UnderlyingRoot = "ES", RequestedTradeDate = new(2026,11,20), CreatedOnUtc = at, Reference = source.Id.ToString("N") }] };
        _ = MessageOrigins.ForPublication(request,new FrozenClock(at),"workflow-benchmark",null,null,source.Id);
        requestBytes = MessagePackSerializer.Serialize(request);
        VerifyBoundary().GetAwaiter().GetResult();
        Console.WriteLine($"QUALIFIED Shape={Shape} SourceBytes={bytes.Length} OutputBytes={commandBytes.Length} FinancialRequestBytes={requestBytes.Length} Outcome={(Shape == "Selection" ? "NoTrade" : Shape == "Regime" ? "TypedRegime" : "NoCandidate")} Receiver=IsolatedInMemory Owner=IsolatedProof");
    }
    ActorSubject Subject(string verb) => new(ActorType.Command, CompleteRegimeDiscoveryCommand.Actor, verb, source.EntityId.Format());
    static byte[] Serialize(ICommand<IntrinsicTimeStrategyWorkflowEntityId> value) => MessagePackSerializer.Serialize(value.GetType(), value);

    async Task VerifyBoundary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ifm-workflow-boundary-verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var saved = new WorkflowPublicationJournal(directory))
            {
                var receiver = new Receiver(saved, source.State.UpdatedAtUtc) { Refuse = true };
                try { await WorkflowPublicationDelivery.PublishAsync(receiver.Context, source, command); throw new InvalidDataException("Refused output reported success."); }
                catch (IOException) { }
                var pending = saved.Find(Key) ?? throw new InvalidDataException("Output was not captured.");
                if (pending.Disposition != WorkflowPublicationDisposition.Pending || !Serialize(pending.Command).AsSpan().SequenceEqual(commandBytes) || !ReferenceEquals(command,pending.Command)) throw new InvalidDataException("Pending original output changed.");
                receiver.Refuse = false;
                if (!await WorkflowPublicationDelivery.RecoverAsync(receiver.Context, source.State) || saved.Find(Key)?.Disposition != WorkflowPublicationDisposition.Accepted || saved.PendingCount != 0 || receiver.Effects != 1) throw new InvalidDataException("Original pending output did not settle once.");
                var calls = receiver.Calls;
                await WorkflowPublicationDelivery.RecoverAsync(receiver.Context, source.State);
                if (receiver.Calls != calls || !Serialize(receiver.Last!).AsSpan().SequenceEqual(commandBytes)) throw new InvalidDataException("Accepted replay resubmitted or changed output.");
                var frozen = saved.FreezeInput(Key, Digest, () => request);
                if (!MessagePackSerializer.Serialize(frozen).AsSpan().SequenceEqual(requestBytes) || saved.FrozenInputCount != 1) throw new InvalidDataException("Financial capture changed its original request.");
            }
            using (var cold = new WorkflowPublicationJournal(directory))
            {
                if (cold.Find(Key)?.Disposition != WorkflowPublicationDisposition.Accepted || !Serialize(cold.Find(Key)!.Command).AsSpan().SequenceEqual(commandBytes) || MessageOrigins.Get(cold.Find(Key)!.Command) != MessageOrigins.Get(command)) throw new InvalidDataException("Cold output identity/timestamp/outcome changed.");
                var frozen = cold.FreezeInput<ReserveFundOrderCompositionRequest>(Key,Digest,()=>throw new InvalidDataException("Cold financial replay invoked creation."));
                if (!MessagePackSerializer.Serialize(frozen).AsSpan().SequenceEqual(requestBytes) || frozen.IdempotencyKey != request.IdempotencyKey || frozen.RequestedAtUtc != request.RequestedAtUtc || MessageOrigins.Get(frozen) != MessageOrigins.Get(request)) throw new InvalidDataException("Cold financial operation identity/hash changed.");
            }
        }
        finally { Directory.Delete(directory,true); }
    }
    [IterationSetup]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(),"ifm-workflow-boundary-bdn-"+Guid.NewGuid().ToString("N"));
        journal = new(root); before = new(null,source.State.UpdatedAtUtc); after = new(journal,source.State.UpdatedAtUtc);
        if (Mode == "AcceptedReplay")
        {
            if (Financial) retained = journal.FreezeInput(Key,Digest,()=>request);
            else { WorkflowPublicationDelivery.PublishAsync(before.Context,source,command).AsTask().GetAwaiter().GetResult(); WorkflowPublicationDelivery.PublishAsync(after.Context,source,command).AsTask().GetAwaiter().GetResult(); }
        }
    }
    [Benchmark(Baseline=true)]
    public async Task BeforeEquivalentVolatileBoundary()
    {
        if (Financial) retained = request.DefensiveCopy();
        else await WorkflowPublicationDelivery.PublishAsync(before.Context,source,command);
    }
    [Benchmark]
    public async Task AfterV2FrozenBoundary()
    {
        if (Financial) retained = journal.FreezeInput(Key,Digest,()=>request);
        else if (Mode == "AcceptedReplay") { if(!await WorkflowPublicationDelivery.RecoverAsync(after.Context,source.State)) throw new InvalidDataException("Expected accepted output."); }
        else await WorkflowPublicationDelivery.PublishAsync(after.Context,source,command);
    }
    [IterationCleanup]
    public void Cleanup() { GC.KeepAlive(retained); journal.Dispose(); Directory.Delete(root,true); }
    public static void VerifyAll()
    {
        foreach(var shape in new[]{"Regime","Selection","Composition","FinancialRequest"}) { var item=new WorkflowBoundaryCoverageBenchmarks{Shape=shape}; item.LoadAndVerify(); }
    }
    sealed class Receiver : IWorkflowPublicationOwner
    {
        public IIntrinsicTimeStrategyWorkflowRealtimeContext Context { get; } = Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        readonly HashSet<Guid> accepted = [];
        public bool Refuse; public int Calls, Effects; public ICommand<IntrinsicTimeStrategyWorkflowEntityId>? Last;
        public Receiver(WorkflowPublicationJournal? journal,DateTime at)
        {
            Context.Publications.Returns(journal); Context.PublicationOwner.Returns(this); Context.TimeProvider.Returns(new FrozenClock(at));
            Context.SendAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteRegimeDiscoveryCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>()).Returns(c=>Receive(c.Arg<CompleteRegimeDiscoveryCommand>()));
            Context.SendAsync<CompleteTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteTradeSelectionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>()).Returns(c=>Receive(c.Arg<CompleteTradeSelectionCommand>()));
            Context.SendAsync<CompleteOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteOrderCompositionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>()).Returns(c=>Receive(c.Arg<CompleteOrderCompositionCommand>()));
        }
        ValueTask Receive(ICommand<IntrinsicTimeStrategyWorkflowEntityId> value)
        { Calls++; Last=value; if(Refuse)throw new IOException("Injected required delivery refusal."); if(accepted.Add(value.CommandId))Effects++; return ValueTask.CompletedTask; }
        public Task<WorkflowPublicationDisposition?> ReconcileAsync(WorkflowPublication publication,IIntrinsicTimeStrategyWorkflowRealtimeContext context,CancellationToken token)
            =>Task.FromResult<WorkflowPublicationDisposition?>(accepted.Contains(publication.Command.CommandId)?WorkflowPublicationDisposition.Accepted:null);
    }
    sealed class FrozenClock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(at); }
}
