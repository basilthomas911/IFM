using System;
using FluentAssertions;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public sealed class PostgresParameterTests
{
    [Fact]
    public void TimestampTz_preserves_utc_and_supports_null()
    {
        var timestamp = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        var value = PostgresParameter.TimestampTz(timestamp);
        var nullable = PostgresParameter.TimestampTz(null);

        value.NpgsqlDbType.Should().Be(NpgsqlDbType.TimestampTz);
        value.Value.Should().Be(timestamp);
        nullable.NpgsqlDbType.Should().Be(NpgsqlDbType.TimestampTz);
        nullable.Value.Should().BeNull();
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void TimestampTz_rejects_non_utc_values(DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(new DateTime(2026, 8, 9, 12, 0, 0), kind);

        var create = () => PostgresParameter.TimestampTz(value);

        create.Should().Throw<ArgumentException>();
    }
}
