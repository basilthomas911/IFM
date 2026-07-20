using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;
using NSubstitute;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class DbMapCollectionTests
    {
        [Fact]
        public void CreateDbMapCollectionOk()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();

            // Act
            var dbMapCollection = new DbMapCollection<TestParameterEntity>(mockRepo);

            // Assert
            dbMapCollection.Should().NotBeNull();
        }

        [Fact]
        public void CreateDbMapCollectionWithNullRepo()
        {
            // Arrange & Act
            var act = () => new DbMapCollection<TestParameterEntity>(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetDbMapOk()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();
            var dbMapCollection = new DbMapCollection<TestParameterEntity>(mockRepo);
            var dbMap = new DbMap<TestParameterEntity>(
                 mockRepo,
                 "TestParameter",
                 new ObjectDataParameterTypeMap<TestParameterEntity>[]
                 {
                     new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0),
                     new ObjectDataParameterTypeMap<TestParameterEntity>("age", 1)
                 });

            // Act
            dbMapCollection.SetDbMap(dbMap);

            // Assert (no exception thrown)
            dbMapCollection.Should().NotBeNull();
        }

        [Fact]
        public void SetDbMapWithNullDbMap()
        {
            // Arrange
            var mockRepo = Substitute.For<IObjectRepository>();
            var dbMapCollection = new DbMapCollection<TestParameterEntity>(mockRepo);

            // Act
            var act = () => dbMapCollection.SetDbMap(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
