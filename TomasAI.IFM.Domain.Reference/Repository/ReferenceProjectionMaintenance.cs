using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Reference.Repository;

/// <summary>Prepares legacy Reference rows without putting allocation or request policy in persistence.</summary>
public static class ReferenceProjectionMaintenance
{
    extension(IReferenceDbContext database)
    {
        /// <summary>Allocates the existing legacy identifier and supplies the complete row to Storage.</summary>
        public async Task InsertLegacyScheduledJobAsync(ScheduledJobReadModel row, ISequenceIdGenerator sequences, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            var sequence = SequenceNameExtensions.ParseSequenceName("ScheduledJobId");
            var id = checked((int)await sequences.GetSequenceIdAsync(sequence, token).ConfigureAwait(false));
            if (id <= 0) throw new InvalidOperationException("The allocated legacy job identifier must be positive.");
            await database.DbWriter.InsertScheduledJobAsync(row with { JobId = id }).ConfigureAwait(false);
        }

        /// <summary>Validates the accepted legacy job before replacing its stored rows.</summary>
        public Task UpdateLegacyScheduledJobAsync(ScheduledJobReadModel row)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.JobId <= 0) throw new ArgumentException("A positive job identifier is required.", nameof(row));
            return database.DbWriter.UpdateScheduledJobAsync(row);
        }

        /// <summary>Validates canonical lookup keys before submitting the prepared batch.</summary>
        public Task WriteMdiForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            RequireMdiForwardLossRatioKeys(rows);
            return database.DbWriter.InsertMDIForwardLossRatiosAsync(rows);
        }

        /// <summary>Reports the PostgreSQL canonical index needs no legacy shadow rows.</summary>
        public Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(int batchSize = 256,
            CancellationToken cancellationToken = default, DateTime? staleOperationCutoffUtc = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
            return Task.FromResult(new ReferenceProjectionBackfillResult(0));
        }
    }

    static void RequireMdiForwardLossRatioKeys(IEnumerable<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
    {
        var logicalKeys = new HashSet<MdiForwardLossRatioLogicalKey>();
        foreach (var ratio in mdiForwardLossRatios)
        {
            var key = new MdiForwardLossRatioLogicalKey(ratio.TrendDirection.ToStringFast(), ratio.TradeType.ToStringFast(), ratio.MDI);
            if (!logicalKeys.Add(key))
            {
                throw new ArgumentException($"MDI forward-loss-ratio batch contains duplicate key " + $"({key.TrendDirection}, {key.TradeType}, {key.Mdi}).", nameof(mdiForwardLossRatios));
            }
        }
    }

    readonly record struct MdiForwardLossRatioLogicalKey(string TrendDirection, string TradeType, int Mdi);
}
