using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Typed scheduled-task repository capabilities.</summary>
public interface IScheduledTaskDbContext : IObjectRepository<ScheduledTaskDbContext>, IScheduledTaskDbReadContext, IScheduledTaskDbWriteContext;