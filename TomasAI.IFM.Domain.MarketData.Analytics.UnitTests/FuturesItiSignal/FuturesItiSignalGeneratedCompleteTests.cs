using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalGeneratedCompleteTests
{
    [Fact]
    public async Task DailyCompletion_DerivesWeeklyAndMonthlyCommandsWithStableIds()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        commandApi.GenerateFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateTime>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Guid?>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()))));

        _ = await source.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            TimeFrameType.Weekly,
            source.FuturesItiSignal!.IntrinsicTime,
            source.FuturesItiSignal.IntrinsicPrice,
            source.VixFuturesPrice,
            FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, TimeFrameType.Weekly));
        await commandApi.Received(1).GenerateFuturesItiSignalAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            TimeFrameType.Monthly,
            source.FuturesItiSignal.IntrinsicTime,
            source.FuturesItiSignal.IntrinsicPrice,
            source.VixFuturesPrice,
            FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, TimeFrameType.Monthly));

        FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, TimeFrameType.Weekly)
            .Should().NotBe(FuturesItiSignalGeneratedComplete.CreateDerivedCommandId(source, TimeFrameType.Monthly));
    }

    [Theory]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task LongerPeriodCompletion_DoesNotRecursivelyGenerateItiCommands(
        TimeFrameType period)
    {
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();

        _ = await CreateCompletion(period).ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task UnmarkedDailyMutation_DoesNotDeriveLongerPeriods()
    {
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var source = CreateCompletion(TimeFrameType.Daily) with
        {
            DeriveLongerPeriods = false,
            VixFuturesPrice = 0
        };

        _ = await source.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default, default);
    }

    [Fact]
    public void GeneratedEvent_CompletionPreservesSourceVixPrice()
    {
        var generated = SampleData.StartOfDayEvent with
        {
            VixFuturesPrice = 22.75,
            DeriveLongerPeriods = true
        };

        var complete = generated.ToCompleteEvent<
            FuturesItiSignalGeneratedCompleteEvent,
            FuturesItiSignalEntityId>();

        complete.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>()
            .Which.VixFuturesPrice.Should().Be(22.75);
        ((FuturesItiSignalGeneratedCompleteEvent)complete).DeriveLongerPeriods.Should().BeTrue();
    }

    static FuturesItiSignalGeneratedCompleteEvent CreateCompletion(TimeFrameType period)
    {
        var entityId = SampleData.EntityIdFor(period);
        var source = SampleData.CreateItiSignalGeneratedCompleteEvent();
        return source with
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalGeneratedCompleteEvent.Actor,
                FuturesItiSignalGeneratedCompleteEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FuturesItiSignal = source.FuturesItiSignal! with { TimePeriod = period },
            VixFuturesPrice = 22.75,
            DeriveLongerPeriods = period == TimeFrameType.Daily
        };
    }
}
