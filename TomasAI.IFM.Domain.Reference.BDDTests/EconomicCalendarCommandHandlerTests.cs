using FluentAssertions;
using TomasAI.IFM.Domain.Reference.EconomicCalendar;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.Events;

namespace TomasAI.IFM.Domain.Reference.BDDTests;

public class EconomicCalendarCommandHandlerTests
{
    [Fact]
    public void GivenAValidCalendarBatch_WhenImported_ThenOneBatchEventRepresentsTheSnapshot()
    {
        var importedDate = new DateTime(2026, 8, 5);
        var command = new ImportEconomicCalendarsCommand(
            [SampleData.EconomicCalendar, SampleData.EconomicCalendarAlternate],
            importedDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ImportEconomicCalendarsCommand.Actor,
                ImportEconomicCalendarsCommand.Verb,
                "20260805-ZZ-ImportEconomicCalendars")
        };
        var state = new EconomicCalendarCommandState();

        var changed = command.Execute(state);

        changed.Should().BeTrue();
        state.Events.Should().ContainSingle();
        state.Events[0].Should().BeOfType<EconomicCalendarsImportedEvent>()
            .Which.EconomicCalendars.Should().HaveCount(2);
    }
}
