using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteReferenceProjectionStateV3(DateTime completedOn, string projectionName, Guid generation) : IBindValue
{
    public object Bind() => new object?[] { completedOn, projectionName, generation };
}
