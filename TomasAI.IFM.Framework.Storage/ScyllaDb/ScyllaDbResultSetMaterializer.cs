using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Provides static methods for materializing ScyllaDB query results into strongly typed collections.
/// </summary>
/// <remarks>This class offers functionality to efficiently convert a ScyllaDB RowSet into a pooled, read-only
/// buffer of value types using a user-supplied data mapping function. It is designed for high-performance scenarios
/// where minimizing allocations and maximizing throughput are important. All methods are static and
/// thread-safe.</remarks>
public static class ScyllaDbResultSetMaterializer
{
    public static PooledReadOnlyBuffer<TResult> GetResultSet<TResult>(
        RowSet rowSet,
        Func<IObjectDataRecord, TResult> dataMapper)
        where TResult : struct
    {
        ArgumentNullException.ThrowIfNull(rowSet);
        ArgumentNullException.ThrowIfNull(dataMapper);

        var record = rowSet.ToObjectDataRecord();
        var builder = new PooledBufferBuilder<TResult>(capacity: 16);

        try
        {
            foreach (var row in rowSet)
                builder.Add(dataMapper(record.SetRow(row)));

            return builder.MoveToResult();
        }
        catch
        {
            builder.Dispose();
            throw;
        }
    }
}