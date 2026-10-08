using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

/// <summary>Owns individual qualified option leases without using the retired in-process epoch.</summary>
public sealed class QualifiedIndividualOptionFeeds(QualifiedCompositionDiscovery discovery,
    DatasetWorkerAdmissionRegistry admissions, IDbContextFactory databases,
    TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, IndividualOptionRiskReader? riskReader = null)
{
    readonly Dictionary<(TickerStreamOwner Owner, string Contract), WorkerOptionChainRequest> leases = [];
    readonly SemaphoreSlim serial = new(1, 1);

    /// <summary>Attaches one exact reviewed contract on a separate physical connection and requires worker acknowledgement.</summary>
    /// <param name="owner">The independent actor/view owner.</param>
    /// <param name="contract">The reviewed option definition.</param>
    /// <param name="valueDate">The active exchange session.</param>
    /// <returns>The observed acquisition operation.</returns>
    public async Task AcquireAsync(TickerStreamOwner owner, FuturesOptionContractReadModel contract, DateOnly valueDate)
    {
        await serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!admissions.TryGet("GLBX.MDP3", out var admitted) || admitted.ValueDate != valueDate)
                throw new InvalidOperationException("OptionMonitoring.WORKER.NOT_ADMITTED");
            var key = (owner, contract.ContractId);
            if (leases.TryGetValue(key, out var existing))
            {
                if (existing.GenerationId == admitted.GenerationId) return;
                await discovery.ReleaseAsync(existing, CancellationToken.None).ConfigureAwait(false);
                riskReader?.Remove(existing.LeaseId);
                leases.Remove(key);
            }
            var candidate = CreateCandidate(contract);
            if (contract.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidDataException("OptionMonitoring.REFERENCE.NOT_REVIEWED");
            var securities = databases.SecuritiesDb;
            if (await securities.GetReferenceVersionAsync(contract.ContractId, contract.MappingVersion!, CancellationToken.None)
                .ConfigureAwait(false) is null)
            {
                var pending = await securities.StageReferenceVersionAsync(contract, CancellationToken.None).ConfigureAwait(false);
                await securities.CommitReferenceVersionAsync(pending, CancellationToken.None).ConfigureAwait(false);
            }
            var maturity = candidate.Definition.MaturityDate;
            var dates = await databases.MarketDataDb.GetTradingDatesAsync(valueDate, maturity,
                MarketType.Futures, CurrencyType.USD, CancellationToken.None).ConfigureAwait(false);
            var calendar = new OptionPricingCalendar(contract.CalendarVersion ?? "IFM-MarketDates",
                contract.ExchangeTimeZoneId ?? "America/New_York", valueDate, maturity,
                new TimeOnly(18, 0), dates.ToImmutableArray());
            var request = new CompositionDiscoveryRequest(Guid.NewGuid(), admitted.GenerationId, valueDate,
                maturity, DateTimeOffset.UtcNow.AddSeconds(60), [candidate], true, calendar, publication, conversion,
                SeparateContractConnection: true);
            var acquired = await discovery.AcquireAsync(request, CancellationToken.None, renewAutomatically: true).ConfigureAwait(false);
            if (acquired.Lease is null || acquired.Failure is not null)
                throw new InvalidOperationException($"OptionMonitoring.ACQUIRE.FAILED: {acquired.Failure?.Code}");
            leases[key] = acquired.Lease;
            riskReader?.Register(acquired.Lease);
        }
        finally { serial.Release(); }
    }

    /// <summary>Releases only the specified owner's lease; other owners of the same leg remain attached.</summary>
    /// <param name="owner">The owner being released.</param>
    /// <param name="contractId">The exact option contract.</param>
    /// <returns>The worker release acknowledgement operation.</returns>
    public async Task ReleaseAsync(TickerStreamOwner owner, string contractId)
    {
        await serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!leases.TryGetValue((owner, contractId), out var lease)) return;
            var released = await discovery.ReleaseAsync(lease, CancellationToken.None).ConfigureAwait(false);
            if (released.Failure is not null)
                throw new InvalidOperationException($"OptionMonitoring.RELEASE.FAILED: {released.Failure.Code}");
            riskReader?.Remove(lease.LeaseId);
            leases.Remove((owner, contractId));
        }
        finally { serial.Release(); }
    }

    /// <summary>Maps exact reviewed metadata; provider symbols and IDs remain inside pricing/transport boundaries.</summary>
    /// <param name="value">The persisted security definition.</param>
    /// <returns>The immutable native definition candidate.</returns>
    internal static OptionDefinitionCandidate CreateCandidate(FuturesOptionContractReadModel value)
    {
        if (value.PublisherId is null || value.InstrumentId is null || value.ExpirationUtc is null
            || string.IsNullOrWhiteSpace(value.MappingVersion) || string.IsNullOrWhiteSpace(value.DefinitionDigest)
            || string.IsNullOrWhiteSpace(value.RawSymbol) || string.IsNullOrWhiteSpace(value.Dataset)
            || string.IsNullOrWhiteSpace(value.UnderlyingContractId)
            || value.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put))
            throw new InvalidDataException("OptionMonitoring.REFERENCE.INCOMPLETE");
        return new(value.ContractId, value.MappingVersion, value.DefinitionDigest, new OptionContractDefinition
        {
            Dataset = value.Dataset, RawSymbol = value.RawSymbol, Ticker = value.Symbol,
            Underlying = value.UnderlyingContractId, Instrument = new(value.PublisherId.Value, value.InstrumentId.Value),
            Right = value.OptionRight == ReferenceOptionRight.Call ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = value.GetExactStrikePrice(), MaturityDate = DateOnly.FromDateTime(value.ExpirationUtc.Value.UtcDateTime),
            ExpirationTimestampNanoseconds = checked((ulong)(value.ExpirationUtc.Value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL),
            ContractMultiplier = value.MultiplierValue is null ? null : checked((int)value.MultiplierValue.Value)
        });
    }
}
