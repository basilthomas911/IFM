using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class ImportEventContractTests
{
    [Fact]
    public void ParameterOnlyCommands_RoundTripAcquisitionIntent()
    {
        var importDate = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var yield = new ImportYieldCurveRatesCommand(importDate, ImportDuplicatePolicy.Reject)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ImportYieldCurveRatesCommand.Actor,
                ImportYieldCurveRatesCommand.Verb,
                "2026"),
            PostEvents = true
        };
        var calendar = new ImportEconomicCalendarsCommand(
            importDate,
            ["US", "CA"],
            ImportDuplicatePolicy.Overwrite)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ImportEconomicCalendarsCommand.Actor,
                ImportEconomicCalendarsCommand.Verb,
                new EconomicCalendarId(importDate, "ZZ", "ImportEconomicCalendars").Format()),
            PostEvents = true
        };

        var yieldRoundTrip = RoundTrip(yield);
        yieldRoundTrip.CommandId.Should().Be(yield.CommandId);
        yieldRoundTrip.Subject.Should().Be(yield.Subject);
        yieldRoundTrip.EntityId.Should().Be(yield.EntityId);
        yieldRoundTrip.ImportDate.Should().Be(yield.ImportDate);
        yieldRoundTrip.DuplicatePolicy.Should().Be(yield.DuplicatePolicy);

        var calendarRoundTrip = RoundTrip(calendar);
        calendarRoundTrip.CommandId.Should().Be(calendar.CommandId);
        calendarRoundTrip.Subject.Should().Be(calendar.Subject);
        calendarRoundTrip.EntityId.Should().Be(calendar.EntityId);
        calendarRoundTrip.ImportedDate.Should().Be(calendar.ImportedDate);
        calendarRoundTrip.CountryCodes.Should().Equal(calendar.CountryCodes);
        calendarRoundTrip.DuplicatePolicy.Should().Be(calendar.DuplicatePolicy);
    }

    [Fact]
    public void YieldCurveImportRequest_RoundTripsItsAcquisitionIntent()
    {
        var entityId = new YieldCurveRateEntityId(2026);
        var expected = new YieldCurveRatesImportedEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedEvent.Actor,
                YieldCurveRatesImportedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = "yield-import",
            EventSource = "test",
            ImportDate = new DateTime(2026, 8, 16),
            RequestedOn = DateTime.UtcNow,
            RequestedBy = "scheduler",
            DuplicatePolicy = ImportDuplicatePolicy.Reject
        };

        var actual = RoundTrip(expected);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EconomicCalendarImportRequest_RoundTripsFiltersAndPolicy()
    {
        var importedDate = new DateTime(2026, 8, 16);
        var entityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        var expected = new EconomicCalendarsImportedEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedEvent.Actor,
                EconomicCalendarsImportedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = "calendar-import",
            EventSource = "test",
            ImportedDate = importedDate,
            CountryCodes = ["US", "CA"],
            RequestedOn = DateTime.UtcNow,
            RequestedBy = "operator",
            DuplicatePolicy = ImportDuplicatePolicy.Overwrite
        };

        var actual = RoundTrip(expected);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void CompleteAndFailEvents_RoundTripAttemptResults()
    {
        var importedDate = new DateTime(2026, 8, 16);
        var calendarId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        var complete = new EconomicCalendarsImportedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarsImportedCompleteEvent.Actor,
                EconomicCalendarsImportedCompleteEvent.Verb, calendarId.Format()),
            EntityId = calendarId,
            CommandId = Guid.NewGuid(),
            AggregateId = "calendar-import",
            EventSource = "test",
            ImportedDate = importedDate,
            CountryCodes = ["US"],
            EconomicCalendars = [new EconomicCalendarReadModel(
                importedDate, "US", "CPI", "2.7", "2.6", "2.5",
                DateTime.UtcNow, "test")],
            ImportedOn = DateTime.UtcNow,
            ImportedBy = "operator"
        };
        var failed = new YieldCurveRatesImportedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedFailEvent.Actor,
                YieldCurveRatesImportedFailEvent.Verb, "2026"),
            EntityId = new YieldCurveRateEntityId(2026),
            CommandId = Guid.NewGuid(),
            AggregateId = "yield-import",
            EventSource = "test",
            ErrorData = "error-data",
            CommandName = "ImportYieldCurveRatesCommand",
            CommandData = "command-data",
            RouteTo = "",
            ImportDate = importedDate,
            DuplicatePolicy = ImportDuplicatePolicy.Reject,
            ErrorMessage = "failed"
        };

        RoundTrip(complete).Should().BeEquivalentTo(complete);
        RoundTrip(failed).Should().BeEquivalentTo(failed);
    }

    static T RoundTrip<T>(T value) =>
        MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
}
