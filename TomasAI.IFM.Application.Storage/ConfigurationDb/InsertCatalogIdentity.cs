using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertCatalogIdentity(short Kind, Guid Id, string Code, DateTime CreatedUtc, string CreatedBy) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Uuid(Id), Text(Code), TimestampTz(CreatedUtc), Text(CreatedBy));
}
