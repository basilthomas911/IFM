namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

public sealed record AwsBackupMonthlyUsage(
    long PrimaryStoredBytes,
    long RecoveryStoredBytes,
    long ReplicatedBytes,
    long RetrievedBytes,
    long EgressBytes,
    long S3Requests,
    long KmsRequests,
    long DynamoDbRequestUnits,
    long AuditEvents,
    decimal RestoreDrillComputeHours);

public sealed record AwsBackupUnitRates(
    decimal PrimaryStorageGbMonth,
    decimal RecoveryStorageGbMonth,
    decimal ReplicationPerGb,
    decimal RetrievalPerGb,
    decimal EgressPerGb,
    decimal S3PerThousandRequests,
    decimal KmsPerThousandRequests,
    decimal DynamoDbPerMillionRequestUnits,
    decimal AuditPerHundredThousandEvents,
    decimal RestoreDrillComputePerHour);

public sealed record AwsBackupMonthlyCost(
    decimal Storage,
    decimal Replication,
    decimal Retrieval,
    decimal Egress,
    decimal Requests,
    decimal Kms,
    decimal DynamoDb,
    decimal Audit,
    decimal RestoreDrills)
{
    public decimal Total => Storage + Replication + Retrieval + Egress + Requests + Kms + DynamoDb + Audit + RestoreDrills;
}

public static class AwsBackupCostModel
{
    const decimal BytesPerGb = 1024m * 1024m * 1024m;

    public static AwsBackupMonthlyCost Estimate(AwsBackupMonthlyUsage usage, AwsBackupUnitRates rates)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(rates);
        if (Values(usage).Any(static value => value < 0) || Rates(rates).Any(static value => value < 0))
            throw new ArgumentOutOfRangeException(nameof(usage), "AWS backup cost inputs cannot be negative.");
        return new AwsBackupMonthlyCost(
            Storage: Gb(usage.PrimaryStoredBytes) * rates.PrimaryStorageGbMonth
                + Gb(usage.RecoveryStoredBytes) * rates.RecoveryStorageGbMonth,
            Replication: Gb(usage.ReplicatedBytes) * rates.ReplicationPerGb,
            Retrieval: Gb(usage.RetrievedBytes) * rates.RetrievalPerGb,
            Egress: Gb(usage.EgressBytes) * rates.EgressPerGb,
            Requests: usage.S3Requests / 1000m * rates.S3PerThousandRequests,
            Kms: usage.KmsRequests / 1000m * rates.KmsPerThousandRequests,
            DynamoDb: usage.DynamoDbRequestUnits / 1_000_000m * rates.DynamoDbPerMillionRequestUnits,
            Audit: usage.AuditEvents / 100_000m * rates.AuditPerHundredThousandEvents,
            RestoreDrills: usage.RestoreDrillComputeHours * rates.RestoreDrillComputePerHour);
    }

    public static void EnsureWithinBudget(AwsBackupMonthlyCost cost, decimal budgetUsd)
    {
        ArgumentNullException.ThrowIfNull(cost);
        if (budgetUsd <= 0) throw new ArgumentOutOfRangeException(nameof(budgetUsd));
        if (cost.Total > budgetUsd)
            throw new InvalidOperationException(
                $"Estimated AWS backup monthly cost {cost.Total:F2} USD exceeds the approved {budgetUsd:F2} USD bound.");
    }

    static decimal Gb(long bytes) => bytes / BytesPerGb;

    static decimal[] Values(AwsBackupMonthlyUsage value) =>
    [
        value.PrimaryStoredBytes, value.RecoveryStoredBytes, value.ReplicatedBytes, value.RetrievedBytes,
        value.EgressBytes, value.S3Requests, value.KmsRequests, value.DynamoDbRequestUnits, value.AuditEvents,
        value.RestoreDrillComputeHours
    ];

    static decimal[] Rates(AwsBackupUnitRates value) =>
    [
        value.PrimaryStorageGbMonth, value.RecoveryStorageGbMonth, value.ReplicationPerGb,
        value.RetrievalPerGb, value.EgressPerGb, value.S3PerThousandRequests, value.KmsPerThousandRequests,
        value.DynamoDbPerMillionRequestUnits, value.AuditPerHundredThousandEvents, value.RestoreDrillComputePerHour
    ];
}
