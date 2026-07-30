using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.FundDb;

public interface IFundDbContext: IObjectRepository<FundDbContext>, IFundDbReadContext, IFundDbWriteContext
{
}
