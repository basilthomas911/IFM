using Npgsql;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PortfolioParameters(NpgsqlParameter[] Values) : IBindValue
{
    public object Bind() => Values;
}

/// <summary>Marks a positional value for PostgreSQL JSONB binding.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PortfolioJson(string Value);
