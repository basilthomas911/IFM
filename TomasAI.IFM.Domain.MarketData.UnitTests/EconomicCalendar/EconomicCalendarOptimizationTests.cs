using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;

public sealed class EconomicCalendarOptimizationTests
{
    [Fact]
    public void Import_ProducesOneCumulativeBatchEvent()
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
    public void Import_DuplicateIdsFailBeforeStateMutation()
    {
        var command = CreateImport([SampleData.EconomicCalendar, SampleData.EconomicCalendar]);
        var state = new EconomicCalendarCommandState();

        var act = () => command.Execute(state);

        act.Should().Throw<AddEconomicCalendarException>();
        state.Count.Should().Be(0);
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void NextWeek_OnMondayStartsFollowingMonday()
    {
        var monday = new DateTime(2026, 8, 3, 15, 30, 0);

        monday.GetNextWeekStartingDate().Should().Be(new DateTime(2026, 8, 10));
    }

    static ImportEconomicCalendarsCommand CreateImport(EconomicCalendarReadModel[] values)
    {
        var importedOn = new DateTime(2026, 8, 5);
        var command = new ImportEconomicCalendarsCommand(values, importedOn);
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
