using System;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataTypeMapperTests
{
    [Fact]
    public void CreateObjectDataTypeMapperOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();

        // Act
        var mapper = new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, "TestTable");

        // Assert
        mapper.Should().NotBeNull();
    }

    [Fact]
    public void CreateObjectDataTypeMapperWithNullRepo()
    {
        // Arrange & Act
        var act = () => new ObjectDataTypeMapper<TestPropertyEntity>(null, "TestTable");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataTypeMapperWithEmptyTableName()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();

        // Act
        var act = () => new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataTypeMapperWithNullTableName()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();

        // Act
        var act = () => new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataTypeMapperWithWhitespaceTableName()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();

        // Act
        var act = () => new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PropertiesOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mapper = new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, "TestTable");

        // Act
        var dbMap = mapper.Properties(pt =>
        {
            pt.Set(e => e.Name, "Name");
            pt.Set(e => e.Age, "Age");
        });

        // Assert
        dbMap.Should().NotBeNull();
        mockRepo.Received(1).AddResultTypeMap(Arg.Any<DbMap<TestPropertyEntity>>());
    }

    [Fact]
    public void PropertiesWithNullAction()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mapper = new ObjectDataTypeMapper<TestPropertyEntity>(mockRepo, "TestTable");

        // Act
        var act = () => mapper.Properties(null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParametersOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mapper = new ObjectDataTypeMapper<TestParameterEntity>(mockRepo, "TestTable");

        // Act
        var dbMap = mapper.Parameters(pt =>
        {
            pt.Set("Name", 0);
            pt.Set("Age", 1);
        });

        // Assert
        dbMap.Should().NotBeNull();
        mockRepo.Received(1).AddResultTypeMap(Arg.Any<DbMap<TestParameterEntity>>());
    }

    [Fact]
    public void ParametersWithNullAction()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mapper = new ObjectDataTypeMapper<TestParameterEntity>(mockRepo, "TestTable");

        // Act
        var act = () => mapper.Parameters(null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
