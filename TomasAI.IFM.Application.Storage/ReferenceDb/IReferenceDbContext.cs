using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public interface IReferenceDbContext: IObjectRepository<ReferenceDbContext>, IReferenceDbReadContext, IReferenceDbWriteContext
{
    IReferenceDbReadContext DbReader { get; }
    IReferenceDbWriteContext DbWriter { get; }
}
