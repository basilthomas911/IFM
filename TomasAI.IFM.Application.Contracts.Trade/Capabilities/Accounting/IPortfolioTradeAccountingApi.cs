using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Typed Portfolio boundary for confirmed broker executions.</summary>
public interface IPortfolioTradeAccountingApi
{
    /// <summary>Posts confirmed settlement, commission, and closing realized P&amp;L idempotently.</summary>
    ValueTask<ServiceResult<Guid>> PostConfirmedExecutionAsync(OrderExecutionDefinition execution,
        Guid sourceEventId, DateTime confirmedAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Releases accepted working capacity only after the owning Trade Order has durably prevented execution.</summary>
    ValueTask<ServiceResult<Guid>> PostExpiredOpeningAsync(TradeOrderDefinition order, Guid executionAttemptId,
        Guid expiryCommandId, DateTime confirmedAtUtc, CancellationToken cancellationToken = default);
}
