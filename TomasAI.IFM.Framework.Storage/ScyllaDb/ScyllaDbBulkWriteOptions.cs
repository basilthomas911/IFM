using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Controls the client-side working set used for ordinary ScyllaDB multi-row writes.
/// </summary>
public sealed record ScyllaDbBulkWriteOptions
{
    public const string MaxConcurrencyVariable = "SCYLLADB_BULK_MAX_CONCURRENCY";
    public const string BoundedCapacityVariable = "SCYLLADB_BULK_BOUNDED_CAPACITY";

    public int MaxConcurrency { get; init; } = 32;
    public int BoundedCapacity { get; init; } = 64;

    internal static ScyllaDbBulkWriteOptions FromEnvironment()
    {
        var maxConcurrency = ReadPositiveInt(MaxConcurrencyVariable, 32, 1024);
        var boundedCapacity = ReadPositiveInt(BoundedCapacityVariable, maxConcurrency * 2, 8192);
        if (boundedCapacity < maxConcurrency)
        {
            throw new StorageException(
                $"{BoundedCapacityVariable} must be greater than or equal to {MaxConcurrencyVariable}.");
        }

        return new ScyllaDbBulkWriteOptions
        {
            MaxConcurrency = maxConcurrency,
            BoundedCapacity = boundedCapacity
        };
    }

    static int ReadPositiveInt(string variableName, int defaultValue, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, out var parsed) || parsed < 1 || parsed > maximum)
        {
            throw new StorageException(
                $"{variableName} must be an integer between 1 and {maximum}.");
        }

        return parsed;
    }
}
