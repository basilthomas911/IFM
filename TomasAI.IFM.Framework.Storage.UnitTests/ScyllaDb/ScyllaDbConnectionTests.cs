using System;
using System.Data;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using Cassandra;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public class ScyllaDbConnectionTests
{
    // --- ScyllaDbObjectDataRepositoryConnection ---

    [Fact]
    public void As_ThrowsNotSupportedException()
    {
        // Arrange
        var connection = new ScyllaDbObjectDataRepositoryConnection();

        // Act
        Action act = () => connection.As<IDbConnection>("any-connection-string");

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*create ScyllaDbConnection directly in provider code*");
    }

    [Fact]
    public void As_EmptyConnectionString_StillThrowsNotSupportedException()
    {
        // Arrange
        var connection = new ScyllaDbObjectDataRepositoryConnection();

        // Act
        Action act = () => connection.As<IDbConnection>(string.Empty);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void QueryOptions_UseReplicaSafeLocalConsistency()
    {
        var options = ScyllaDbConnection.CreateQueryOptions();

        options.GetConsistencyLevel().Should().Be(ConsistencyLevel.LocalQuorum);
        options.GetSerialConsistencyLevel().Should().Be(ConsistencyLevel.LocalSerial);
    }
}
