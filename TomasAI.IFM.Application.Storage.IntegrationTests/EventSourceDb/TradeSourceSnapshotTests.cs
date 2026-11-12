using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class TradeSourceSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Order_Execution_and_Vertical_Spread_restore_the_latest_complete_source_snapshots()
    {
        const int head = 129;
        var source = fixture.ActorEventDb;
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        var actors = Substitute.For<IActorService>();
        var order = new TradeOrderId(11, 12, Random.Shared.Next(1_000, int.MaxValue));
        var execution = new OrderExecutionId(order, Guid.NewGuid());
        var execute = new StartOrderExecutionCommand { CommandId = Guid.NewGuid(), EntityId = execution,
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, StartOrderExecutionCommand.Verb, execution.Format()) };
        factory.CreateState<OrderExecutionCommandState>().Returns(_ => new OrderExecutionCommandState());
        var executionRepository = new OrderExecutionStateRepository(factory, source, actors,
            Substitute.For<IEventProjector<OrderExecutionCommandActor>>(), NullLogger<OrderExecutionStateRepository>.Instance);
        var executionFacts = Enumerable.Range(1, head).Select(sequence => new OrderExecutionChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = execute.CommandId, EntityId = execution,
            OrderExecutionDefinition = new() { TradeOrderId = order, ExecutionAttemptId = execution.ExecutionAttemptId,
                OrderRevision = 1, OrderQuantity = head, CumulativeFilledQuantity = sequence }
        });
        await source.SaveEventsAsync(execute.StreamId, execute.CommandId, new DomainEventCollection(executionFacts),
            0, CancellationToken.None);
        var loadedExecution = await executionRepository.LoadStateAsync(execute);
        Assert.Equal(execution, loadedExecution.OrderExecutionDefinition!.Id);
        Assert.Equal(head, loadedExecution.OrderExecutionDefinition.CumulativeFilledQuantity);
        Assert.Equal(head, loadedExecution.CommittedStreamVersion);
        Assert.Empty(loadedExecution.Events);

        var position = StrategyPositionId.Create(new(order.PortfolioId, order.FundId, order.OrderId, 14), TradeStrategyKind.VerticalSpread);
        var open = new OpenVerticalSpreadPositionCommand { CommandId = Guid.NewGuid(), EntityId = position,
            Subject = new(ActorType.Command, FuturesVerticalSpreadTradePositionCommandActor.ActorName, OpenVerticalSpreadPositionCommand.Verb, position.Format()) };
        factory.CreateState<VerticalSpreadPositionCommandState>().Returns(_ => new VerticalSpreadPositionCommandState());
        var positionRepository = new VerticalSpreadPositionStateRepository(factory, source, actors,
            Substitute.For<IEventProjector<FuturesVerticalSpreadTradePositionCommandActor>>(),
            NullLogger<VerticalSpreadPositionStateRepository>.Instance);
        var positionFacts = Enumerable.Range(1, head).Select(sequence => new VerticalSpreadPositionChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = open.CommandId, EntityId = position,
            PositionSnapshot = new() { Id = position, StrategyKind = TradeStrategyKind.VerticalSpread,
                PositionSequence = sequence, RouteGeneration = 2, DailyPnlPoints = sequence }
        });
        await source.SaveEventsAsync(open.StreamId, open.CommandId, new DomainEventCollection(positionFacts),
            0, CancellationToken.None);
        var loadedPosition = await positionRepository.LoadStateAsync(open);
        Assert.Equal(position, loadedPosition.Current!.Id);
        Assert.Equal(head, loadedPosition.Current.PositionSequence);
        Assert.Equal((decimal)head, loadedPosition.Current.DailyPnlPoints);
        Assert.Equal(head, loadedPosition.CommittedStreamVersion);
        Assert.Empty(loadedPosition.Events);
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("Futures")]
    [InlineData("Options")]
    public async Task Complete_replacement_rejects_a_wrong_owner_at_the_committed_head(string owner)
    {
        var source = fixture.ActorEventDb;
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        var actors = Substitute.For<IActorService>();
        var orderId = new TradeOrderId(2, 3, Random.Shared.Next(1_000, int.MaxValue));
        var tradeId = new TradeEntityId(orderId.PortfolioId, orderId.FundId, orderId.OrderId, 7);
        var wrongTradeId = tradeId with { TradeId = 8 };
        if (owner == "Order")
        {
            var command = new CreateTradeOrderCommand { CommandId = Guid.NewGuid(), EntityId = orderId,
                Subject = new(ActorType.Command, TradeOrderCommandActor.ActorName, CreateTradeOrderCommand.Verb, orderId.Format()) };
            var valid = new TradeOrderChangedEvent { Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = orderId,
                TradeOrderDefinition = new() { Id = orderId, Revision = 2, Components = [new()] } };
            factory.CreateState<TradeOrderCommandState>().Returns(_ => new TradeOrderCommandState());
            var repository = new TradeOrderStateRepository(factory, source, actors, Substitute.For<IEventProjector<TradeOrderCommandActor>>(),
                NullLogger<TradeOrderStateRepository>.Instance);
            await Verify(command, valid, valid with { TradeOrderDefinition = new() },
                valid with { TradeOrderDefinition = valid.TradeOrderDefinition with { Id = orderId with { OrderId = 9 } } },
                async () => (await repository.LoadStateAsync(command)).CommittedStreamVersion);
            return;
        }
        var trade = new EstablishedTradeDefinition
        {
            Id = tradeId, EvidenceRevision = 2, Legs = [new() { TradeLegId = Guid.NewGuid() }],
            AssetFamily = owner == "Futures" ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption
        };
        if (owner == "Futures")
        {
            var command = new CreateFuturesTradeCommand { CommandId = Guid.NewGuid(), EntityId = tradeId,
                Subject = new(ActorType.Command, FuturesTradeActorNames.Command, CreateFuturesTradeCommand.Verb, tradeId.Format()) };
            var valid = new FuturesTradeChangedEvent { Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = tradeId,
                EstablishedTradeDefinition = trade };
            factory.CreateState<FuturesTradeCommandState>().Returns(_ => new FuturesTradeCommandState());
            var repository = new FuturesTradeStateRepository(factory, source, actors,
                Substitute.For<IEventProjector<FuturesTradeCommandActor>>(), NullLogger<FuturesTradeStateRepository>.Instance);
            await Verify(command, valid, valid with { EstablishedTradeDefinition = new() },
                valid with { EstablishedTradeDefinition = trade with { Id = wrongTradeId } },
                async () => (await repository.LoadStateAsync(command)).CommittedStreamVersion);
        }
        else
        {
            var command = new CreateOptionTradeCommand { CommandId = Guid.NewGuid(), EntityId = tradeId,
                Subject = new(ActorType.Command, FuturesOptionTradeActorNames.Command, CreateOptionTradeCommand.Verb, tradeId.Format()) };
            var valid = new OptionTradeChangedEvent { Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = tradeId,
                EstablishedTradeDefinition = trade };
            factory.CreateState<FuturesOptionTradeCommandState>().Returns(_ => new FuturesOptionTradeCommandState());
            var repository = new FuturesOptionTradeStateRepository(factory, source, actors,
                Substitute.For<IEventProjector<FuturesOptionTradeCommandActor>>(), NullLogger<FuturesOptionTradeStateRepository>.Instance);
            await Verify(command, valid, valid with { EstablishedTradeDefinition = new() },
                valid with { EstablishedTradeDefinition = trade with { Id = wrongTradeId } },
                async () => (await repository.LoadStateAsync(command)).CommittedStreamVersion);
        }

        async Task Verify(ICommand command, IEvent latest, IEvent incomplete, IEvent foreign, Func<Task<long>> load)
        {
            await source.SaveEventsAsync(command.StreamId, command.CommandId, new DomainEventCollection([incomplete, latest]),
                0, CancellationToken.None);
            Assert.Equal(2, await load());
            await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([foreign]),
                2, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await load());
        }
    }

    [Fact]
    public async Task Iron_Condor_restores_source_only_and_explicitly_reconciles_legacy_evidence_once()
    {
        var source = fixture.ActorEventDb;
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, source.ConnectionString, "System.Data.Postgres")
            .Add(TradeDbContext.TradeDbConnection, source.ConnectionString, "System.Data.Postgres");
        var transactions = new PostgresEventTransaction(settings);
        await transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], token));
        var databases = Substitute.For<IDbContextFactory>();
        var tradeDb = new TradeDbContext(settings, databases, Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(),
            NullLogger<DbProvider>.Instance);
        databases.TradeDb.Returns(tradeDb);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<IronCondorPositionCommandState>().Returns(_ => new IronCondorPositionCommandState());
        var repository = new IronCondorPositionStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(),
            NullLogger<IronCondorPositionStateRepository>.Instance, transactions, databases);
        var tradeId = new TradeEntityId(71, 72, 73, Random.Shared.Next(1_000, int.MaxValue));
        var id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor);
        var now = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);
        var legs = Enumerable.Range(1, 4).Select(number => new TradeLegDefinition
        {
            TradeLegId = Guid.NewGuid(), ContractId = $"ES-legacy-{number}", CashMultiplier = 50,
            AssetFamily = TradeAssetFamily.FuturesOption, SignedQuantity = number % 2 == 0 ? 1 : -1
        }).ToArray();
        var trade = new EstablishedTradeDefinition
        {
            Id = tradeId, AssetFamily = TradeAssetFamily.FuturesOption, StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = now, EvidenceRevision = 1, OpeningCommission = 4m, Legs = legs
        };
        var command = new ReconcileIronCondorPositionCurrencyEvidenceCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ExpectedStreamVersion = 131,
            Subject = new(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName,
                ReconcileIronCondorPositionCurrencyEvidenceCommand.Verb, id.Format())
        };
        var position = new StrategyPositionSnapshot
        {
            Id = id, StrategyKind = TradeStrategyKind.IronCondor, IsOpen = true, RouteGeneration = 1,
            AsOfUtc = now, LastMarketObservationUtc = now, HistoryAsOfUtc = now, HistoryPositionSequence = 1,
            ValueDate = trade.TradeDate!.Value, DailyPnlPoints = 2m, Phase = StrategyPositionPhase.MarkToMarket,
            Legs = legs.Select(leg => new StrategyPositionLeg
            {
                TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, AssetFamily = leg.AssetFamily,
                SignedQuantity = leg.SignedQuantity
            }).ToArray()
        };
        var first = new IronCondorPositionChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, ReceivedOn = now,
            PositionSnapshot = position with { PositionSequence = 1 }
        };
        await source.SaveEventsAsync(command.StreamId, first.CommandId, new DomainEventCollection([first,
            new IronCondorMonitoringInitializedEvent
            {
                Id = Guid.NewGuid(), CommandId = first.CommandId, EntityId = id, ReceivedOn = now,
                TradeLimits = new() { TradeId = tradeId.TradeId, MaxLossLimit = 125m }
            }, .. Enumerable.Range(2, 129).Select(sequence => first with
            {
                Id = Guid.NewGuid(), PositionSnapshot = position with { PositionSequence = sequence }
            })]), 0, CancellationToken.None);

        // A conflicting projection is available before the owning source fact exists.
        await transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await tradeDb.ProjectTradeAsync(db, trade with { OpeningCommission = 999m }, token);
            return true;
        });
        var legacy = await repository.LoadStateAsync(command);
        Assert.Null(legacy.Current!.OpeningCommission);
        Assert.Equal(125m, legacy.TradeLimits!.MaxLossLimit);
        Assert.Equal(131, legacy.CommittedStreamVersion);
        Assert.Equal(IronCondorCurrencyEvidenceReconciliation.Unavailable, await repository.ReconcileLegacyCurrencyEvidenceAsync(command));
        Assert.Equal(131, (await repository.LoadStateAsync(command)).CommittedStreamVersion);

        await CompareTimingsAsync("Iron Condor prior projection lookup / source-only", async () =>
        {
            var before = (await repository.LoadStateAsync(command)).Current!;
            var projected = await tradeDb.GetEstablishedTradeAsync(before.Id.Trade);
            _ = StrategyPositionCurrencyPnl.Apply(before, Assert.IsType<EstablishedTradeDefinition>(projected));
        }, async () => { _ = await repository.LoadStateAsync(command); });

        var optionTradeStream = new ActorSubject(ActorType.Command, FuturesOptionTradeActorNames.Command, string.Empty, tradeId.Format()).StreamId;
        var evidence = new OptionTradeChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = tradeId, ReceivedOn = now,
            EstablishedTradeDefinition = trade, IsInitialEstablishment = true
        };
        var incompatible = evidence with
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
            EstablishedTradeDefinition = trade with { Legs = [legs[0] with { ContractId = "different-contract" }, .. legs.Skip(1)] }
        };
        await source.SaveEventsAsync(optionTradeStream, incompatible.CommandId, new DomainEventCollection([incompatible]),
            0, CancellationToken.None);
        Assert.Equal(IronCondorCurrencyEvidenceReconciliation.Unavailable, await repository.ReconcileLegacyCurrencyEvidenceAsync(command));
        Assert.Equal(131, (await repository.LoadStateAsync(command)).CommittedStreamVersion);
        var committed = await source.SaveEventsAsync(optionTradeStream, evidence.CommandId, new DomainEventCollection([evidence]),
            1, CancellationToken.None);
        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.ReconcileLegacyCurrencyEvidenceAsync(command with { ExpectedStreamVersion = 130 }));
        Assert.Equal(IronCondorCurrencyEvidenceReconciliation.Reconciled, await repository.ReconcileLegacyCurrencyEvidenceAsync(command));
        Assert.Equal(IronCondorCurrencyEvidenceReconciliation.AlreadyPresent, await repository.ReconcileLegacyCurrencyEvidenceAsync(command));

        var restored = await repository.LoadStateAsync(command);
        Assert.Equal(132, restored.CommittedStreamVersion);
        Assert.Equal(131, restored.Current!.PositionSequence);
        Assert.Equal(1, restored.Current.HistoryPositionSequence);
        Assert.Equal(now, restored.Current.LastMarketObservationUtc);
        Assert.Equal(4m, restored.Current.OpeningCommission);
        Assert.Equal(50m, restored.Current.ContractCashMultiplier);
        Assert.Equal(96m, restored.Current.DailyPnl);
        Assert.Equal(125m, restored.TradeLimits!.MaxLossLimit);
        Assert.Equal(96m, (await tradeDb.GetStrategyPositionAsync(id))!.DailyPnl);
        var streamId = await source.GetEventStreamIdAsync(command.StreamId);
        var last = (await source.LoadActorEventStreamAsync<IronCondorPositionCommandState>(streamId)).MaxBy(row => row.StreamVersion);
        Assert.Equal(committed.Single().EventId, Assert.IsType<IronCondorPositionChangedEvent>(last!.ToDomainEvent()).CurrencyEvidenceEventId);
        Assert.Equal(1L, await transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(
            "SELECT count(*) FROM command_log WHERE CommandId=$1;", [command.CommandId], token)));
    }

    [Fact]
    public async Task Intrinsic_Time_checkpoint_reconstructs_legacy_prefix_and_rejects_unknown_tail()
    {
        var source = fixture.ActorEventDb;
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<IntrinsicTimeStrategyWorkflowCommandState>().Returns(_ => new IntrinsicTimeStrategyWorkflowCommandState());
        var repository = new IntrinsicTimeStrategyWorkflowStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>(),
            NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance);
        var now = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new("snapshot-" + Guid.NewGuid().ToString("N"),
            DateOnly.FromDateTime(now), TimeFrameType.Daily));
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(), EntityId = entity, ProposedWorkflowId = workflow,
            Subject = new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entity.Format())
        };
        var prefix = new IntrinsicTimeStrategyWorkflowStartedEvent { Id = Guid.NewGuid(), CommandId = command.CommandId };
        var committed = await source.SaveEventsAsync(command.StreamId, command.CommandId,
            new DomainEventCollection([prefix, .. Enumerable.Range(1, 129).Select(Fact)]), 0, CancellationToken.None);
        await Assert.ThrowsAsync<LegacyWorkflowStreamException>(async () => await repository.LoadStateAsync(command));
        var checkpoint = new IntrinsicTimeStrategyWorkflowStateSnapshottedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), Latest = committed.OfType<WorkflowStrategyStateUpdatedEvent>().Last(),
            AppliedEventCount = 129, LastStartDecision = StrategyWorkflowStartDecision.Accepted, ReceivedOn = now
        };
        await source.SaveEventsAsync(command.StreamId, checkpoint.CommandId, new DomainEventCollection([checkpoint, Fact(130)]),
            130, CancellationToken.None);
        var restored = await repository.LoadStateAsync(command);
        Assert.Equal(132, restored.PersistedStreamVersion);
        Assert.Equal(132, restored.CommittedStreamVersion);
        Assert.Equal(130, restored.AppliedEntityEventCount);
        Assert.Equal(StrategyWorkflowStartDecision.Accepted, restored.LastStartDecision);
        Assert.Equal(130, restored.CurrentView!.WorkflowRevision);
        Assert.Equal(workflow, restored.CurrentView.WorkflowId);
        Assert.False(restored.Updated);
        Assert.Empty(restored.Events);

        var streamId = await source.GetEventStreamIdAsync(command.StreamId);
        var versions = new List<long>();
        await source.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<IntrinsicTimeStrategyWorkflowCommandState>(streamId,
            [typeof(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent)], page => versions.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 131, 132 }, versions);
        await CompareTimingsAsync("Intrinsic Time verified source replay / checkpoint (legacy prefix already verified)", async () =>
        {
            var baseline = new IntrinsicTimeStrategyWorkflowCommandState();
            var stream = await source.GetEventStreamIdFromDbAsync(command.StreamId);
            await source.MapReduceActorEventStreamFromCreationAsync<IntrinsicTimeStrategyWorkflowCommandState>(stream!.EventStreamId,
                page => baseline.ReplayEvents(page.Select(row => row.ToDomainEvent()).OfType<WorkflowStrategyStateUpdatedEvent>()));
            Assert.Equal(130, baseline.AppliedEntityEventCount);
        }, async () => { _ = await repository.LoadStateAsync(command); });

        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([prefix with { Id = Guid.NewGuid() }]),
            132, CancellationToken.None);
        await Assert.ThrowsAsync<LegacyWorkflowStreamException>(async () => await repository.LoadStateAsync(command));

        WorkflowStrategyStateUpdatedEvent Fact(int revision) => new()
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity, WorkflowId = workflow, WorkflowRevision = revision,
            ReceivedOn = now, UpdatedAtUtc = now, State = new()
            {
                EntityId = entity, WorkflowId = workflow, WorkflowRevision = revision, Status = WorkflowStrategyMachineStatus.Started,
                CurrentStage = StrategyWorkflowStage.RegimeDiscovery, StartedAtUtc = now, UpdatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(2)
            }
        };
    }

    private async Task CompareTimingsAsync(string label, Func<Task> before, Func<Task> after)
    {
        var baseline = new double[20];
        var candidate = new double[20];
        for (var sample = 0; sample < baseline.Length; sample++)
        {
            if (sample % 2 == 0)
            {
                baseline[sample] = await TimeAsync(before);
                candidate[sample] = await TimeAsync(after);
            }
            else
            {
                candidate[sample] = await TimeAsync(after);
                baseline[sample] = await TimeAsync(before);
            }
        }
        Array.Sort(baseline);
        Array.Sort(candidate);
        output.WriteLine($"{label}: median {(baseline[9] + baseline[10]) / 2:F2} -> {(candidate[9] + candidate[10]) / 2:F2} ms, " +
            $"p95 {baseline[18]:F2} -> {candidate[18]:F2} ms, stream resolution included.");

        static async Task<double> TimeAsync(Func<Task> action)
        {
            var started = Stopwatch.GetTimestamp();
            await action();
            return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }
}
