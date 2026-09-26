using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

internal readonly record struct DeleteOptionPricerDevice(int DeviceId, string DeviceName) : IBindValue
{
    public object Bind() => new object?[] { DeviceId, DeviceName };
}

internal readonly record struct DeleteSpreadDistribution(int TradeId, DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { TradeId, ValueDate };
}

internal readonly record struct DeleteSpreadDistributionJob(int OrderId, int TradeId, DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { OrderId, TradeId, ValueDate };
}

internal readonly record struct DeleteSpreadDistributionJobs(int OrderId, int TradeId) : IBindValue
{
    public object Bind() => new object?[] { OrderId, TradeId };
}

internal readonly record struct GetSpreadDistributionJobs(int OrderId, int TradeId) : IBindValue
{
    public object Bind() => new object?[] { OrderId, TradeId };
}

internal readonly record struct GetSpreadDistribution(
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry) : IBindValue
{
    public object Bind() => new object?[] { TradeId, ValueDate, TradeType, TradeStatus, DaysToExpiry };
}

internal readonly record struct InsertOptionPricerDevice(
    int DeviceId,
    string DeviceName,
    int SpreadPaths,
    int VolatilityPaths,
    int MaxBatchSize,
    string OptionType,
    bool Enabled) : IBindValue
{
    public object Bind() => new object?[]
    {
        DeviceId,
        DeviceName,
        SpreadPaths,
        VolatilityPaths,
        MaxBatchSize,
        OptionType,
        Enabled
    };
}

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
    public object Bind() => new object?[]
    {
        Id,
        TradeId,
        ValueDate,
        TradeType,
        TradeStatus,
        DaysToExpiry,
        ForwardPrice,
        LossProbability,
        LossThreshold,
        LossThresholdCount,
        ShortVolatility,
        LongVolatility,
        ForwardLossRatio,
        CreatedOn
    };
}

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
    public object Bind() => new object?[]
    {
        OrderId,
        TradeId,
        TradeType,
        TradeStatus,
        ValueDate,
        DaysToExpiry,
        JobSubmitted,
        JobStatus,
        JobCompleted,
        JobFailed,
        InProgress,
        LossProbabilityFactor
    };
}

internal readonly record struct UpdateSpreadDistributionJobStatus(
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    string JobStatus,
    DateTime JobCompleted,
    DateTime? JobFailed,
    bool InProgress) : IBindValue
{
    public object Bind() => new object?[]
    {
        JobCompleted,
        JobFailed,
        JobStatus,
        InProgress,
        OrderId,
        TradeId,
        ValueDate
    };
}
