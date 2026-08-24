using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

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
    static long MapToSequenceId(IObjectDataRecord o) => o.GetLong(0);

    /// <summary>
    /// get the configured PostgreSQL sequence increment
    /// </summary>
    public async Task<long> GetSequenceAllocationSizeAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
            .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetSequenceAllocationSize)}", SequenceIdDbSql.GetSequenceAllocationSize)
            .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))
            .ExecuteScalarAsync(MapToSequenceId, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// get the highest sequence id reserved by PostgreSQL
    /// </summary>
    public async Task<long> GetCurrentSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
            .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetCurrentSequenceId)}", SequenceIdDbSql.GetCurrentSequenceId)
            .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))
            .ExecuteScalarAsync(MapToSequenceId, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// get next sequence id
    /// </summary>
    /// <param name="sequenceName"></param>
    /// <returns></returns>
    public async Task<long> GetNextSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
                .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetNextSequenceId)}", SequenceIdDbSql.GetNextSequenceId)
                .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))
                .ExecuteScalarAsync(MapToSequenceId, cancellationToken)
                .ConfigureAwait(false);
    
}
