using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb.Parameters;

/// <summary>Typed canonical ITI observation query inputs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeItiDayMode(string contractId, DateOnly valueDate, string mode, long afterSequenceId) : IBindValue
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with the element type.")]
    public object Bind() => new NpgsqlParameter[] { Text(contractId), Date(valueDate), Text(mode), Bigint(afterSequenceId) };
}
