using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;
using NSubstitute;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class DbMapTests
    {
        [Fact]
        public void DbMapCreateOk()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var dbMap = new DbMap<TestParameterEntity>(
                mockRepo,
                "TestParameter",
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Assert
            dbMap.Table.Should().Be("TestParameter");
            dbMap.ParameterTypeMaps.Should().NotBeNull();
            dbMap.ParameterTypeMaps.Should().HaveCount(2);
        }

        [Fact]
        public void DbMapCreateWithNullRepo()
        {
            // Arrange & Act
            var act = () => new DbMap<TestParameterEntity>(
                null,
                "TestParameter",
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreateWithNullParameterTypeMaps()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var act = () => new DbMap<TestParameterEntity>(
                mockRepo,
                "TestParameter",
                default(ObjectDataParameterTypeMap<TestParameterEntity>[]));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreateWithNullPropertyTypeMaps()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var act = () => new DbMap<TestPropertyEntity>(
                mockRepo,
                "TestProperty",
                default(ObjectDataPropertyTypeMap<TestPropertyEntity>[]));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreateWithNullTableName()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var act = () => new DbMap<TestParameterEntity>(
                mockRepo,
                null,
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreateWithEmptyTableName()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var act = () => new DbMap<TestParameterEntity>(
                mockRepo,
                "",
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreateWithBlankTableName()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var act = () => new DbMap<TestParameterEntity>(
                mockRepo,
                "   ",
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbMapCreatePropertyEntity()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var dbMap = new DbMap<TestPropertyEntity>(
                mockRepo,
                "TestProperty",
                new ObjectDataPropertyTypeMap<TestPropertyEntity>[]
                {
                    new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Name, "name"),
                    new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Age, "age")
                });

            // Assert
            dbMap.Table.Should().Be("TestProperty");
            dbMap.PropertyTypeMaps.Should().NotBeNull();
            dbMap.PropertyTypeMaps.Should().HaveCount(2);
        }

        [Fact]
        public void GetPropertyResultTypeMapOk()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();
            var dbMap = new DbMap<TestPropertyEntity>(
                mockRepo,
                "TestProperty",
                new ObjectDataPropertyTypeMap<TestPropertyEntity>[]
                {
                    new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Name, "name"),
                    new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Age, "age")
                });

            // Act
            var resultTypeMap = dbMap.GetResultTypeMap();

            // Assert
            resultTypeMap.Should().NotBeNull();
            resultTypeMap.Should().BeAssignableTo<IEnumerable<IObjectPropertyTypeMap<TestPropertyEntity>>>();
        }

        [Fact]
        public void GetParameterResultTypeMapOk()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();
            var dbMap = new DbMap<TestParameterEntity>(
                mockRepo,
                "TestParameter",
                new ObjectDataParameterTypeMap<TestParameterEntity>[]
                {
                    new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                    new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                });

            // Act
            var resultTypeMap = dbMap.GetResultTypeMap();

            // Assert
            resultTypeMap.Should().NotBeNull();
            resultTypeMap.Should().BeAssignableTo<IEnumerable<IObjectParameterTypeMap<TestParameterEntity>>>();
        }
    }
}
