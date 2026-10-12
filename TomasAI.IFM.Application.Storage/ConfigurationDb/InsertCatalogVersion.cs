using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertCatalogVersion(string Json, string Hash, DateTime CreatedUtc, string CreatedBy) : IBindValue
{
    public object Bind() => Values(Text(Json), Text(Hash), TimestampTz(CreatedUtc), Text(CreatedBy));
}
