using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public class PeriodRangeStateRepositoryTests
{
    [Fact]
    public async Task PeriodBasedRepositoriesRetainCreationForDailyAndIntradayStreams()
    {
        const long streamId = 73;
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesMacdSignalCommandState>().Returns(_ => new FuturesMacdSignalCommandState());
        stateFactory.CreateState<FuturesAdxSignalCommandState>().Returns(_ => new FuturesAdxSignalCommandState());
        stateFactory.CreateState<FuturesAtrSignalCommandState>().Returns(_ => new FuturesAtrSignalCommandState());
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.GetEventStreamIdAsync(Arg.Any<string>(), CancellationToken.None).Returns(streamId);
        var actorService = Substitute.For<IActorService>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var macdRepository = new FuturesMacdSignalStateRepository(
            stateFactory, eventDb, actorService, dbFactory,
            Substitute.For<IEventProjector<FuturesMacdSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesMacdSignalStateRepository>>());
        var adxRepository = new FuturesAdxSignalStateRepository(
            stateFactory, eventDb, actorService, dbFactory,
            Substitute.For<IEventProjector<FuturesAdxSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesAdxSignalStateRepository>>());
        var atrRepository = new FuturesAtrSignalStateRepository(
            stateFactory, eventDb, actorService, dbFactory,
            Substitute.For<IEventProjector<FuturesAtrSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesAtrSignalStateRepository>>());

        var macd = WithSubject(SampleData.MacdGenerateCommand, SampleData.MacdEntityId.Format());
        var macdDailyId = new FuturesMacdDailySignalEntityId(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
        var macdDaily = WithSubject(new GenerateFuturesMacdDailySignalCommand
        {
            EntityId = macdDailyId,
            FuturesMacdSignalId = SampleData.MacdSignalId
        }, macdDailyId.Format());
        var adx = WithSubject(SampleData.AdxGenerateCommand, SampleData.AdxEntityId.Format());
        var adxDailyId = new FuturesAdxDailySignalEntityId(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
        var adxDaily = WithSubject(new GenerateFuturesAdxDailySignalCommand
        {
            EntityId = adxDailyId,
            FuturesAdxSignalId = SampleData.AdxSignalId
        }, adxDailyId.Format());
        var atr = WithSubject(SampleData.AtrGenerateCommand, SampleData.AtrEntityId.Format());
        var atrDailyId = new FuturesAtrDailySignalEntityId(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
        var atrDaily = WithSubject(new GenerateFuturesAtrDailySignalCommand
        {
            EntityId = atrDailyId,
            FuturesAtrSignalId = SampleData.AtrSignalId
        }, atrDailyId.Format());

        await macdRepository.LoadStateAsync(macd);
        await macdRepository.LoadStateAsync(macdDaily);
        await adxRepository.LoadStateAsync(adx);
        await adxRepository.LoadStateAsync(adxDaily);
        await atrRepository.LoadStateAsync(atr);
        await atrRepository.LoadStateAsync(atrDaily);

        await eventDb.Received(2).MapReduceActorEventStreamFromCreationAsync<FuturesMacdSignalCommandState>(streamId, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
        await eventDb.Received(2).MapReduceActorEventStreamFromCreationAsync<FuturesAdxSignalCommandState>(streamId, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
        await eventDb.Received(2).MapReduceActorEventStreamFromCreationAsync<FuturesAtrSignalCommandState>(streamId, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
    }

    [Fact]
    public void MacdStateReplaysDailyGeneratedEvents()
    {
        var state = new FuturesMacdSignalCommandState();
        var dailyEvent = new FuturesMacdDailySignalGeneratedEvent
        {
            EntityId = new FuturesMacdDailySignalEntityId(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength),
            FuturesMacdSignal = SampleData.CreateMacdSignalGeneratedEvent().FuturesMacdSignal
        };

        state.ReplayEvents([
            new EventStreamReadModel
            {
                EventVersion = 1,
                EventTypeName = typeof(FuturesMacdDailySignalGeneratedEvent).AssemblyQualifiedName!,
                EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(dailyEvent)
            }
        ]);

        Assert.Single(state.MacdSignals);
    }

    static GenerateFuturesMacdSignalCommand WithSubject(GenerateFuturesMacdSignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId)
        };

    static GenerateFuturesMacdDailySignalCommand WithSubject(GenerateFuturesMacdDailySignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesMacdDailySignalCommand.Actor, GenerateFuturesMacdDailySignalCommand.Verb, entityId)
        };

    static GenerateFuturesAdxSignalCommand WithSubject(GenerateFuturesAdxSignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesAdxSignalCommand.Actor, GenerateFuturesAdxSignalCommand.Verb, entityId)
        };

    static GenerateFuturesAdxDailySignalCommand WithSubject(GenerateFuturesAdxDailySignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesAdxDailySignalCommand.Actor, GenerateFuturesAdxDailySignalCommand.Verb, entityId)
        };

    static GenerateFuturesAtrSignalCommand WithSubject(GenerateFuturesAtrSignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId)
        };

    static GenerateFuturesAtrDailySignalCommand WithSubject(GenerateFuturesAtrDailySignalCommand command, string entityId)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(GenerateFuturesAtrDailySignalCommand.Actor, GenerateFuturesAtrDailySignalCommand.Verb, entityId)
        };

    static ActorSubject Subject(string actor, string verb, string entityId)
        => new(ActorType.Command, actor, verb, entityId);

}
