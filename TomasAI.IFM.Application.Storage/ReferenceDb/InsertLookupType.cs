using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertLookupType(string lookupTypeName, string shortCode, int orderId, string description, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { lookupTypeName, shortCode, orderId, description, createdOn, createdBy };
}
