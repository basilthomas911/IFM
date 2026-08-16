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
            var connectionString = @"Data Source = https://example.invalid/sanitized-yield-fixture.csv";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.Uri.Should().NotBeNull();
            dro.Uri.AbsoluteUri.Should().Be(@"https://example.invalid/sanitized-yield-fixture.csv");
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
            var connectionString = @"Data Source = https://example.com/data.csv; ApiKey = test-key";

            // Act
            var dro = new DataReaderOptions(connectionString);

            // Assert
            dro.ApiKey.Should().Be("test-key");
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
