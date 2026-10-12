using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct VerifyLegacyParameterReference(string Kind, Guid LegacySetId, int LegacyVersion, Guid GenericSetId, string Hash, string Codec) : IBindValue
{
    public object Bind() => Values(Text(Kind), Uuid(LegacySetId), Integer(LegacyVersion), Uuid(GenericSetId), Text(Hash), Text(Codec));
}
