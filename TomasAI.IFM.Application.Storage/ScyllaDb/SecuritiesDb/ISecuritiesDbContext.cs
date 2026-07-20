using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

public interface ISecuritiesDbContext : IObjectRepository<SecuritiesDbContext>, ISecuritiesDbReadContext, ISecuritiesDbWriteContext
{
}
