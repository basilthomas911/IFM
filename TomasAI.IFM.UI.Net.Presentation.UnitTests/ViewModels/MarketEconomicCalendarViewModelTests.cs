using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class MarketEconomicCalendarViewModelTests
{
    static readonly DateTime Today = new(2026, 8, 11);

    [Fact]
    public async Task LoadAndRefresh_PublishCountryCalendarAndSelectionState()
    {
        var calendar = Calendar(Today.AddHours(8), "US", "CPI");
        var subject = CreateSubject(calendars: new ServiceOk<EconomicCalendarReadModel[]>([calendar]));
        subject.ViewModel.SelectCalendarPeriod("Today", Today);

        await subject.ViewModel.LoadCountryCodesOperation.ExecuteAsync();
        await subject.ViewModel.RefreshOperation.ExecuteAsync();

        subject.ViewModel.CountryCodes.Should().Equal("CA", "US");
        subject.ViewModel.SelectedCountryCode.Should().Be("US");
        subject.ViewModel.EconomicCalendars.Should().Equal(calendar);
        subject.ViewModel.SelectedEconomicCalendar.Should().BeSameAs(calendar);
        subject.ViewModel.CalendarDate.Should().Be("Tuesday, August 11, 2026");
    }

    [Fact]
    public async Task SafeSelections_ControlTheNextRefresh()
    {
        var subject = CreateSubject();
        await subject.ViewModel.LoadCountryCodesOperation.ExecuteAsync();

        subject.ViewModel.SelectCountryCode(-1).Should().BeFalse();
        subject.ViewModel.SelectCountryCode(0).Should().BeTrue();
        subject.ViewModel.SelectCalendarPeriod("Next Week", Today);
        await subject.ViewModel.RefreshOperation.ExecuteAsync();

        subject.ViewModel.SelectedCountryCode.Should().Be("CA");
        await subject.Api.Received(1).GetEconomicCalendarsAsync(
            Today,
            EconomicCalendarViewType.NextWeek,
            "CA");
        subject.ViewModel.SelectEconomicCalendar(0).Should().BeFalse();
    }

    [Fact]
    public async Task CalendarEvents_RefreshWhileRunningAndAreIgnoredAfterStop()
    {
        var subject = CreateSubject();
        await subject.ViewModel.LoadCountryCodesOperation.ExecuteAsync();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.EventSource.PublishAdded();
        await WaitForCalendarQueriesAsync(subject.Api, 1);

        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.PublishAdded();
        await Task.Yield();

        await subject.Api.Received(1).GetEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<EconomicCalendarViewType>(),
            Arg.Any<string>());
        subject.EventSource.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task CodedFailure_IsObservableAndPublicSurfaceHasNoCallbacks()
    {
        var subject = CreateSubject(
            countries: new ServiceFailed<EconomicCalendarCountryCodeReadModel[]>(
                818,
                "country codes unavailable"));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadCountryCodesOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(818);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(818);
        typeof(MarketEconomicCalendarViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject(
        ServiceResult<EconomicCalendarCountryCodeReadModel[]>? countries = null,
        ServiceResult<EconomicCalendarReadModel[]>? calendars = null)
    {
        countries ??= new ServiceOk<EconomicCalendarCountryCodeReadModel[]>(
        [
            new EconomicCalendarCountryCodeReadModel("CA"),
            new EconomicCalendarCountryCodeReadModel("US")
        ]);
        calendars ??= new ServiceOk<EconomicCalendarReadModel[]>([]);

        var api = Substitute.For<IMarketDataQueryApi>();
        api.GetEconomicCalendarCountryCodesAsync().Returns(Task.FromResult(countries));
        api.GetEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<EconomicCalendarViewType>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(calendars));
        api.GetEconomicCalendarDateAsync(
                Arg.Any<DateTime>(),
                Arg.Any<EconomicCalendarViewType>())
            .Returns(Task.FromResult<ServiceResult<string>>(
                new ServiceOk<string>("Tuesday, August 11, 2026")));

        var consumer = Substitute.For<IEconomicCalendarUIEventConsumer>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        var eventSource = new TestEventSource(consumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<MarketDataQueryModel>().Returns(new MarketDataQueryModel(api, feedApi));
        appRoot.GetModel<EconomicCalendarEventModel>()
            .Returns(new EconomicCalendarEventModel(consumer));
        return new Subject(new MarketEconomicCalendarViewModel(appRoot), api, eventSource);
    }

    static EconomicCalendarReadModel Calendar(DateTime date, string countryCode, string eventName)
        => new(date, countryCode, eventName, "1", "2", "3", Today, "test");

    static async Task WaitForCalendarQueriesAsync(IMarketDataQueryApi api, int expected)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var calls = api.ReceivedCalls().Count(call =>
                call.GetMethodInfo().Name == nameof(IMarketDataQueryApi.GetEconomicCalendarsAsync));
            if (calls >= expected)
                return;
            await Task.Delay(5);
        }

        throw new TimeoutException("The expected calendar refresh did not complete.");
    }

    sealed record Subject(
        MarketEconomicCalendarViewModel ViewModel,
        IMarketDataQueryApi Api,
        TestEventSource EventSource);

    sealed class TestEventSource
    {
        Action<EconomicCalendarAddedCompleteEvent>? _added;

        public TestEventSource(IEconomicCalendarUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Action<EconomicCalendarAddedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarChangedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarRemovedCompleteEvent>>(),
                    Arg.Any<Action<EconomicCalendarsImportedCompleteEvent>>())
                .Returns(call =>
                {
                    _added = call.ArgAt<Action<EconomicCalendarAddedCompleteEvent>>(0);
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

        public void PublishAdded()
            => (_added ?? throw new InvalidOperationException("Listener not started."))(null!);
    }
}
