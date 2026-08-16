using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarOptimizationTests
{
    [Fact]
    public void Import_ProducesOneNormalizedBatchEvent()
    {
        var second = SampleData.EconomicCalendar with { EventName = "CPI" };
        var command = CreateImport([SampleData.EconomicCalendar, second]);
        var state = new EconomicCalendarCommandState();

        command.Execute(state).Should().BeTrue();

        state.Count.Should().Be(2);
        state.Events.Should().ContainSingle();
        state.Events[0].Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.EconomicCalendars.Should().HaveCount(2);
    }

    [Fact]
    public void Import_ExactDuplicateIdsCollapseBeforeStateMutation()
    {
        var command = CreateImport([SampleData.EconomicCalendar, SampleData.EconomicCalendar]);
        var state = new EconomicCalendarCommandState();

        command.Execute(state).Should().BeTrue();

        state.Count.Should().Be(1);
        state.Events.Should().ContainSingle();
        state.Events[0].Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.EconomicCalendars.Should().ContainSingle();
    }

    [Fact]
    public void NextWeek_OnMondayStartsFollowingMonday()
    {
        var monday = new DateTime(2026, 8, 3, 15, 30, 0);

        monday.GetNextWeekStartingDate().Should().Be(new DateTime(2026, 8, 10));
    }

    [Fact]
    public void Import_RejectThrowsForExistingLogicalKey()
    {
        var state = new EconomicCalendarCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new EconomicCalendarsImportedEvent { EconomicCalendars = [SampleData.EconomicCalendar] }
        });
        var command = CreateImport([SampleData.EconomicCalendar], ImportDuplicatePolicy.Reject);

        var act = () => command.Execute(state);

        act.Should().Throw<MarketDataImportDuplicateException>();
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Import_ConflictingInResponseDuplicateAlwaysThrows()
    {
        var revised = SampleData.EconomicCalendar with { Actual = "revised" };
        var command = CreateImport([SampleData.EconomicCalendar, revised]);
        var state = new EconomicCalendarCommandState();

        var act = () => command.Execute(state);

        act.Should().Throw<MarketDataImportDuplicateException>();
        state.Events.Should().BeEmpty();
    }

    static ImportEconomicCalendarsCommand CreateImport(
        EconomicCalendarReadModel[] values,
        ImportDuplicatePolicy duplicatePolicy = ImportDuplicatePolicy.Overwrite)
    {
        var importedOn = new DateTime(2026, 8, 5);
        var command = new ImportEconomicCalendarsCommand(values, importedOn, duplicatePolicy);
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
