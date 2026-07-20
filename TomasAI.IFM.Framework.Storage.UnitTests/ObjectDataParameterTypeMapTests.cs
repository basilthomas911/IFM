using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataParameterTypeMapTests
    {
        [Fact]
        public void CreateObjectDataParameterTypeMapOk()
        {
            // Arrange & Act
            var odParamTypeMap = new ObjectDataParameterTypeMap<TestParameterEntity>("name", 0);

            // Assert
            odParamTypeMap.FieldName.Should().Be("name");
            odParamTypeMap.Index.Should().Be(0);
            odParamTypeMap.AsTypeOf.Should().BeNull();
        }

        [Fact]
        public void CreateObjectDataParameterTypeMapWithNullFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataParameterTypeMap<TestParameterEntity>(null, 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataParameterTypeMapWithEmptyFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataParameterTypeMap<TestParameterEntity>("", 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataParameterTypeMapWithBlankFieldName()
        {
            // Arrange & Act
            var act = () => new ObjectDataParameterTypeMap<TestParameterEntity>("   ", 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataParameterTypeMapWithNegativeIndex()
        {
            // Arrange & Act
            var act = () => new ObjectDataParameterTypeMap<TestParameterEntity>("name", -1);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
