using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataRepositoryParameterTests
{
    [Fact]
    public void CreateParameterForPostgres()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create("System.Data.Postgres");

        // Assert
        parameter.Should().NotBeNull();
        parameter.Should().BeOfType<PostgresObjectDataRepositoryParameter>();
    }

    [Fact]
    public void CreateParameterForCassandraReturnsNull()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create("System.Data.Cassandra");

        // Assert
        parameter.Should().BeNull();
    }

    [Fact]
    public void CreateParameterForScyllaReturnsNull()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create("System.Data.Scylla");

        // Assert
        parameter.Should().BeNull();
    }

    [Fact]
    public void PostgresParameterPropertyOk()
    {
        // Arrange
        var repoParam = new PostgresObjectDataRepositoryParameter();

        // Act
        var dbParam = repoParam.Parameter;

        // Assert
        dbParam.Should().NotBeNull();
        dbParam.Should().BeOfType<Npgsql.NpgsqlParameter>();
    }
}
