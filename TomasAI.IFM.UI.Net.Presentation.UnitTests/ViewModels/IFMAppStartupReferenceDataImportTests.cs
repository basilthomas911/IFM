using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class IFMAppStartupReferenceDataImportTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan TerminalTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Completion_UsesListenersBeforeCommandsAndLeavesNoFailureReport()
    {
        var subject = CreateSubject();
        var yieldCommandId = Guid.NewGuid();
        var calendarCommandId = Guid.NewGuid();
        subject.CommandApi.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(_ => CompleteYieldCurveBeforeResponse());
        subject.CommandApi.ImportEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<string[]?>())
            .Returns(_ => CompleteCalendarBeforeResponse());

        await subject.ViewModel.ImportReferenceDataAtStartupAsync(CancellationToken.None);

        subject.ViewModel.YieldCurveStartupImport.Should().BeEquivalentTo(
            new
            {
                Outcome = StartupReferenceDataImportOutcome.Completed,
                CommandId = yieldCommandId,
                ErrorCode = 0
            });
        subject.ViewModel.EconomicCalendarStartupImport.Should().BeEquivalentTo(
            new
            {
                Outcome = StartupReferenceDataImportOutcome.Completed,
                CommandId = calendarCommandId,
                ErrorCode = 0
            });
        subject.ViewModel.LastError.Should().BeNull();
        subject.YieldCurveEvents.IsStarted.Should().BeFalse();
        subject.CalendarEvents.IsStarted.Should().BeFalse();
        await subject.StatusWriter.DidNotReceive().WriteConsoleAsync(
            Arg.Any<TomasAI.IFM.Shared.StatusConsole.LogSourceType>(),
            Arg.Any<string>());

        async Task<ServiceResult<Guid>> CompleteYieldCurveBeforeResponse()
        {
            subject.BothListenersShouldBeStarted();
            await subject.YieldCurveEvents.PublishAsync(new YieldCurveRatesImportedCompleteEvent
            {
                CommandId = yieldCommandId
            });
            return new ServiceOk<Guid>(yieldCommandId);
        }

        async Task<ServiceResult<Guid>> CompleteCalendarBeforeResponse()
        {
            subject.BothListenersShouldBeStarted();
            subject.CalendarEvents.PublishComplete(new EconomicCalendarsImportedCompleteEvent
            {
                CommandId = calendarCommandId
            });
            await Task.Yield();
            return new ServiceOk<Guid>(calendarCommandId);
        }
    }

    [Fact]
    public async Task TypedFailure_IsReportedOnceWhileOtherImportCanComplete()
    {
        var subject = CreateSubject();
        var yieldCommandId = Guid.NewGuid();
        var calendarCommandId = Guid.NewGuid();
        subject.CommandApi.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(_ => FailYieldCurveBeforeResponse());
        subject.CommandApi.ImportEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<string[]?>())
            .Returns(_ => CompleteCalendarBeforeResponse());

        await subject.ViewModel.ImportReferenceDataAtStartupAsync(CancellationToken.None);

        subject.ViewModel.YieldCurveStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.Failed);
        subject.ViewModel.YieldCurveStartupImport.ErrorCode.Should().Be(503);
        subject.ViewModel.EconomicCalendarStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.Completed);
        subject.ViewModel.LastError!.Message.Should().Contain("provider unavailable");
        subject.ViewModel.LastError.Message.Should().Contain("No automatic retry was attempted");
        await subject.CommandApi.Received(1).ImportYieldCurveRatesAsync(Arg.Any<DateTime>());
        await subject.CommandApi.Received(1).ImportEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<string[]?>());

        async Task<ServiceResult<Guid>> FailYieldCurveBeforeResponse()
        {
            await subject.YieldCurveEvents.PublishAsync(new YieldCurveRatesImportedFailEvent
            {
                CommandId = yieldCommandId,
                ErrorCode = 503,
                ErrorMessage = "provider unavailable"
            });
            return new ServiceOk<Guid>(yieldCommandId);
        }

        async Task<ServiceResult<Guid>> CompleteCalendarBeforeResponse()
        {
            subject.CalendarEvents.PublishComplete(new EconomicCalendarsImportedCompleteEvent
            {
                CommandId = calendarCommandId
            });
            await Task.Yield();
            return new ServiceOk<Guid>(calendarCommandId);
        }
    }

    [Fact]
    public async Task MissingTerminalEvents_TimeOutWithoutRetryAndStopBothListeners()
    {
        var subject = CreateSubject();
        subject.CommandApi.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        subject.CommandApi.ImportEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<string[]?>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));

        var operation = subject.ViewModel.ImportReferenceDataAtStartupAsync(CancellationToken.None);
        await WaitForBothCommandsAsync(subject.CommandApi);
        subject.TimeProvider.Advance(TerminalTimeout);
        await operation;

        subject.ViewModel.YieldCurveStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.NotObserved);
        subject.ViewModel.EconomicCalendarStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.NotObserved);
        subject.ViewModel.LastError!.Message.Should().Contain("outcome was not observed");
        subject.ViewModel.LastError.Message.Should().Contain("No automatic retry was attempted");
        subject.YieldCurveEvents.IsStarted.Should().BeFalse();
        subject.CalendarEvents.IsStarted.Should().BeFalse();
        await subject.CommandApi.Received(1).ImportYieldCurveRatesAsync(Arg.Any<DateTime>());
        await subject.CommandApi.Received(1).ImportEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<string[]?>());
    }

    [Fact]
    public async Task CallerCancellation_StopsListenersAndDoesNotConvertCancellationToFailure()
    {
        var subject = CreateSubject();
        subject.CommandApi.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        subject.CommandApi.ImportEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<string[]?>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        using var cancellation = new CancellationTokenSource();

        var operation = subject.ViewModel.ImportReferenceDataAtStartupAsync(cancellation.Token);
        await WaitForBothCommandsAsync(subject.CommandApi);
        cancellation.Cancel();

        await FluentActions.Awaiting(() => operation).Should().ThrowAsync<OperationCanceledException>();
        subject.ViewModel.YieldCurveStartupImport.Should().BeNull();
        subject.ViewModel.EconomicCalendarStartupImport.Should().BeNull();
        subject.ViewModel.LastError.Should().BeNull();
        subject.YieldCurveEvents.IsStarted.Should().BeFalse();
        subject.CalendarEvents.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task OneListenerStartFailure_DoesNotPreventTheIndependentImport()
    {
        var subject = CreateSubject(failYieldCurveListenerStart: true);
        var calendarCommandId = Guid.NewGuid();
        subject.CommandApi.ImportEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<string[]?>())
            .Returns(_ => CompleteCalendarBeforeResponse());

        await subject.ViewModel.ImportReferenceDataAtStartupAsync(CancellationToken.None);

        subject.ViewModel.YieldCurveStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.Failed);
        subject.ViewModel.YieldCurveStartupImport.Message.Should().Contain("listener could not start");
        subject.ViewModel.EconomicCalendarStartupImport!.Outcome
            .Should().Be(StartupReferenceDataImportOutcome.Completed);
        await subject.CommandApi.DidNotReceive().ImportYieldCurveRatesAsync(Arg.Any<DateTime>());
        await subject.CommandApi.Received(1).ImportEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<string[]?>());

        async Task<ServiceResult<Guid>> CompleteCalendarBeforeResponse()
        {
            subject.CalendarEvents.PublishComplete(new EconomicCalendarsImportedCompleteEvent
            {
                CommandId = calendarCommandId
            });
            await Task.Yield();
            return new ServiceOk<Guid>(calendarCommandId);
        }
    }

    static Subject CreateSubject(bool failYieldCurveListenerStart = false)
    {
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var yieldCurveConsumer = Substitute.For<IMarketDataUIEventConsumer>();
        var calendarConsumer = Substitute.For<IEconomicCalendarUIEventConsumer>();
        var yieldCurveEvents = new TestMarketDataEventSource(
            yieldCurveConsumer,
            failYieldCurveListenerStart);
        var calendarEvents = new TestEconomicCalendarEventSource(calendarConsumer);
        var commandResponseConsumer = Substitute.For<ICommandResponseUIEventConsumer>();
        var statusWriter = Substitute.For<IStatusConsoleWriter>();
        statusWriter.WriteConsoleAsync(
                Arg.Any<TomasAI.IFM.Shared.StatusConsole.LogSourceType>(),
                Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.CommandResponses.Returns(new CommandResponseEventService(commandResponseConsumer));
        appRoot.Services.MarketDataEvents
            .Returns(_ => new MarketDataEventService(yieldCurveConsumer));
        appRoot.Services.MarketDataCommands
            .Returns(_ => new MarketDataCommandService(commandApi));
        appRoot.GetStatusConsoleWriter().Returns(statusWriter);
        var timeProvider = new ManualTimeProvider(Now);

        var viewModel = new IFMAppViewModel(
            appRoot,
            new Version(1, 2, 3),
            "Test",
            Substitute.For<IIFMAppLiveViewAdapter>(),
            UiServiceFactory.CreateEconomicCalendar(
                Substitute.For<IMarketDataQueryApi>(),
                commandApi,
                calendarConsumer),
            timeProvider,
            TerminalTimeout);
        return new Subject(
            viewModel,
            commandApi,
            yieldCurveEvents,
            calendarEvents,
            statusWriter,
            timeProvider);
    }

    static async Task WaitForBothCommandsAsync(IMarketDataCommandApi commandApi)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (commandApi.ReceivedCalls().Count(call =>
                    call.GetMethodInfo().Name is nameof(IMarketDataCommandApi.ImportYieldCurveRatesAsync)
                        or nameof(IMarketDataCommandApi.ImportEconomicCalendarsAsync)) == 2)
            {
                await Task.Yield();
                return;
            }
            await Task.Delay(5);
        }
        throw new TimeoutException("Both startup import commands were not submitted.");
    }

    sealed record Subject(
        IFMAppViewModel ViewModel,
        IMarketDataCommandApi CommandApi,
        TestMarketDataEventSource YieldCurveEvents,
        TestEconomicCalendarEventSource CalendarEvents,
        IStatusConsoleWriter StatusWriter,
        ManualTimeProvider TimeProvider)
    {
        public void BothListenersShouldBeStarted()
        {
            YieldCurveEvents.IsStarted.Should().BeTrue();
            CalendarEvents.IsStarted.Should().BeTrue();
        }
    }

    sealed class TestMarketDataEventSource
    {
        Func<IEvent, ValueTask>? _listener;

        public TestMarketDataEventSource(
            IMarketDataUIEventConsumer consumer,
            bool failStart)
        {
            consumer.StartAsync(Arg.Any<ICollection<IEvent>>(), Arg.Any<Func<IEvent, ValueTask>>())
                .Returns(call =>
                {
                    if (failStart)
                        return ValueTask.FromException(new InvalidOperationException("yield listener unavailable"));
                    _listener = call.ArgAt<Func<IEvent, ValueTask>>(1);
                    IsStarted = true;
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync().Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public ValueTask PublishAsync(IEvent @event)
            => (_listener ?? throw new InvalidOperationException("Listener not started."))(@event);
    }

    sealed class TestEconomicCalendarEventSource
    {
        Action<EconomicCalendarsImportedCompleteEvent>? _complete;

        public TestEconomicCalendarEventSource(IEconomicCalendarUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Action<EconomicCalendarAddedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarAddedFailEvent>>(),
                    Arg.Any<Action<EconomicCalendarChangedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarChangedFailEvent>>(),
                    Arg.Any<Action<EconomicCalendarRemovedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarRemovedFailEvent>>(),
                    Arg.Any<Action<EconomicCalendarsImportedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarsImportedFailEvent>>())
                .Returns(call =>
                {
                    _complete = call.ArgAt<Action<EconomicCalendarsImportedCompleteEvent>>(6);
                    IsStarted = true;
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync().Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public void PublishComplete(EconomicCalendarsImportedCompleteEvent @event)
            => (_complete ?? throw new InvalidOperationException("Listener not started."))(@event);
    }
}
