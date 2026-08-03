using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.YieldCurveRatesDb;

/// <summary>
/// yield curve rates database
/// </summary>
/// <remarks>
/// yield curve rates database constructor
/// </remarks>
/// <param name="connectionSettings"></param>
public class YieldCurveRatesDbContext(
    IDbConnectionSettings connectionSettings, 
    IDbContextFactory dbFactory, 
    ILogger<DbProvider> logger)
    : ObjectDataRepository<YieldCurveRatesDbContext>(connectionSettings[YieldCurveRatesDbConnection], logger  ), IYieldCurveRatesDbContext
{
    public const string YieldCurveRatesDbConnection = "YieldCurveRatesDbConnection";
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override YieldCurveRatesDbContext Database => this;

    static YieldCurveRateJsonModel MapYieldCurveRate(IObjectDataRecord row)
        => new(
            row.GetDateTime(0),
            row.GetDouble(1),
            row.GetDouble(2),
            row.GetDouble(3),
            row.GetDouble(4),
            row.GetDouble(5),
            row.GetDouble(6),
            row.GetDouble(7),
            row.GetDouble(8),
            row.GetDouble(9),
            row.GetDouble(10),
            row.GetDouble(11),
            row.GetDouble(12));

    /// <summary>
    /// read yield curve rates from external web site
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<YieldCurveRateReadModel>> ReadAsync()
    {
        var yieldCurveRates = await _dbFactory.YieldCurveRatesDb
            .Use(connectionString => new DataReaderOptions(connectionString))
            .ReadAsync(MapYieldCurveRate);
        return [.. yieldCurveRates.Select(e => e.ToViewModel())];
    }

}
