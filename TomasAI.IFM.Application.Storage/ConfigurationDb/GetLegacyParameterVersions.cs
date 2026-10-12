using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLegacyParameterVersions(Guid? SetId, int Version, int Offset) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Integer(Version), Integer(Offset));
}
