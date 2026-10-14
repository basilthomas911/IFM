using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Combines the Reference repository with its read and write capabilities.</summary>
public interface IReferenceDbContext : IObjectRepository<ReferenceDbContext>, IReferenceDbReadContext, IReferenceDbWriteContext
{
    /// <summary>Gets the Reference read capability.</summary>
    IReferenceDbReadContext DbReader { get; }

    /// <summary>Gets the Reference write capability.</summary>
    IReferenceDbWriteContext DbWriter { get; }
}
