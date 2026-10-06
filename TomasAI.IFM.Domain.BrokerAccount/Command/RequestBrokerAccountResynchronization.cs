using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Command.Model;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Computes and applies the RequestBrokerAccountResynchronization account operation through a domain event.</summary>
public static class RequestBrokerAccountResynchronization
{
    /// <summary>Checks the computed account change before applying its event; duplicates remain successful no-ops.</summary>
    /// <param name="command">The requested account operation.</param>
    /// <param name="context">The broker adapter used for one explicit resynchronization.</param>
    /// <param name="state">The authoritative account state and pending events.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    /// <exception cref="Exception">Broker resynchronization failures propagate to the actor exception boundary.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this RequestBrokerAccountResynchronizationCommand command, IBrokerAccountCommandContext context, BrokerAccountCommandState state)
    {
        var snapshot = await context.TradeBroker.ResynchronizeAccountAsync().ConfigureAwait(false);
        var errorMsg = "BrokerAccount.STATE.APPLY_FAILED;unable to apply broker account change event";
        var updated = command.Compute(state.BrokerAccountDefinition, context.TradeBroker.Environment, BrokerAccountSnapshotEvidence.From(snapshot), out var brokerAccountChange) switch
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

    /// <summary>Computes the resynchronized account definition without mutation or broker calls.</summary>
    /// <param name="command">The originating resynchronization request.</param>
    /// <param name="brokerAccountDefinition">The authoritative account, if initialized.</param>
    /// <param name="environment">The broker environment owning the snapshot.</param>
    /// <param name="snapshotEvidence">The coherent result returned by the broker.</param>
    /// <param name="brokerAccountChange">The accepted change, no-op, or rejection.</param>
    /// <returns>True when the snapshot can be accepted.</returns>
    internal static bool Compute(this RequestBrokerAccountResynchronizationCommand command,
        BrokerAccountDefinition? brokerAccountDefinition,
        TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment environment,
        BrokerAccountSnapshotEvidence snapshotEvidence, out BrokerAccountChange brokerAccountChange)
    {
        brokerAccountChange = BrokerAccountComputation.RecordBrokerAccountSnapshot(new RecordBrokerAccountSnapshotCommand
        {
            CommandId = command.CommandId, Subject = command.Subject, EntityId = command.EntityId,
            Environment = environment, Snapshot = snapshotEvidence
        }, brokerAccountDefinition);
        return brokerAccountChange.Accepted;
    }

    /// <summary>Creates the accepted account event without changing state.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="brokerAccountChange">The accepted business data to persist.</param>
    /// <returns>The account event applied by the owning command state.</returns>
    internal static BrokerAccountChangedEvent CreateBrokerAccountChangedEvent(this RequestBrokerAccountResynchronizationCommand command,
        BrokerAccountChange brokerAccountChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor, BrokerAccountChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        BrokerAccountDefinition = brokerAccountChange.BrokerAccountDefinition!
    };
}
