using System.Data;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Postgres;

/// <summary>Owns conversion of application C# values to PostgreSQL driver parameters.</summary>
public static class PostgresValueBinding
{
    public static NpgsqlParameter Create(object? value)
    {
        if (value is NpgsqlParameter existing) return existing.Clone();
        if (value is BindParameter typed)
        {
            var parameter = new NpgsqlParameter { Value = typed.Value ?? DBNull.Value };
            if (typed.IsJson) parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
            else if (typed.Type is { } type) parameter.DbType = type;
            return parameter;
        }
        var inferred = new NpgsqlParameter { Value = value ?? DBNull.Value };
        // Array element mapping belongs to this provider, including empty arrays.
        var arrayType = value switch
        {
            string[] => NpgsqlDbType.Text,
            int[] => NpgsqlDbType.Integer,
            long[] => NpgsqlDbType.Bigint,
            Guid[] => NpgsqlDbType.Uuid,
            decimal[] => NpgsqlDbType.Numeric,
            _ => NpgsqlDbType.Unknown
        };
        if (arrayType != NpgsqlDbType.Unknown) inferred.NpgsqlDbType = NpgsqlDbType.Array | arrayType;
        return inferred;
    }
    public static NpgsqlParameter Named(string name, object? value)
    {
        var parameter = Create(value);
        parameter.ParameterName = name;
        return parameter;
    }
    public static NpgsqlParameter[] CreateMany(object?[] values) => values.Select(Create).ToArray();
}
