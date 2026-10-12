using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Combines the Securities repository, read, write, and rollover contracts.</summary>
public interface ISecuritiesDbContext :
    IObjectRepository<SecuritiesDbContext>,
    ISecuritiesDbReadContext,
    ISecuritiesDbWriteContext,
    IFuturesContractRolloverStore
{
    /// <summary>Gets the Securities read capability.</summary>
    ISecuritiesDbReadContext DbReader { get; }

    /// <summary>Gets the Securities write capability.</summary>
    ISecuritiesDbWriteContext DbWriter { get; }
}
