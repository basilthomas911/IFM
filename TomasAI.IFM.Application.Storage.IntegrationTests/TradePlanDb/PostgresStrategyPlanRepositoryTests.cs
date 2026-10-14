using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresStrategyPlanRepositoryTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(TradeStrategyKind.FuturesOutright, false, false)]
    [InlineData(TradeStrategyKind.FuturesOutright, false, true)]
    [InlineData(TradeStrategyKind.FuturesOutright, true, false)]
    [InlineData(TradeStrategyKind.VerticalSpread, false, false)]
    [InlineData(TradeStrategyKind.VerticalSpread, false, true)]
    [InlineData(TradeStrategyKind.VerticalSpread, true, false)]
    public async Task Repository_commits_source_and_plan_together_or_rolls_back_both(TradeStrategyKind kind, bool failAfterPlanWrite, bool failPublication)
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(10_000_000, 15_000_000), 1, -42m, true);
        plan = plan with { Position = plan.Position with { StrategyKind = kind } };
        plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
        var source = Substitute.For<IEventSourceActorDbContext>();
        source.ProviderName.Returns("System.Data.Postgres");
        source.ConnectionString.Returns(fixture.Connection);
        var factory = Substitute.For<IDbContextFactory>();
        ITradePlanDbContext target = fixture.Plans;
        if (failAfterPlanWrite)
        {
            target = Substitute.For<ITradePlanDbContext>();
            target.ProviderName.Returns("System.Data.Postgres");
            target.ConnectionString.Returns(fixture.Connection);
            target.ProjectMaterialAsync(Arg.Any<IEnlistedPostgresTransaction>(), Arg.Any<StrategyTradePlanSnapshot>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    await fixture.Plans.ProjectMaterialAsync(call.Arg<IEnlistedPostgresTransaction>(), call.Arg<StrategyTradePlanSnapshot>(), call.Arg<CancellationToken>());
                    throw new InvalidOperationException("Injected failure after required plan rows were written.");
                });
        }
        factory.TradePlanDb.Returns(target);
        var commandId = Guid.NewGuid();
        Func<Task> save;
        string stream;
        if (kind == TradeStrategyKind.FuturesOutright)
        {
            var id = new FuturesTradePlanId(plan.Position.Id, plan.ValueDate);
            var request = new UpdateFuturesTradePlanCommand { CommandId = commandId, EntityId = id, Position = plan.Position,
                Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor, UpdateFuturesTradePlanCommand.Verb, id.Format()) };
            var state = new FuturesTradePlanFunctionState().Prepare(request);
            state.TryComplete(new() { Id = Guid.NewGuid(), CommandId = commandId, EntityId = id, Subject = request.Subject, Plan = plan }, request).Should().BeTrue();
            var projector = Substitute.For<IEventProjector<FuturesTradePositionCommandActor>>();
            if (failPublication)
                projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).ThrowsAsync(new InvalidOperationException("Injected notification failure after commit."));
            var repository = new FuturesTradePlanFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(), source,
                Substitute.For<IActorService>(), projector,
                NullLogger<FuturesTradePlanFunctionStateRepository>.Instance, factory, fixture.Transactions);
            save = async () => await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), state, request);
            stream = request.StreamId;
        }
        else
        {
            var id = new VerticalSpreadTradePlanId(plan.Position.Id, plan.ValueDate);
            var request = new UpdateVerticalSpreadTradePlanCommand { CommandId = commandId, EntityId = id, Position = plan.Position,
                Subject = new(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor, UpdateVerticalSpreadTradePlanCommand.Verb, id.Format()) };
            var state = new VerticalSpreadTradePlanFunctionState().Prepare(request);
            state.TryComplete(new() { Id = Guid.NewGuid(), CommandId = commandId, EntityId = id, Subject = request.Subject, Plan = plan }, request).Should().BeTrue();
            var projector = Substitute.For<IEventProjector<FuturesVerticalSpreadTradePositionCommandActor>>();
            if (failPublication)
                projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).ThrowsAsync(new InvalidOperationException("Injected notification failure after commit."));
            var repository = new VerticalSpreadTradePlanFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(), source,
                Substitute.For<IActorService>(), projector,
                NullLogger<VerticalSpreadTradePlanFunctionStateRepository>.Instance, factory, fixture.Transactions);
            save = async () => await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), state, request);
            stream = request.StreamId;
        }
        if (failAfterPlanWrite)
        {
            await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected failure*");
            (await fixture.EventCountAsync(stream)).Should().Be(0);
            (await fixture.Plans.GetCurrentAsync(plan.Position.Id, kind, plan.ValueDate)).Should().BeNull();
        }
        else
        {
            await save();
            (await fixture.EventCountAsync(stream)).Should().Be(1);
            (await fixture.Plans.GetCurrentAsync(plan.Position.Id, kind, plan.ValueDate)).Should().BeEquivalentTo(plan);
        }
    }
}
