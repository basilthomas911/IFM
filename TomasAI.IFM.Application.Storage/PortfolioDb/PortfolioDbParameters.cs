using Npgsql;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

internal readonly record struct PortfolioJson(string Value);

internal readonly record struct PortfolioParameters(NpgsqlParameter[] Values) : IBindValue
{
    public object Bind() => Values;
}
