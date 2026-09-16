using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Closes new-risk through a durable manual trading hold.</summary>
public static class SetManualTradingHold
{
    /// <summary>Applies the hold once while preserving account evidence.</summary>
    public static ServiceResult<GuidResult> Execute(this SetManualTradingHoldCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BA.NOT_FOUND");
        if (current.ManualHold && current.Reason == command.Reason)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        return BrokerAccountCommandResult.Apply(command, state, current with
        {
            ManualHold = true,
            Gate = BrokerAccountOperationalGate.Closed,
            Reason = command.Reason,
            ChangedAtUtc = command.EffectiveAtUtc,
            Revision = current.Revision + 1
        });
    }
}
