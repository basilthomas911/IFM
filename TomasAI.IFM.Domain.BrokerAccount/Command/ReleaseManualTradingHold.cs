using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Releases a manual hold while recomputing the evidence-based gate.</summary>
public static class ReleaseManualTradingHold
{
    /// <summary>Reopens only an otherwise qualified and complete account.</summary>
    public static ServiceResult<GuidResult> Execute(this ReleaseManualTradingHoldCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BA.NOT_FOUND");
        if (!current.ManualHold) return new ServiceOk<GuidResult>(new(command.CommandId));
        var next = current with
        {
            ManualHold = false,
            Reason = command.Reason,
            ChangedAtUtc = command.EffectiveAtUtc,
            Revision = current.Revision + 1
        };
        next = next with { Gate = BrokerAccountCommandResult.Gate(next) };
        return BrokerAccountCommandResult.Apply(command, state, next);
    }
}
