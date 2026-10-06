using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Model;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;

/// <summary>Computes InsertSpreadDistribution business values and applies the resulting source event.</summary>
public static class InsertSpreadDistribution
{
    /// <summary>Computes proposed business values, checks failure guards, and applies one source event.</summary>
    /// <param name="command">The originating command supplying business inputs and identity.</param>
    /// <param name="state">The owning state; mutations occur through Update and Apply.</param>
    /// <returns>The command identity on success, otherwise the business or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertSpreadDistributionCommand command, SpreadDistributionCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply SpreadDistributionInsertedEvent";
        var updated = command.Compute(out var spreadDistributionChange) switch
        {
            _ => state.Update(command.CreateSpreadDistributionInsertedEvent(spreadDistributionChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable business values without changing state or pending events.</summary>
    /// <param name="command">The requested business operation.</param>
    /// <param name="spreadDistributionChange">The computed business values to carry in the event.</param>
    /// <returns>True when computation completes; acceptance is checked before application.</returns>
    internal static bool Compute(this InsertSpreadDistributionCommand command, out SpreadDistributionInsertion spreadDistributionChange)
    {
        spreadDistributionChange = new(command.PutSpreadDistribution, command.CallSpreadDistribution);
        return true;
    }

    /// <summary>
    /// Creates a new SpreadDistributionInsertedEvent instance using the details provided in the specified command.
    /// </summary>
    /// <param name="command">The command containing the entity identifier, spread distribution values, and metadata required to construct the
    /// event. Cannot be null.</param>
    /// <param name="spreadDistributionChange">The computed accepted business values carried by the source event.</param>
    /// <returns>A SpreadDistributionInsertedEvent that encapsulates the inserted spread distribution and associated metadata.</returns>
    internal static SpreadDistributionInsertedEvent CreateSpreadDistributionInsertedEvent(this InsertSpreadDistributionCommand command, SpreadDistributionInsertion spreadDistributionChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionInsertedEvent.Actor, SpreadDistributionInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = new SpreadDistributionEntityId(command.EntityId.TradeId, command.EntityId.ValueDate),
            PutSpreadDistribution = spreadDistributionChange.PutSpreadDistribution,
            CallSpreadDistribution = spreadDistributionChange.CallSpreadDistribution,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
