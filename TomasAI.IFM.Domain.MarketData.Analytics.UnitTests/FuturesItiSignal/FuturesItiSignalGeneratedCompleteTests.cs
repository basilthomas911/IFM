using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
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
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
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
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength
        };
        var tdi = SampleData.TdiReadModelFor(TimeFrameType.FifteenSeconds);
        var itiSignal = source.FuturesItiSignal!;
        var iti = new FuturesItiSignalDataReadModel(itiSignal, itiSignal, itiSignal);

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
        context.RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(
                Arg.Any<UpdateFuturesTradeSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        var result = await source.ExecuteAsync(
            context,
            statusConsole,
            Substitute.For<ILogger>());

        result.Should().BeTrue(handlerError);
        await context.Received(1).RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(
            Arg.Is<GetFuturesRsiSignalQuery>(query =>
                query.TimePeriod == TimeFrameType.FifteenSeconds
                && query.PeriodLength == FuturesIntradaySignalActivationProfile.RsiPeriodLength));
        await context.Received(1).RequestAsync<FuturesTdiSignalReadModel, GetFuturesTdiSignalQuery>(
            Arg.Is<GetFuturesTdiSignalQuery>(query =>
                query.TimePeriod == TimeFrameType.FifteenSeconds));
        await context.Received(1).RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(
            Arg.Is<GetFuturesItiSignalDataQuery>(query =>
                query.TimePeriod == TimeFrameType.Daily));
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(default!);
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<VixFuturesEodDataReadModel, GetLastVixFuturesEodDataQuery>(default!);
        await context.Received(1)
            .RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(
                Arg.Is<UpdateFuturesTradeSignalCommand>(command =>
                    command.FuturesEodData == eod
                    && command.FuturesRsiSignal == rsi
                    && command.FuturesTdiSignal == tdi
                    && command.FuturesItiSignalData == iti
                    && command.VixFuturesPrice == Convert.ToDecimal(source.VixFuturesPrice)
                    && command.EntityId.TimePeriod == TimeFrameType.FifteenSeconds));
    }

    [Fact]
    public async Task RealtimeCompletion_ProjectsPopulatedTradeSignalEvent()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var context = Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>();
        var eod = SampleData.EodData;
        var rsi = SampleData.AtrRsiSignals[0] with
        {
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength
        };
        var tdi = SampleData.TdiReadModelFor(TimeFrameType.FifteenSeconds);
        var itiSignal = source.FuturesItiSignal!;
        var iti = new FuturesItiSignalDataReadModel(itiSignal, itiSignal, itiSignal);

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

        var projector = Substitute.For<IRealtimeProjector<FuturesItiSignalRealtimeActor>>();
        FuturesTradeSignalUpdatedEvent? projected = null;
        projector.ProcessRealtimeEventAsync(
                Arg.Do<IEvent>(value => projected = value as FuturesTradeSignalUpdatedEvent),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));

        var result = await TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime
            .FuturesItiSignalGeneratedComplete.ExecuteRealtimeAsync(
                source,
                context,
                projector,
                Substitute.For<IStatusConsoleWriter>(),
                Substitute.For<ILogger>());

        result.Should().BeTrue();
        projected.Should().NotBeNull();
        projected!.CommandId.Should().Be(source.CommandId);
        projected.FuturesTradeSignal.Should().NotBeNull();
        projected.FuturesTradeSignal!.TimePeriod.Should().Be(TimeFrameType.FifteenSeconds);
        projected.FuturesTradeSignal.FiftyDMA.Should().Be(eod.FiftyDMA);
        projected.FuturesTradeSignal.TwoHundredDMA.Should().Be(eod.TwoHundredDMA);
    }

    [Fact]
    public async Task DailyCompletion_MissingPrerequisitesIsAcknowledgedWithoutExceptionOrCommand()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        var statusConsole = Substitute.For<IStatusConsoleWriter>();
        context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(
                Arg.Any<GetLastFuturesEodDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
                new ServiceFailed<FuturesEodDataV2ReadModel>(1, "not ready")));
        context.RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(
                Arg.Any<GetFuturesRsiSignalQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesRsiSignalReadModel>>(
                new ServiceFailed<FuturesRsiSignalReadModel>(1, "not ready")));
        context.RequestAsync<FuturesTdiSignalReadModel, GetFuturesTdiSignalQuery>(
                Arg.Any<GetFuturesTdiSignalQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesTdiSignalReadModel>>(
                new ServiceFailed<FuturesTdiSignalReadModel>(1, "not ready")));
        context.RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(
                Arg.Any<GetFuturesItiSignalDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesItiSignalDataReadModel>>(
                new ServiceFailed<FuturesItiSignalDataReadModel>(1, "not ready")));

        var result = await source.ExecuteAsync(
            context,
            statusConsole,
            Substitute.For<ILogger>());

        result.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(default!);
        await statusConsole.DidNotReceiveWithAnyArgs().WriteConsoleAsync(default, default, default!);
    }

    [Fact]
    public async Task DailyCompletion_DoesNotDeriveLongerPeriodCommands()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        _ = await source.ExecuteAsync(
            context,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Theory]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task LongerPeriodCompletion_DoesNotRecursivelyGenerateItiCommands(
        TimeFrameType period)
    {
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();

        _ = await CreateCompletion(period).ExecuteAsync(
            context,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Theory]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task LongerPeriodCompletion_DoesNotGenerateDuplicateTradeSignal(
        TimeFrameType period)
    {
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();

        var result = await CreateCompletion(period).ExecuteAsync(
            context,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        result.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(default!);
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<FuturesRsiSignalReadModel, GetFuturesRsiSignalQuery>(default!);
    }

    [Fact]
    public async Task UnmarkedDailyMutation_DoesNotDeriveLongerPeriods()
    {
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        var source = CreateCompletion(TimeFrameType.Daily) with
        {
            DeriveLongerPeriods = false,
            VixFuturesPrice = 0
        };

        _ = await source.ExecuteAsync(
            context,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public async Task DurableCompletion_PublishesSerializableItiNotification()
    {
        var source = CreateCompletion(TimeFrameType.Weekly);
        source = source with
        {
            FuturesItiSignal = source.FuturesItiSignal! with
            {
                BandLevel = 1.25,
                ReversalLevel = 0.40
            }
        };
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        FuturesItiSignalUpdatedNotifyEvent? published = null;
        context.SendAsync<FuturesItiSignalUpdatedNotifyEvent, FuturesItiSignalEntityId>(
                Arg.Do<FuturesItiSignalUpdatedNotifyEvent>(value => published = value))
            .Returns(ValueTask.CompletedTask);

        var result = await source.ExecuteAsync(
            context,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger>());

        result.Should().BeTrue();
        published.Should().NotBeNull();
        published!.Subject.Is(
            ActorType.Notify,
            FuturesItiSignalUpdatedNotifyEvent.Actor,
            FuturesItiSignalUpdatedNotifyEvent.Verb).Should().BeTrue();
        published.SourceEventId.Should().Be(source.Id);
        published.CommandId.Should().Be(source.CommandId);
        published.EntityId.Should().Be(source.EntityId);
        published.FuturesItiSignal.Should().Be(source.FuturesItiSignal);
        published.IsValid.Should().BeTrue();

        var roundTrip = MessagePackSerializer.Deserialize<FuturesItiSignalUpdatedNotifyEvent>(
            MessagePackSerializer.Serialize(published));
        roundTrip.Should().BeEquivalentTo(published);
        roundTrip.FuturesItiSignal.BandLevel.Should().Be(1.25);
        roundTrip.FuturesItiSignal.ReversalLevel.Should().Be(0.40);
    }

    [Fact]
    public async Task RealtimeCompletion_PublishesItiNotificationForLongerPeriod()
    {
        var source = CreateCompletion(TimeFrameType.Monthly);
        var context = Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>();

        var result = await TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime
            .FuturesItiSignalGeneratedComplete.ExecuteRealtimeAsync(
                source,
                context,
                Substitute.For<IRealtimeProjector<FuturesItiSignalRealtimeActor>>(),
                Substitute.For<IStatusConsoleWriter>(),
                Substitute.For<ILogger>());

        result.Should().BeTrue();
        await context.Received(1)
            .SendAsync<FuturesItiSignalUpdatedNotifyEvent, FuturesItiSignalEntityId>(
                Arg.Is<FuturesItiSignalUpdatedNotifyEvent>(notification =>
                    notification.FuturesItiSignal.TimePeriod == TimeFrameType.Monthly
                    && notification.SourceEventId == source.Id
                    && notification.IsValid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completion_NotificationFailureDoesNotFailPersistedSignal(bool realtime)
    {
        var source = CreateCompletion(TimeFrameType.Weekly);
        var eventContext = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        var realtimeContext = Substitute.For<IEventActorContext<FuturesItiSignalRealtimeActor>>();
        eventContext.SendAsync<FuturesItiSignalUpdatedNotifyEvent, FuturesItiSignalEntityId>(
                Arg.Any<FuturesItiSignalUpdatedNotifyEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Core NATS unavailable"));
        realtimeContext.SendAsync<FuturesItiSignalUpdatedNotifyEvent, FuturesItiSignalEntityId>(
                Arg.Any<FuturesItiSignalUpdatedNotifyEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Core NATS unavailable"));

        Func<Task> act = realtime
            ? () => TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime
                .FuturesItiSignalGeneratedComplete.ExecuteRealtimeAsync(
                    source,
                    realtimeContext,
                    Substitute.For<IRealtimeProjector<FuturesItiSignalRealtimeActor>>(),
                    Substitute.For<IStatusConsoleWriter>(),
                    Substitute.For<ILogger>())
                .AsTask()
            : () => source.ExecuteAsync(
                    eventContext,
                    Substitute.For<IStatusConsoleWriter>(),
                    Substitute.For<ILogger>())
                .AsTask();

        await act.Should().NotThrowAsync();
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
