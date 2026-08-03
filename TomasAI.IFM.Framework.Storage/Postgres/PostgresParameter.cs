using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace TomasAI.IFM.Framework.Storage.Postgres;

/// <summary>
/// Creates strongly typed, unnamed Npgsql parameters for native PostgreSQL positional placeholders.
/// </summary>
public static class PostgresParameter
{
    public static NpgsqlParameter[] Values(params NpgsqlParameter[] parameters) => parameters;

    public static NpgsqlParameter Text(string? value) => Create(value, NpgsqlDbType.Text);
    public static NpgsqlParameter Integer(int value) => Create(value, NpgsqlDbType.Integer);
    public static NpgsqlParameter Integer(int? value) => Create(value, NpgsqlDbType.Integer);
    public static NpgsqlParameter Bigint(long value) => Create(value, NpgsqlDbType.Bigint);
    public static NpgsqlParameter Bigint(long? value) => Create(value, NpgsqlDbType.Bigint);
    public static NpgsqlParameter Smallint(short value) => Create(value, NpgsqlDbType.Smallint);
    public static NpgsqlParameter Smallint(short? value) => Create(value, NpgsqlDbType.Smallint);
    public static NpgsqlParameter Smallint(byte value) => Create(value, NpgsqlDbType.Smallint);
    public static NpgsqlParameter Smallint(byte? value) => Create(value, NpgsqlDbType.Smallint);
    public static NpgsqlParameter Boolean(bool value) => Create(value, NpgsqlDbType.Boolean);
    public static NpgsqlParameter Boolean(bool? value) => Create(value, NpgsqlDbType.Boolean);
    public static NpgsqlParameter Timestamp(DateTime value) => Create(value, NpgsqlDbType.Timestamp);
    public static NpgsqlParameter Timestamp(DateTime? value) => Create(value, NpgsqlDbType.Timestamp);
    public static NpgsqlParameter Date(DateOnly value) => Create(value, NpgsqlDbType.Date);
    public static NpgsqlParameter Date(DateOnly? value) => Create(value, NpgsqlDbType.Date);
    public static NpgsqlParameter Money(decimal value) => Create(value, NpgsqlDbType.Money);
    public static NpgsqlParameter Money(decimal? value) => Create(value, NpgsqlDbType.Money);
    public static NpgsqlParameter Real(float value) => Create(value, NpgsqlDbType.Real);
    public static NpgsqlParameter Real(float? value) => Create(value, NpgsqlDbType.Real);
    public static NpgsqlParameter Double(double value) => Create(value, NpgsqlDbType.Double);
    public static NpgsqlParameter Double(double? value) => Create(value, NpgsqlDbType.Double);
    public static NpgsqlParameter Uuid(Guid value) => Create(value, NpgsqlDbType.Uuid);
    public static NpgsqlParameter Uuid(Guid? value) => Create(value, NpgsqlDbType.Uuid);
    public static NpgsqlParameter Bytea(byte[]? value) => Create(value, NpgsqlDbType.Bytea);

    static NpgsqlParameter Create<T>(T value, NpgsqlDbType type)
        => new NpgsqlParameter<T>
        {
            NpgsqlDbType = type,
            Direction = ParameterDirection.Input,
            TypedValue = value
        };
}
