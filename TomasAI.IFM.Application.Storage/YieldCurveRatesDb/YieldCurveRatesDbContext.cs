using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
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

    /// <summary>
    /// initialize yield curve rates view model mappings
    /// </summary>
    /// <param name="model"></param>
    public override void OnCreateModel(DbModel<YieldCurveRatesDbContext> model)
    {
        YieldCurveRates = model.Map(e => e.YieldCurveRates)
            .Parameters(e =>
                e.Set(o => o.Date)
                 .Set(o => o.Month1)
                 .Set(o => o.Month2)
                 .Set(o => o.Month3)
                 .Set(o => o.Month6)
                 .Set(o => o.Year1)
                 .Set(o => o.Year2)
                 .Set(o => o.Year3)
                 .Set(o => o.Year5)
                 .Set(o => o.Year7)
                 .Set(o => o.Year10)
                 .Set(o => o.Year20)
                 .Set(o => o.Year30)
            );
    }

    public DbMap<YieldCurveRateJsonModel> YieldCurveRates { get; private set; }

    /// <summary>
    /// read yield curve rates from external web site
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<YieldCurveRateReadModel>> ReadAsync()
    {
        var yieldCurveRates = await _dbFactory.YieldCurveRatesDb
            .Use(connectionString => new DataReaderOptions(connectionString))
            .ReadAsync<YieldCurveRateJsonModel>();
        return [.. yieldCurveRates.Select(e => e.ToViewModel())];
    }

}
