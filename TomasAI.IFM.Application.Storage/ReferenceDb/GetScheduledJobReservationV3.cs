using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetScheduledJobReservationV3(string jobName) : IBindValue
{
    public object Bind() => new object?[] { jobName };
}
