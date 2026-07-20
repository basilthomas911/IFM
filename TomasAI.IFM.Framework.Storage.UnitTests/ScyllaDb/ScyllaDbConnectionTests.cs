using System;
using System.Data;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public class ScyllaDbConnectionTests
{
    // --- ScyllaDbObjectDataRepositoryConnection ---

    [Fact]
    public void As_ThrowsNotImplementedException()
    {
        // Arrange
        var connection = new ScyllaDbObjectDataRepositoryConnection();

        // Act
        Action act = () => connection.As<IDbConnection>("any-connection-string");

        // Assert
        act.Should().Throw<NotImplementedException>()
            .WithMessage("*create ScyllaDbConnection directly in provider code*");
    }

    [Fact]
    public void As_EmptyConnectionString_StillThrowsNotImplementedException()
    {
        // Arrange
        var connection = new ScyllaDbObjectDataRepositoryConnection();

        // Act
        Action act = () => connection.As<IDbConnection>(string.Empty);

        // Assert
        act.Should().Throw<NotImplementedException>();
    }
}
