using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Typed scheduled-task repository capabilities.</summary>
public interface IScheduledTaskDbContext : IObjectRepository<ScheduledTaskDbContext>, IScheduledTaskDbReadContext, IScheduledTaskDbWriteContext
{
    /// <summary>Gets the read capability of this repository instance.</summary>
    IScheduledTaskDbReadContext DbReader { get; }
    /// <summary>Gets the write capability of this repository instance.</summary>
    IScheduledTaskDbWriteContext DbWriter { get; }
}