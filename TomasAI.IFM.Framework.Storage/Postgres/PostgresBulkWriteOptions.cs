using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.Postgres;

/// <summary>
/// Bounds the number of PostgreSQL commands retained in one client-side batch.
/// </summary>
public sealed record PostgresBulkWriteOptions
{
    public const string BatchSizeVariable = "POSTGRES_BULK_BATCH_SIZE";

    public int BatchSize { get; init; } = 256;

    internal static PostgresBulkWriteOptions FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(BatchSizeVariable);
        if (string.IsNullOrWhiteSpace(value))
            return new PostgresBulkWriteOptions();

        if (!int.TryParse(value, out var batchSize) || batchSize is < 1 or > 4096)
        {
            throw new StorageException(
                $"{BatchSizeVariable} must be an integer between 1 and 4096.");
        }

        return new PostgresBulkWriteOptions { BatchSize = batchSize };
    }
}
