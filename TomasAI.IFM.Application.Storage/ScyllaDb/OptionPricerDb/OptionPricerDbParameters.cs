using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;

internal readonly record struct DeleteOptionPricerDevice(int deviceId, string deviceName) : IBindValue
{
    public object Bind() => new { deviceId, deviceName };
}
internal readonly record struct DeleteSpreadDistribution(int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { tradeId, valueDate };
}
internal readonly record struct DeleteSpreadDistributionJobs(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetSpreadDistributionJobs(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct GetSpreadDistribution(int tradeId, string tradeType, string tradeStatus, DateOnly valueDate, int daysToExpiry) : IBindValue
{
    public object Bind() => new { tradeId, tradeType, tradeStatus, valueDate, daysToExpiry };
}
internal readonly record struct InsertOptionPricerDevice(int deviceId, string deviceName, int spreadPaths, int volatilityPaths, int maxBatchSize, string optionType, bool enabled) : IBindValue
{
    public object Bind() => new { deviceId, deviceName, spreadPaths, volatilityPaths, maxBatchSize, optionType, enabled };
}
internal readonly record struct InsertSpreadDistribution(long id, int tradeId, string tradeType, string tradeStatus, DateOnly valueDate, int daysToExpiry, double forwardPrice, double lossProbability, double shortVolatility, double longVolatility, decimal lossThreshold, int lossThresholdCount, double forwardLossRatio, DateTime createdOn) : IBindValue
{
    public object Bind() => new { id, tradeId, tradeType, tradeStatus, valueDate, daysToExpiry, forwardPrice, lossProbability, shortVolatility, longVolatility, lossThreshold, lossThresholdCount, forwardLossRatio, createdOn };
}
internal readonly record struct InsertSpreadDistributionJob(int orderId, int tradeId, string tradeType, string tradeStatus, DateOnly valueDate, int daysToExpiry, DateTime jobSubmitted, string jobStatus, DateTime? jobCompleted, DateTime? jobFailed, bool inProgress, double lossProbabilityFactor) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeType, tradeStatus, valueDate, daysToExpiry, jobSubmitted, jobStatus, jobCompleted, jobFailed, inProgress, lossProbabilityFactor };
}
internal readonly record struct UpdateSpreadDistributionJobStatus(int orderId, int tradeId, string jobStatus, DateTime jobCompleted, DateTime? jobFailed, bool inProgress) : IBindValue
{
    public object Bind() => new { orderId, tradeId, jobStatus, jobCompleted, jobFailed, inProgress };
}

