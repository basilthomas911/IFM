using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Csv;

public class CsvObjectDataReaderTests
{
    [Fact]
    public void ReadAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var mockDataReader = Substitute.For<ICsvDataReader>();
        var reader = new CsvObjectDataReader<CsvJsonTestEntity>(mockDataReader);

        // Act
        Func<Task> act = () => reader.ReadAsync();

        // Assert
        act.Should().Throw<NotImplementedException>();
    }
}
