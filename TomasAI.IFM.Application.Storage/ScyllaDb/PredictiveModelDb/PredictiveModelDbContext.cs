using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;

/// <summary>
/// Predictive model database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class PredictiveModelDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger) 
    : ObjectDataRepository<PredictiveModelDbContext>(connectionSettings[PredictiveModelDbConnection], logger), IPredictiveModelDbContext
{
    public const string PredictiveModelDbConnection = "PredictiveModelDbConnection";
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override PredictiveModelDbContext Database => this;

    public IPredictiveModelDbReadContext DbReader => this;
    public IPredictiveModelDbWriteContext DbWriter => this;

    /// <summary>
    /// Create predictive model database tables 
    /// </summary>
    /// <returns></returns>
    public async Task CreatePredictiveModelDbTablesAsync()
    {
        var db = _dbFactory.PredictiveModelDb;
        var queuedCommands = new List<object>();
        queuedCommands.AddRange([
            db.Use(PredictiveModelDbCql.CreateFuturesItiTrendDeltaModelTable).QueueCommand(),
            db.Use(PredictiveModelDbCql.CreateFuturesItiTrendDeltaDataTable).QueueCommand(),
            db.Use(PredictiveModelDbCql.CreateFuturesItiTrendClassModelTable).QueueCommand(),
            db.Use(PredictiveModelDbCql.CreateFuturesItiTrendClassDataTable).QueueCommand()
        ]);
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

}