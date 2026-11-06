using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

public sealed class IronCondorCurrencyMaintenanceModeTests(TradeDatabaseFixture fixture) : IClassFixture<TradeDatabaseFixture>
{
    [Fact]
    public async Task Explicit_maintenance_argument_repairs_one_isolated_position_without_starting_its_host()
    {
        var source = fixture.ActorEventSourceDb;
        var tradeId = new TradeEntityId(80, 81, 82, Random.Shared.Next(1_000, int.MaxValue));
        var positionId = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor);
        var now = new DateTime(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc);
        var legs = Enumerable.Range(1, 4).Select(number => new TradeLegDefinition
        {
            TradeLegId = Guid.NewGuid(), ContractId = $"maintenance-es-{number}", CashMultiplier = 50,
            AssetFamily = TradeAssetFamily.FuturesOption, SignedQuantity = number % 2 == 0 ? 1 : -1
        }).ToArray();
        var trade = new EstablishedTradeDefinition
        {
            Id = tradeId, AssetFamily = TradeAssetFamily.FuturesOption, StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = now, EvidenceRevision = 1, OpeningCommission = 2m, Legs = legs
        };
        var positionStream = new ActorSubject(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName,
            OpenIronCondorPositionCommand.Verb, positionId.Format()).StreamId;
        var position = new IronCondorPositionChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = positionId, ReceivedOn = now,
            PositionSnapshot = new()
            {
                Id = positionId, StrategyKind = TradeStrategyKind.IronCondor, PositionSequence = 1,
                Phase = StrategyPositionPhase.Close, AsOfUtc = now, ValueDate = trade.TradeDate!.Value, DailyPnlPoints = 3m,
                Legs = legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId,
                    ContractId = leg.ContractId, AssetFamily = leg.AssetFamily, SignedQuantity = leg.SignedQuantity }).ToArray()
            }
        };
        await source.SaveEventsAsync(positionStream, position.CommandId, new DomainEventCollection([position]), 0, CancellationToken.None);
        var tradeStream = new ActorSubject(ActorType.Command, FuturesOptionTradeActorNames.Command, CreateOptionTradeCommand.Verb,
            tradeId.Format()).StreamId;
        var evidence = new OptionTradeChangedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = tradeId,
            ReceivedOn = now, EstablishedTradeDefinition = trade, IsInitialEstablishment = true };

        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            source.ConnectionString, "System.Data.Postgres");
        var states = Substitute.For<IEventSourceActorStateFactory>();
        states.CreateState<IronCondorPositionCommandState>().Returns(_ => new IronCondorPositionCommandState());
        var repository = new IronCondorPositionStateRepository(states, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(),
            NullLogger<IronCondorPositionStateRepository>.Instance, new PostgresEventTransaction(settings), fixture.DbFactory);
        using var container = new SimpleInjector.Container();
        container.RegisterInstance<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>>(repository);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Services.AddSingleton(container);
        await using var app = builder.Build();
        string[] args = [$"--reconcile-iron-condor-currency-evidence={positionId.Format()},1,{Guid.NewGuid():N}"];

        await Assert.ThrowsAsync<ArgumentException>(() => ApiServerModes.RunBuiltHostModeAsync(app,
            ["--reconcile-iron-condor-currency-evidence=invalid"], NullLogger.Instance));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApiServerModes.RunBuiltHostModeAsync(app, args, NullLogger.Instance));
        await source.SaveEventsAsync(tradeStream, evidence.CommandId, new DomainEventCollection([evidence]), 0, CancellationToken.None);
        Assert.True(await ApiServerModes.RunBuiltHostModeAsync(app, args, NullLogger.Instance));
        Assert.True(await ApiServerModes.RunBuiltHostModeAsync(app, args, NullLogger.Instance));
        var projected = await fixture.TradeDb.GetStrategyPositionAsync(positionId);
        Assert.Equal(148m, projected!.DailyPnl);
        Assert.Equal(2L, projected.PositionSequence);
        Assert.False(app.Lifetime.ApplicationStarted.IsCancellationRequested);
    }
}
