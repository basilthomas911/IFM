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
    public async Task ReadAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var mockDataReader = Substitute.For<ICsvDataReader>();
        var reader = new CsvObjectDataReader<CsvJsonTestEntity>(mockDataReader);

        // Act
        Func<Task> act = () => reader.ReadAsync();

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }
}
