using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertScheduledJob(int jobId, string jobName, string jobSchedule, DateTime jobScheduleDate, double jobScheduleInterval, string taskName, bool taskEnabled, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { jobId, jobName, jobSchedule, jobScheduleDate, jobScheduleInterval, taskName, taskEnabled, createdOn, createdBy, null, null };
}
