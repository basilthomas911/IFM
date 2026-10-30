using System.Data;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresValueBindingTests
{
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
}
