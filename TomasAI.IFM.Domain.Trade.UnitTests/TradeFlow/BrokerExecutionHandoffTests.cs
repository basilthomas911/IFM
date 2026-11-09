using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class BrokerExecutionHandoffTests
{
    [Theory]
    [InlineData(OrderExecutionStatus.PartiallyFilled)]
    [InlineData(OrderExecutionStatus.Filled)]
    public async Task Working_partial_execution_updates_the_fund_without_finalizing_the_trade(OrderExecutionStatus status)
    {
        var broker = Placement();
        var actors = Substitute.For<IActorService>();
        actors.SendAsync<RecordFundTradeSubmissionCommand, PortfolioFundId>(Arg.Any<RecordFundTradeSubmissionCommand>(),
            Arg.Any<PortfolioFundId>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var context = Substitute.For<IOrderExecutionCommandContext>();
        context.ActorService.Returns(actors);
        context.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());

        context.Logger.Returns(NullLogger<OrderExecutionCommandActor>.Instance);
        var factory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        tradeDb.ProviderName.Returns("System.Data.Postgres");
        factory.TradeDb.Returns(tradeDb);
        context.DbFactory.Returns(factory);
        var accounting = Substitute.For<IPortfolioTradeAccountingApi>();
        accounting.PostConfirmedExecutionAsync(Arg.Any<OrderExecutionDefinition>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<ServiceResult<Guid>>(
                new ServiceFailed<Guid>(1, "Accounting reconciliation required")));
        context.PortfolioAccounting.Returns(accounting);
        var source = new OrderExecutionChangedEvent {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = broker.Id.Execution,
            ReceivedOn = DateTime.UtcNow,
            OrderExecutionDefinition = new() { Order = broker.Order, TradeOrderId = broker.Order.Id,
                Components = broker.Order.Components, ExecutionAttemptId = broker.Id.Execution.ExecutionAttemptId,
                Status = status, OrderQuantity = 2, CumulativeFilledQuantity = 1,
                CompletedAtUtc = DateTime.UtcNow, Fills = [new() { ExecutionFillId = Guid.NewGuid() }] }
        };
        var descriptor = new OrderExecutionEventProjector(context).ProjectionDescriptors.Single();
        if (status == OrderExecutionStatus.Filled)
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await descriptor.ApplyAsync(source, default!));
        else
            await descriptor.ApplyAsync(source, default!);
        await actors.Received(1).SendAsync<RecordFundTradeSubmissionCommand, PortfolioFundId>(
            Arg.Is<RecordFundTradeSubmissionCommand>(command => command.ExecutionEvidence.ExecutionStatus == status.ToString()
                && command.ExecutionEvidence.ExecutionAttemptId == broker.Id.Execution.ExecutionAttemptId),
            Arg.Any<PortfolioFundId>());
        await actors.DidNotReceive().SendAsync<RecordFundTradeOpeningCommand, PortfolioFundId>(
            Arg.Any<RecordFundTradeOpeningCommand>(), Arg.Any<PortfolioFundId>());
    }

    [Theory]
    [InlineData(true, OrderExecutionStatus.PartiallyFilled, true)]
    [InlineData(false, OrderExecutionStatus.PartiallyFilled, false)]
    [InlineData(true, OrderExecutionStatus.Cancelled, false)]
    public async Task Reconcile_repairs_only_a_committed_final_cancellation_with_unfinished_execution(
        bool committedEvidence, OrderExecutionStatus status, bool shouldRepair)
    {
        var order = Placement() with { Status = BrokerOrderStatus.Cancelled };
        var id = order.Id.Execution;
        var fact = new BrokerObservation
        {
            Kind = BrokerObservationKind.Cancelled, ExecutionFinal = true, ObservationId = Guid.NewGuid(),
            AccountAlias = order.Order.BrokerAccountAlias, BrokerOrderId = order.Id.Format(),
            ComponentId = order.Id.ComponentId, OperationId = order.OperationId, OccurredAtUtc = order.ChangedAtUtc
        };
        var state = new BrokerOrderCommandState();
        state.ReplayEvents(new DomainEventCollection([new BrokerOrderStateSnapshottedEvent
        {
            Definition = order,
            ObservationHashes = committedEvidence
                ? new Dictionary<Guid, string> { [fact.ObservationId] = BrokerOrderObservationBridge.ContentHash(fact) }
                : []
        }]));
        var context = BrokerContext();
        var broker = context.TradeBroker;
        broker.AccountAlias.Returns(fact.AccountAlias);
        broker.Environment.Returns((TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment)(byte)order.Order.BrokerEnvironment);
        broker.ReconcileOrderAsync(order.Id.Format(), Arg.Any<CancellationToken>()).Returns(new[] { fact });
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.GetJSProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IJSActorProducer>());
        await using var bridge = new BrokerOrderObservationBridge(broker, supervisor, NullLogger<BrokerOrderObservationBridge>.Instance);
        context.ObservationBridge.Returns(bridge);
        var database = Substitute.For<ITradeDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.TradeDb.Returns(database);
        context.DbFactory.Returns(factory);
        database.GetOrderExecutionAsync(id.TradeOrder, id.ExecutionAttemptId, Arg.Any<CancellationToken>())
            .Returns(new OrderExecutionDefinition { Status = status });
        var expected = BrokerOrderEventProjector.CreateCancellationHandoff(id, fact.ObservationId, fact.OccurredAtUtc, recovery: true);
        Assert.NotEqual(expected.CommandId,
            BrokerOrderEventProjector.CreateCancellationHandoff(id, fact.ObservationId, fact.OccurredAtUtc, recovery: false).CommandId);
        context.ActorService.RequestAsync<CancelOrderExecutionCommand, OrderExecutionId>(expected, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(expected.CommandId));

        var command = new ReconcileBrokerOrderCommand { CommandId = Guid.NewGuid(), EntityId = order.Id };
        Assert.True((await command.ExecuteAsync(context, state)).Success);
        await context.ActorService.Received(shouldRepair ? 1 : 0)
            .RequestAsync<CancelOrderExecutionCommand, OrderExecutionId>(expected, Arg.Any<CancellationToken>());
        await broker.DidNotReceive().CancelAsync(Arg.Any<BrokerCancelRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Committed_placement_rejection_releases_unfilled_execution_with_or_without_legacy_submit(bool alreadySubmitted)
    {
        var order = Placement();
        var context = BrokerContext();
        var actors = context.ActorService;
        var projector = new BrokerOrderEventProjector(context);
        var execution = new OrderExecutionCommandState();
        var id = order.Id.Execution;
        var start = new StartOrderExecutionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, Order = order.Order, ExecutionAttemptId = id.ExecutionAttemptId,
            Channel = ExecutionChannel.Broker, EffectiveAtUtc = order.ChangedAtUtc,
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, StartOrderExecutionCommand.Verb, id.Format())
        };
        Assert.True(start.Execute(execution).Success);
        if (alreadySubmitted)
        {
            var submit = new SubmitOrderExecutionCommand
            {
                CommandId = Guid.NewGuid(), EntityId = id,
                Subject = start.Subject with { Verb = SubmitOrderExecutionCommand.Verb }
            };
            Assert.True(submit.Execute(execution).Success);
        }

        actors.SendAsync<RejectOrderExecutionCommand, OrderExecutionId>(Arg.Any<RejectOrderExecutionCommand>(), id)
            .Returns(call =>
            {
                var result = call.Arg<RejectOrderExecutionCommand>().Execute(execution);
                Assert.True(result.Success, result.ErrorMessage);
                return new ServiceOk<Guid>(result.Value!.Guid);
            });
        var receipt = Receipt(order, BrokerDispatchResult.RejectedLocally);
        var recorded = BrokerOrderComputation.RecordBrokerDispatch(receipt, order).BrokerOrderDefinition!;
        var change = Change(recorded);
        await projector.ProjectionDescriptors.Single().ApplyAsync(change, default!);
        await projector.ProjectionDescriptors.Single().ApplyAsync(change, default!);

        Assert.Equal(OrderExecutionStatus.Rejected, execution.OrderExecutionDefinition!.Status);
        Assert.Empty(execution.OrderExecutionDefinition.Fills);
        var terminal = Assert.Single(execution.Events.OfType<OrderExecutionChangedEvent>(),
            item => item.OrderExecutionDefinition.Status == OrderExecutionStatus.Rejected);
        var executionContext = Substitute.For<IOrderExecutionCommandContext>();
        executionContext.ActorService.Returns(actors);
        executionContext.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        executionContext.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());

        executionContext.Logger.Returns(NullLogger<OrderExecutionCommandActor>.Instance);
        var factory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        tradeDb.ProviderName.Returns("System.Data.Postgres");
        factory.TradeDb.Returns(tradeDb);
        executionContext.DbFactory.Returns(factory);
        actors.SendAsync<ReleaseTradeOrderExecutionCommand, TradeOrderId>(Arg.Any<ReleaseTradeOrderExecutionCommand>(), id.TradeOrder)
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        actors.SendAsync<ReleaseFundTradeSubmissionCommand, PortfolioFundId>(Arg.Any<ReleaseFundTradeSubmissionCommand>(), Arg.Any<PortfolioFundId>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        await new OrderExecutionEventProjector(executionContext).ProjectionDescriptors.Single().ApplyAsync(terminal, default!);

        await actors.Received(1).SendAsync<ReleaseTradeOrderExecutionCommand, TradeOrderId>(
            Arg.Is<ReleaseTradeOrderExecutionCommand>(command => command.ZeroExposureConfirmed && command.ExecutionAttemptId == id.ExecutionAttemptId), id.TradeOrder);
        await actors.Received(1).SendAsync<ReleaseFundTradeSubmissionCommand, PortfolioFundId>(
            Arg.Is<ReleaseFundTradeSubmissionCommand>(command => command.ExecutionEvidence.ExecutionAttemptId == id.ExecutionAttemptId),
            new PortfolioFundId(id.TradeOrder.PortfolioId, id.TradeOrder.FundId));
        Assert.Empty(context.TradeBroker.ReceivedCalls());
    }

    [Theory]
    [InlineData(BrokerDispatchResult.AcceptedForDispatch, BrokerOrderStatus.Dispatched)]
    [InlineData(BrokerDispatchResult.OutcomeUnknown, BrokerOrderStatus.OutcomeUnknown)]
    public async Task Placement_receipt_does_not_redispatch_submit_or_reject(BrokerDispatchResult outcome, BrokerOrderStatus status)
    {
        var order = Placement();
        var recorded = BrokerOrderComputation.RecordBrokerDispatch(Receipt(order, outcome), order).BrokerOrderDefinition!;
        Assert.Equal(status, recorded.Status);
        var context = BrokerContext();
        var projector = new BrokerOrderEventProjector(context);
        await projector.ProjectionDescriptors.Single().ApplyAsync(Change(recorded), default!);
        Assert.Empty(context.TradeBroker.ReceivedCalls());
        Assert.Empty(context.ActorService.ReceivedCalls());
    }

    [Theory]
    [InlineData(BrokerMutationKind.UpdateLimit, BrokerOrderStatus.UpdatePending)]
    [InlineData(BrokerMutationKind.Cancel, BrokerOrderStatus.CancelPending)]
    public async Task Timed_out_mutation_records_unknown_and_keeps_the_original_operation(BrokerMutationKind mutation, BrokerOrderStatus pending)
    {
        var order = Placement() with { PendingMutation = mutation, Status = pending, BrokerRevision = 1, CurrentSignedNetDebitLimit = 10m };
        var context = BrokerContext();
        context.TradeBroker.ModifyLimitAsync(Arg.Any<BrokerLimitUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<BrokerDispatchReceipt>(Task.FromException<BrokerDispatchReceipt>(new TimeoutException("no receipt"))));
        context.TradeBroker.CancelAsync(Arg.Any<BrokerCancelRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<BrokerDispatchReceipt>(Task.FromException<BrokerDispatchReceipt>(new TimeoutException("no receipt"))));
        RecordBrokerDispatchCommand? receipt = null;
        context.ActorService.SendAsync<RecordBrokerDispatchCommand, BrokerOrderId>(Arg.Any<RecordBrokerDispatchCommand>(), order.Id)
            .Returns(call =>
            {
                receipt = call.Arg<RecordBrokerDispatchCommand>();
                return new ServiceOk<Guid>(receipt.CommandId);
            });
        var projector = new BrokerOrderEventProjector(context).ProjectionDescriptors.Single();
        await projector.ApplyAsync(Change(order), default!);
        Assert.NotNull(receipt);
        Assert.Equal(order.OperationId, receipt.OperationId);
        Assert.Equal(BrokerDispatchResult.OutcomeUnknown, receipt.Outcome);
        var unknown = BrokerOrderComputation.RecordBrokerDispatch(receipt, order).BrokerOrderDefinition!;
        Assert.Equal(BrokerOrderStatus.OutcomeUnknown, unknown.Status);
        Assert.Equal(10m, unknown.CurrentSignedNetDebitLimit);
        var calls = context.TradeBroker.ReceivedCalls().Count();
        await projector.ApplyAsync(Change(unknown) with { IsDispatchReceipt = true }, default!);
        Assert.Equal(calls, context.TradeBroker.ReceivedCalls().Count());
    }

    [Theory]
    [InlineData(BrokerOrderObservationKind.Execution, BrokerOrderStatus.PartiallyFilled)]
    [InlineData(BrokerOrderObservationKind.Commission, BrokerOrderStatus.OutcomeUnknown)]
    [InlineData(BrokerOrderObservationKind.Cancelled, BrokerOrderStatus.Cancelled)]
    [InlineData(BrokerOrderObservationKind.Rejected, BrokerOrderStatus.Rejected)]
    [InlineData(BrokerOrderObservationKind.Acknowledged, BrokerOrderStatus.Working)]
    public void Authoritative_callbacks_resolve_or_add_evidence_to_an_unknown_dispatch(BrokerOrderObservationKind kind, BrokerOrderStatus status)
    {
        var order = Placement();
        var ambiguous = BrokerOrderComputation.RecordBrokerDispatch(Receipt(order, BrokerDispatchResult.OutcomeUnknown), order).BrokerOrderDefinition!;
        var observed = Observe(ambiguous, kind);
        Assert.True(observed.Accepted);
        Assert.Equal(status, observed.BrokerOrderDefinition!.Status);
        Assert.False(BrokerOrderEventProjector.IsDispatchIntent(observed.BrokerOrderDefinition));
    }

    [Theory]
    [InlineData(BrokerDispatchResult.AcceptedForDispatch)]
    [InlineData(BrokerDispatchResult.OutcomeUnknown)]
    public void Late_receipt_does_not_reopen_an_early_broker_rejection(BrokerDispatchResult outcome)
    {
        var order = Placement();
        var rejected = Observe(order, BrokerOrderObservationKind.Rejected).BrokerOrderDefinition!;
        Assert.Equal(BrokerOrderStatus.Rejected, rejected.Status);
        Assert.Same(rejected, BrokerOrderComputation.RecordBrokerDispatch(Receipt(order, outcome), rejected).BrokerOrderDefinition);
    }

    [Fact]
    public void Local_update_or_cancel_failure_does_not_reject_the_placement()
    {
        var order = Placement();
        foreach (var (status, kind) in new[] { (BrokerOrderStatus.UpdatePending, BrokerMutationKind.UpdateLimit), (BrokerOrderStatus.CancelPending, BrokerMutationKind.Cancel) })
        {
            var mutation = order with { Status = status, PendingMutation = kind, StatusBeforeMutation = BrokerOrderStatus.Working, PriorOperationId = Guid.NewGuid(), CurrentSignedNetDebitLimit = 10m, RequestedSignedNetDebitLimit = 9m };
            var recorded = BrokerOrderComputation.RecordBrokerDispatch(Receipt(mutation, BrokerDispatchResult.RejectedLocally), mutation).BrokerOrderDefinition!;
            Assert.Equal(BrokerOrderStatus.Working, recorded.Status);
            Assert.Equal(10m, recorded.CurrentSignedNetDebitLimit);
            Assert.Null(recorded.RequestedSignedNetDebitLimit);
            Assert.Null(BrokerOrderEventProjector.CreatePlacementRejection(recorded));
            Assert.False(BrokerOrderEventProjector.IsDispatchIntent(recorded));
        }
    }

    private static IBrokerOrderCommandContext BrokerContext()
    {
        var context = Substitute.For<IBrokerOrderCommandContext>();
        context.Logger.Returns(NullLogger<BrokerOrderCommandActor>.Instance);
        context.ReadStore.Returns(new BrokerOrderReadStore());
        context.TradeBroker.Returns(Substitute.For<ITradeBroker>());
        return context;
    }

    private static BrokerOrderDefinition Placement()
    {
        var id = new BrokerOrderId(new OrderExecutionId(new TradeOrderId(101, 701, 2001), Guid.NewGuid()), Guid.NewGuid());
        return new BrokerOrderDefinition
        {
            Id = id, OperationId = Guid.NewGuid(), PendingMutation = BrokerMutationKind.Place,
            Status = BrokerOrderStatus.PlacePending, ChangedAtUtc = DateTime.UtcNow,
            Order = new TradeOrderDefinition
            {
                Id = id.Execution.TradeOrder, Status = TradeOrderStatus.Executing, BrokerAccountAlias = "EMU",
                Components = [new() { ComponentId = id.ComponentId, ReservedTradeId = 1901 }],
                SetupTrade = new() { FundId = 701, OrderId = 1201, TradeId = 1801 }
            }
        };
    }

    private static BrokerOrderChangedEvent Change(BrokerOrderDefinition order) => new() { EntityId = order.Id, BrokerOrderDefinition = order };

    private static RecordBrokerDispatchCommand Receipt(BrokerOrderDefinition order, BrokerDispatchResult outcome) => new()
    {
        EntityId = order.Id, OperationId = order.OperationId, Outcome = outcome, Category = outcome.ToString(), RecordedAtUtc = order.ChangedAtUtc
    };

    private static BrokerOrderCompute Observe(BrokerOrderDefinition order, BrokerOrderObservationKind kind) =>
        BrokerOrderComputation.RecordBrokerOrderObservation(new RecordBrokerOrderObservationCommand
        {
            EntityId = order.Id,
            Observation = new BrokerOrderObservationEvidence
            {
                ObservationId = Guid.NewGuid(), Kind = kind, AccountAlias = order.Order.BrokerAccountAlias,
                OperationId = order.OperationId, ComponentId = order.Id.ComponentId
            }
        }, order, null);
}
