using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertScheduledJobDays(int jobId, bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool sunday) : IBindValue
{
    public object Bind() => new object?[] { jobId, monday, tuesday, wednesday, thursday, friday, saturday, sunday };
}
