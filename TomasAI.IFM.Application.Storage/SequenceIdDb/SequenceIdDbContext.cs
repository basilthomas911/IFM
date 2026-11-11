using TomasAI.IFM.Shared.Identity;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>
/// Provides PostgreSQL sequence allocation and metadata reads.
/// </summary>
/// <param name="connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name="dbFactory">The factory exposing the sequence repository.</param>
/// <param name="logger">The database-provider logger.</param>
public class SequenceIdDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger)
    : ObjectDataRepository<SequenceIdDbContext>(connectionSettings[SequenceIdDbConnection], logger), ISequenceIdDbContext
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override SequenceIdDbContext Database => this;

    /// <summary>Gets the sequence read capability.</summary>
    public ISequenceIdDbReadContext DbReader => this;

    /// <summary>Gets the sequence allocation capability.</summary>
    public ISequenceIdDbWriteContext DbWriter => this;

    /// <summary>The named PostgreSQL connection used for identifier allocation.</summary>
    public const string SequenceIdDbConnection = "SequenceIdDbConnection";
    static long MapToSequenceId(IObjectDataRecord o) => o.GetLong(0);

    /// <inheritdoc cref="ISequenceIdDbReadContext.GetSequenceAllocationSizeAsync"/>
    public async Task<long> GetSequenceAllocationSizeAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
            .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetSequenceAllocationSize)}", SequenceIdDbSql.GetSequenceAllocationSize)

            .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))

            .ExecuteScalarAsync(MapToSequenceId, cancellationToken)

            .ConfigureAwait(false);

    /// <inheritdoc cref="ISequenceIdDbReadContext.GetCurrentSequenceIdAsync"/>
    public async Task<long> GetCurrentSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
            .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetCurrentSequenceId)}", SequenceIdDbSql.GetCurrentSequenceId)

            .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))

            .ExecuteScalarAsync(MapToSequenceId, cancellationToken)

            .ConfigureAwait(false);

    /// <inheritdoc cref="ISequenceIdDbWriteContext.GetNextSequenceIdAsync"/>
    public async Task<long> GetNextSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _dbFactory.SequenceIdDb
                .Use($"{nameof(SequenceIdDbSql)}.{nameof(SequenceIdDbSql.GetNextSequenceId)}", SequenceIdDbSql.GetNextSequenceId)

            .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))

            .ExecuteScalarAsync(MapToSequenceId, cancellationToken)

            .ConfigureAwait(false);

}
