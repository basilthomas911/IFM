using System.Reflection;
using MessagePack;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Domain.Trade.Order.Command;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using App = TomasAI.IFM.Application.TradeBroker.Contracts;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

// Executes production command handlers, event projectors, typed API clients and emulator.
// Transport and database ports are in-memory; these tests do not connect to a running API.
public sealed class EmulatorActorWorkflowIntegrationTests
{
    [Theory]
    [InlineData(TradeStrategyKind.FuturesOutright, false)]
    [InlineData(TradeStrategyKind.VerticalSpread, false)]
    [InlineData(TradeStrategyKind.IronCondor, false)]
    [InlineData(TradeStrategyKind.FuturesOutright, true)]
    [InlineData(TradeStrategyKind.VerticalSpread, true)]
    [InlineData(TradeStrategyKind.IronCondor, true)]
    public async Task Place_update_then_cancel_or_fill_completes_actor_workflow(TradeStrategyKind strategy, bool fill)
    {
        var h = new Harness();
        var order = h.Order(strategy);
        var result = await new TradeOrderLifecycleApi(h.Producer).SubmitAcceptedAsync(order, Guid.NewGuid(), ExecutionChannel.Broker);
        Assert.True(result.Success, result.ErrorMessage);
        await h.DrainAsync();
        Assert.Equal(OrderExecutionStatus.Submitted, h.Execution.OrderExecutionDefinition!.Status);
        Assert.Equal(BrokerOrderStatus.Working, h.Broker.BrokerOrderDefinition!.Status);
        Assert.Equal("GTC", h.Broker.BrokerOrderDefinition.Order.TimeInForce);
        Assert.Equal("Urgent", h.Broker.BrokerOrderDefinition.Order.AlgorithmPace);
        Assert.Equal("GTC", h.Store.Load()!.Orders.Single().Request.TimeInForce);
        Assert.Equal("Urgent", h.Store.Load()!.Orders.Single().Request.AlgorithmPace);
        Assert.Equal(BrokerAlgorithm.Adaptive, h.Broker.BrokerOrderDefinition.Order.BrokerAlgorithm);
        var commands = new BrokerOrderCommandApi(h.Producer);
        var id = h.Broker.BrokerOrderDefinition.Id;
        var price = order.Components[0].SignedNetDebitLimit!.Value;
        var updated = await commands.UpdatePriceAsync(id, price - .25m, Guid.NewGuid());
        Assert.True(updated.Success, updated.ErrorMessage);
        await h.DrainAsync();
        Assert.Equal(price - .25m, h.Broker.BrokerOrderDefinition!.CurrentSignedNetDebitLimit);
        Assert.Equal(2, h.Broker.BrokerOrderDefinition.BrokerRevision);
        var rejected = await commands.UpdatePriceAsync(id, price + 1m, Guid.NewGuid());
        Assert.False(rejected.Success);
        Assert.Equal(price - .25m, h.Broker.BrokerOrderDefinition.CurrentSignedNetDebitLimit);
        if (!fill)
        {
            var cancelled = await commands.CancelAsync(id, Guid.NewGuid());
            Assert.True(cancelled.Success, cancelled.ErrorMessage);
            await h.DrainAsync();
            Assert.Equal(BrokerOrderStatus.Cancelled, h.Broker.BrokerOrderDefinition!.Status);
            Assert.Equal(OrderExecutionStatus.Cancelled, h.Execution.OrderExecutionDefinition!.Status);
            Assert.Equal(TradeOrderStatus.Ready, h.Trade.TradeOrderDefinition!.Status);
            Assert.Empty(h.Execution.OrderExecutionDefinition.Fills);
        }
        else
        {
            foreach (var leg in order.Components[0].Legs)
                await h.BrokerPort.PublishMarketQuoteAsync(new(leg.ContractId, 10m, 10.05m, 10, 10, DateTime.UtcNow, 1, 1));
            await h.DrainAsync();
            Assert.Equal(BrokerOrderStatus.Filled, h.Broker.BrokerOrderDefinition!.Status);
            Assert.Equal(OrderExecutionStatus.Filled, h.Execution.OrderExecutionDefinition!.Status);
            Assert.Equal(TradeOrderStatus.Completed, h.Trade.TradeOrderDefinition!.Status);
            Assert.Equal(order.Components[0].Legs.Length, h.Execution.OrderExecutionDefinition.Fills.Length);
            Assert.All(h.Execution.OrderExecutionDefinition.Fills, x => Assert.True(x.Commission > 0));
        }
        var account = await h.BrokerPort.GetAccountSnapshotAsync();
        Assert.Equal(account.CashBalance, account.AvailableFunds);
        if (!fill) Assert.Equal(1_000_000m, account.AvailableFunds);
    }

    [Fact]
    public void Legacy_order_definition_defaults_time_in_force_and_pace_after_wire_round_trip()
    {
        var order = new Harness().Order(TradeStrategyKind.FuturesOutright);
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(order));
        Assert.Equal(28, reader.ReadArrayHeader());
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(26);
        for (var i = 0; i < 26; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var legacy = MessagePackSerializer.Deserialize<TradeOrderDefinition>(buffer.WrittenMemory);
        Assert.Equal("Day", legacy.TimeInForce);
        Assert.Equal("Normal", legacy.AlgorithmPace);
    }

    private sealed class Harness
    {
        public IActorProducer Producer { get; } = Substitute.For<IActorProducer>();
        private readonly IActorService _actors = Substitute.For<IActorService>();
        private readonly Queue<object> _pending = new();
        public TradeOrderCommandState Trade { get; } = new();
        public OrderExecutionCommandState Execution { get; } = new();
        public BrokerOrderCommandState Broker { get; } = new();
        private readonly TestBrokerAccountReadStore _accounts = new();
        private readonly Guid _accountApproval = Guid.NewGuid();
        public InMemoryEmulatorLedgerStore Store { get; } = new();
        public InteractiveBrokersEmulatorTradeBroker BrokerPort { get; }
        private readonly TradeOrderEventProjector _tradeProjector;
        private readonly OrderExecutionEventProjector _executionProjector;
        private readonly BrokerOrderEventProjector _brokerProjector;
        private readonly HashSet<Guid> _seen = [];
        public Harness()
        {
            var ledger = new EmulatorLedger(EmulatorScenario.Development("UI-WORKFLOW"), new SystemEmulatorClock(), Store);
            BrokerPort = new(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
            _accounts.Set(new BrokerAccountDefinition
            {
                Id = new("UI-WORKFLOW"), Environment = App.BrokerEnvironment.Emulator,
                QualificationStatus = BrokerAccountQualificationStatus.Accepted,
                Gate = BrokerAccountOperationalGate.Open, ApprovalId = _accountApproval,
                Snapshot = new() { AccountAlias = "UI-WORKFLOW", Complete = true, NewRiskAllowed = true, Generation = 1, AsOfUtc = DateTime.UtcNow }
            });
            var tc = Substitute.For<ITradeOrderCommandContext>(); tc.ActorService.Returns(_actors); tc.Logger.Returns(NullLogger<TradeOrderCommandActor>.Instance);
            var ec = Substitute.For<IOrderExecutionCommandContext>(); ec.ActorService.Returns(_actors); ec.Logger.Returns(NullLogger<OrderExecutionCommandActor>.Instance);
            ec.PortfolioAccounting.PostConfirmedExecutionAsync(Arg.Any<OrderExecutionDefinition>(), Arg.Any<Guid>(), Arg.Any<DateTime>())
                .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
            var bc = Substitute.For<IBrokerOrderCommandContext>(); bc.ActorService.Returns(_actors); bc.TradeBroker.Returns(BrokerPort);
            bc.ReadStore.Returns(new BrokerOrderReadStore()); bc.Logger.Returns(NullLogger<BrokerOrderCommandActor>.Instance);
            _tradeProjector = new(tc); _executionProjector = new(ec); _brokerProjector = new(bc);
            Client<CreateTradeOrderCommand, TradeOrderId>(); Client<ApproveTradeOrderCommand, TradeOrderId>();
            Client<ReadyTradeOrderCommand, TradeOrderId>(); Client<BindTradeOrderExecutionCommand, TradeOrderId>();
            Client<RequestBrokerOrderLimitUpdateCommand, BrokerOrderId>(); Client<RequestBrokerOrderCancelCommand, BrokerOrderId>();
            Route<StartOrderExecutionCommand, OrderExecutionId>(); Route<SubmitOrderExecutionCommand, OrderExecutionId>();
            Route<CreateBrokerOrderCommand, BrokerOrderId>(); Route<RecordBrokerDispatchCommand, BrokerOrderId>();
            Route<AddOrderExecutionFillCommand, OrderExecutionId>(); Route<UpdateOrderExecutionFillCostCommand, OrderExecutionId>();
            Route<AcceptOrderExecutionCommand, OrderExecutionId>(); Route<CancelOrderExecutionCommand, OrderExecutionId>();
            Route<RejectOrderExecutionCommand, OrderExecutionId>(); Route<ReleaseTradeOrderExecutionCommand, TradeOrderId>();
            Route<CompleteTradeOrderCommand, TradeOrderId>();
            // Established-trade/position storage is outside this broker-cycle integration test.
            _actors.SendAsync<TomasAI.IFM.Domain.Trade.Shared.Futures.CreateFuturesTradeCommand, TradeEntityId>(Arg.Any<TomasAI.IFM.Domain.Trade.Shared.Futures.CreateFuturesTradeCommand>(), Arg.Any<TradeEntityId>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
            _actors.SendAsync<TomasAI.IFM.Domain.Trade.Shared.Futures.Option.CreateOptionTradeCommand, TradeEntityId>(Arg.Any<TomasAI.IFM.Domain.Trade.Shared.Futures.Option.CreateOptionTradeCommand>(), Arg.Any<TradeEntityId>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        }
        private void Client<T, TId>() where T : class, ICommand<TId> where TId : IActorEntityId
        {
            Producer.RequestAsync<T, TId, GuidResult>(Arg.Any<ActorSubject>(), Arg.Any<T>(), Arg.Any<TId>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<ServiceResult<GuidResult>>(Apply(MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(call.Arg<T>())))));
        }
        private void Route<T, TId>() where T : class, ICommand<TId> where TId : IActorEntityId
        {
            _actors.RequestAsync<T, TId>(Arg.Any<T>()).Returns(call =>
            {
                var result = Apply(MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(call.Arg<T>())));
                return new ValueTask<ServiceResult<Guid>>(result.Success
                    ? new ServiceOk<Guid>(call.Arg<T>().CommandId)
                    : new ServiceFailed<Guid>(result.ErrorCode, result.ErrorMessage));
            });
            _actors.SendAsync<T, TId>(Arg.Any<T>(), Arg.Any<TId>()).Returns(call =>
            {
                _pending.Enqueue(MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(call.Arg<T>())));
                return new ValueTask<ServiceResult<Guid>>(new ServiceOk<Guid>(call.Arg<T>().CommandId));
            });
        }
        private ServiceResult<GuidResult> Apply(object command)
        {
            var tradeCount = Trade.Events.Count; var executionCount = Execution.Events.Count; var brokerCount = Broker.Events.Count;
            var result = command switch
            {
                CreateTradeOrderCommand c => c.Execute(Trade), ApproveTradeOrderCommand c => c.Execute(Trade),
                ReadyTradeOrderCommand c => c.Execute(Trade), BindTradeOrderExecutionCommand c => c.Execute(Trade),
                CompleteTradeOrderCommand c => c.Execute(Trade), ReleaseTradeOrderExecutionCommand c => c.Execute(Trade),
                StartOrderExecutionCommand c => c.Execute(Execution), SubmitOrderExecutionCommand c => c.Execute(Execution),
                AddOrderExecutionFillCommand c => c.Execute(Execution), UpdateOrderExecutionFillCostCommand c => c.Execute(Execution),
                AcceptOrderExecutionCommand c => c.Execute(Execution), CancelOrderExecutionCommand c => c.Execute(Execution),
                RejectOrderExecutionCommand c => c.Execute(Execution), CreateBrokerOrderCommand c => c.Execute(Broker, _accounts),
                RecordBrokerDispatchCommand c => c.Execute(Broker), RecordBrokerOrderObservationCommand c => c.Execute(Broker),
                RequestBrokerOrderLimitUpdateCommand c => c.Execute(Broker), RequestBrokerOrderCancelCommand c => c.Execute(Broker),
                _ => throw new InvalidOperationException(command.GetType().Name)
            };
            foreach (var e in Trade.Events.Skip(tradeCount).Concat(Execution.Events.Skip(executionCount)).Concat(Broker.Events.Skip(brokerCount))) _pending.Enqueue(e);
            return result;
        }
        public async Task DrainAsync()
        {
            for (var iteration = 0; iteration < 200; iteration++)
            {
                while (_pending.TryDequeue(out var item))
                {
                    if (item is IEvent e)
                    {
                        var roundtrip = (IEvent)MessagePackSerializer.Deserialize(e.GetType(), MessagePackSerializer.Serialize(e.GetType(), e));
                        Assert.False(string.IsNullOrWhiteSpace(roundtrip.Subject.Name));
                        Assert.False(string.IsNullOrWhiteSpace(roundtrip.Subject.Verb));
                        Assert.Equal(e.Subject, roundtrip.Subject);
                        object projector = e switch { TradeOrderChangedEvent => _tradeProjector, OrderExecutionChangedEvent => _executionProjector, BrokerOrderChangedEvent => _brokerProjector, _ => throw new InvalidOperationException() };
                        var method = projector.GetType().GetMethod("ProjectAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
                        await (Task)method.Invoke(projector, [e])!;
                    }
                    else { var result = Apply(item); Assert.True(result.Success, result.ErrorMessage); }
                }
                if (Broker.BrokerOrderDefinition is null) return;
                var observations = await BrokerPort.ReconcileOrderAsync(Broker.BrokerOrderDefinition.Id.Format());
                var count = 0;
                foreach (var o in observations.Where(x => x.Kind is not (App.BrokerObservationKind.AccountSnapshot or App.BrokerObservationKind.GateChanged or App.BrokerObservationKind.ConnectionChanged) && _seen.Add(x.ObservationId)))
                {
                    count++;
                    var evidence = new BrokerOrderObservationEvidence
                    {
                        ObservationId = o.ObservationId, Kind = o.Kind switch
                        { App.BrokerObservationKind.OrderCompleted => BrokerOrderObservationKind.OrderCompleted, App.BrokerObservationKind.Execution => BrokerOrderObservationKind.Execution, App.BrokerObservationKind.Commission => BrokerOrderObservationKind.Commission, App.BrokerObservationKind.Cancelled => BrokerOrderObservationKind.Cancelled, App.BrokerObservationKind.Rejected => BrokerOrderObservationKind.Rejected, _ => BrokerOrderObservationKind.Acknowledged },
                        AccountAlias = o.AccountAlias, OperationId = o.OperationId, ComponentId = o.ComponentId, LegId = o.LegId,
                        ContractId = o.ContractId, ExternalExecutionId = o.ExternalExecutionId, SignedQuantity = o.SignedQuantity,
                        Price = o.Price, Commission = o.Commission, OrderRevision = o.OrderRevision, SourceEpoch = o.SourceEpoch,
                        SourceSequence = o.SourceSequence, OccurredAtUtc = o.OccurredAtUtc, ContentHash = o.ObservationId.ToString("N")
                    };
                    var result = Apply(new RecordBrokerOrderObservationCommand { CommandId = Guid.NewGuid(), EntityId = Broker.BrokerOrderDefinition.Id, Observation = evidence, Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RecordBrokerOrderObservationCommand.Verb, Broker.BrokerOrderDefinition.Id.Format()) });
                    Assert.True(result.Success, result.ErrorMessage);
                }
                if (count == 0) return;
            }
            throw new InvalidOperationException("Workflow did not settle.");
        }
        public TradeOrderDefinition Order(TradeStrategyKind strategy)
        {
            var count = strategy == TradeStrategyKind.IronCondor ? 4 : strategy == TradeStrategyKind.VerticalSpread ? 2 : 1;
            var component = new TradeOrderComponentDefinition
            {
                ComponentId = Guid.NewGuid(), ReservedTradeId = 1, StrategyKind = strategy,
                SignedNetDebitLimit = strategy == TradeStrategyKind.FuturesOutright ? 11m : 1m,
                MinimumSignedNetDebitLimit = strategy == TradeStrategyKind.FuturesOutright ? 10m : 0m,
                MaximumSignedNetDebitLimit = strategy == TradeStrategyKind.FuturesOutright ? 11m : 1m,
                TickIncrement = .05m, PermitBalancedPartialAcceptance = true,
                Legs = Enumerable.Range(0, count).Select(i => new TradeLegDefinition
                { TradeLegId = Guid.NewGuid(), ContractId = $"LEG-{i}", ContractKey = $"LEG-{i}", CashMultiplier = 1m,
                    SignedQuantity = i % 2 == 0 ? 1 : -1, AssetFamily = count == 1 ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
                    Strike = count == 1 ? null : 100m + i * 5m, Expiry = count == 1 ? null : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), PutCall = 1 }).ToArray()
            };
            return new TradeOrderDefinition
            {
                Id = new(1, 1, 1), Revision = 1, Status = TradeOrderStatus.Approved, PositionType = TradeOrderPositionType.Opening,
                ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), ValidUntilUtc = DateTime.UtcNow.AddMinutes(5), Components = [component],
                BrokerAccountAlias = "UI-WORKFLOW", BrokerEnvironment = BrokerEnvironment.Emulator, PortfolioApprovalId = Guid.NewGuid(),
                AccountPromotionApprovalReference = _accountApproval.ToString("N"), DefinitionHash = "approval", MicroExecutionProfileHash = "profile",
                RequiredCapital = 1000m, MaximumLoss = 1000m, BrokerAlgorithm = BrokerAlgorithm.Adaptive,
                TimeInForce = "GTC", AlgorithmPace = "Urgent"
            };
        }
    }
}
