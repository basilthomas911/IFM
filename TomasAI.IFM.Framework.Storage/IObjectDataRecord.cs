namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides index-based typed access to column values in a data row.
/// </summary>
/// <remarks>
/// This interface enables implementations to read column values directly from the underlying data source
/// (e.g., a Cassandra <c>Row</c> or ADO.NET <c>IDataRecord</c>) without requiring an
/// intermediate <c>object[]</c> allocation or boxing for value types.
/// </remarks>
public interface IObjectDataRecord
{
    /// <summary>
    /// Determines whether the value at the specified column index is null.
    /// </summary>
    bool IsNull(int index);

    /// <summary>
    /// Determines whether a collection value has no elements. Implementations must
    /// return <see langword="false"/> when the value cannot be read, so callers that
    /// use an empty collection as a readiness condition fail closed.
    /// </summary>
    bool IsCollectionEmpty(int index) => false;

    /// <summary>
    /// Retrieves the 16-bit integer value at the specified column index.
    /// </summary>
    short GetShort(int index);

    /// <summary>
    /// Retrieves the integer value at the specified column index.
    /// </summary>
    int GetInt(int index);

    /// <summary>
    /// Retrieves the float value at the specified column index.
    /// </summary>
    float GetFloat(int index);

    /// <summary>
    /// Retrieves the double value at the specified column index.
    /// </summary>
    double GetDouble(int index);

    /// <summary>
    /// Retrieves the decimal value at the specified column index.
    /// </summary>
    decimal GetDecimal(int index);

    /// <summary>
    /// Retrieves the boolean value at the specified column index.
    /// </summary>
    bool GetBool(int index);

    /// <summary>
    /// Retrieves the long value at the specified column index.
    /// </summary>
    long GetLong(int index);

    /// <summary>
    /// Retrieves the DateTime value at the specified column index.
    /// </summary>
    DateTime GetDateTime(int index);

    /// <summary>
    /// Retrieves the DateOnly value at the specified column index.
    /// </summary>
    DateOnly GetDateOnly(int index);

    /// <summary>
    /// Retrieves the TimeOnly value at the specified column index.
    /// </summary>
    TimeOnly GetTimeOnly(int index);

    /// <summary>
    /// Retrieves the TimeSpan value at the specified column index.
    /// </summary>
    TimeSpan GetTimeSpan(int index);

    /// <summary>
    /// Retrieves the enum value of type <typeparamref name="T"/> at the specified column index.
    /// </summary>
    T GetEnum<T>(int index) where T : struct, Enum;

    /// <summary>
    /// Retrieves the Guid value at the specified column index.
    /// </summary>
    Guid GetGuid(int index);

    /// <summary>
    /// Retrieves the string value at the specified column index.
    /// </summary>
    string GetString(int index);

    /// <summary>
    /// Retrieves the byte array value at the specified column index.
    /// </summary>
    byte[] GetBytes(int index);
}
