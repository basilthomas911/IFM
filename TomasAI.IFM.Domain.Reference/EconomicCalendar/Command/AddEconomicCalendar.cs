using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.State;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command;

public static class AddEconomicCalendar
{
    /// <summary>
    /// Executes the <see cref="AddEconomicCalendarCommand"/> against the provided <see cref="EconomicCalendarCommandState"/>.
    /// </summary>
    /// <param name="e">The add economic calendar command to execute.</param>
    /// <param name="state">The economic calendar command state against which to execute the command.</param>
    /// <returns>true if the economic calendar was successfully added; otherwise, false.</returns>
    /// <exception cref="AddEconomicCalendarException">Thrown if the economic calendar already exists in the state.</exception>
    public static bool Execute(this AddEconomicCalendarCommand e, EconomicCalendarCommandState state)
    {
        return e switch
        {
            _ when state.EconomicCalendarExists(e.EntityId) => throw new AddEconomicCalendarException(e.EconomicCalendarAlreadyExistsErrorMsg()),
            _ => state.Update(e.CreateEconomicCalendarAddedEvent(), e)
        };
    }

    /// <summary>
    /// Creates an <see cref="EconomicCalendarAddedEvent"/> from an <see cref="AddEconomicCalendarCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static EconomicCalendarAddedEvent CreateEconomicCalendarAddedEvent(this AddEconomicCalendarCommand e)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, EconomicCalendarAddedEvent.Actor, EconomicCalendarAddedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        EconomicCalendar = e.EconomicCalendar,
        CreatedOn = e.OriginatedOn,
        CreatedBy = e.OriginatedBy
    };
}
