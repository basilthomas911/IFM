using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Plan;

/// <summary>Exercises the current strategy-specific Trade Plan Function and Query routes.</summary>
public sealed class StrategyTradePlanApiTests(WebApplicationFactory<Program> sourceFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Futures_plan_update_replays_and_is_visible_through_current_and_history_queries()
    {
        await using var host = sourceFactory.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.Trade")
            .UseSetting("IFM_TEST_NATS_URL", Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")
                ?? "nats://127.0.0.1:14222"));
        _ = host.CreateClient();
        var producer = host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new ActorMailboxId(ActorType.Function, $"CurrentTradePlan{Guid.NewGuid():N}"));
        try
        {
            var now = DateTime.UtcNow;
            var positionId = new StrategyPositionId(new TradeEntityId(101, 102, 103, 104), Guid.NewGuid());
            var position = new StrategyPositionSnapshot
            {
                Id = positionId, StrategyKind = TradeStrategyKind.FuturesOutright,
                Phase = StrategyPositionPhase.MarkToMarket, PositionSequence = 1, RouteGeneration = 1,
                Legs = [new StrategyPositionLeg
                {
                    TradeLegId = Guid.NewGuid(), ContractId = "ESZ6", AssetFamily = TradeAssetFamily.Futures,
                    SignedQuantity = 1, OpeningPrice = 5_000m, CurrentPrice = 5_001m,
                    LastSourceSequence = 1, LastPriceAtUtc = now
                }],
                MarketValue = 5_001m, UnrealizedPnl = 1m, AsOfUtc = now, IsOpen = true
            };
            var valueDate = DateOnly.FromDateTime(now);
            var id = new FuturesTradePlanId(positionId, valueDate);
            var command = new UpdateFuturesTradePlanCommand
            {
                CommandId = Guid.NewGuid(),
                Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor,
                    UpdateFuturesTradePlanCommand.Verb, id.Format()),
                EntityId = id, Position = position, Parameters = new TradePlanParameters(),
                SourceEventId = Guid.NewGuid(), RequestedAtUtc = now
            };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var first = await producer.RequestFunctionAsync<UpdateFuturesTradePlanCommand,
                FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent,
                    TradePlanFailedEvent<FuturesTradePlanId>>>(command.Subject, command, id, deadline.Token);
            first.Success.Should().BeTrue(first.ErrorMessage);
            first.Value!.IsCompleted.Should().BeTrue(first.Value.Failed?.ErrorMessage);
            var replay = await producer.RequestFunctionAsync<UpdateFuturesTradePlanCommand,
                FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent,
                    TradePlanFailedEvent<FuturesTradePlanId>>>(command.Subject, command, id, deadline.Token);
            replay.Success.Should().BeTrue(replay.ErrorMessage);
            replay.Value!.Completed!.Id.Should().Be(first.Value.Completed!.Id);

            var queries = new StrategyTradePlanQueryApi(producer);
            var current = await queries.GetCurrentFuturesAsync(positionId, valueDate, deadline.Token);
            current.Success.Should().BeTrue(current.ErrorMessage);
            current.Value!.Position.Id.Should().Be(positionId);
            var history = await queries.GetFuturesHistoryAsync(positionId, valueDate, cancellationToken: deadline.Token);
            history.Success.Should().BeTrue(history.ErrorMessage);
            history.Value!.Items.Should().ContainSingle();
        }
        finally { await producer.StopAsync(); }
    }
}
