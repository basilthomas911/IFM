using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.SqlServer;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataRepositoryParameterTests
{
    [Fact]
    public void CreateParameterForSqlServer()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create(null);

        // Assert
        parameter.Should().NotBeNull();
        parameter.Should().BeOfType<SqlServerObjectDataRepositoryParameter>();
    }

    [Fact]
    public void CreateParameterForSqlServerWithDefaultProvider()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create("SomeUnknownProvider");

        // Assert
        parameter.Should().NotBeNull();
        parameter.Should().BeOfType<SqlServerObjectDataRepositoryParameter>();
    }

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
    public void CreateParameterForEmptyProvider()
    {
        // Arrange & Act
        var parameter = ObjectDataRepositoryParameter.Create("");

        // Assert
        parameter.Should().NotBeNull();
        parameter.Should().BeOfType<SqlServerObjectDataRepositoryParameter>();
    }

    [Fact]
    public void SqlServerParameterPropertyOk()
    {
        // Arrange
        var repoParam = new SqlServerObjectDataRepositoryParameter();

        // Act
        var dbParam = repoParam.Parameter;

        // Assert
        dbParam.Should().NotBeNull();
        dbParam.Should().BeOfType<Microsoft.Data.SqlClient.SqlParameter>();
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
