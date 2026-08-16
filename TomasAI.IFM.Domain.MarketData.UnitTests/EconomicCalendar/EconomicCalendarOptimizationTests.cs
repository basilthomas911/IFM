using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarOptimizationTests
{
    [Fact]
    public void Import_ProducesOneRequestEventWithoutMutatingRecordState()
    {
        var command = CreateImport(["US", "CA"]);
        var state = new EconomicCalendarCommandState();

        command.Execute(state).Should().BeTrue();

        state.Count.Should().Be(0);
        state.Events.Should().ContainSingle();
        state.Events[0].Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.CountryCodes.Should().Equal("US", "CA");
    }

    [Fact]
    public void Import_AllCountriesUsesAnEmptyCountryArray()
    {
        var command = CreateImport([]);
        var state = new EconomicCalendarCommandState();

        command.Execute(state).Should().BeTrue();

        state.Count.Should().Be(0);
        state.Events.Should().ContainSingle();
        state.Events[0].Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.CountryCodes.Should().BeEmpty();
    }

    [Fact]
    public void NextWeek_OnMondayStartsFollowingMonday()
    {
        var monday = new DateTime(2026, 8, 3, 15, 30, 0);

        monday.GetNextWeekStartingDate().Should().Be(new DateTime(2026, 8, 10));
    }

    [Fact]
    public void Import_RejectPolicyIsDeferredToStorage()
    {
        var state = new EconomicCalendarCommandState();
        var command = CreateImport(["US"], ImportDuplicatePolicy.Reject);

        command.Execute(state).Should().BeTrue();
        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.DuplicatePolicy.Should().Be(ImportDuplicatePolicy.Reject);
    }

    [Fact]
    public void ImportEventReplayIsAnOperationMarkerOnly()
    {
        var state = new EconomicCalendarCommandState();
        state.ReplayEvents([new EconomicCalendarsImportedEvent
        {
            ImportedDate = new DateTime(2026, 8, 5),
            CountryCodes = ["US"]
        }]);

        state.Count.Should().Be(0);
    }

    [Fact]
    public void ImportCountryFilters_AcceptEmptyAndRejectMalformedCodes()
    {
        new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidateImportCountryCodes([], nameof(ImportEconomicCalendarsCommand))
            .Should().BeEmpty();

        new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidateImportCountryCodes(["US", " C1 "], nameof(ImportEconomicCalendarsCommand))
            .Should().ContainSingle(error => error.ErrorMessage.Contains("CountryCodes"));
    }

    static ImportEconomicCalendarsCommand CreateImport(
        string[] countryCodes,
        ImportDuplicatePolicy duplicatePolicy = ImportDuplicatePolicy.Overwrite)
    {
        var importedOn = new DateTime(2026, 8, 5);
        var command = new ImportEconomicCalendarsCommand(importedOn, countryCodes, duplicatePolicy);
        return command with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ImportEconomicCalendarsCommand.Actor,
                ImportEconomicCalendarsCommand.Verb,
                command.EntityId.Format())
        };
    }
}
