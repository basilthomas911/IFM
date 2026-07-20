using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;

/// <summary>
/// sequence id database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class SequenceIdDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger) 
    : ObjectDataRepository<SequenceIdDbContext>(connectionSettings[SequenceIdDbConnection], logger), ISequenceIdDbContext
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override SequenceIdDbContext Database => this;

    public const string SequenceIdDbConnection = "SequenceIdDbConnection";
    static Func<IObjectMapReader<Scalar<long>>, long>? MapToSequenceId;



    public override void OnCreateModel(DbModel<SequenceIdDbContext> model)
    {
        MapToSequenceId ??= (o => o.Get(e => e.Value));
    }

    /// <summary>
    /// get next sequence id
    /// </summary>
    /// <param name="sequenceName"></param>
    /// <returns></returns>
    public async Task<long> GetNextSequenceIdAsync(SequenceName sequenceName)
        => await _dbFactory.SequenceIdDb
                .Use(SequenceIdDbSql.GetNextSequenceId)
                .SetParameters(new { sequenceName = sequenceName.ToStringFast() })
                .ExecuteScalarAsync(MapToSequenceId!);
    
}
