using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using System.Collections.Concurrent;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact, Trait("Gate", "TS-06")]
    public async Task Durable_pending_and_reserved_handoffs_recover_over_NATS_without_allocating_new_Portfolio_identities()
    {
        // Stop only the notification boundary to simulate a crash after each authoritative commit.
        var snapshots = new ConcurrentQueue<WorkflowStrategyStateUpdatedEvent>();
        var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(call =>
        {
            foreach (var item in call.Arg<DomainEventCollection>().OfType<WorkflowStrategyStateUpdatedEvent>()) snapshots.Enqueue(item);
            return ValueTask.CompletedTask;
        });
        await using var factory = Host(services =>
        {
            var container = (SimpleInjector.Container)services.Single(x => x.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.RegisterInstance(projector);
            container.RegisterSingleton<ICompositionPreparationStore>(() => new TomasAI.IFM.Application.Storage.MarketDataDb.CompositionPreparationStore(
                container.GetInstance<IDbContextFactory>().MarketDataDb));
            services.AddSingleton(_ => container.GetInstance<ICompositionPreparationStore>());
        });
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var producer = factory.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, "SelectionHandoffVerification"));
        var probe = new CompositionCapture();
        supervisor.AddActor(probe);
        await probe.StartAsync(supervisor);
        try
        {
            var scope = Random.Shared.Next(10000000, 20000000);
            var c = await TradeSelectionFixture.Command("ShortBalancedIronCondor", atUtc: DateTime.UtcNow,
                contractId: "ES.TEST." + Guid.NewGuid().ToString("N"), scopeId: scope, compositionReady: true, compositionIntegrationTiming: true);
            var result = TradeSelectionEvaluator.Evaluate(c);
            var complete = new CompleteTradeSelectionCommand
            {
                CommandId = Guid.NewGuid(), Subject = Subject(CompleteTradeSelectionCommand.Verb, c.WorkflowEntityId),
                EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision,
                SourceEventId = result.ResultId, CorrelationId = c.CorrelationId, CausationId = result.ResultId,
                CompletedAtUtc = DateTime.UtcNow,
                Result = StrategyStageResultEnvelope.CreateSelection(result)
            };
            var seed = new WorkflowStrategyStateUpdatedEvent
            {
                Id = Guid.NewGuid(), EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId,
                WorkflowRevision = c.InputWorkflowRevision, State = c.WorkflowView with { SelectionDispatch = c },
                Subject = new(ActorType.Event, CompleteTradeSelectionCommand.Actor, WorkflowStrategyStateUpdatedEvent.Verb, c.WorkflowEntityId.Format())
            };
            await factory.Services.GetRequiredService<IEventSourceActorDbContext>().SaveEventsAsync(complete.StreamId, Guid.NewGuid(), new DomainEventCollection([seed]), 0, CancellationToken.None);
            var accepted = await producer.RequestAsync<CompleteTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId, GuidResult>(complete.Subject, complete, complete.EntityId);
            accepted.Success.Should().BeTrue(accepted.ErrorMessage);
            var pendingSnapshot = await UntilSnapshot(snapshots, x => x.State.CompositionHandoff?.Status == CompositionHandoffStatus.ReservationPending);
            var repository = factory.Services.GetRequiredService<SimpleInjector.Container>()
                .GetInstance<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
            var recovered = await repository.LoadStateAsync(complete);
            var pending = recovered.CurrentView!.CompositionHandoff!;
            MessagePackSerializer.Serialize(pending).Should().Equal(MessagePackSerializer.Serialize(pendingSnapshot.State.CompositionHandoff));

            // Real Portfolio aggregate and PostgreSQL history, including restart and committed-command lookup.
            var authority = c.SelectionBinding.PortfolioSnapshot;
            var fundId = new PortfolioFundId(scope, scope);
            var store = new PortfolioEventStore(factory.Services.GetRequiredService<IEventSourceActorDbContext>());
            var fund = new PortfolioFundAggregate();
            var created = fund.Create(Guid.NewGuid(), authority.Fund with { OperatingState = FundOperatingState.Draft }, c.EvaluatedAtUtc, "selection-integration");
            await store.AppendFundAsync(fundId, created, 0);
            var active = fund.ChangeState(Guid.NewGuid(), fund.Revision, FundOperatingState.Active, "test activation", new(true, 1, true, true), c.EvaluatedAtUtc, "selection-integration");
            await store.AppendFundAsync(fundId, active, 1);
            var reserveId = pending.Request.IdempotencyKey;
            var reservedEvent = fund.ReserveComposition(reserveId, fund.Revision, pending.Request, authority,
                scope + 1, [scope + 2], DateTime.UtcNow, "selection-integration");
            await store.AppendFundAsync(fundId, reservedEvent, 2);
            var restarted = await new PortfolioEventStore(factory.Services.GetRequiredService<IEventSourceActorDbContext>()).LoadFundAsync(fundId);
            restarted.TryComposition(reserveId, out var reservation).Should().BeTrue();
            (await store.FindCommittedFundCommandAsync(fundId, reserveId)).Should().NotBeNull();
            restarted.Orders.Should().ContainSingle();
            reservation.Trades.Should().ContainSingle();
            reservation.Order.OrderId.Should().Be(scope + 1);
            TradeSelectionHandoff.ValidateReservation(pending, reservation);

            var callback = new CompleteTradeSelectionReservationCommand
            {
                CommandId = Guid.NewGuid(), Subject = Subject(CompleteTradeSelectionReservationCommand.Verb, c.WorkflowEntityId),
                EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId, InputWorkflowRevision = pending.AcceptedSelectionRevision,
                SourceEventId = pending.SelectionSourceEventId, Reservation = reservation,
                ReservationRequestSha256 = pending.ReservationRequestSha256, CompletedAtUtc = DateTime.UtcNow,
                CorrelationId = c.CorrelationId, CausationId = pending.SelectionSourceEventId
            };
            await producer.SendAsync<CompleteTradeSelectionReservationCommand, IntrinsicTimeStrategyWorkflowEntityId>(callback.Subject, callback, callback.EntityId);
            var reserved = await UntilSnapshot(snapshots, x => x.State.CurrentStage == StrategyWorkflowStage.OrderComposition);
            var afterRestart = await repository.LoadStateAsync(callback);
            afterRestart.CurrentView!.CompositionHandoff!.Reservation!.Order.OrderId.Should().Be(scope + 1);
            probe.Commands.Should().BeEmpty("notification was withheld after durable reservation acceptance");
            // Complete-empty market evidence exercises durable preparation without inventing live prices.
            // A real composer evaluates this as NoCandidate; this probe tests only the saved dispatch boundary.
            var preparations = factory.Services.GetRequiredService<ICompositionPreparationStore>();
            var at = DateTimeOffset.UtcNow;
            var marketRequest = new CompositionSnapshotRequest(Guid.NewGuid(), "complete-empty-test", reserved.State.TriggerEvent.EntityId.TimePeriod.ToString(),
                Guid.NewGuid(), at, at.AddSeconds(5), false);
            var marketSnapshot = new MarketCompositionSnapshot(1, marketRequest.SnapshotId, marketRequest.ScopeId, "complete-empty-v1", marketRequest.Horizon,
                marketRequest.GenerationId, at, at.AddSeconds(5), [], "");
            marketSnapshot = marketSnapshot with { Digest = PricingSemanticHash.Compute(marketSnapshot) };
            var preparation = new CompositionPreparation(1, CompositionPreparationAcceptance.Key(reserved.State), "GLBX.MDP3",
                marketRequest, marketSnapshot, at, "");
            preparation = preparation with { Digest = PricingSemanticHash.Compute(preparation) };
            await preparations.CommitAsync(preparation, default);
            await Notify(producer, reserved);
            var acceptedPreparation = await UntilSnapshot(snapshots, x => x.State.CompositionDispatch is not null);
            probe.Commands.Should().BeEmpty("acceptance committed before its dispatch notification");
            var persistedPreparation = await repository.LoadStateAsync(callback);
            persistedPreparation.CurrentView!.CompositionDispatch!.MarketEvidence!.PreparationSha256.Should().Be(preparation.Digest);
            var execute = persistedPreparation.CurrentView!.CompositionExecution!;
            execute.Should().NotBeNull(); execute.MarketSnapshot.Digest.Should().Be(marketSnapshot.Digest);
            execute.Reservation.Order.OrderId.Should().Be(scope + 1);
            // Simulate a lost Function reply before workflow notification; recovery reuses the saved request.
            var first = await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events.OrderCompositionFunctionCompletedEvent,
                    Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events.OrderCompositionFunctionFailedEvent>>(execute.Subject, execute, execute.EntityId);
            first.Value!.IsCompleted.Should().BeTrue(first.ErrorMessage);
            first.Value.Completed!.Result.ReadCompositionResult().Outcome.Should().Be(
                Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionOutcome.NoCandidate);
            await Notify(producer, acceptedPreparation);
            var stopped = await UntilSnapshot(snapshots, x => x.State.Status == WorkflowStrategyMachineStatus.Completed
                && x.State.Outcome == StrategyWorkflowOutcome.NoTrade);
            var authoritative = (await repository.LoadStateAsync(callback)).CurrentView!;
            authoritative.OrderComposition.Result!.PayloadSha256.Should().Be(first.Value.Completed.Result.PayloadSha256);
            authoritative.RiskManagement.ProcessingStatus.Should().NotBe(StrategyActorProcessingStatus.Processing);
            var revision = authoritative.WorkflowRevision;
            await Notify(producer, acceptedPreparation);
            (await repository.LoadStateAsync(callback)).CurrentView!.WorkflowRevision.Should().Be(revision);
            probe.Commands.Should().BeEmpty("the historical Start route is not dispatched");
            restarted = await store.LoadFundAsync(fundId);
            restarted.Orders.Should().ContainSingle();
            restarted.Composition(scope + 1).Trades.Single().TradeId.Should().Be(scope + 2);
        }
        finally
        {
            await probe.StopAsync(); supervisor.RemoveActor(probe);
            await supervisor.ShutdownAsync(); await producer.StopAsync();
        }
    }

    static ActorSubject Subject(string verb, IntrinsicTimeStrategyWorkflowEntityId entity)
        => new(ActorType.Command, CompleteTradeSelectionCommand.Actor, verb, entity.Format());

    static async Task<WorkflowStrategyStateUpdatedEvent> UntilSnapshot(ConcurrentQueue<WorkflowStrategyStateUpdatedEvent> snapshots, Func<WorkflowStrategyStateUpdatedEvent, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            if (snapshots.LastOrDefault(predicate) is { } found) return found;
            if (timeout.IsCancellationRequested) throw new InvalidOperationException("No matching committed snapshot; observed: " + string.Join(",", snapshots.Select(x => $"{x.State.Status}/{x.State.CurrentStage}/{x.State.StopReasonCode}/{x.State.TradeSelection.Failure?.ErrorMessage}")));
            await Task.Delay(25);
        }
    }

    static ValueTask Notify(IActorProducer producer, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var value = snapshot with { Subject = new(ActorType.Realtime, IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName, WorkflowStrategyStateUpdatedEvent.Verb, snapshot.EntityId.Format()) };
        return producer.SendAsync<WorkflowStrategyStateUpdatedEvent, IntrinsicTimeStrategyWorkflowEntityId>(value.Subject, value);
    }

    // Captures the composer boundary only; does not pretend to implement an order builder.
    sealed class CompositionCapture : IActor
    {
        public ActorMailboxId Id { get; } = new(ActorType.Command, StartOrderCompositionPipelineCommand.Actor);
        public IActorMailbox Mailbox { get; private set; } = null!;
        public bool IsRunning { get; private set; }
        public ConcurrentDictionary<Guid, StartOrderCompositionPipelineCommand> Commands { get; } = new();
        public ConcurrentQueue<StartOrderCompositionPipelineCommand> Deliveries { get; } = new();
        public TaskCompletionSource First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Second { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int count;
        public ValueTask StartAsync(IActorSupervisor supervisor) { Mailbox = supervisor.CreateMailbox(Id); IsRunning = true; return ValueTask.CompletedTask; }
        public ValueTask StopAsync() { IsRunning = false; return ValueTask.CompletedTask; }
        public async ValueTask HandleMessageAsync(IActorMessage message)
        {
            Guid commandId;
            try
            {
                var command = message.AsCommand<StartOrderCompositionPipelineCommand>()!;
                commandId = command.CommandId;
                Commands.TryAdd(command.CommandId, command);
                Deliveries.Enqueue(command);
                if (Interlocked.Increment(ref count) == 1) First.TrySetResult(); else Second.TrySetResult();
            }
            finally { message.ReleasePayload(); }
            await message.ReplyAsync<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(commandId)));
        }
    }
}
