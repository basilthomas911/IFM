using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PredictiveModelDb;

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

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override PredictiveModelDbContext Database => this;

    public IPredictiveModelDbReadContext DbReader => this;
    public IPredictiveModelDbWriteContext DbWriter => this;

}
