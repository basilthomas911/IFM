using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Json;

public class JsonObjectDataReaderTests
{
    [Fact]
    public void ReadAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var mockDataReader = Substitute.For<IJsonDataReader>();
        var reader = new JsonObjectDataReader<CsvJsonTestEntity>(mockDataReader);

        // Act
        Func<Task> act = () => reader.ReadAsync();

        // Assert
        act.Should().Throw<NotImplementedException>();
    }
}
