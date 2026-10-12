using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ListCatalogs(short Kind, string AfterCode, int Limit) : IBindValue
{
    public object Bind() => Values(Smallint(Kind), Text(AfterCode), Integer(Limit));
}
