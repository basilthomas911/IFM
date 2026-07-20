using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Json;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Json;

public class JsonDataReaderTests
{
    static IStringReader CreateMockReader(string json)
    {
        var mock = Substitute.For<IStringReader>();
        mock.ReadToEndAsync().Returns(Task.FromResult(json));
        return mock;
    }

    static JsonDataReader<CsvJsonTestEntity> CreateReaderWithData()
    {
        var json = @"[
            {""Name"":""Alice"",""Age"":30,""IsActive"":true,""Score"":95.5,""Balance"":1234.56,""Rating"":4.5,""BigNumber"":9999999999,""SmallNumber"":100,""CreatedDate"":""2024-06-15"",""UniqueId"":""d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a""},
            {""Name"":""Bob"",""Age"":25,""IsActive"":false,""Score"":80.0,""Balance"":500.00,""Rating"":3.2,""BigNumber"":1234567890,""SmallNumber"":50,""CreatedDate"":""2023-01-01"",""UniqueId"":""00000000-0000-0000-0000-000000000000""}
        ]";
        var sr = CreateMockReader(json);
        return new JsonDataReader<CsvJsonTestEntity>(sr);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ValidJson_SetsFieldCount()
    {
        // Arrange & Act
        var reader = CreateReaderWithData();

        // Assert
        reader.FieldCount.Should().Be(10);
    }

    [Fact]
    public void Constructor_EmptyJson_FieldCountStillMatchesProperties()
    {
        // Arrange
        var sr = CreateMockReader("[]");

        // Act
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);

        // Assert
        reader.FieldCount.Should().Be(10);
    }

    [Fact]
    public void Constructor_NullOrWhitespaceJson_NoRows()
    {
        // Arrange
        var sr = CreateMockReader("   ");

        // Act
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);

        // Assert
        reader.FieldCount.Should().Be(10);
        reader.Read().Should().BeFalse();
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

        // Act & Assert
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void Read_EmptyArray_ReturnsFalse()
    {
        // Arrange
        var sr = CreateMockReader("[]");
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);

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
    public void GetInt32_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var json = @"[{""Name"":""Alice"",""Age"":30,""IsActive"":true,""Score"":95.5,""Balance"":1234.56,""Rating"":4.5,""BigNumber"":999,""SmallNumber"":100,""CreatedDate"":""2024-06-15"",""UniqueId"":""d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a""}]";
        var sr = CreateMockReader(json);
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act — parse the Name column (string "Alice") as Int32
        Action act = () => reader.GetInt32(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetBoolean_ReturnsFalse()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();
        reader.Read(); // Bob row

        // Act
        var result = reader.GetBoolean(2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetBoolean_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act — parse the Name column as boolean
        Action act = () => reader.GetBoolean(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetDouble_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetDouble(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetFloat_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetFloat(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetInt16_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetInt16(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetDateTime_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var json = @"[{""Name"":""notadate"",""Age"":30,""IsActive"":true,""Score"":95.5,""Balance"":1234.56,""Rating"":4.5,""BigNumber"":999,""SmallNumber"":100,""CreatedDate"":""2024-06-15"",""UniqueId"":""d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a""}]";
        var sr = CreateMockReader(json);
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);
        reader.Read();

        // Act
        Action act = () => reader.GetDateTime(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
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
    public void GetGuid_InvalidValue_ThrowsInvalidCastException()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetGuid(0);

        // Assert
        act.Should().Throw<InvalidCastException>();
    }

    // --- IsDBNull ---

    [Fact]
    public void IsDBNull_EmptyField_ReturnsTrue()
    {
        // Arrange
        var json = @"[{""Name"":"""",""Age"":30,""IsActive"":true,""Score"":95.5,""Balance"":1234.56,""Rating"":4.5,""BigNumber"":999,""SmallNumber"":100,""CreatedDate"":""2024-06-15"",""UniqueId"":""d3b07384-d113-4ec6-a1c4-3b6c8b5b1c3a""}]";
        var sr = CreateMockReader(json);
        var reader = new JsonDataReader<CsvJsonTestEntity>(sr);
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
        result.Should().Be(0);
    }

    [Fact]
    public void GetOrdinal_InvalidName_ReturnsNegativeOne()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetOrdinal("NonExistent");

        // Assert
        result.Should().Be(-1);
    }

    // --- GetName ---

    [Fact]
    public void GetName_ValidIndex_ReturnsPropertyName()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetName(0);

        // Assert
        result.Should().Be("Name");
    }

    // --- GetFieldType ---

    [Fact]
    public void GetFieldType_ValidIndex_ReturnsCorrectType()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        var result = reader.GetFieldType(1);

        // Assert
        result.Should().Be(typeof(int));
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
        result.TableName.Should().Be("JsonDataReader");
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
        var reader = CreateReaderWithData();
        reader.Close();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var reader = CreateReaderWithData();
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

    // --- ValidIndex edge case ---

    [Fact]
    public void GetString_NegativeIndex_ThrowsIndexOutOfRange()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetString(-1);

        // Assert
        act.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void GetString_IndexBeyondFieldCount_ThrowsIndexOutOfRange()
    {
        // Arrange
        var reader = CreateReaderWithData();
        reader.Read();

        // Act
        Action act = () => reader.GetString(100);

        // Assert
        act.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void GetString_BeforeRead_ThrowsIndexOutOfRange()
    {
        // Arrange
        var reader = CreateReaderWithData();

        // Act
        Action act = () => reader.GetString(0);

        // Assert
        act.Should().Throw<IndexOutOfRangeException>();
    }
}
