using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command;

/// <summary>Handles the retired ChangeTradeStrategyFamilyCommand transport contract.</summary>
public static class ChangeTradeStrategyFamily
{

    /// <param name="command">
    /// The retired legacy mutation request.
    /// </param>
    extension(ChangeTradeStrategyFamilyCommand command)
    {
        /// <summary>Rejects the legacy mutation and directs callers to the ConfigurationDb catalog.</summary>
        ///
        /// <returns>A faulted task explaining the supported catalog route.</returns>
        /// <exception cref="InvalidOperationException">The legacy catalog is read-only.</exception>
        public ValueTask<ServiceResult<GuidResult>> ExecuteAsync()
            => ValueTask.FromException<ServiceResult<GuidResult>>(new InvalidOperationException(
                "Legacy trade strategy families are read-only. Use the ConfigurationDb strategy catalog in Reference Data Manager."));
    }
}
