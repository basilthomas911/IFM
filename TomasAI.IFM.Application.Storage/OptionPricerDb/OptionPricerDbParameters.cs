using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>Ordered PostgreSQL bindings for DeleteOptionPricerDevice.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteOptionPricerDevice(int DeviceId, string DeviceName) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(DeviceId), Text(DeviceName));
}

/// <summary>Ordered PostgreSQL bindings for DeleteSpreadDistribution.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSpreadDistribution(int TradeId, DateOnly ValueDate) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(TradeId), Date(ValueDate));
}


/// <summary>Ordered PostgreSQL bindings for DeleteSpreadDistributionJobs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSpreadDistributionJobs(int OrderId, int TradeId) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(OrderId), Integer(TradeId));
}

/// <summary>Ordered PostgreSQL bindings for GetSpreadDistribution.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSpreadDistribution(
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(TradeId), Date(ValueDate), Text(TradeType), Text(TradeStatus), Integer(DaysToExpiry));
}

/// <summary>Ordered PostgreSQL bindings for GetSpreadDistributionJobs.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSpreadDistributionJobs(int OrderId, int TradeId) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(OrderId), Integer(TradeId));
}

/// <summary>Ordered PostgreSQL bindings for InsertOptionPricerDevice.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionPricerDevice(
    int DeviceId,
    string DeviceName,
    int SpreadPaths,
    int VolatilityPaths,
    int MaxBatchSize,
    string OptionType,
    bool Enabled) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(DeviceId), Text(DeviceName), Integer(SpreadPaths), Integer(VolatilityPaths), Integer(MaxBatchSize), Text(OptionType), Boolean(Enabled));
}

/// <summary>Ordered PostgreSQL bindings for InsertSpreadDistribution.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSpreadDistribution(
    long Id,
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry,
    double ForwardPrice,
    double LossProbability,
    double ShortVolatility,
    double LongVolatility,
    decimal LossThreshold,
    int LossThresholdCount,
    double ForwardLossRatio,
    DateTime CreatedOn) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Bigint(Id), Integer(TradeId), Date(ValueDate), Text(TradeType), Text(TradeStatus), Integer(DaysToExpiry), Double(ForwardPrice), Double(LossProbability), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = LossThreshold }, Integer(LossThresholdCount), Double(ShortVolatility), Double(LongVolatility), Double(ForwardLossRatio), TimestampTz(CreatedOn.AsOptionPricerUtc()));
}

/// <summary>Ordered PostgreSQL bindings for InsertSpreadDistributionJob.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSpreadDistributionJob(
    int OrderId,
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry,
    DateTime JobSubmitted,
    string JobStatus,
    DateTime? JobCompleted,
    DateTime? JobFailed,
    bool InProgress,
    double LossProbabilityFactor) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(Integer(OrderId), Integer(TradeId), Text(TradeType), Text(TradeStatus), Date(ValueDate), Integer(DaysToExpiry), TimestampTz(JobSubmitted.AsOptionPricerUtc()), Text(JobStatus), TimestampTz(JobCompleted.AsOptionPricerUtc()), TimestampTz(JobFailed.AsOptionPricerUtc()), Boolean(InProgress), Double(LossProbabilityFactor));
}

/// <summary>Ordered PostgreSQL bindings for UpdateSpreadDistributionJobStatus.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateSpreadDistributionJobStatus(
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    string JobStatus,
    DateTime JobCompleted,
    DateTime? JobFailed,
    bool InProgress) : IBindValue
{
    /// <summary>Gets the ordered PostgreSQL parameter values.</summary>
    public object Bind() => Values(TimestampTz(JobCompleted.AsOptionPricerUtc()), TimestampTz(JobFailed.AsOptionPricerUtc()), Text(JobStatus), Boolean(InProgress), Integer(OrderId), Integer(TradeId), Date(ValueDate));
}
