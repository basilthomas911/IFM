using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Performs an explicit account resynchronization without polling.</summary>
public static class RequestBrokerAccountResynchronization
{
    /// <summary>Reads the broker once and applies the returned snapshot through the normal state rule.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this RequestBrokerAccountResynchronizationCommand command,
        IBrokerAccountCommandContext context,
        BrokerAccountCommandState state)
    {
        var snapshot = await context.TradeBroker.ResynchronizeAccountAsync().ConfigureAwait(false);
        return new RecordBrokerAccountSnapshotCommand
        {
            CommandId = command.CommandId,
            Subject = command.Subject,
            EntityId = command.EntityId,
            Environment = context.TradeBroker.Environment,
            Snapshot = BrokerAccountSnapshotEvidence.From(snapshot)
        }.Execute(state);
    }
}
