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
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class EconomicCalendarEditorViewModelTests
{
    static readonly DateTime ImportDate = new(2026, 8, 11);
    static readonly DateTime ImportDateUtc = new(2026, 8, 11, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LoadCountryCodes_StartsListenerBeforeImportCanExecute()
    {
        var subject = CreateSubject();
        subject.ViewModel.PrepareImport(ImportDate, "US");
        subject.ViewModel.ImportOperation.CanExecute.Should().BeFalse();

        await subject.ViewModel.LoadCountryCodes();

        subject.EventSource.IsStarted.Should().BeTrue();
        subject.ViewModel.CountryCodes.Select(value => value.CountryCode).Should().Equal("CA", "US");
        subject.ViewModel.ImportOperation.CanExecute.Should().BeTrue();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Add_WaitsForExactTerminalEventBeforeRefreshingAndCompletingUiCallback()
    {
        var commandId = Guid.NewGuid();
        var added = Calendar(ImportDate.AddHours(8), "US", "G2 Calendar");
        var subject = CreateSubject([added]);
        var callbackCount = 0;
        subject.CommandApi.AddEconomicCalendarAsync(added).Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();

        var operation = subject.ViewModel.AddEconomicCalendar(added, () => callbackCount++);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        subject.EventSource.PublishAddedComplete(new EconomicCalendarAddedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            EconomicCalendar = added
        });
        operation.IsCompleted.Should().BeFalse();
        callbackCount.Should().Be(0);
        subject.EventSource.PublishAddedComplete(new EconomicCalendarAddedCompleteEvent
        {
            CommandId = commandId,
            EconomicCalendar = added
        });
        await operation;

        callbackCount.Should().Be(1);
        subject.ViewModel.EconomicCalendars.Should().Equal(added);
        subject.ViewModel.LastStatusMessage.Should().Contain("Added");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Change_PropagatesExactTerminalFailureWithoutRefreshingOrCallingUiCallback()
    {
        var commandId = Guid.NewGuid();
        var original = Calendar(ImportDate.AddHours(8), "US", "G2 Calendar");
        var changed = original with { Actual = "2.01", Forecast = "2.02", Prior = "2.03" };
        var subject = CreateSubject();
        var callbackCount = 0;
        subject.CommandApi.ChangeEconomicCalendarAsync(original.Id, changed, true)
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();

        var operation = subject.ViewModel.ChangeEconomicCalendar(
            original.Id,
            changed,
            true,
            () => callbackCount++);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        subject.EventSource.PublishChangedFail(new EconomicCalendarChangedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 7002,
            ErrorMessage = "durable change failed"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<ModelOperationException>();
        exception.Which.ErrorCode.Should().Be(7002);
        callbackCount.Should().Be(0);
        subject.ViewModel.EconomicCalendars.Should().BeEmpty();
        await subject.QueryApi.DidNotReceive().GetEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<EconomicCalendarViewType>(),
            Arg.Any<string>());
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Remove_WaitsForExactTerminalEventAndRefreshesTheBoundedDateCountryView()
    {
        var commandId = Guid.NewGuid();
        var removed = Calendar(ImportDate.AddHours(8), "US", "G2 Calendar");
        var subject = CreateSubject();
        subject.CommandApi.RemoveEconomicCalendarAsync(removed.Id, true)
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();

        var operation = subject.ViewModel.RemoveEconomicCalendar(removed.Id, true);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        subject.EventSource.PublishRemovedComplete(new EconomicCalendarRemovedCompleteEvent
        {
            CommandId = commandId,
            EntityId = removed.Id
        });
        await operation;

        subject.ViewModel.EconomicCalendars.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().Contain("Removed");
        await subject.QueryApi.Received(1).GetEconomicCalendarsAsync(
            ImportDateUtc,
            EconomicCalendarViewType.Today,
            "US");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Import_IgnoresUnrelatedTerminalEventAndRefreshesDurableProjectionAfterComplete()
    {
        var commandId = Guid.NewGuid();
        var imported = Calendar(ImportDate.AddHours(8), "US", "CPI");
        var subject = CreateSubject([imported]);
        subject.CommandApi.ImportEconomicCalendarsAsync(
                ImportDateUtc,
                Arg.Is<string[]>(codes => codes.Length == 1 && codes[0] == "US"))
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();
        subject.ViewModel.PrepareImport(ImportDate, "US");

        var operation = subject.ViewModel.ImportOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        subject.EventSource.PublishComplete(new EconomicCalendarsImportedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            ImportedDate = ImportDate,
            CountryCodes = ["US"]
        });
        operation.IsCompleted.Should().BeFalse();
        subject.EventSource.PublishComplete(new EconomicCalendarsImportedCompleteEvent
        {
            CommandId = commandId,
            ImportedDate = ImportDate,
            CountryCodes = ["US"],
            EconomicCalendars = [imported]
        });
        await operation;

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.EconomicCalendars.Should().Equal(imported);
        subject.ViewModel.LastStatusMessage.Should().Contain("2026-08-11").And.Contain("US");
        subject.ViewModel.ImportOperation.CanExecute.Should().BeFalse();
        await subject.QueryApi.Received(1).GetEconomicCalendarsAsync(
            ImportDateUtc,
            EconomicCalendarViewType.Today,
            "US");
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Import_PreservesCodedFailureAndDoesNotRefreshProjection()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        subject.CommandApi.ImportEconomicCalendarsAsync(ImportDateUtc, Arg.Any<string[]>())
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();
        subject.ViewModel.PrepareImport(ImportDate, "US");

        var operation = subject.ViewModel.ImportOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        subject.EventSource.PublishFail(new EconomicCalendarsImportedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 429,
            ErrorMessage = "provider rate limited",
            ImportedDate = ImportDate,
            CountryCodes = ["US"]
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<ModelOperationException>();
        exception.Which.ErrorCode.Should().Be(429);
        subject.ViewModel.ImportOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().BeEmpty();
        await subject.QueryApi.DidNotReceive().GetEconomicCalendarsAsync(
            Arg.Any<DateTime>(),
            Arg.Any<EconomicCalendarViewType>(),
            Arg.Any<string>());
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBufferedAndZeroRowsRemainSuccessful()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        subject.CommandApi.ImportEconomicCalendarsAsync(ImportDateUtc, Arg.Any<string[]>())
            .Returns(_ => PublishEarlyCompletionAsync());
        await subject.ViewModel.LoadCountryCodes();
        subject.ViewModel.PrepareImport(ImportDate, "US");

        await subject.ViewModel.ImportOperation.ExecuteAsync();

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.EconomicCalendars.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().StartWith("Economic Calendars Imported");
        await subject.ViewModel.StopAsync(CancellationToken.None);

        async Task<ServiceResult<Guid>> PublishEarlyCompletionAsync()
        {
            subject.EventSource.PublishComplete(new EconomicCalendarsImportedCompleteEvent
            {
                CommandId = commandId,
                ImportedDate = ImportDate,
                CountryCodes = ["US"],
                EconomicCalendars = []
            });
            await Task.Yield();
            return new ServiceOk<Guid>(commandId);
        }
    }

    [Fact]
    public async Task EmptyCommandIdentifier_IsRejectedWithoutWaitingForAnEvent()
    {
        var subject = CreateSubject();
        subject.CommandApi.ImportEconomicCalendarsAsync(ImportDateUtc, Arg.Any<string[]>())
            .Returns(new ServiceOk<Guid>(Guid.Empty));
        await subject.ViewModel.LoadCountryCodes();
        subject.ViewModel.PrepareImport(ImportDate, "US");

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.ImportOperation.ExecuteAsync())
            .Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("empty correlation identifier");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_CancelsPendingTerminalWaitAndStopsListener()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        subject.CommandApi.ImportEconomicCalendarsAsync(ImportDateUtc, Arg.Any<string[]>())
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadCountryCodes();
        subject.ViewModel.PrepareImport(ImportDate, "US");
        var operation = subject.ViewModel.ImportOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);

        await subject.ViewModel.StopAsync(CancellationToken.None);

        await FluentActions.Awaiting(() => operation).Should().ThrowAsync<OperationCanceledException>();
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.EventSource.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task Retry_UsesASecondCommandIdentifierAndDuplicateTerminalDeliveryIsHarmless()
    {
        var firstCommandId = Guid.NewGuid();
        var secondCommandId = Guid.NewGuid();
        var subject = CreateSubject();
        subject.CommandApi.ImportEconomicCalendarsAsync(ImportDateUtc, Arg.Any<string[]>()).Returns(
            new ServiceOk<Guid>(firstCommandId),
            new ServiceOk<Guid>(secondCommandId));
        await subject.ViewModel.LoadCountryCodes();

        subject.ViewModel.PrepareImport(ImportDate, "US");
        var first = subject.ViewModel.ImportOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, firstCommandId);
        var firstCompletion = new EconomicCalendarsImportedCompleteEvent
        {
            CommandId = firstCommandId,
            ImportedDate = ImportDate,
            CountryCodes = ["US"]
        };
        subject.EventSource.PublishComplete(firstCompletion);
        subject.EventSource.PublishComplete(firstCompletion);
        await first;

        subject.ViewModel.PrepareImport(ImportDate, "US");
        var second = subject.ViewModel.ImportOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, secondCommandId);
        subject.EventSource.PublishComplete(new EconomicCalendarsImportedCompleteEvent
        {
            CommandId = secondCommandId,
            ImportedDate = ImportDate,
            CountryCodes = ["US"]
        });
        await second;

        await subject.CommandApi.Received(2).ImportEconomicCalendarsAsync(
            ImportDateUtc,
            Arg.Is<string[]>(codes => codes.Length == 1 && codes[0] == "US"));
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    static Subject CreateSubject(EconomicCalendarReadModel[]? calendars = null)
    {
        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetEconomicCalendarCountryCodesAsync().Returns(
            new ServiceOk<EconomicCalendarCountryCodeReadModel[]>(
            [
                new("CA"),
                new("US")
            ]));
        queryApi.GetEconomicCalendarsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<EconomicCalendarViewType>(),
                Arg.Any<string>())
            .Returns(new ServiceOk<EconomicCalendarReadModel[]>(calendars ?? []));

        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var consumer = Substitute.For<IEconomicCalendarUIEventConsumer>();
        var eventSource = new TestEconomicCalendarEventSource(consumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<MarketDataQueryModel>().Returns(new MarketDataQueryModel(queryApi, feedQueryApi));
        appRoot.GetModel<MarketDataCommandModel>().Returns(new MarketDataCommandModel(commandApi));
        appRoot.GetModel<EconomicCalendarEventModel>().Returns(new EconomicCalendarEventModel(consumer));

        return new Subject(
            new EconomicCalendarEditorViewModel(appRoot),
            queryApi,
            commandApi,
            eventSource);
    }

    static EconomicCalendarReadModel Calendar(DateTime date, string countryCode, string eventName)
        => new(date, countryCode, eventName, "1", "2", "3", ImportDate, "test");

    static async Task WaitForCommandAsync(
        EconomicCalendarEditorViewModel viewModel,
        Guid expectedCommandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != expectedCommandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(expectedCommandId);
    }

    sealed record Subject(
        EconomicCalendarEditorViewModel ViewModel,
        IMarketDataQueryApi QueryApi,
        IMarketDataCommandApi CommandApi,
        TestEconomicCalendarEventSource EventSource);

    sealed class TestEconomicCalendarEventSource
    {
        Action<EconomicCalendarAddedCompleteEvent>? _addedComplete;
        Action<EconomicCalendarAddedFailEvent>? _addedFail;
        Action<EconomicCalendarChangedCompleteEvent>? _changedComplete;
        Action<EconomicCalendarChangedFailEvent>? _changedFail;
        Action<EconomicCalendarRemovedCompleteEvent>? _removedComplete;
        Action<EconomicCalendarRemovedFailEvent>? _removedFail;
        Action<EconomicCalendarsImportedCompleteEvent>? _complete;
        Action<EconomicCalendarsImportedFailEvent>? _fail;

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
                    _addedComplete = call.ArgAt<Action<EconomicCalendarAddedCompleteEvent>>(0);
                    _addedFail = call.ArgAt<Action<EconomicCalendarAddedFailEvent>>(1);
                    _changedComplete = call.ArgAt<Action<EconomicCalendarChangedCompleteEvent>>(2);
                    _changedFail = call.ArgAt<Action<EconomicCalendarChangedFailEvent>>(3);
                    _removedComplete = call.ArgAt<Action<EconomicCalendarRemovedCompleteEvent>>(4);
                    _removedFail = call.ArgAt<Action<EconomicCalendarRemovedFailEvent>>(5);
                    _complete = call.ArgAt<Action<EconomicCalendarsImportedCompleteEvent>>(6);
                    _fail = call.ArgAt<Action<EconomicCalendarsImportedFailEvent>>(7);
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

        public void PublishAddedComplete(EconomicCalendarAddedCompleteEvent @event)
            => (_addedComplete ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishAddedFail(EconomicCalendarAddedFailEvent @event)
            => (_addedFail ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishChangedComplete(EconomicCalendarChangedCompleteEvent @event)
            => (_changedComplete ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishChangedFail(EconomicCalendarChangedFailEvent @event)
            => (_changedFail ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishRemovedComplete(EconomicCalendarRemovedCompleteEvent @event)
            => (_removedComplete ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishRemovedFail(EconomicCalendarRemovedFailEvent @event)
            => (_removedFail ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishComplete(EconomicCalendarsImportedCompleteEvent @event)
            => (_complete ?? throw new InvalidOperationException("Listener not started."))(@event);

        public void PublishFail(EconomicCalendarsImportedFailEvent @event)
            => (_fail ?? throw new InvalidOperationException("Listener not started."))(@event);
    }
}
