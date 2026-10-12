using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertParameterSet(Guid SetId, string ComponentCode, string Name, string Description, long Revision) : IBindValue
{
    public object Bind() => Values(Uuid(SetId), Text(ComponentCode), Text(Name), Text(Description), Bigint(Revision));
}
