using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

/// <summary>Runs the real history writer only against an explicitly selected disposable keyspace.</summary>
public sealed class PositionLifecycleStorageFactAttribute : FactAttribute
{
    /// <summary>Skips storage qualification when its dedicated connection has not been supplied.</summary>
    public PositionLifecycleStorageFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_TEST_POSITION_LIFECYCLE_CONNECTION")))
            Skip = "Provide IFM_TEST_POSITION_LIFECYCLE_CONNECTION for the disposable position_lifecycle_verification keyspace.";
    }
}

/// <summary>Characterizes the implemented lifecycle and its gaps against the requested daily position-history contract.</summary>
/// <param name="output">Records synthetic fixture results; no live broker or financial account is used.</param>
public sealed class IronCondorPositionLifecycleAuditTests(ITestOutputHelper output)
{
    static readonly DateTime OpenedAt = new(2026, 10, 7, 14, 0, 0, DateTimeKind.Utc);

    /// <summary>Exercises execution-created Open, multiple MTMs, EOD, next-session MTM and fill-backed Closed through real Scylla readback.</summary>
    [PositionLifecycleStorageFact]
    public async Task Concrete_commands_persist_open_each_mark_eod_and_fill_backed_close_as_separate_history_rows()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_TEST_POSITION_LIFECYCLE_CONNECTION")!;
        if (!connection.Contains("Default Keyspace=position_lifecycle_verification", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This audit requires the isolated position_lifecycle_verification keyspace.");
        var settings = new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, connection, "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new TradeSchemaDb(settings, logger).CreateAsync(["strategy_position_current", "strategy_position_history"]);
        var factory = Substitute.For<IDbContextFactory>();
        var database = new TradeDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), logger);
        factory.TradeDb.Returns(database);
        var trade = CreateExecutedTrade();
        var state = OpenPosition(trade);
        var snapshots = new List<StrategyPositionSnapshot> { state.PositionSnapshot! };
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        var leg = trade.Legs[0];
        ApplyMark(state, leg, 11, 1, OpenedAt.AddMinutes(1));
        snapshots.Add(state.PositionSnapshot!);
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        ApplyMark(state, leg, 12, 2, OpenedAt.AddMinutes(2));
        snapshots.Add(state.PositionSnapshot!);
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        new EndOfDayIronCondorPositionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = state.PositionSnapshot!.Id,
            Subject = PositionSubject(EndOfDayIronCondorPositionCommand.Verb, state.PositionSnapshot.Id),
            EffectiveAtUtc = OpenedAt.Date.AddHours(21)
        }.Execute(state).Success.Should().BeTrue();
        snapshots.Add(state.PositionSnapshot!);
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        ApplyMark(state, leg, 13, 3, OpenedAt.AddDays(1));
        snapshots.Add(state.PositionSnapshot!);
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        var close = ExecuteOppositeOrder(trade, state.PositionSnapshot!.Id, OpenedAt.AddDays(1).AddMinutes(1), closing: true);
        close.CreatedTrades.Should().BeEmpty("explicit closing execution must retain the original trade identity");
        var acceptedClose = close.ClosedPositions.Should().ContainSingle().Subject;
        acceptedClose.PositionId.Should().Be(state.PositionSnapshot.Id);
        var tradeState = new FuturesOptionTradeCommandState();
        new CreateOptionTradeCommand { CommandId = Guid.NewGuid(), EntityId = trade.Id, Trade = trade,
            Subject = OptionSubject(CreateOptionTradeCommand.Verb, trade.Id) }.Execute(tradeState).Success.Should().BeTrue();
        new BeginCloseOptionTradeCommand { CommandId = Guid.NewGuid(), EntityId = trade.Id,
            Subject = OptionSubject(BeginCloseOptionTradeCommand.Verb, trade.Id) }.Execute(tradeState).Success.Should().BeTrue();
        new CloseOptionTradeCommand { CommandId = Guid.NewGuid(), EntityId = trade.Id,
            Subject = OptionSubject(CloseOptionTradeCommand.Verb, trade.Id), ClosingFills = acceptedClose.Fills,
            ClosedAtUtc = acceptedClose.CompletedAtUtc }.Execute(tradeState).Success.Should().BeTrue();
        tradeState.Current!.Status.Should().Be(EstablishedTradeStatus.Closed);
        new CloseIronCondorPositionCommand { CommandId = Guid.NewGuid(), EntityId = acceptedClose.PositionId,
            Subject = PositionSubject(CloseIronCondorPositionCommand.Verb, acceptedClose.PositionId),
            ClosingFills = acceptedClose.Fills, EffectiveAtUtc = acceptedClose.CompletedAtUtc
        }.Execute(state).Success.Should().BeTrue();
        snapshots.Add(state.PositionSnapshot!);
        await database.UpsertStrategyPositionAsync(state.PositionSnapshot!);
        var current = await database.GetStrategyPositionAsync(state.PositionSnapshot.Id);
        current.Should().BeEquivalentTo(state.PositionSnapshot);
        current!.Phase.Should().Be(StrategyPositionPhase.Close);
        current.IsOpen.Should().BeFalse();
        current.Legs.Should().OnlyContain(value => value.SignedQuantity == 0);
        var history = (await database.GetStrategyPositionHistoryAsync(current.Id.PositionId,
            OpenedAt.AddMinutes(-1), acceptedClose.CompletedAtUtc.AddMinutes(1), 20)).Items.OrderBy(value => value.PositionSequence).ToArray();
        history.Should().BeEquivalentTo(new[] { snapshots[0], snapshots[3], snapshots[4], snapshots[5] }, options => options.WithStrictOrdering());
        history.Select(value => value.Phase).Should().Equal(StrategyPositionPhase.Open,
            StrategyPositionPhase.EndOfDay, StrategyPositionPhase.MarkToMarket, StrategyPositionPhase.Close);
        history.Count(value => value.Phase == StrategyPositionPhase.MarkToMarket && value.AsOfUtc.Date == OpenedAt.Date)
            .Should().Be(0, "the daily MTM row is finalized in place as EOD");
        var evidence = new { Test = nameof(Concrete_commands_persist_open_each_mark_eod_and_fill_backed_close_as_separate_history_rows),
            SyntheticFixture = true, PositionId = current.Id, FinalTradeStatus = tradeState.Current.Status,
            History = history.Select(value => new { value.PositionSequence, Phase = value.Phase.ToString(), value.AsOfUtc,
                value.IsOpen, value.UnrealizedPnl, value.RealizedPnl }),
            RequestedDailyMtmRowBehaviorImplemented = true };
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("IFM_POSITION_LIFECYCLE_EVIDENCE") is { Length: > 0 } evidencePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidencePath))!);
            await File.WriteAllTextAsync(evidencePath, json);
        }
    }

    /// <summary>Verifies that EOD seals its value date against a later market mark.</summary>
    [Fact]
    public void Same_session_tick_cannot_replace_finalized_eod()
    {
        var trade = CreateExecutedTrade();
        var state = OpenPosition(trade);
        var id = state.PositionSnapshot!.Id;
        new EndOfDayIronCondorPositionCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = PositionSubject(EndOfDayIronCondorPositionCommand.Verb, id), EffectiveAtUtc = OpenedAt.AddHours(1)
        }.Execute(state).Success.Should().BeTrue();
        state.PositionSnapshot.Phase.Should().Be(StrategyPositionPhase.EndOfDay);
        var finalized = state.PositionSnapshot;
        var leg = trade.Legs[0];
        new ChangeTradeLegDataCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = PositionSubject(ChangeTradeLegDataCommand.Verb, id), TradeLegId = leg.TradeLegId,
            ContractId = leg.ContractId, TradeType = TradeStrategyKind.IronCondor, Price = 11, SourceSequence = 1,
            RouteGeneration = finalized.RouteGeneration, EffectiveAtUtc = OpenedAt.AddHours(1).AddSeconds(1)
        }.Execute(state).Success.Should().BeFalse();
        state.PositionSnapshot.Should().BeSameAs(finalized);
    }

    /// <summary>Shows the current PnL remains measured from opening fills, rather than resetting against the prior EOD.</summary>
    [Fact]
    public void Next_session_pnl_is_cumulative_from_opening_basis()
    {
        var trade = CreateExecutedTrade();
        var state = OpenPosition(trade);
        ApplyMark(state, trade.Legs[0], 11, 1, OpenedAt.AddMinutes(1));
        new EndOfDayIronCondorPositionCommand { CommandId = Guid.NewGuid(), EntityId = state.PositionSnapshot!.Id,
            Subject = PositionSubject(EndOfDayIronCondorPositionCommand.Verb, state.PositionSnapshot.Id),
            EffectiveAtUtc = OpenedAt.Date.AddHours(21) }.Execute(state).Success.Should().BeTrue();
        var previousEodPnl = state.PositionSnapshot.UnrealizedPnl;
        ApplyMark(state, trade.Legs[0], 13, 2, OpenedAt.AddDays(1));
        previousEodPnl.Should().Be(1);
        state.PositionSnapshot!.UnrealizedPnl.Should().Be(3);
        (state.PositionSnapshot.UnrealizedPnl - previousEodPnl).Should().Be(2);
        state.PositionSnapshot.DailyPnl.Should().Be(2);
    }

    /// <summary>Proves reversing quantities without an explicit closing target creates a second established trade.</summary>
    [Fact]
    public void Opposite_opening_order_creates_another_trade_instead_of_closing_the_original()
    {
        var trade = CreateExecutedTrade();
        var position = OpenPosition(trade).PositionSnapshot!;
        var result = ExecuteOppositeOrder(trade, position.Id, OpenedAt.AddMinutes(1), closing: false);
        result.ClosedPositions.Should().BeEmpty();
        var opposite = result.CreatedTrades.Should().ContainSingle().Subject;
        opposite.Id.Should().NotBe(trade.Id);
        opposite.Status.Should().Be(EstablishedTradeStatus.Open);
        position.IsOpen.Should().BeTrue();
        output.WriteLine("Gap: opposite legs are not sufficient; the order must specify Closing and TargetPositionId.");
    }

    /// <summary>Documents the retained compatibility path that allows position closure without accepted fills.</summary>
    [Fact]
    public void Legacy_time_only_close_marks_position_closed_without_execution_fills()
    {
        var state = OpenPosition(CreateExecutedTrade());
        var id = state.PositionSnapshot!.Id;
        new CloseIronCondorPositionCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = PositionSubject(CloseIronCondorPositionCommand.Verb, id), EffectiveAtUtc = OpenedAt.AddMinutes(1)
        }.Execute(state).Success.Should().BeTrue();
        state.PositionSnapshot!.Phase.Should().Be(StrategyPositionPhase.Close);
        state.PositionSnapshot.IsOpen.Should().BeFalse();
        state.PositionSnapshot.ClosingFills.Should().BeEmpty();
        output.WriteLine("Gap: the legacy time-only command can close a position without broker fill evidence.");
    }

    /// <summary>Builds one synthetic Iron Condor from the actual execution command handlers.</summary>
    static EstablishedTradeDefinition CreateExecutedTrade()
    {
        var legs = Enumerable.Range(0, 4).Select(index => new TradeLegDefinition
        {
            TradeLegId = Guid.NewGuid(), ContractId = $"AUDIT-ES-OPTION-{index}", ContractKey = $"AUDIT-ES-OPTION-{index}",
            AssetFamily = TradeAssetFamily.FuturesOption, SignedQuantity = index is 0 or 3 ? 1 : -1,
            PutCall = (byte)(index < 2 ? 1 : 2), Strike = new[] { 8450m, 8400m, 7700m, 7650m }[index],
            Expiry = new DateOnly(2026, 11, 20), CashMultiplier = 50
        }).ToArray();
        var order = new TradeOrderDefinition { Id = new(9000001, 9000002, Random.Shared.Next(9000003, 10000000)),
            Revision = 1, Status = TradeOrderStatus.Executing, PositionType = TradeOrderPositionType.Opening,
            ValueDate = DateOnly.FromDateTime(OpenedAt), ValidUntilUtc = OpenedAt.AddDays(3), Origin = "PositionLifecycleAudit",
            Components = [new() { ComponentId = Guid.NewGuid(), ReservedTradeId = 9000004,
                StrategyKind = TradeStrategyKind.IronCondor, Legs = legs }] };
        return ExecuteOrder(order, OpenedAt).CreatedTrades.Should().ContainSingle().Subject;
    }

    /// <summary>Submits opposite synthetic fills using either an explicit close intent or a second opening intent.</summary>
    static OrderExecutionChangedEvent ExecuteOppositeOrder(EstablishedTradeDefinition trade, StrategyPositionId target,
        DateTime filledAtUtc, bool closing)
    {
        var order = new TradeOrderDefinition { Id = new(trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId + 1),
            Revision = 1, Status = TradeOrderStatus.Executing,
            PositionType = closing ? TradeOrderPositionType.Closing : TradeOrderPositionType.Opening,
            TargetPositionId = closing ? target : null, ValueDate = DateOnly.FromDateTime(filledAtUtc),
            ValidUntilUtc = filledAtUtc.AddHours(1), Origin = "PositionLifecycleAudit",
            Components = [new() { ComponentId = Guid.NewGuid(), ReservedTradeId = trade.Id.TradeId + 1,
                StrategyKind = TradeStrategyKind.IronCondor, Legs = trade.Legs.Select(leg => leg with { SignedQuantity = -leg.SignedQuantity }).ToArray() }] };
        return ExecuteOrder(order, filledAtUtc);
    }

    /// <summary>Runs Start, Submit, four AddFill commands and Accept without contacting a broker or accounting service.</summary>
    static OrderExecutionChangedEvent ExecuteOrder(TradeOrderDefinition order, DateTime filledAtUtc)
    {
        var attempt = Guid.NewGuid();
        var id = new OrderExecutionId(order.Id, attempt);
        var state = new OrderExecutionCommandState();
        var subject = new ActorSubject(ActorType.Command, OrderExecutionActorNames.Command, StartOrderExecutionCommand.Verb, id.Format());
        new StartOrderExecutionCommand { CommandId = Guid.NewGuid(), EntityId = id, Subject = subject,
            Order = order, ExecutionAttemptId = attempt, Channel = ExecutionChannel.Broker, EffectiveAtUtc = filledAtUtc
        }.Execute(state).Success.Should().BeTrue();
        new SubmitOrderExecutionCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = subject with { Verb = SubmitOrderExecutionCommand.Verb } }.Execute(state).Success.Should().BeTrue();
        var component = order.Components.Single();
        foreach (var (leg, index) in component.Legs.Select((leg, index) => (leg, index)))
            new AddOrderExecutionFillCommand { CommandId = Guid.NewGuid(), EntityId = id,
                Subject = subject with { Verb = AddOrderExecutionFillCommand.Verb }, Fill = new()
                {
                    ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt, ComponentId = component.ComponentId,
                    TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                    Price = 10m * (index + 1), Commission = .25m, FilledAtUtc = filledAtUtc,
                    ExternalExecutionId = $"audit-{attempt:N}-{index}"
                } }.Execute(state).Success.Should().BeTrue();
        new AcceptOrderExecutionCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = subject with { Verb = AcceptOrderExecutionCommand.Verb }, EffectiveAtUtc = filledAtUtc
        }.Execute(state).Success.Should().BeTrue();
        state.OrderExecutionDefinition!.Status.Should().Be(OrderExecutionStatus.Filled);
        return state.Events.OfType<OrderExecutionChangedEvent>().Last();
    }

    /// <summary>Applies the concrete opening command to command-owned event-sourced state.</summary>
    static IronCondorPositionCommandState OpenPosition(EstablishedTradeDefinition trade)
    {
        var state = new IronCondorPositionCommandState();
        var id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        new OpenIronCondorPositionCommand { CommandId = Guid.NewGuid(), EntityId = id, Trade = trade,
            Subject = PositionSubject(OpenIronCondorPositionCommand.Verb, id), EffectiveAtUtc = trade.EstablishedAtUtc
        }.Execute(state).Success.Should().BeTrue();
        state.PositionSnapshot!.Phase.Should().Be(StrategyPositionPhase.Open);
        return state;
    }

    /// <summary>Applies a genuine-shaped market observation through ChangeTradeLegData's computation/event/state path.</summary>
    static void ApplyMark(IronCondorPositionCommandState state, TradeLegDefinition leg, decimal price, long sequence, DateTime observedAtUtc)
    {
        var id = state.PositionSnapshot!.Id;
        new ChangeTradeLegDataCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = PositionSubject(ChangeTradeLegDataCommand.Verb, id), TradeLegId = leg.TradeLegId,
            ContractId = leg.ContractId, TradeType = TradeStrategyKind.IronCondor, Price = price, SourceSequence = sequence,
            RouteGeneration = state.PositionSnapshot.RouteGeneration, EffectiveAtUtc = observedAtUtc
        }.Execute(state).Success.Should().BeTrue();
    }

    /// <summary>Constructs the concrete strategy-position actor route.</summary>
    static ActorSubject PositionSubject(string verb, StrategyPositionId id) => new(ActorType.Command, PositionActorNames.IronCondorCommand, verb, id.Format());
    /// <summary>Constructs the established option-trade actor route.</summary>
    static ActorSubject OptionSubject(string verb, TradeEntityId id) => new(ActorType.Command, FuturesOptionTradeActorNames.Command, verb, id.Format());
}
