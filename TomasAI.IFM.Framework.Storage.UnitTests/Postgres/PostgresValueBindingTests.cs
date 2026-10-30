using System.Data;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresValueBindingTests
{
    [Fact]
    public void Existing_command_binds_neutral_values_in_order_without_sharing_parameters()
    {
        using var first = new NpgsqlCommand();
        using var second = new NpgsqlCommand();
        object?[] values = [BindValues.Text(null), 42L, new byte[] { 1, 2 }, Array.Empty<string>()];
        PostgresValueBinding.AddParameters(first, values);
        PostgresValueBinding.AddParameters(second, values);
        Assert.Equal(4, first.Parameters.Count);
        Assert.Equal(DBNull.Value, first.Parameters[0].Value);
        Assert.Equal(NpgsqlDbType.Text, first.Parameters[0].NpgsqlDbType);
        Assert.Equal(42L, first.Parameters[1].Value);
        Assert.Equal(NpgsqlDbType.Bytea, first.Parameters[2].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Text, first.Parameters[3].NpgsqlDbType);
        Assert.NotSame(first.Parameters[0], second.Parameters[0]);
        Assert.IsType<BindParameter>(values[0]);
    }

    [Fact]
    public void Named_parameters_and_bulk_audit_arrays_preserve_types()
    {
        using var command = new NpgsqlCommand();
        PostgresValueBinding.AddNamedParameter(command, "operation", BindValues.Uuid(null));
        Assert.Equal("operation", command.Parameters[0].ParameterName);
        Assert.Equal(NpgsqlDbType.Uuid, command.Parameters[0].NpgsqlDbType);
        object?[] arrays = [Array.Empty<byte[]>(), Array.Empty<short>(), Array.Empty<Guid>(), Array.Empty<long>()];
        PostgresValueBinding.AddParameters(command, arrays);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Bytea, command.Parameters[1].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Smallint, command.Parameters[2].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Uuid, command.Parameters[3].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Bigint, command.Parameters[4].NpgsqlDbType);
    }

    [Fact]
    public void Single_array_value_is_not_expanded_into_multiple_parameters()
    {
        using var command = new NpgsqlCommand();
        PostgresValueBinding.AddParameter(command, new[] { "ES", "NQ" });
        PostgresValueBinding.AddParameter(command, null);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(new[] { "ES", "NQ" }, command.Parameters[0].Value);
        Assert.Equal(DBNull.Value, command.Parameters[1].Value);
    }

    [Fact]
    public void Scylla_keeps_ordinary_values_and_unwraps_neutral_metadata()
    {
        var values = new[] { "ES", "NQ" };
        Assert.Same(values, TomasAI.IFM.Framework.Storage.ScyllaDb.ScyllaDbObjectDataRepositoryProvider.UnwrapValue(values));
        Assert.Equal(12, TomasAI.IFM.Framework.Storage.ScyllaDb.ScyllaDbObjectDataRepositoryProvider.UnwrapValue(BindValues.Integer(12)));
        Assert.Null(TomasAI.IFM.Framework.Storage.ScyllaDb.ScyllaDbObjectDataRepositoryProvider.UnwrapValue(BindValues.Text(null)));
    }
    [Fact]
    public void Ordinary_values_and_arrays_keep_their_values()
    {
        object?[] values = [12, 9L, 1.25m, "text", null, new byte[] { 1, 2 }, new[] { "ES", "NQ" }, new DateOnly(2026, 10, 30)];
        var parameters = PostgresValueBinding.CreateMany(values);
        Assert.Equal(values.Length, parameters.Length);
        for (var i = 0; i < values.Length; i++) Assert.Equal(values[i] ?? DBNull.Value, parameters[i].Value);
    }
    [Fact]
    public void Neutral_metadata_preserves_json_currency_and_typed_nulls()
    {
        Assert.Equal(NpgsqlDbType.Jsonb, PostgresValueBinding.Create(BindValues.Json("{}" )).NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Money, PostgresValueBinding.Create(BindValues.Money(1.25m)).NpgsqlDbType);
        var nullable = PostgresValueBinding.Create(BindValues.Bigint((long?)null));
        Assert.Equal(NpgsqlDbType.Bigint, nullable.NpgsqlDbType);
        Assert.Equal(DBNull.Value, nullable.Value);
        Assert.Equal(NpgsqlDbType.TimestampTz, PostgresValueBinding.Create(BindValues.TimestampTz(DateTime.UtcNow)).NpgsqlDbType);
    }
    [Fact]
    public void Parameters_are_fresh_for_each_command()
    {
        var value = BindValues.Integer(4);
        Assert.NotSame(PostgresValueBinding.Create(value), PostgresValueBinding.Create(value));
    }

    [Fact]
    public void Wall_clock_and_absolute_timestamps_remain_distinct_including_nulls()
    {
        var wallClock = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = DateTime.SpecifyKind(wallClock, DateTimeKind.Utc);
        Assert.Equal(NpgsqlDbType.Timestamp, PostgresValueBinding.Create(BindValues.Timestamp(wallClock)).NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Timestamp, PostgresValueBinding.Create(BindValues.Timestamp(null)).NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, PostgresValueBinding.Create(BindValues.TimestampTz(null)).NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Timestamp, PostgresValueBinding.Create(wallClock).NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, PostgresValueBinding.Create(utc).NpgsqlDbType);
        Assert.Throws<ArgumentException>(() => BindValues.TimestampTz(wallClock));
    }
}
