using System.Data;
using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Postgres;

/// <summary>Owns conversion of application C# values to PostgreSQL driver parameters.</summary>
public static class PostgresValueBinding
{
    /// <summary>Binds positional C# values on an existing command, including enlisted transactions.</summary>
    public static void AddParameters(DbCommand command, object?[] values)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(values);
        command.Parameters.AddRange(CreateMany(values));
    }

    /// <summary>Binds one value; a CLR array remains one database array parameter.</summary>
    public static void AddParameter(DbCommand command, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.Add(Create(value));
    }

    /// <summary>Binds a named C# value without exposing driver parameters to the caller.</summary>
    public static void AddNamedParameter(DbCommand command, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.Add(Named(name, value));
    }

    /// <summary>Writes a COPY row using the same provider-owned type mapping as commands.</summary>
    public static async Task WriteRowAsync(NpgsqlBinaryImporter importer, object?[] values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(values);
        await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
        foreach (var value in values)
        {
            var parameter = Create(value);
            if (parameter.Value is DBNull)
                await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
            else
                await importer.WriteAsync(parameter.Value, parameter.NpgsqlDbType, cancellationToken).ConfigureAwait(false);
        }
    }

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
        // Resolve common CLR types here so ordinary commands and COPY share a mapping,
        // even before a connection's type mapper has been initialized.
        var resolvedType = value switch
        {
            DateTime timestamp => timestamp.Kind == DateTimeKind.Utc ? NpgsqlDbType.TimestampTz : NpgsqlDbType.Timestamp,
            byte[] => NpgsqlDbType.Bytea,
            Array array => ArrayType(array.GetType().GetElementType()!),
            null or DBNull => NpgsqlDbType.Unknown,
            _ => ScalarType(value.GetType())
        };
        if (resolvedType != NpgsqlDbType.Unknown) inferred.NpgsqlDbType = resolvedType;
        return inferred;
    }

    static NpgsqlDbType ArrayType(Type elementType)
    {
        var type = elementType == typeof(byte[]) ? NpgsqlDbType.Bytea : ScalarType(elementType);
        return type == NpgsqlDbType.Unknown ? type : NpgsqlDbType.Array | type;
    }

    static NpgsqlDbType ScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return NpgsqlDbType.Text;
        if (type == typeof(short) || type == typeof(byte)) return NpgsqlDbType.Smallint;
        if (type == typeof(int)) return NpgsqlDbType.Integer;
        if (type == typeof(long)) return NpgsqlDbType.Bigint;
        if (type == typeof(float)) return NpgsqlDbType.Real;
        if (type == typeof(double)) return NpgsqlDbType.Double;
        if (type == typeof(decimal)) return NpgsqlDbType.Numeric;
        if (type == typeof(bool)) return NpgsqlDbType.Boolean;
        if (type == typeof(Guid)) return NpgsqlDbType.Uuid;
        if (type == typeof(DateOnly)) return NpgsqlDbType.Date;
        if (type == typeof(TimeOnly)) return NpgsqlDbType.Time;
        if (type == typeof(TimeSpan)) return NpgsqlDbType.Interval;
        if (type == typeof(DateTimeOffset)) return NpgsqlDbType.TimestampTz;
        return NpgsqlDbType.Unknown;
    }
    public static NpgsqlParameter Named(string name, object? value)
    {
        var parameter = Create(value);
        parameter.ParameterName = name;
        return parameter;
    }
    public static NpgsqlParameter[] CreateMany(object?[] values) => values.Select(Create).ToArray();
}
