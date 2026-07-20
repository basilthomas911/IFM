using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Csv;

public class CsvDataReaderTests
{
    static IStringReader CreateMockReader(params string[] lines)
    {
        var mock = Substitute.For<IStringReader>();
        mock.ReadLinesAsync().Returns(ToAsyncEnumerable(lines));
        return mock;
    }

    static async IAsyncEnumerable<string> ToAsyncEnumerable(
        string[] items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    static CsvDataReader<CsvJsonTestEntity> CreateReaderWithData()
    {
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,9999999999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a",
            "Bob,25,false,80.0,500.00,3.2,1234567890,50,2023-01-01,00000000-0000-0000-0000-000000000000");
        return new CsvDataReader<CsvJsonTestEntity>(sr);
    }

    // --- Constructor & FieldCount ---

    [Fact]
    public void Constructor_WithHeader_SetsFieldCount()
    {
        // Arrange & Act
        var reader = CreateReaderWithData();

        // Assert
        reader.FieldCount.Should().Be(10);
    }

    [Fact]
    public void Constructor_NoRows_FieldCountMatchesProperties()
    {
        // Arrange
        var sr = CreateMockReader("Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId");

        // Act
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);

        // Assert
        reader.FieldCount.Should().Be(10);
    }

    // --- Properties ---

    [Fact]
    public void Depth_ReturnsZero()
    {
        var reader = CreateReaderWithData();
        reader.Depth.Should().Be(0);
    }

    [Fact]
    public void IsClosed_ReturnsFalse()
    {
        var reader = CreateReaderWithData();
        reader.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void RecordsAffected_ReturnsZero()
    {
        var reader = CreateReaderWithData();
        reader.RecordsAffected.Should().Be(0);
    }

    // --- Read ---

    [Fact]
    public void Read_ReturnsTrueForEachRow()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act & Assert — pad at _rows[0] skipped, Alice at _rows[1], Bob at _rows[2]
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void Read_NoRows_ReturnsFalse()
    {
        // Arrange
        var sr = CreateMockReader("Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);

        // Act & Assert
        reader.Read().Should().BeFalse();
    }

    // --- GetString ---

    [Fact]
    public void GetString_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetString(0);

        // Assert
        result.Should().Be("Alice");
    }

    // --- GetInt32 ---

    [Fact]
    public void GetInt32_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetInt32(1);

        // Assert
        result.Should().Be(30);
    }

    [Fact]
    public void GetInt32_InvalidValue_ReturnsZero()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,notanumber,true,95.5,1234.56,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetInt32(1);

        // Assert
        result.Should().Be(0);
    }

    // --- GetBoolean ---

    [Fact]
    public void GetBoolean_ReturnsTrue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetBoolean(2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetBoolean_ReturnsFalseForFalseValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read(); // Alice
        reader.Read(); // Bob

        // Act
        var result = reader.GetBoolean(2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetBoolean_InvalidValue_ReturnsFalse()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,notabool,95.5,1234.56,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetBoolean(2);

        // Assert
        result.Should().BeFalse();
    }

    // --- GetDouble ---

    [Fact]
    public void GetDouble_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetDouble(3);

        // Assert
        result.Should().Be(95.5);
    }

    [Fact]
    public void GetDouble_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,bad,1234.56,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetDouble(3);

        // Assert
        result.Should().Be(double.MinValue);
    }

    // --- GetDecimal ---

    [Fact]
    public void GetDecimal_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetDecimal(4);

        // Assert
        result.Should().Be(1234.56m);
    }

    [Fact]
    public void GetDecimal_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,bad,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetDecimal(4);

        // Assert
        result.Should().Be(decimal.MinValue);
    }

    // --- GetFloat ---

    [Fact]
    public void GetFloat_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetFloat(5);

        // Assert
        result.Should().Be(4.5f);
    }

    [Fact]
    public void GetFloat_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,bad,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetFloat(5);

        // Assert
        result.Should().Be(float.MinValue);
    }

    // --- GetInt64 ---

    [Fact]
    public void GetInt64_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetInt64(6);

        // Assert
        result.Should().Be(9999999999L);
    }

    [Fact]
    public void GetInt64_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,bad,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetInt64(6);

        // Assert
        result.Should().Be(long.MinValue);
    }

    // --- GetInt16 ---

    [Fact]
    public void GetInt16_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetInt16(7);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void GetInt16_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,999,bad,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetInt16(7);

        // Assert
        result.Should().Be(short.MinValue);
    }

    // --- GetDateTime ---

    [Fact]
    public void GetDateTime_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetDateTime(8);

        // Assert
        result.Should().Be(new DateTime(2024, 6, 15));
    }

    [Fact]
    public void GetDateTime_InvalidValue_ReturnsMinValue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,999,100,not-a-date,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetDateTime(8);

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    // --- GetGuid ---

    [Fact]
    public void GetGuid_ReturnsCorrectValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetGuid(9);

        // Assert
        result.Should().Be(new Guid("d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a"));
    }

    [Fact]
    public void GetGuid_InvalidValue_ReturnsEmpty()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,999,100,2024-06-15,not-a-guid");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.GetGuid(9);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    // --- IsDBNull ---

    [Fact]
    public void IsDBNull_EmptyField_ReturnsTrue()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            ",30,true,95.5,1234.56,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        var result = reader.IsDBNull(0);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDBNull_NonEmptyField_ReturnsFalse()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.IsDBNull(0);

        // Assert
        result.Should().BeFalse();
    }

    // --- GetOrdinal ---

    [Fact]
    public void GetOrdinal_ValidName_ReturnsIndex()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetOrdinal("Name");

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void GetOrdinal_InvalidName_ReturnsNegativeOne()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetOrdinal("NonExistentColumn");

        // Assert
        result.Should().Be(-1);
    }

    // --- GetName ---

    [Fact]
    public void GetName_ValidIndex_ReturnsPropertyName()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetName(0);

        // Assert
        result.Should().Be("Name");
    }

    // --- GetFieldType ---

    [Fact]
    public void GetFieldType_ValidIndex_ReturnsType()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetFieldType(0);

        // Assert
        result.Should().Be(typeof(string));
    }

    // --- GetDataTypeName ---

    [Fact]
    public void GetDataTypeName_ValidIndex_ReturnsTypeName()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetDataTypeName(0);

        // Assert
        result.Should().Be("String");
    }

    // --- GetSchemaTable ---

    [Fact]
    public void GetSchemaTable_ReturnsDataTableWithColumns()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetSchemaTable();

        // Assert
        result.Should().NotBeNull();
        result.TableName.Should().Be("CsvDataReader");
        result.Columns.Count.Should().Be(10);
    }

    // --- GetValue ---

    [Fact]
    public void GetValue_ReturnsTypedValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader.GetValue(1);

        // Assert
        result.Should().Be(30);
    }

    // --- GetValues ---

    [Fact]
    public void GetValues_FillsArray()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();
        var values = new object[10];

        // Act
        var count = reader.GetValues(values);

        // Assert
        count.Should().Be(10);
        values[0].Should().Be("Alice");
    }

    // --- Indexer ---

    [Fact]
    public void IndexerByInt_ReturnsValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader[0];

        // Assert
        result.Should().Be("Alice");
    }

    [Fact]
    public void IndexerByName_ReturnsValue()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        var result = reader["Name"];

        // Assert
        result.Should().Be("Alice");
    }

    // --- Close/Dispose ---

    [Fact]
    public void Close_DoesNotThrow()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act & Assert
        reader.Close();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act & Assert
        reader.Dispose();
    }

    // --- NextResult ---

    [Fact]
    public void NextResult_ThrowsNotImplementedException()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        Action act = () => reader.NextResult();

        // Assert
        act.Should().Throw<NotImplementedException>();
    }

    // --- GetByte / GetBytes / GetChar / GetChars / GetData ---

    [Fact]
    public void GetByte_ThrowsNotImplementedException()
    {
        var reader = CreateReaderWithData();
        reader.Read();
        Action act = () => reader.GetByte(0);
        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void GetBytes_ThrowsNotImplementedException()
    {
        var reader = CreateReaderWithData();
        reader.Read();
        Action act = () => reader.GetBytes(0, 0, null, 0, 0);
        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void GetChar_ThrowsNotImplementedException()
    {
        var reader = CreateReaderWithData();
        reader.Read();
        Action act = () => reader.GetChar(0);
        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void GetChars_ThrowsNotImplementedException()
    {
        var reader = CreateReaderWithData();
        reader.Read();
        Action act = () => reader.GetChars(0, 0, null, 0, 0);
        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void GetData_ThrowsNotImplementedException()
    {
        var reader = CreateReaderWithData();
        reader.Read();
        Action act = () => reader.GetData(0);
        act.Should().Throw<NotImplementedException>();
    }

    // --- Quoted CSV values ---

    [Fact]
    public void Constructor_QuotedHeaderValues_ParsedCorrectly()
    {
        // Arrange
        var sr = CreateMockReader(
            "\"Name\",\"Age\",\"IsActive\",\"Score\",\"Balance\",\"Rating\",\"BigNumber\",\"SmallNumber\",\"CreatedDate\",\"UniqueId\"",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30,true,95.5,1234.56,4.5,999,100,2024-06-15,d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);

        // Act
        reader.Read();
        var result = reader.GetString(0);

        // Assert
        result.Should().Be("Alice");
    }

    // --- Multiple rows ---

    [Fact]
    public void Read_MultipleRows_ReturnsCorrectData()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act & Assert
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Alice");

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Bob");

        reader.Read().Should().BeFalse();
    }

    // --- Fewer columns than header ---

    [Fact]
    public void Constructor_FewerColumnsThanHeader_PadsWithEmpty()
    {
        // Arrange
        var sr = CreateMockReader(
            "Name,Age,IsActive,Score,Balance,Rating,BigNumber,SmallNumber,CreatedDate,UniqueId",
            "_pad,0,false,0,0,0,0,0,2000-01-01,00000000-0000-0000-0000-000000000000",
            "Alice,30");
        var reader = new CsvDataReader<CsvJsonTestEntity>(sr);

        // Act
        reader.Read();
        var isEmpty = reader.IsDBNull(9);

        // Assert
        isEmpty.Should().BeTrue();
    }
}
