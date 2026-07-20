using System;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresObjectDataReaderTests
{
    // --- ReadAsync ---

    [Fact]
    public async Task ReadAsync_DelegatesToDbDataReader()
    {
        // Arrange
        var mockDbReader = Substitute.For<DbDataReader>();
        mockDbReader.ReadAsync().Returns(Task.FromResult(true));
        var reader = new PostgresObjectDataReader<object>(mockDbReader);

        // Act
        var result = await reader.ReadAsync();

        // Assert
        result.Should().BeTrue();
        await mockDbReader.Received(1).ReadAsync();
    }

    [Fact]
    public async Task ReadAsync_ReturnsFalseWhenNoMoreRows()
    {
        // Arrange
        var mockDbReader = Substitute.For<DbDataReader>();
        mockDbReader.ReadAsync().Returns(Task.FromResult(false));
        var reader = new PostgresObjectDataReader<object>(mockDbReader);

        // Act
        var result = await reader.ReadAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_MultipleCallsDelegateEachTime()
    {
        // Arrange
        var mockDbReader = Substitute.For<DbDataReader>();
        mockDbReader.ReadAsync().Returns(
            Task.FromResult(true),
            Task.FromResult(true),
            Task.FromResult(false));
        var reader = new PostgresObjectDataReader<object>(mockDbReader);

        // Act & Assert
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeFalse();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_AcceptsDbDataReader()
    {
        // Arrange
        var mockDbReader = Substitute.For<DbDataReader>();

        // Act
        var reader = new PostgresObjectDataReader<object>(mockDbReader);

        // Assert
        reader.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_AcceptsNullResultTypeMap()
    {
        // Arrange
        var mockDbReader = Substitute.For<DbDataReader>();

        // Act
        var reader = new PostgresObjectDataReader<object>(mockDbReader, null);

        // Assert
        reader.Should().NotBeNull();
    }
}
