using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetParameterSets(
    string ComponentCode,
    Guid? SetId,
    string AfterName,
    Guid? AfterSetId,
    int AfterVersion,
    int Limit) : IBindValue
{
    public object Bind() => Values(
        Text(ComponentCode), Uuid(SetId), Text(AfterName), Uuid(AfterSetId), Integer(AfterVersion), Integer(Limit));
}
