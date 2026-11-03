using Cassandra;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Extension methods for Cassandra <see cref="RowSet"/> providing <see cref="IObjectDataRecord"/> access.
/// </summary>
public static class RowSetExtension
{

    extension(RowSet rowSet)
    {
        /// <summary>
        /// Creates a reusable <see cref="IObjectDataRecord"/> adapter for the specified <see cref="RowSet"/>.
        /// Call <see cref="ScyllaDbDataRecord.SetRow"/> to advance to each row during iteration.
        /// </summary>
        public ScyllaDbDataRecord ToObjectDataRecord()

        {
            ArgumentNullException.ThrowIfNull(rowSet);
            return new(rowSet.Columns);
        }
    }
}
