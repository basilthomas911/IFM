using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

public interface IFundDbContext: IObjectRepository<FundDbContext>, IFundDbReadContext, IFundDbWriteContext
{
}
