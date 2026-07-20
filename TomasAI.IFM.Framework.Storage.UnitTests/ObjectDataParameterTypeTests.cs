using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataParameterTypeTests
    {
        [Fact]
        public void CreateObjectDataParameterTypeOk()
        {
            // Arrange & Act
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Assert
            odParameterType.ParameterTypeMaps.Should().NotBeNull();
            odParameterType.ParameterTypeMaps.Should().BeEmpty();
        }

        [Fact]
        public void SetParameterTypeOk()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            odParameterType.Set("Name", 0);

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(1);
            odParameterType.ParameterTypeMaps.ElementAt(0).FieldName.Should().Be("Name");
        }

        [Fact]
        public void SetParameterTypeWithNullFieldName()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set((string)null, 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetParameterTypeWithBlankFieldName()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set("   ", 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetParameterTypeWithEmptyFieldName()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set("", 0);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetParameterTypeWithIndexGreaterThanMaxParameters()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set("Name", 3);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetParameterTypeWithDefaultIndexValues()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var param1 = odParameterType.Set("Name");
            var param2 = odParameterType.Set("Age");

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(2);
            param1.ParameterTypeMaps.ElementAt(0).Index.Should().Be(0);
            param2.ParameterTypeMaps.ElementAt(1).Index.Should().Be(1);
        }

        [Fact]
        public void SetParameterTypeWithAsTypeOfFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var param1 = odParameterType.Set("Name", e => e.Value);

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(1);
            param1.ParameterTypeMaps.ElementAt(0).AsTypeOf.Should().NotBeNull();
        }

        [Fact]
        public void SetParameterTypeWithNullAsTypeOfFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set("Name", null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetParameterTypeWithStringPropertyFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            odParameterType.Set(e => e.Name);

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(1);
            odParameterType.ParameterTypeMaps.ElementAt(0).FieldName.Should().Be("Name");
        }

        [Fact]
        public void SetParameterTypeWithInvalidStringPropertyFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set(e => "NotAProperty");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetParameterTypeWithIntegerPropertyFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            odParameterType.Set(e => e.Age);

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(1);
            odParameterType.ParameterTypeMaps.ElementAt(0).FieldName.Should().Be("Age");
        }

        [Fact]
        public void SetParameterTypeWithInvalidIntegerPropertyFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var act = () => odParameterType.Set(e => 1);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetParameterTypeWithIntegerPropertyAsTypeOfFunctionParameter()
        {
            // Arrange
            var odParameterType = new ObjectDataParameterType<TestParameterEntity>();

            // Act
            var param1 = odParameterType.Set(e => e.Age, o => o.As<string>());

            // Assert
            odParameterType.ParameterTypeMaps.Should().HaveCount(1);
            param1.ParameterTypeMaps.ElementAt(0).AsTypeOf.Should().NotBeNull();
        }
    }
}
