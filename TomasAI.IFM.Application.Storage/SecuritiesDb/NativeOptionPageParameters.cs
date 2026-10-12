using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionPageParameters(string Symbol, DateOnly? Month, string? OptionType,
    double? Strike, string? ContractId, int Limit) : IBindValue
{
    public object Bind() => new NpgsqlParameter[] { Text(Symbol), Date(Month), Text(OptionType), Double(Strike), Text(ContractId), Integer(Limit) };
}
