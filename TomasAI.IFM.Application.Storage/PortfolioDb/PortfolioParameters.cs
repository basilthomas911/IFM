using Npgsql;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PortfolioParameters(NpgsqlParameter[] Values) : IBindValue
{
    public object Bind() => Values;
}
