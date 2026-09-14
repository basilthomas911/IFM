using System;
using System.Collections.Generic;
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
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class TradeFlowStorageFixture
{
    public TradeFlowStorageFixture()
    {
        var settings = new DbConnectionSettings().Add(
            TradeDbContext.TradeDbConnection,
            "Contact Points=localhost;Port=9042;Default Keyspace=trade_test_db",
            "System.Data.ScyllaDb");
        var contexts = new Dictionary<Type, TradeDbContext>();
        var resolver = new DbContextResolver(type => contexts[type]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        var factory = new DbContextFactory(resolver);

        new TradeSchemaDb(settings, logger).CreateAllAsync().GetAwaiter().GetResult();
        contexts.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(
            settings,
            factory,
            Substitute.For<ISequenceIdGenerator>(),
            logger));
        TradeDb = (TradeDbContext)factory.TradeDb;
    }

    public TradeDbContext TradeDb { get; }
}

public sealed class TradeFlowStorageTests(TradeFlowStorageFixture fixture)
    : IClassFixture<TradeFlowStorageFixture>
{
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
            ContractKey = $"ES-{suffix}"
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
            OpeningValue = 5_000m,
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
                OpeningPrice = 5_000m,
                CurrentPrice = 5_000m,
                LastSourceSequence = 1,
                LastPriceAtUtc = now
            }],
            MarketValue = 5_000m,
            AsOfUtc = now,
            IsOpen = true
        };

        await fixture.TradeDb.UpsertTradeOrderAsync(order);
        await fixture.TradeDb.UpsertOrderExecutionAsync(execution);
        await fixture.TradeDb.UpsertEstablishedTradeAsync(trade);
        await fixture.TradeDb.UpsertStrategyPositionAsync(openPosition);
        await fixture.TradeDb.ReplaceOpenPositionRoutesAsync(openPosition);

        (await fixture.TradeDb.GetTradeOrderAsync(orderId)).Should().BeEquivalentTo(order);
        (await fixture.TradeDb.GetOrderExecutionAsync(orderId, executionAttemptId)).Should().BeEquivalentTo(execution);
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
        (await fixture.TradeDb.GetStrategyPositionHistoryAsync(positionId.PositionId,
            now.AddMinutes(-1), now.AddMinutes(1), 10)).Items.Should().HaveCount(2);
        (await fixture.TradeDb.GetOpenPositionRoutesAsync(replacementLeg.ContractId)).Should().BeEmpty();
        (await fixture.TradeDb.GetOpenPositionRouteSnapshotAsync())
            .Should().NotContain(entry => entry.Route.StrategyPositionId == positionId.PositionId);
    }
}
