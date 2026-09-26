using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Combines the SystemAdmin repository with its read and write capabilities.</summary>
public interface ISystemAdminDbContext :
    IObjectRepository<SystemAdminDbContext>,
    ISystemAdminDbReadContext,
    ISystemAdminDbWriteContext
{
}
