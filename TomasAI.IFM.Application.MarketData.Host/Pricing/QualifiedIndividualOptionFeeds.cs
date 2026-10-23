using TomasAI.IFM.Shared.Diagnostics;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
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

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Owns individual qualified option leases without using the retired in-process epoch.</summary>
public sealed class QualifiedIndividualOptionFeeds(QualifiedCompositionDiscovery discovery,
    DatasetWorkerAdmissionRegistry admissions, IDbContextFactory databases,
    TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, IndividualOptionRiskReader? riskReader = null, ILogger? logger = null, TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth? health = null) : IAsyncDisposable
{
    RecoveringOptionLeaseRegistry recovery = new(discovery, admissions, riskReader, logger, health);
    SemaphoreSlim serial = new(1, 1);
    readonly object generations = new();
    readonly HashSet<string> sessions = new(StringComparer.Ordinal);
    readonly HashSet<string> retiredSessions = new(StringComparer.Ordinal);
    Guid ownershipGeneration = Guid.NewGuid();
    /// <summary>Changes when all monitoring owners are retired; durable trade intent can create a fresh owner.</summary>
    public Guid OwnershipGeneration { get { lock (generations) return ownershipGeneration; } }

    static string Session(TickerStreamOwner owner) => Guid.TryParse(owner.WorkflowId.Split('.').Last(), out var id)
        ? id.ToString("N") : owner.WorkflowType + "|" + owner.WorkflowId;
    public void ValidateOwner(TickerStreamOwner owner)
    {
        owner.Validate();
        lock (generations)
            if (retiredSessions.Contains(Session(owner))) throw new InvalidOperationException("Monitoring owner was retired; enable Live Feed with a fresh session.");
    }
    public Task AdoptRestoredLeasesAsync(IReadOnlyList<WorkerOptionChainRequest> leases, CancellationToken token)
        => recovery.AdoptRestoredLeasesAsync(leases, token);
    public async Task RetireSessionAsync(CancellationToken token)
    {
        RecoveringOptionLeaseRegistry previous;
        lock (generations)
        {
            foreach (var session in sessions) retiredSessions.Add(session);
            sessions.Clear();
            ownershipGeneration = Guid.NewGuid();
            previous = recovery;
            recovery = new(discovery, admissions, riskReader, logger, health);
            serial = new(1, 1);
        }
        var retiring = previous.DisposeAsync().AsTask();
        try { await retiring.WaitAsync(TimeSpan.FromSeconds(1), token).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            // The retired registry is canceled, detached, and references only retired owners.
            // Its late completion cannot acquire against the new worker identity.
            _ = retiring.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            logger?.LogWarning("Retired option registry cleanup continues after its generation was fenced.");
        }
    }

    /// <summary>Stops admission-driven recovery when the owning actor shuts down.</summary>
    public ValueTask DisposeAsync() => recovery.DisposeAsync();

    /// <summary>Attaches one exact reviewed contract on a separate physical connection and requires worker acknowledgement.</summary>
    /// <param name="owner">The independent actor/view owner.</param>
    /// <param name="contract">The reviewed option definition.</param>
    /// <param name="valueDate">The active exchange session.</param>
    /// <returns>The observed acquisition operation.</returns>
    public async Task AcquireAsync(TickerStreamOwner owner, FuturesOptionContractReadModel contract, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(contract);
        using var operation = new OptionFeedTrace("option.subscription.acquire", logger, $"{owner.WorkflowId}|{valueDate:yyyy-MM-dd}|{contract.ContractId}");
        ValidateOwner(owner);
        SemaphoreSlim currentSerial;
        RecoveringOptionLeaseRegistry currentRecovery;
        lock (generations)
        {
            if (retiredSessions.Count >= 4096) throw new InvalidOperationException("Monitoring retirement capacity reached; restart MarketData Host.");
            if (retiredSessions.Contains(Session(owner))) throw new InvalidOperationException("Monitoring owner was retired; enable Live Feed with a fresh session.");
            sessions.Add(Session(owner));
            currentSerial = serial; currentRecovery = recovery;
        }
        using (var step = new OptionFeedTrace("option.subscription.wait_serial", logger, contract.ContractId))
        {
            await currentSerial.WaitAsync().ConfigureAwait(false);
            step.Complete();
        }
        try
        {
            var candidate = CreateCandidate(contract);
            if (contract.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidDataException("OptionMonitoring.REFERENCE.NOT_REVIEWED");
            var securities = databases.SecuritiesDb;
            using (var step = new OptionFeedTrace("option.subscription.reference", logger, contract.ContractId))
            {
                if (await securities.GetReferenceVersionAsync(contract.ContractId, contract.MappingVersion!, CancellationToken.None)
                    .ConfigureAwait(false) is null)
                {
                    var pending = await securities.StageReferenceVersionAsync(contract, CancellationToken.None).ConfigureAwait(false);
                    await securities.CommitReferenceVersionAsync(pending, CancellationToken.None).ConfigureAwait(false);
                }
                step.Complete();
            }
            var maturity = candidate.Definition.MaturityDate;
            using var calendarStep = new OptionFeedTrace("option.subscription.calendar", logger, contract.ContractId);
            var dates = await databases.MarketDataDb.GetTradingDatesAsync(valueDate, maturity,
                MarketType.Futures, CurrencyType.USD, CancellationToken.None).ConfigureAwait(false);
            var calendar = new OptionPricingCalendar(contract.CalendarVersion ?? "IFM-MarketDates",
                contract.ExchangeTimeZoneId ?? "America/New_York", valueDate, maturity,
                new TimeOnly(18, 0), dates.ToImmutableArray());
            calendarStep.Complete();
            calendarStep.Dispose();
            var request = new CompositionDiscoveryRequest(Guid.NewGuid(), Guid.Empty, valueDate,
                maturity, DateTimeOffset.UtcNow.AddSeconds(60), [candidate], true, calendar, publication, conversion,
                SeparateContractConnection: true);
            currentRecovery.Start();
            using var leaseStep = new OptionFeedTrace("option.subscription.lease", logger, $"{contract.ContractId}|{request.LeaseId}");
            await currentRecovery.AcquireAsync(owner, contract.ContractId, request).ConfigureAwait(false);
            leaseStep.Complete();
            operation.Complete();
        }
        catch (Exception error) { operation.Fail(error); throw; }
        finally { currentSerial.Release(); }
    }

    /// <summary>Releases only the specified owner's lease; other owners of the same leg remain attached.</summary>
    /// <param name="owner">The owner being released.</param>
    /// <param name="contractId">The exact option contract.</param>
    /// <returns>The worker release acknowledgement operation.</returns>
    public async Task ReleaseAsync(TickerStreamOwner owner, string contractId)
    {
        await recovery.ReleaseAsync(owner, contractId).ConfigureAwait(false);
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
