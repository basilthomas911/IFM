using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command;

/// <summary>Handles the retired ChangeTradeStrategyFamilyCommand transport contract.</summary>
public static class ChangeTradeStrategyFamily
{
    /// <summary>Rejects the legacy mutation and directs callers to the ConfigurationDb catalog.</summary>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ChangeTradeStrategyFamilyCommand command)
        => ValueTask.FromException<ServiceResult<GuidResult>>(new InvalidOperationException(
            "Legacy trade strategy families are read-only. Use the ConfigurationDb strategy catalog in Reference Data Manager."));
}
