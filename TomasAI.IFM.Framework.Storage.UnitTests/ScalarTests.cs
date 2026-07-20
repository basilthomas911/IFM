using System;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ScalarTests
{
    [Fact]
    public void CreateScalarWithInt()
    {
        // Arrange
        var expected = 42;

        // Act
        var scalar = new Scalar<int>(expected);

        // Assert
        scalar.Value.Should().Be(expected);
    }

    [Fact]
    public void CreateScalarWithDouble()
    {
        // Arrange
        var expected = 3.14;

        // Act
        var scalar = new Scalar<double>(expected);

        // Assert
        scalar.Value.Should().Be(expected);
    }

    [Fact]
    public void CreateScalarWithBool()
    {
        // Arrange & Act
        var scalar = new Scalar<bool>(true);

        // Assert
        scalar.Value.Should().BeTrue();
    }

    [Fact]
    public void CreateScalarWithDateTime()
    {
        // Arrange
        var dt = new DateTime(2024, 1, 15);

        // Act
        var scalar = new Scalar<DateTime>(dt);

        // Assert
        scalar.Value.Should().Be(dt);
    }

    [Fact]
    public void CreateScalarWithGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var scalar = new Scalar<Guid>(guid);

        // Assert
        scalar.Value.Should().Be(guid);
    }

    [Fact]
    public void CreateScalarWithDecimal()
    {
        // Arrange
        var expected = 99.99m;

        // Act
        var scalar = new Scalar<decimal>(expected);

        // Assert
        scalar.Value.Should().Be(expected);
    }

    [Fact]
    public void CreateScalarWithDefaultInt()
    {
        // Arrange & Act
        var scalar = new Scalar<int>(default);

        // Assert
        scalar.Value.Should().Be(0);
    }

    [Fact]
    public void ScalarEqualityOk()
    {
        // Arrange
        var scalar1 = new Scalar<int>(42);
        var scalar2 = new Scalar<int>(42);

        // Act & Assert
        scalar1.Should().Be(scalar2);
    }

    [Fact]
    public void ScalarInequalityOk()
    {
        // Arrange
        var scalar1 = new Scalar<int>(42);
        var scalar2 = new Scalar<int>(99);

        // Act & Assert
        scalar1.Should().NotBe(scalar2);
    }

    [Fact]
    public void ScalarToStringOk()
    {
        // Arrange
        var scalar = new Scalar<int>(42);

        // Act
        var str = scalar.ToString();

        // Assert
        str.Should().NotBeNull();
        str.Should().Contain("42");
    }

    [Fact]
    public void ScalarGetHashCodeEqualForSameValue()
    {
        // Arrange
        var scalar1 = new Scalar<int>(42);
        var scalar2 = new Scalar<int>(42);

        // Act & Assert
        scalar1.GetHashCode().Should().Be(scalar2.GetHashCode());
    }

    [Fact]
    public void ScalarGetHashCodeDifferentForDifferentValue()
    {
        // Arrange
        var scalar1 = new Scalar<int>(42);
        var scalar2 = new Scalar<int>(99);

        // Act & Assert
        scalar1.GetHashCode().Should().NotBe(scalar2.GetHashCode());
    }
}
