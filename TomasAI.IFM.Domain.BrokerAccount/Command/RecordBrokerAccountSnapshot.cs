using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Records a coherent application-level broker-account snapshot.</summary>
public static class RecordBrokerAccountSnapshot
{
    /// <summary>Ignores stale generations and updates the operational gate from authoritative evidence.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RecordBrokerAccountSnapshotCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current ?? new BrokerAccountDefinition
        {
            Id = command.EntityId,
            Environment = command.Environment,
            QualificationStatus = BrokerAccountQualificationStatus.EvidencePending
        };
        if (current.Environment != BrokerEnvironment.Unknown && current.Environment != command.Environment)
            return command.UpdateFailed("BA.ENVIRONMENT.CONFLICT");
        if (current.Snapshot is { } prior && command.Snapshot.Generation < prior.Generation)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        if (current.Snapshot == command.Snapshot)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        var next = current with
        {
            Environment = command.Environment,
            Snapshot = command.Snapshot,
            Revision = current.Revision + 1,
            ChangedAtUtc = command.Snapshot.AsOfUtc
        };
        next = next with { Gate = BrokerAccountCommandResult.Gate(next) };
        return BrokerAccountCommandResult.Apply(command, state, next);
    }
}
