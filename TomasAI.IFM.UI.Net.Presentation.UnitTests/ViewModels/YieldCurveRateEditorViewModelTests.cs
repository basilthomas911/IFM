using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class YieldCurveRateEditorViewModelTests
{
    [Fact]
    public async Task LoadOperation_StartsListenerAndPublishesCurrentMonthSnapshot()
    {
        var rate = CreateRate(DateOnly.FromDateTime(DateTime.Today));
        var subject = CreateSubject([rate]);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.EventSource.IsStarted.Should().BeTrue();
        subject.ViewModel.TimePeriods.Should().Equal("Current Month", "2025", "2026");
        subject.ViewModel.SelectedTimePeriod.Should().Be("Current Month");
        subject.ViewModel.RangeStart.Day.Should().Be(1);
        subject.ViewModel.RangeEnd.Should().Be(subject.ViewModel.RangeStart.AddMonths(1).AddDays(-1));
        subject.ViewModel.YieldCurveRates.Should().Equal(rate);
        subject.ViewModel.CanChangeRemove.Should().BeTrue();
        subject.ViewModel.GetYieldCurveRate(-1).Should().BeNull();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LoadRatesOperation_UsesSelectedCalendarYearRange()
    {
        var rate = CreateRate(new DateOnly(2025, 3, 1));
        var subject = CreateSubject([]);
        subject.QueryApi.GetYieldCurveRatesAsync(
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 12, 31))
            .Returns(new ServiceOk<YieldCurveRateReadModel[]>([rate]));
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.SelectTimePeriod(1, new DateOnly(2026, 8, 11));
        await subject.ViewModel.LoadRatesOperation.ExecuteAsync();

        subject.ViewModel.SelectedTimePeriod.Should().Be("2025");
        subject.ViewModel.RangeStart.Should().Be(new DateOnly(2025, 1, 1));
        subject.ViewModel.RangeEnd.Should().Be(new DateOnly(2025, 12, 31));
        subject.ViewModel.YieldCurveRates.Should().Equal(rate);
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddOperation_IgnoresUnrelatedEventAndRefreshesAfterCompletion()
    {
        var commandId = Guid.NewGuid();
        var existing = CreateRate(new DateOnly(2026, 8, 10));
        var added = CreateRate(new DateOnly(2043, 3, 29));
        var subject = CreateSubject([existing]);
        subject.QueryApi.GetYieldCurveRateYearsAsync().Returns(
            new ServiceOk<YieldCurveRateYearsReadModel>(new YieldCurveRateYearsReadModel([2025, 2026])),
            new ServiceOk<YieldCurveRateYearsReadModel>(new YieldCurveRateYearsReadModel([2025, 2026, 2043])));
        subject.QueryApi.GetYieldCurveRatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            new ServiceOk<YieldCurveRateReadModel[]>([existing]),
            new ServiceOk<YieldCurveRateReadModel[]>([added]));
        subject.CommandApi.AddYieldCurveRateAsync(added, false).Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareAdd(added);

        var operation = subject.ViewModel.AddOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new YieldCurveRateAddedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            YieldCurveRate = added
        });
        operation.IsCompleted.Should().BeFalse();
        await subject.EventSource.PublishAsync(new YieldCurveRateAddedCompleteEvent
        {
            CommandId = commandId,
            YieldCurveRate = added
        });
        await operation;

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.SelectedTimePeriod.Should().Be("2043");
        subject.ViewModel.RangeStart.Should().Be(new DateOnly(2043, 1, 1));
        subject.ViewModel.RangeEnd.Should().Be(new DateOnly(2043, 12, 31));
        subject.ViewModel.YieldCurveRates.Should().Equal(added);
        subject.ViewModel.LastStatusMessage.Should().Contain("Added");
        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RemoveOperation_PreservesCodedTerminalFailure()
    {
        var commandId = Guid.NewGuid();
        var rate = CreateRate(new DateOnly(2026, 8, 11));
        var subject = CreateSubject([rate]);
        subject.CommandApi.RemoveYieldCurveRateAsync(rate.ValueDate, true).Returns(
            new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareRemove(rate);

        var operation = subject.ViewModel.RemoveOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new YieldCurveRateRemovedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 715,
            ErrorMessage = "rate is referenced"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<UiServiceOperationException>();
        exception.Which.ErrorCode.Should().Be(715);
        subject.ViewModel.RemoveOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ImportCompletionBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var imported = CreateRate(new DateOnly(2026, 8, 11));
        var subject = CreateSubject([]);
        subject.QueryApi.GetYieldCurveRatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            new ServiceOk<YieldCurveRateReadModel[]>([]),
            new ServiceOk<YieldCurveRateReadModel[]>([imported]));
        subject.CommandApi.ImportYieldCurveRatesAsync(Arg.Any<DateTime>())
            .Returns(_ => PublishEarlyCompletionAsync());
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareImport(new DateTime(2026, 8, 11));

        await subject.ViewModel.ImportOperation.ExecuteAsync();

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.YieldCurveRates.Should().Equal(imported);
        subject.ViewModel.LastStatusMessage.Should().StartWith("Yield Curve Rates Imported");
        await subject.ViewModel.StopAsync(CancellationToken.None);

        async Task<ServiceResult<Guid>> PublishEarlyCompletionAsync()
        {
            await subject.EventSource.PublishAsync(new YieldCurveRatesImportedCompleteEvent
            {
                CommandId = commandId,
                ImportDate = new DateTime(2026, 8, 11),
                YieldCurveRates = [imported]
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    [Fact]
    public void ViewModel_DeclaresObservableStateWithoutViewCallbacks()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(YieldCurveRateEditorViewModel))
            .Should().BeTrue();

        var callbacks = typeof(YieldCurveRateEditorViewModel)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });

        callbacks.Should().BeEmpty();
    }

    static Subject CreateSubject(YieldCurveRateReadModel[] rates)
    {
        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetYieldCurveRateYearsAsync().Returns(
            new ServiceOk<YieldCurveRateYearsReadModel>(new YieldCurveRateYearsReadModel([2025, 2026])));
        queryApi.GetYieldCurveRatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            new ServiceOk<YieldCurveRateReadModel[]>(rates));
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var eventConsumer = Substitute.For<IMarketDataUIEventConsumer>();
        var eventSource = new TestMarketDataEventSource(eventConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.MarketDataQueries.Returns(new MarketDataQueryService(queryApi, feedQueryApi));
        appRoot.Services.MarketDataCommands.Returns(new MarketDataCommandService(commandApi));
        appRoot.Services.MarketDataEvents.Returns(new MarketDataEventService(eventConsumer));

        return new Subject(
            new YieldCurveRateEditorViewModel(appRoot),
            queryApi,
            commandApi,
            eventSource);
    }

    static YieldCurveRateReadModel CreateRate(DateOnly valueDate)
        => new(valueDate, 1, 2, 3, 6, 10, 20, 30, 50, 70, 100, 200, 300);

    static async Task WaitForCommandAsync(YieldCurveRateEditorViewModel viewModel, Guid expectedCommandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != expectedCommandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(expectedCommandId);
    }

    sealed record Subject(
        YieldCurveRateEditorViewModel ViewModel,
        IMarketDataQueryApi QueryApi,
        IMarketDataCommandApi CommandApi,
        TestMarketDataEventSource EventSource);

    sealed class TestMarketDataEventSource
    {
        Func<IEvent, ValueTask>? _listener;

        public TestMarketDataEventSource(IMarketDataUIEventConsumer consumer)
        {
            consumer.StartAsync(Arg.Any<ICollection<IEvent>>(), Arg.Any<Func<IEvent, ValueTask>>())
                .Returns(call =>
                {
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
            => _listener?.Invoke(@event)
                ?? throw new InvalidOperationException("The event listener has not started.");
    }
}
