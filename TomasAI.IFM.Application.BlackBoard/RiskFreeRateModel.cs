using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// risk free rate blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class RiskFreeRateModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.RiskFreeRate}";
    readonly IRedisCache _redisCache = redisCache;

    /// <summary>
    /// return cached risk free rate
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="getRiskFreeRateAsync"></param>
    /// <returns>risk free rate</returns>
    public async ValueTask<double> GetAsync(DateOnly valueDate, Func<DateOnly, ValueTask<double>> getRiskFreeRateAsync)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? Convert.ToDouble(value)
            : await GetRiskFreeRateValueAsync();

        /// <summary>
        /// Retrieves the risk free rate value from the source and caches it.
        /// </summary>
        /// <returns>The risk free rate value.</returns>
        async Task<double> GetRiskFreeRateValueAsync()
        {
            var riskFreeRate = await getRiskFreeRateAsync(valueDate);
            _redisCache.Set(key, $"{riskFreeRate}", TimeSpan.FromMinutes(60));
            return riskFreeRate;
        }
    }

    /// <summary>
    /// Retrieves the cached double value associated with the specified date.
    /// </summary>
    /// <remarks>If no value is found in the cache for the given date, the method returns 0. The method uses a
    /// serialized key based on the cache name and date to access the cache.</remarks>
    /// <param name="valueDate">The date for which to retrieve the cached value.</param>
    /// <returns>The cached double value for the specified date if present; otherwise, 0.</returns>
    public double Get(DateOnly valueDate)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? Convert.ToDouble(value)
            : default;
    }

    /// <summary>
    /// clear risk free rate cache
    /// </summary>
    /// <param name="valueDate"></param>
    public void Clear(DateOnly valueDate)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        _redisCache.Set(key, string.Empty);
    }
}
