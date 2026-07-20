using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataPropertyTypeMapTests
    {
        [Fact]
        public void CreateObjectDataPropertyTypeMapOk()
        {
            // Arrange & Act
            var odPropTypeMap = new ObjectDataPropertyTypeMap<TestPropertyEntity>(
                e => e.Age, "age");

            // Assert
            odPropTypeMap.PropertyName.Should().Be("Age");
            odPropTypeMap.FieldName.Should().Be("age");
        }

        [Fact]
        public void CreateObjectDataPropertyTypeMapWithNullPropertyExpression()
        {
            // Arrange & Act
            var act = () => new ObjectDataPropertyTypeMap<TestPropertyEntity>(null, "age");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataPropertyTypeMapWithNullFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Age, null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataPropertyTypeMapWithEmptyFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Age, "");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataPropertyTypeMapWithBlankFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataPropertyTypeMap<TestPropertyEntity>(e => e.Age, "   ");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
