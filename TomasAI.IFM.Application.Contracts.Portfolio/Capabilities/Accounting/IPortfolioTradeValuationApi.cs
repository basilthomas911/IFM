using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Defines the Portfolio-owned boundary for posting trade valuations.</summary>
public interface IPortfolioTradeValuationApi
{
    /// <summary>Posts an absolute unrealized trade valuation through the Portfolio General Ledger.</summary>
    /// <param name="request">The canonical trade valuation and its source evidence.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The completed ledger event identifier.</returns>
    ValueTask<ServiceResult<Guid>> PostAsync(PortfolioTradeValuationRequest request,
        CancellationToken cancellationToken = default);
}
