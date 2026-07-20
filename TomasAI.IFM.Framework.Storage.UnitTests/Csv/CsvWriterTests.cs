using System;
using System.IO;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Csv;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Csv;

public class CsvWriterTests : IDisposable
{
    readonly string _tempFilePath;

    public CsvWriterTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"csvwriter_test_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }

    // --- Happy path ---

    [Fact]
    public void WriteToCsv_WritesHeaderAndData()
    {
        // Arrange
        var data = new List<SimpleWriteEntity>
        {
            new() { Id = 1, Name = "Alice", Value = 99.5 }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines.Length.Should().Be(2);
        lines[0].Should().Be("Id,Name,Value");
        lines[1].Should().Contain("1");
        lines[1].Should().Contain("'Alice'");
        lines[1].Should().Contain("99.5");
    }

    [Fact]
    public void WriteToCsv_MultipleRows_WritesAll()
    {
        // Arrange
        var data = new List<SimpleWriteEntity>
        {
            new() { Id = 1, Name = "Alice", Value = 10.0 },
            new() { Id = 2, Name = "Bob", Value = 20.0 },
            new() { Id = 3, Name = "Charlie", Value = 30.0 }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines.Length.Should().Be(4); // header + 3 rows
    }

    [Fact]
    public void WriteToCsv_EmptyCollection_WritesHeaderOnly()
    {
        // Arrange
        var data = new List<SimpleWriteEntity>();

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines.Length.Should().Be(1);
        lines[0].Should().Be("Id,Name,Value");
    }

    // --- String formatting ---

    [Fact]
    public void WriteToCsv_StringProperty_WrappedInQuotes()
    {
        // Arrange
        var data = new List<SimpleWriteEntity>
        {
            new() { Id = 1, Name = "TestValue", Value = 0 }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines[1].Should().Contain("'TestValue'");
    }

    // --- DateTime formatting ---

    [Fact]
    public void WriteToCsv_DateTimeProperty_FormattedWithRoundTrip()
    {
        // Arrange
        var data = new List<DateTimeWriteEntity>
        {
            new() { Created = new DateTime(2024, 6, 15, 10, 30, 0) }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines[1].Should().Contain("2024-06-15");
    }

    // --- TimeOnly formatting ---

    [Fact]
    public void WriteToCsv_TimeOnlyProperty_FormattedWithRoundTrip()
    {
        // Arrange
        var data = new List<TimeOnlyWriteEntity>
        {
            new() { StartTime = new TimeOnly(14, 30, 0) }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines[1].Should().Contain("14:30");
    }

    // --- Int property (raw value, no quotes) ---

    [Fact]
    public void WriteToCsv_IntProperty_NotWrappedInQuotes()
    {
        // Arrange
        var data = new List<SimpleWriteEntity>
        {
            new() { Id = 42, Name = "Test", Value = 0 }
        };

        // Act
        CsvWriter.WriteToCsv(data, _tempFilePath);

        // Assert
        var lines = File.ReadAllLines(_tempFilePath);
        lines[1].Should().StartWith("42,");
    }

    // --- Test helper entities ---

    public class SimpleWriteEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public class DateTimeWriteEntity
    {
        public DateTime Created { get; set; }
    }

    public class TimeOnlyWriteEntity
    {
        public TimeOnly StartTime { get; set; }
    }
}
