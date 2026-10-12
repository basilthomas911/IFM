using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertParameterSetVersion(Guid SetId, int Version, string Hash, short Status, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version), Text(Hash), Smallint(Status), Text(Body));
}
