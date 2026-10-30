using System.Data;



namespace TomasAI.IFM.Shared.Storage;

/// <summary>
/// Describes positional C# values using provider-neutral type metadata when required.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1720", Justification = "Preserves the existing externally referenced identity and named API contract.")]
public sealed record BindParameter
{
    public object? Value { get; init; }
    public DbType? Type { get; init; }
    public bool IsJson { get; init; }
}

public static class BindValues
{
    public static object?[] Values(params object?[] parameters) => parameters;

    public static object Text(string? value) => Create(value, DbType.String);
    public static object Integer(int value) => Create(value, DbType.Int32);
    public static object Integer(int? value) => Create(value, DbType.Int32);
    public static object Bigint(long value) => Create(value, DbType.Int64);
    public static object Bigint(long? value) => Create(value, DbType.Int64);
    public static object Smallint(short value) => Create(value, DbType.Int16);
    public static object Smallint(short? value) => Create(value, DbType.Int16);
    public static object Smallint(byte value) => Create(value, DbType.Int16);
    public static object Smallint(byte? value) => Create(value, DbType.Int16);
    public static object Boolean(bool value) => Create(value, DbType.Boolean);
    public static object Boolean(bool? value) => Create(value, DbType.Boolean);
    public static object Timestamp(DateTime value) => Create(value, DbType.DateTime);
    public static object Timestamp(DateTime? value) => Create(value, DbType.DateTime);
    public static object TimestampTz(DateTime value) => Create(RequireUtc(value), DbType.DateTimeOffset);
    public static object TimestampTz(DateTime? value)
        => Create<DateTime?>(value.HasValue ? RequireUtc(value.Value) : null, DbType.DateTimeOffset);
    public static object Date(DateOnly value) => Create(value, DbType.Date);
    public static object Date(DateOnly? value) => Create(value, DbType.Date);
    public static object Money(decimal value) => Create(value, DbType.Currency);
    public static object Money(decimal? value) => Create(value, DbType.Currency);
    public static object Real(float value) => Create(value, DbType.Single);
    public static object Real(float? value) => Create(value, DbType.Single);
    public static object Double(double value) => Create(value, DbType.Double);
    public static object Double(double? value) => Create(value, DbType.Double);
    public static object Uuid(Guid value) => Create(value, DbType.Guid);
    public static object Uuid(Guid? value) => Create(value, DbType.Guid);
    public static object Bytea(byte[]? value) => Create(value, DbType.Binary);

    public static object Numeric(decimal? value) => new BindParameter { Value = value, Type = DbType.Decimal };
    public static object Time(TimeOnly value) => new BindParameter { Value = value, Type = DbType.Time };
    public static object Json(string? value) => new BindParameter { Value = value, IsJson = true };
    public static object TextArray(string[] value) => value;

    static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("A UTC DateTime is required for an absolute timestamp.", nameof(value));

    static BindParameter Create<T>(T value, DbType type)
        => new BindParameter
        {
            Value = value,
            Type = type
        };
}
