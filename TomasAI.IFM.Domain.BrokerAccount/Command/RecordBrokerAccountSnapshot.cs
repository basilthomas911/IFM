using TomasAI.IFM.Domain.BrokerAccount.Command.Model;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Computes and applies the RecordBrokerAccountSnapshot account operation through a domain event.</summary>
public static class RecordBrokerAccountSnapshot
{
    /// <summary>Checks the computed account change before applying its event; duplicates remain successful no-ops.</summary>
    /// <param name="command">The requested account operation.</param>
    /// <param name="state">The authoritative account state and pending events.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this RecordBrokerAccountSnapshotCommand command, BrokerAccountCommandState state)
    {
        var errorMsg = "BrokerAccount.STATE.APPLY_FAILED;unable to apply broker account change event";
        var updated = command.Compute(state.BrokerAccountDefinition, out var brokerAccountChange) switch
        {
            _ when !brokerAccountChange.Accepted => command.UpdateFailed(ref errorMsg, brokerAccountChange.RejectionReason),
            _ when !brokerAccountChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "BrokerAccount.DEFINITION.INVALID;computed account does not belong to this account"),
            _ when brokerAccountChange.IsUnchanged => true,
            _ => state.Update(command.CreateBrokerAccountChangedEvent(brokerAccountChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed account change without mutating state or publishing events.</summary>
    /// <param name="command">The requested account operation.</param>
    /// <param name="brokerAccountDefinition">The existing account, if initialized.</param>
    /// <param name="brokerAccountChange">The accepted change, no-op, or business rejection.</param>
    /// <returns>True when the requested operation is accepted.</returns>
    internal static bool Compute(this RecordBrokerAccountSnapshotCommand command, BrokerAccountDefinition? brokerAccountDefinition,
        out BrokerAccountChange brokerAccountChange)
    {
        brokerAccountChange = BrokerAccountComputation.RecordBrokerAccountSnapshot(command, brokerAccountDefinition);
        return brokerAccountChange.Accepted;
    }

    /// <summary>Creates the accepted account event without changing state.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="brokerAccountChange">The accepted business data to persist.</param>
    /// <returns>The account event applied by the owning command state.</returns>
    internal static BrokerAccountChangedEvent CreateBrokerAccountChangedEvent(this RecordBrokerAccountSnapshotCommand command,
        BrokerAccountChange brokerAccountChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor, BrokerAccountChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        BrokerAccountDefinition = brokerAccountChange.BrokerAccountDefinition!
    };
}
