using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

public interface ISecuritiesDbContext :
    IObjectRepository<SecuritiesDbContext>,
    ISecuritiesDbReadContext,
    ISecuritiesDbWriteContext,
    IFuturesContractRolloverStore
{
}
