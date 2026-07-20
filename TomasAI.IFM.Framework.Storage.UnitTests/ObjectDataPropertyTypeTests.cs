using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataPropertyTypeTests
    {
        [Fact]
        public void CreateObjectDataPropertyTypeOk()
        {
            // Arrange & Act
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Assert
            odPropertyType.PropertyTypeMaps.Should().BeEmpty();
        }

        [Fact]
        public void SetPropertyTypeOk()
        {
            // Arrange
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Act
            odPropertyType.Set(e => e.Name, "name");

            // Assert
            odPropertyType.PropertyTypeMaps.Should().HaveCount(1);
            odPropertyType.PropertyTypeMaps.ElementAt(0).FieldName.Should().Be("name");
            odPropertyType.PropertyTypeMaps.ElementAt(0).PropertyName.Should().Be("Name");
        }

        [Fact]
        public void SetPropertyTypeWithNullPropertyExpression()
        {
            // Arrange
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Act
            var act = () => odPropertyType.Set(null, "name");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetPropertyTypeWithNullFieldName()
        {
            // Arrange
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Act
            var act = () => odPropertyType.Set(e => e.Name, null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetPropertyTypeWithEmptyFieldName()
        {
            // Arrange
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Act
            var act = () => odPropertyType.Set(e => e.Name, "");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetPropertyTypeWithBlankFieldName()
        {
            // Arrange
            var odPropertyType = new ObjectDataPropertyType<TestPropertyEntity>();

            // Act
            var act = () => odPropertyType.Set(e => e.Name, "   ");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
