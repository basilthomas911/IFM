using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Typed scheduled-task write capabilities.</summary>
public interface IScheduledTaskDbWriteContext : IScheduledTaskProjectionWriter, IScheduledTaskTransactionalProjectionWriter;