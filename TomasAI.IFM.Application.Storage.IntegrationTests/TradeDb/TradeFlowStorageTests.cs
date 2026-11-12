using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class TradeFlowStorageFixture : IDisposable
{
    readonly TomasAI.IFM.IntegrationTesting.IsolatedIntegrationInfrastructure infrastructure = new("storage-trade-flow", includeCql: false);
    public TradeFlowStorageFixture()
    {
        infrastructure.StartAsync().GetAwaiter().GetResult();
        var settings = new DbConnectionSettings().Add(
            TradeDbContext.TradeDbConnection,
            infrastructure.PostgresConnectionString,
            "System.Data.Postgres");
        var contexts = new Dictionary<Type, TradeDbContext>();
        var resolver = new DbContextResolver(type => contexts[type]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        var factory = new DbContextFactory(resolver);

        var schema = new TradeSchemaDb(settings, logger);
        schema.CreateAllAsync().GetAwaiter().GetResult();
        contexts.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(
            settings,
            factory,
            Substitute.For<ISequenceIdGenerator>(),
            logger));
        TradeDb = (TradeDbContext)factory.TradeDb;
    }

    public TradeDbContext TradeDb { get; }
    public void Dispose() => infrastructure.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

public sealed class TradeFlowStorageTests(TradeFlowStorageFixture fixture)
    : IClassFixture<TradeFlowStorageFixture>
{
    [Fact]
    public async Task Fund_order_fills_page_includes_unfilled_orders_and_four_distinct_fills_after_replay()
    {
        const int portfolio = 910001;
        const int fund = 710001;
        var date = new DateOnly(2026, 11, 12);
        var now = new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);
        for (var id = 1; id <= 10; id++)
        {
            var projected = Order(new TradeOrderId(portfolio, fund, id), date);
            await fixture.TradeDb.UpsertTradeOrderAsync(projected, now);
        }
        var order = Order(new TradeOrderId(portfolio, fund, 8), date);
        await fixture.TradeDb.UpsertTradeOrderAsync(order with { Revision = 2, DefinitionHash = "revision-2" }, now.AddMinutes(1));
        await fixture.TradeDb.UpsertTradeOrderAsync(Order(new TradeOrderId(portfolio, fund + 1, 11), date), now);
        await fixture.TradeDb.UpsertTradeOrderAsync(Order(new TradeOrderId(portfolio, fund, 12), date.AddDays(-1)), now);
        await fixture.TradeDb.UpsertTradeOrderAsync(Order(new TradeOrderId(portfolio + 1, fund, 13), date), now);

        var attempt = Guid.NewGuid();
        var fillEvidence = new[] { "ES-C7900", "ES-C7950", "ES-P7175", "ES-P7125" }.Select((contract, index) =>
            new ExecutionFillEvidence
            {
                ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt, ComponentId = Guid.NewGuid(),
                TradeLegId = Guid.NewGuid(), ContractId = contract, SignedQuantity = index % 2 == 0 ? -1 : 1,
                Price = index + 0.25m, FilledAtUtc = now, ExternalExecutionId = contract
            }).ToArray();
        var execution = new OrderExecutionDefinition
        {
            TradeOrderId = order.Id, ExecutionAttemptId = attempt, OrderRevision = 2, Channel = ExecutionChannel.Manual,
            Status = OrderExecutionStatus.Filled, StartedAtUtc = now, CompletedAtUtc = now, Fills = fillEvidence,
            OrderQuantity = 1, CumulativeFilledQuantity = 1,
            Order = order with { ExecutionBoundAtUtc = now.AddSeconds(-30) }
        };
        await fixture.TradeDb.UpsertOrderExecutionAsync(execution);
        await fixture.TradeDb.UpsertOrderExecutionAsync(execution);
        await fixture.TradeDb.UpsertOrderExecutionAsync(execution with
        {
            TradeOrderId = new(portfolio, fund, 9), ExecutionAttemptId = Guid.NewGuid(), OrderRevision = 1,
            Status = OrderExecutionStatus.Rejected, Fills = [], CumulativeFilledQuantity = 0,
            RejectionDetail = "EM.EXPIRED: Approval expired."
        });

        var first = await fixture.TradeDb.GetFundOrderFillsAsync(portfolio, fund, date, 0, Guid.Empty);
        first.Select(row => row.OrderId).Should().Equal(Enumerable.Range(1, 8));
        first[0].Fills.Should().BeEmpty();
        first[^1].TradeId.Should().Be(4301);
        first[^1].Status.Should().Be("Filled");
        first[^1].Fills.Should().BeEquivalentTo(fillEvidence);
        first[^1].OrderQuantity.Should().Be(1);
        first[^1].FilledQuantity.Should().Be(1);
        first[^1].OrderDateTime.Should().Be(now.AddSeconds(-30));
        first[0].OrderDateTime.Should().Be(now);
        first[^1].LastFillDateTime.Should().Be(now);
        first[0].LastFillDateTime.Should().BeNull();
        var second = await fixture.TradeDb.GetFundOrderFillsAsync(portfolio, fund, date, first[^1].OrderId, first[^1].ExecutionAttemptId);
        second.Select(row => row.OrderId).Should().Equal(9, 10);
        second[0].Status.Should().Be("Rejected");
        second[0].RejectionDetail.Should().Be("EM.EXPIRED: Approval expired.");
        second[0].FilledQuantity.Should().Be(0);
        second[0].LastFillDateTime.Should().BeNull();

        static TradeOrderDefinition Order(TradeOrderId id, DateOnly valueDate) => new()
        {
            Id = id, Revision = 1, ValueDate = valueDate, DefinitionHash = "revision-1",
            SetupTrade = new() { FundId = id.FundId, OrderId = 4001, TradeId = 4301 }
        };
    }

    [Fact]
    public async Task Trade_flow_projections_round_trip_and_closed_position_removes_realtime_routes()
    {
        var suffix = Random.Shared.Next(10_000, 900_000);
        var orderId = new TradeOrderId(suffix, suffix + 1, suffix + 2);
        var tradeId = new TradeEntityId(
            orderId.PortfolioId,
            orderId.FundId,
            orderId.OrderId,
            suffix + 3);
        var componentId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var executionAttemptId = Guid.NewGuid();
        var positionId = new StrategyPositionId(tradeId, Guid.NewGuid());
        var now = DateTime.UtcNow;
        var leg = new TradeLegDefinition
        {
            TradeLegId = legId,
            ContractId = $"ES-{suffix}",
            AssetFamily = TradeAssetFamily.Futures,
            SignedQuantity = 1,
            ContractKey = $"ES-{suffix}",
            CashMultiplier = 50m
        };
        var component = new TradeOrderComponentDefinition
        {
            ComponentId = componentId,
            ReservedTradeId = tradeId.TradeId,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            Legs = [leg]
        };
        var order = new TradeOrderDefinition
        {
            PositionType = TradeOrderPositionType.Opening,
            Id = orderId,
            Revision = 1,
            Status = TradeOrderStatus.Completed,
            ValueDate = DateOnly.FromDateTime(now),
            ValidUntilUtc = now.AddMinutes(5),
            Origin = nameof(TradeFlowStorageTests),
            DefinitionHash = Guid.NewGuid().ToString("N"),
            Components = [component]
        };
        var fill = new ExecutionFillEvidence
        {
            ExecutionFillId = Guid.NewGuid(),
            ExecutionAttemptId = executionAttemptId,
            ComponentId = componentId,
            TradeLegId = legId,
            ContractId = leg.ContractId,
            SignedQuantity = 1,
            Price = 5_000m,
            Commission = 1.25m,
            FilledAtUtc = now,
            ExternalExecutionId = $"manual-{suffix}"
        };
        var execution = new OrderExecutionDefinition
        {
            PositionType = TradeOrderPositionType.Opening,
            TradeOrderId = orderId,
            ExecutionAttemptId = executionAttemptId,
            Channel = ExecutionChannel.Manual,
            Status = OrderExecutionStatus.Filled,
            OrderRevision = 1,
            Components = [component],
            Fills = [fill],
            StartedAtUtc = now.AddSeconds(-1),
            CompletedAtUtc = now
        };
        var trade = new EstablishedTradeDefinition
        {
            Id = tradeId,
            AssetFamily = TradeAssetFamily.Futures,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            SourceComponentId = componentId,
            ExecutionAttemptId = executionAttemptId,
            Status = EstablishedTradeStatus.Open,
            Legs = [leg],
            OriginalFills = [fill],
            OpeningValue = 250_000m,
            OpeningCommission = 1.25m,
            EstablishedAtUtc = now,
            EvidenceRevision = 1
        };
        var openPosition = new StrategyPositionSnapshot
        {
            Id = positionId,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            Phase = StrategyPositionPhase.Open,
            PositionSequence = 1,
            RouteGeneration = 1,
            Legs = [new StrategyPositionLeg
            {
                TradeLegId = legId,
                ContractId = leg.ContractId,
                SignedQuantity = 1,
                CashMultiplier = 50m,
                OpeningPrice = 5_000m,
                CurrentPrice = 5_000m,
                LastSourceSequence = 1,
                LastPriceAtUtc = now
            }],
            MarketValue = 250_000m,
            ValueDate = trade.TradeDate!.Value,
            OpeningValueDate = trade.TradeDate,
            OpeningCommission = 1.25m,
            ContractCashMultiplier = 50m,
            DailyPnl = -1.25m,
            AsOfUtc = now,
            IsOpen = true
        };

        await fixture.TradeDb.UpsertTradeOrderAsync(order, now);
        await fixture.TradeDb.UpsertOrderExecutionAsync(execution);
        await fixture.TradeDb.UpsertEstablishedTradeAsync(trade);
        await fixture.TradeDb.UpsertStrategyPositionAsync(openPosition);
        await fixture.TradeDb.ReplaceOpenPositionRoutesAsync(openPosition);

        (await fixture.TradeDb.GetTradeOrderAsync(orderId)).Should().BeEquivalentTo(order);
        (await fixture.TradeDb.GetOrderExecutionAsync(orderId, executionAttemptId)).Should().BeEquivalentTo(execution);
        var legacyExecution = (await fixture.TradeDb.GetFundOrderFillsAsync(orderId.PortfolioId, orderId.FundId, order.ValueDate, 0, Guid.Empty)).Single();
        legacyExecution.OrderQuantity.Should().Be(1);
        legacyExecution.FilledQuantity.Should().Be(1);
        legacyExecution.Components.Should().BeEquivalentTo(order.Components);
        (await fixture.TradeDb.GetEstablishedTradeAsync(tradeId)).Should().BeEquivalentTo(trade);
        (await fixture.TradeDb.GetEstablishedTradesAsync(tradeId.PortfolioId, tradeId.FundId,
            TradeStrategyKind.FuturesOutright, now.AddMinutes(-1), now.AddMinutes(1), 10))
            .Items.Should().ContainEquivalentOf(trade);
        (await fixture.TradeDb.GetStrategyPositionAsync(positionId)).Should().BeEquivalentTo(openPosition);
        (await fixture.TradeDb.GetStrategyPositionHistoryAsync(positionId.PositionId,
            now.AddMinutes(-1), now.AddMinutes(1), 10)).Items.Should().ContainEquivalentOf(openPosition);

        var routes = await fixture.TradeDb.GetOpenPositionRoutesAsync(leg.ContractId);
        routes.Should().ContainSingle(entry => entry.Route.StrategyPositionId == positionId.PositionId);
        (await fixture.TradeDb.GetOpenPositionRouteSnapshotAsync())
            .Should().Contain(entry => entry.Route.StrategyPositionId == positionId.PositionId);

        var replacementLeg = openPosition.Legs[0] with
        {
            TradeLegId = Guid.NewGuid(),
            ContractId = $"NQ-{suffix}"
        };
        var amendedPosition = openPosition with
        {
            PositionSequence = 2,
            RouteGeneration = 2,
            Legs = [replacementLeg],
            AsOfUtc = now.AddMilliseconds(500)
        };
        await fixture.TradeDb.ReplaceOpenPositionRoutesAsync(amendedPosition);

        (await fixture.TradeDb.GetOpenPositionRoutesAsync(leg.ContractId)).Should().BeEmpty();
        (await fixture.TradeDb.GetOpenPositionRoutesAsync(replacementLeg.ContractId))
            .Should().ContainSingle(entry => entry.Route.StrategyPositionId == positionId.PositionId
                && entry.Route.TradeLegId == replacementLeg.TradeLegId);

        var closedPosition = amendedPosition with
        {
            Phase = StrategyPositionPhase.Close,
            PositionSequence = 3,
            RouteGeneration = 3,
            AsOfUtc = now.AddSeconds(1),
            IsOpen = false
        };
        await fixture.TradeDb.UpsertStrategyPositionAsync(closedPosition);
        await fixture.TradeDb.ReplaceOpenPositionRoutesAsync(closedPosition);

        (await fixture.TradeDb.GetStrategyPositionAsync(positionId)).Should().BeEquivalentTo(closedPosition);
        var history = await fixture.TradeDb.GetStrategyPositionHistoryAsync(positionId.PositionId,
            now.AddMinutes(-1), now.AddMinutes(1), 10);
        history.Items.Select(row => row.PositionSequence).Should().Equal(1L, 2L, 3L);
        history.Items.Should().ContainEquivalentOf(amendedPosition);
        history.Items.Should().ContainEquivalentOf(closedPosition);
        await fixture.TradeDb.UpsertStrategyPositionAsync(closedPosition);
        (await fixture.TradeDb.GetStrategyPositionHistoryAsync(positionId.PositionId,
            now.AddMinutes(-1), now.AddMinutes(1), 10)).Items.Should().HaveCount(3,
                "replaying the closed projection must not create another history row");
        (await fixture.TradeDb.GetOpenPositionRoutesAsync(replacementLeg.ContractId)).Should().BeEmpty();
        (await fixture.TradeDb.GetOpenPositionRouteSnapshotAsync())
            .Should().NotContain(entry => entry.Route.StrategyPositionId == positionId.PositionId);
    }
}
