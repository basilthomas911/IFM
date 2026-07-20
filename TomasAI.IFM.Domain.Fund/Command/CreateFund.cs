using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command;

public static class CreateFund
{
    /// Attempts to create a new fund in the specified state using the provided command context and creation command.
    /// </summary>
    /// <param name="e">The command containing the details of the fund to be created.</param>
    /// <param name="state">The current state of the fund, used to determine if creation is allowed and to apply the creation event.</param>
    /// <returns>true if the fund was successfully created; otherwise, false.</returns>
    /// <exception cref="CreateFundException">Thrown if a fund already exists in the specified state.</exception>
    public static ServiceResult<GuidResult> Execute(this CreateFundCommand e, FundCommandState state)
        =>e switch
        {
            _ when state.FundExists 
                => e.UpdateFailed(FundAlreadyExists(e, state.FundId)),
            _ => e.UpdatedOk(() => state.Update(e.CreateFundCreatedEvent(), e))
        };

    /// <summary>
    /// Creates a FundCreatedEvent based on the details provided in the CreateFundCommand. 
    /// This event encapsulates the information necessary to represent the creation of a new fund, including the command ID, subject, entity ID, and the new fund details.
    /// </summary>
    /// <param name="e">The command containing the details of the fund to be created.</param>
    /// <returns>The created FundCreatedEvent.</returns>
    static FundCreatedEvent CreateFundCreatedEvent(this CreateFundCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            NewFund = e.NewFund
        };

    public static string FundAlreadyExists(CreateFundCommand e, int existingFundId) 
        => $"{e.CommandName}: fundId {existingFundId} already exists";
}
