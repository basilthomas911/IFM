using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalGeneratedCompleteTests
{
    [Fact]
    public async Task Completion_LoadsRequiredInputsAndSendsTradeSignalUpdate()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var context = Substitute.For<IEventActorContext>();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var statusConsole = Substitute.For<IStatusConsoleWriter>();
        string? handlerError = null;
        statusConsole.WriteConsoleAsync(
                Arg.Any<LogSourceType>(),
                Arg.Any<int>(),
                Arg.Do<string>(message => handlerError = message))
            .Returns(Task.CompletedTask);
        var eod = SampleData.EodData;
        var rsi = SampleData.AtrRsiSignals[0] with
        {
            TimePeriod = TimeFrameType.Daily,
            PeriodLength = 14
        };
        var tdi = SampleData.TdiReadModelFor(TimeFrameType.FifteenSeconds);
        var itiSignal = source.FuturesItiSignal!;
        var iti = new FuturesItiSignalDataReadModel(itiSignal, itiSignal, itiSignal);
        const string vixContractId = "VX20260916";
        const decimal vixClose = 20m;
        FuturesContractV2ReadModel[] vixContracts =
        [
            new(
                vixContractId,
                "VIX Futures",
                "VX",
                "VXU6",
                "FUT",
                "USD",
                "CFE",
                "1000",
                new DateOnly(2026, 9, 16),
                true)
        ];
        var vixEod = new VixFuturesEodDataReadModel(
            vixContractId,
            source.EntityId.ValueDate,
            19m,
            21m,
            18m,
            vixClose,
            100);

        context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(
                Arg.Any<GetLastFuturesEodDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
                new ServiceOk<FuturesEodDataV2ReadModel>(eod)));
        context.RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(
                Arg.Any<GetFuturesRsiSignalQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesRsiSignalReadModel>>(
                new ServiceOk<FuturesRsiSignalReadModel>(rsi)));
        context.RequestAsync<FuturesTdiSignalReadModel, GetFuturesTdiSignalQuery>(
                Arg.Any<GetFuturesTdiSignalQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesTdiSignalReadModel>>(
                new ServiceOk<FuturesTdiSignalReadModel>(tdi)));
        context.RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(
                Arg.Any<GetFuturesItiSignalDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesItiSignalDataReadModel>>(
                new ServiceOk<FuturesItiSignalDataReadModel>(iti)));
        context.RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(
                Arg.Any<GetCurrentlyTradedFuturesContractsQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesContractV2ReadModel[]>>(
                new ServiceOk<FuturesContractV2ReadModel[]>(vixContracts)));
        context.RequestAsync<VixFuturesEodDataReadModel, GetLastVixFuturesEodDataQuery>(
                Arg.Any<GetLastVixFuturesEodDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<VixFuturesEodDataReadModel>>(
                new ServiceOk<VixFuturesEodDataReadModel>(vixEod)));
        commandApi.UpdateFuturesTradeSignalAsync(
                Arg.Any<FuturesEodDataV2ReadModel>(),
                Arg.Any<FuturesRsiSignalReadModel>(),
                Arg.Any<FuturesTdiSignalReadModel>(),
                Arg.Any<FuturesItiSignalDataReadModel>(),
                Arg.Any<decimal>(),
                Arg.Any<TimeFrameType>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        var result = await source.ExecuteAsync(
            context,
            commandApi,
            statusConsole,
            Substitute.For<ILogger>());

        result.Should().BeTrue(handlerError);
        await context.Received(1).RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(
            Arg.Is<GetFuturesRsiSignalQuery>(query =>
                query.TimePeriod == TimeFrameType.Daily
                && query.PeriodLength == 14));
        await context.Received(1).RequestAsync<FuturesTdiSignalReadModel, GetFuturesTdiSignalQuery>(
            Arg.Is<GetFuturesTdiSignalQuery>(query =>
                query.TimePeriod == TimeFrameType.FifteenSeconds));
        await context.Received(1).RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(
            Arg.Is<GetFuturesItiSignalDataQuery>(query =>
                query.TimePeriod == source.EntityId.TimePeriod));
        await commandApi.Received(1).UpdateFuturesTradeSignalAsync(
            eod,
            rsi,
            tdi,
            iti,
            vixClose,
            TimeFrameType.FifteenSeconds);
    }

    [Fact]
    public async Task DailyCompletion_DoesNotDeriveLongerPeriodCommands()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        _ = await source.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesItiSignalAsync(
            default!, default, default, default, default, default, default, default);
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
