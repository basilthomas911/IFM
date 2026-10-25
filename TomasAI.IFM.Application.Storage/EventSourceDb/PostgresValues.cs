using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
namespace TomasAI.IFM.Application.Storage.EventSourceDb;
/// <summary>Constructs typed JSON bindings without exposing the PostgreSQL SDK to callers.</summary>
public static class PostgresValues
{
    public static DbParameter Jsonb(string json) => new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json };
}
