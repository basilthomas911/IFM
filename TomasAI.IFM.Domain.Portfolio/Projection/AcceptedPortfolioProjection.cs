using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

/// <summary>Chooses accepted mandate versions while the caller holds the existing PostgreSQL revision fence.</summary>
public static class AcceptedPortfolioProjection
{
    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Writes the committed portfolio projection and its Fund versions in the established lock order.</summary>
        public async Task PersistAcceptedPortfolioAsync(IEvent source, PortfolioProjectionFacts facts, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(facts);
            if (facts.SchemaVersion != 1 || facts.Revision <= 0 || source.EventId <= 0 || facts.UpdatedAtUtc.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Accepted Portfolio projection facts require schema 1, committed identity, revision and UTC timestamp.");

            await transaction.WriteAcceptedPortfolioHeaderAsync(source, facts, token).ConfigureAwait(false);
            if (facts.Fund is { } fund)
                await transaction.WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel>.Create(
                    fund, facts.Revision, source.EventId, facts.UpdatedAtUtc), token).ConfigureAwait(false);
            await transaction.WriteAcceptedPortfolioTailAsync(source, facts, token).ConfigureAwait(false);
        }

        /// <summary>Disables superseded mandates and stores the accepted mandate within the same caller transaction.</summary>
        public async Task WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken token)
        {
            var fund = row.Value;
            var versions = await transaction.ReadLockedFundProjectionsAsync(fund.PortfolioId, fund.FundId, token)
                .ConfigureAwait(false);
            if (versions.Any(version => version.FundMandateVersion > fund.FundMandateVersion))
            {
                fund = fund with { OperatingState = FundOperatingState.Disabled };
                row = PortfolioProjection<FundMandateReadModel>.Create(fund, row.AggregateVersion, row.SourceEventId, row.UpdatedOnUtc);
            }
            foreach (var old in versions.Where(version =>
                version.FundMandateVersion < fund.FundMandateVersion && version.OperatingState != FundOperatingState.Disabled))
            {
                var disabled = PortfolioProjection<FundMandateReadModel>.Create(
                    old with { OperatingState = FundOperatingState.Disabled }, row.AggregateVersion, row.SourceEventId, row.UpdatedOnUtc);
                await transaction.DisablePreparedFundProjectionAsync(disabled, token).ConfigureAwait(false);
            }
            await transaction.UpsertPreparedFundProjectionAsync(row, token).ConfigureAwait(false);
        }
    }
}
