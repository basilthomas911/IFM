using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public sealed class EconomicCalendarImportFlowTests(MarketDataFixture fixture)
    : IClassFixture<MarketDataFixture>
{
    [Fact]
    public async Task ImportedEvent_AcquiresMapsDurablyStoresAndCompletes()
    {
        var importDate = new DateOnly(8991, 8, 16);
        var eventName = $"integration-import-{Guid.NewGuid():N}";
        var eventTime = new DateTimeOffset(8991, 8, 16, 12, 30, 0, TimeSpan.Zero);
        var retrievedAt = DateTimeOffset.UtcNow;
        var entry = new EconomicCalendarEntry(
            eventTime,
            "QY",
            eventName,
            "2.7",
            "2.6",
            "2.5",
            "high",
            "%",
            "0.1",
            "3.8",
            retrievedAt,
            "integration-provider");
        var provider = Substitute.For<IEconomicCalendar>();
        provider.GetAsync(
                importDate,
                importDate,
                Arg.Is<IReadOnlySet<string>?>(countries =>
                    countries != null && countries.Count == 1 && countries.Contains("QY")),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([entry]));
        var referenceData = Substitute.For<IReferenceDataApi>();
        referenceData.EconomicCalendar.Returns(provider);
        var context = Substitute.For<IEventActorContext>();
        EconomicCalendarsImportedCompleteEvent? completed = null;
        context.SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(
                Arg.Do<EconomicCalendarsImportedCompleteEvent>(value => completed = value))
            .Returns(ValueTask.CompletedTask);
        var importedDate = importDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var entityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        var request = new EconomicCalendarsImportedEvent
        {
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            Subject = new ActorSubject(
                ActorType.Event,
                EconomicCalendarsImportedEvent.Actor,
                EconomicCalendarsImportedEvent.Verb,
                entityId.Format()),
            ImportedDate = importedDate,
            CountryCodes = ["qy"],
            RequestedOn = DateTime.UtcNow,
            RequestedBy = "integration-test",
            DuplicatePolicy = ImportDuplicatePolicy.Overwrite
        };
        var durableId = new EconomicCalendarId(eventTime.UtcDateTime, "QY", eventName);

        try
        {
            var result = await request.ExecuteAsync(
                context,
                referenceData,
                fixture.DbFactory,
                NullLogger<EconomicCalendarEventActor>.Instance);

            result.Should().BeTrue();
            var stored = await fixture.MarketDataDb.GetEconomicCalendarAsync(durableId);
            stored.Should().NotBeNull();
            stored!.Actual.Should().Be("2.7");
            stored.Impact.Should().Be("high");
            stored.CreatedBy.Should().Be("integration-provider");
            completed.Should().NotBeNull();
            completed!.CommandId.Should().Be(request.CommandId);
            completed.EconomicCalendars.Should().ContainSingle(row => row.Id == durableId);
        }
        finally
        {
            await fixture.MarketDataDb.DeleteEconomicCalendarAsync(durableId);
        }
    }
}
