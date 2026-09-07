using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarsImportedHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NormalizesFiltersBulkStoresAndCompletesTheAttempt()
    {
        var date = new DateOnly(2026, 8, 14);
        var entry = new EconomicCalendarEntry(
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            "US", "CPI", "2.7%", "2.6%", "2.5%", "high", "%", "0.1", "3.8%",
            DateTimeOffset.UtcNow, "FinancialModelingPrep");
        var (api, calendar, dbFactory, db, context) = Dependencies();
        IReadOnlySet<string>? receivedCountries = null;
        calendar.GetAsync(date, date, Arg.Do<IReadOnlySet<string>?>(value => receivedCountries = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([entry]));
        EconomicCalendarsImportedCompleteEvent? completed = null;
        context.SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(
                Arg.Do<EconomicCalendarsImportedCompleteEvent>(value => completed = value))
            .Returns(ValueTask.CompletedTask);
        var request = Request(date, [" us ", "CA"], ImportDuplicatePolicy.Reject);

        var result = await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<EconomicCalendarEventActor>.Instance);

        result.Should().BeTrue();
        receivedCountries.Should().BeEquivalentTo(["US", "CA"]);
        await db.Received(1).InsertEconomicCalendarsAsync(
            Arg.Is<EconomicCalendarReadModel[]>(rows =>
                rows.Length == 1 && rows[0].EventName == "CPI" && rows[0].CreatedBy == "FinancialModelingPrep"),
            ImportDuplicatePolicy.Reject,
            request.CommandId);
        completed.Should().NotBeNull();
        completed!.EconomicCalendars.Should().ContainSingle();
        completed.CountryCodes.Should().Equal(" us ", "CA");
        completed.DownloadOutcome!.Validate();
        completed.DownloadOutcome.Scope.Should().Be("CA,US");
        completed.DownloadOutcome.DownloadedRecordCount.Should().Be(1);
        completed.DownloadOutcome.PersistedRecordCount.Should().Be(1);
        completed.DownloadOutcome.ImportCommandId.Should().Be(request.CommandId);
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyProviderResultCompletesWithZeroRecords()
    {
        var date = new DateOnly(2026, 8, 15);
        var (api, calendar, dbFactory, db, context) = Dependencies();
        calendar.GetAsync(date, date, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([]));
        var request = Request(date, [], ImportDuplicatePolicy.Overwrite);

        await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<EconomicCalendarEventActor>.Instance);

        await db.Received(1).InsertEconomicCalendarsAsync(
            Arg.Is<EconomicCalendarReadModel[]>(rows => rows.Length == 0),
            ImportDuplicatePolicy.Overwrite,
            request.CommandId);
        await context.Received(1).SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(
            Arg.Is<EconomicCalendarsImportedCompleteEvent>(value => value.EconomicCalendars.Length == 0));
    }

    [Fact]
    public async Task ExecuteAsync_StorageFailurePublishesFailWithRequestParameters()
    {
        var date = new DateOnly(2026, 8, 16);
        var entry = new EconomicCalendarEntry(
            DateTimeOffset.UtcNow, "US", "CPI", null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow, "test");
        var (api, calendar, dbFactory, db, context) = Dependencies();
        calendar.GetAsync(date, date, Arg.Any<IReadOnlySet<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([entry]));
        db.InsertEconomicCalendarsAsync(
                Arg.Any<EconomicCalendarReadModel[]>(), Arg.Any<ImportDuplicatePolicy>(), Arg.Any<Guid>())
            .Returns(Task.FromException(new InvalidOperationException("storage unavailable")));
        var request = Request(date, ["US"], ImportDuplicatePolicy.Overwrite);

        Func<Task> act = async () => await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<EconomicCalendarEventActor>.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("storage unavailable");
        await context.Received(1).SendAsync<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(
            Arg.Is<EconomicCalendarsImportedFailEvent>(value =>
                value.CommandId == request.CommandId
                && value.ImportedDate == request.ImportedDate
                && value.CountryCodes.SequenceEqual(request.CountryCodes)
                && value.DownloadOutcome != null
                && value.DownloadOutcome.DownloadedRecordCount == 1
                && value.DownloadOutcome.PersistedRecordCount == null));
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCanonicalRowFailsBeforeStorage()
    {
        var date = new DateOnly(2026, 8, 16);
        var invalid = new EconomicCalendarEntry(
            DateTimeOffset.UtcNow, "", "CPI", null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow, "test");
        var (api, calendar, dbFactory, db, context) = Dependencies();
        calendar.GetAsync(date, date, Arg.Any<IReadOnlySet<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([invalid]));
        var request = Request(date, [], ImportDuplicatePolicy.Overwrite);

        Func<Task> act = async () => await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<EconomicCalendarEventActor>.Instance);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CountryCode*");
        await db.DidNotReceiveWithAnyArgs().InsertEconomicCalendarsAsync(default!, default, default);
        await context.Received(1).SendAsync<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(
            Arg.Any<EconomicCalendarsImportedFailEvent>());
    }

    static (IReferenceDataApi Api, IEconomicCalendar Calendar, IDbContextFactory Factory,
        IMarketDataDbContext Db, IEventActorContext Context) Dependencies()
    {
        var api = Substitute.For<IReferenceDataApi>();
        var calendar = Substitute.For<IEconomicCalendar>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        var context = Substitute.For<IEventActorContext>();
        api.EconomicCalendar.Returns(calendar);
        dbFactory.MarketDataDb.Returns(db);
        db.InsertEconomicCalendarsAsync(
                Arg.Any<EconomicCalendarReadModel[]>(),
                Arg.Any<ImportDuplicatePolicy>(),
                Arg.Any<Guid>())
            .Returns(Task.CompletedTask);
        return (api, calendar, dbFactory, db, context);
    }

    static EconomicCalendarsImportedEvent Request(
        DateOnly date,
        string[] countries,
        ImportDuplicatePolicy policy)
    {
        var importedDate = date.ToDateTime(TimeOnly.MinValue);
        var entityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        return new EconomicCalendarsImportedEvent
        {
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedEvent.Actor,
                EconomicCalendarsImportedEvent.Verb, entityId.Format()),
            ImportedDate = importedDate,
            CountryCodes = countries,
            RequestedOn = DateTime.UtcNow,
            RequestedBy = "test",
            DuplicatePolicy = policy
        };
    }
}
