using System;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class DataReaderOptionsTests
    {
        [Fact]
        public void CreateDataReaderOptionsOk()
        {
            // Arrange
            var connectionString = @"Data Source = https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.Uri.Should().NotBeNull();
            dro.Uri.AbsoluteUri.Should().Be(@"https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ");
            dro.DataReaderType.Should().Be(DataReaderType.Csv);
            dro.DataSourceType.Should().Be(DataSourceType.Uri);
        }

        [Fact]
        public void CreateDataReaderOptionsWithNullConnectionString()
        {
            // Arrange
            var connectionString = default(string);

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.Should().NotBeNull();
            dro.DataReaderType.Should().Be(DataReaderType.Csv);
            dro.DataSourceType.Should().Be(DataSourceType.Uri);
            dro.ApiKey.Should().Be(string.Empty);
        }

        [Fact]
        public void CreateDataReaderOptionsWithEmptyConnectionString()
        {
            // Arrange & Act
            var dro = new DataReaderOptions("");

            // Assert
            dro.DataReaderType.Should().Be(DataReaderType.Csv);
            dro.DataSourceType.Should().Be(DataSourceType.Uri);
        }

        [Fact]
        public void CreateDataReaderOptionsWithJsonDataReaderType()
        {
            // Arrange
            var connectionString = @"Data Source = https://example.com/data.json; DataReaderType = JSON";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.Uri.Should().NotBeNull();
            dro.DataReaderType.Should().Be(DataReaderType.JSON);
        }

        [Fact]
        public void CreateDataReaderOptionsWithApiKey()
        {
            // Arrange
            var connectionString = @"Data Source = https://example.com/data.csv; ApiKey = MySecretKey123";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.ApiKey.Should().Be("MySecretKey123");
        }

        [Fact]
        public void CreateDataReaderOptionsWithoutApiKeyDefaultsToEmpty()
        {
            // Arrange
            var connectionString = @"Data Source = https://example.com/data.csv";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.ApiKey.Should().Be(string.Empty);
        }

        [Fact]
        public void CreateDataReaderOptionsDefaultsCsvWhenNoDataReaderType()
        {
            // Arrange
            var connectionString = @"Data Source = https://example.com/data.csv";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.DataReaderType.Should().Be(DataReaderType.Csv);
        }

        [Fact]
        public void CreateDataReaderOptionsWithMultipleOptions()
        {
            // Arrange
            var connectionString = @"Data Source = https://example.com/api; DataReaderType = JSON; ApiKey = TestKey";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.Uri.Should().NotBeNull();
            dro.DataReaderType.Should().Be(DataReaderType.JSON);
            dro.ApiKey.Should().Be("TestKey");
            dro.DataSourceType.Should().Be(DataSourceType.Uri);
        }
    }
}
