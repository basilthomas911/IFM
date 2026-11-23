using System.Security.Cryptography;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.Recovery;

public sealed record PositionClosingInstrument(string ContractId, uint InstrumentId, string Dataset, bool IsOption = false, string? RawSymbol = null, uint PublisherId = 0)
{
    public TomasAI.IFM.Shared.MarketData.Contracts.Pricing.OptionPricingConvention? ExpiryConvention { get; init; }
    public string ReferencePayloadSha256 { get; init; } = "";
    public uint UnderlyingInstrumentId { get; init; }
    public uint UnderlyingPublisherId { get; init; }
}
public sealed record AcquiredPositionClosingQuote(StoredPositionClosingQuote Quote, byte[] OriginalRecord);
public sealed record PositionEndOfDaySource(long EventId, StrategyPositionSnapshot Snapshot);
public sealed record PositionEndOfDayJob(StrategyPositionId PositionId, DateOnly ValueDate, DateTime BoundaryUtc,
    Guid LeaseId, int Attempts);

public interface IPositionClosingQuoteSource
{
    Task<AcquiredPositionClosingQuote?> AcquireAsync(PositionClosingInstrument instrument, DateOnly date,
        DateTime boundaryUtc, CancellationToken token);
}

public interface IPositionEndOfDayRecoveryQueue
{
    Task ScheduleAsync(StrategyPositionId id, DateOnly date, DateTime boundaryUtc, CancellationToken token);
}

public interface IPositionEndOfDayRecoveryStore : IPositionEndOfDayRecoveryQueue
{
    Task<int> CountOutstandingAsync(DateTimeOffset now, CancellationToken token);
    Task<PositionEndOfDayJob?> ClaimAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken token);
    Task FinishAsync(PositionEndOfDayJob job, DateTimeOffset now, string? error, CancellationToken token);
    Task<PositionEndOfDaySource?> ReadSourceAsync(StrategyPositionId id, DateOnly date, CancellationToken token);
    Task<PositionClosingInstrument?> ResolveInstrumentAsync(string contractId, DateOnly date, CancellationToken token);
    Task<StoredPositionClosingQuote?> ReadQuoteAsync(string contractId, uint instrumentId, DateOnly date, CancellationToken token);
    Task<StrategyPositionSnapshot?> ReadPreviousSealAsync(StrategyPositionId id, DateOnly date, CancellationToken token) => Task.FromResult<StrategyPositionSnapshot?>(null);
    Task SaveQuoteAsync(AcquiredPositionClosingQuote quote, CancellationToken token);
}

/// <summary>Restartable work: persist each original quote before requesting one audited position correction.</summary>
public sealed class PositionEndOfDayRecovery(IPositionEndOfDayRecoveryStore jobs, IOpenPositionEndOfDayStore positions,
    IPositionClosingQuoteSource provider, IStrategyPositionCommandApi commands, TimeProvider clock,
    OpenPositionEndOfDayOptions options)
{
    public Task<int> GetOutstandingCountAsync(CancellationToken token) => jobs.CountOutstandingAsync(clock.GetUtcNow(), token);

    public async Task<bool> RunOneAsync(CancellationToken token)
    {
        var job = await jobs.ClaimAsync(clock.GetUtcNow(), TimeSpan.FromMinutes(10), token).ConfigureAwait(false);
        if (job is null) return false;
        string? failure = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(8));
            var ct = deadline.Token;
            var current = await positions.LoadPositionAsync(job.PositionId, ct).ConfigureAwait(false);
            if (current is null) throw new InvalidOperationException("EOD.RECOVERY.POSITION_MISSING");
            if (current.IsOpen)
            {
                var seal = await positions.ReadSealAsync(current.Id, job.ValueDate, job.BoundaryUtc.AddDays(-2), job.BoundaryUtc, ct).ConfigureAwait(false);
                if (!IsQualified(seal, job.ValueDate, job.BoundaryUtc, options.MaximumCloseObservationAge))
                {
                    var source = await jobs.ReadSourceAsync(current.Id, job.ValueDate, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("EOD.RECOVERY.COMMITTED_SOURCE_UNAVAILABLE");
                    var quotes = new List<StoredPositionClosingQuote>();
                    foreach (var contract in source.Snapshot.Legs.Where(l=>l.SignedQuantity!=0).Select(l=>l.ContractId).Distinct(StringComparer.Ordinal))
                    {
                        var instrument = await jobs.ResolveInstrumentAsync(contract, job.ValueDate, ct).ConfigureAwait(false)
                            ?? throw new InvalidOperationException($"EOD.RECOVERY.EXACT_INSTRUMENT_UNAVAILABLE:{contract}");
                        var quote = await jobs.ReadQuoteAsync(contract, instrument.InstrumentId, job.ValueDate, ct).ConfigureAwait(false);
                        if (!IsQualified(quote, instrument, job.ValueDate, job.BoundaryUtc))
                        {
                            var acquired = await provider.AcquireAsync(instrument, job.ValueDate, job.BoundaryUtc, ct).ConfigureAwait(false);
                            if (acquired is null || !IsQualified(acquired.Quote, instrument, job.ValueDate, job.BoundaryUtc)
                                || !string.Equals(Convert.ToHexString(SHA256.HashData(acquired.OriginalRecord)), acquired.Quote.SourceArchiveSha256, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException($"EOD.RECOVERY.AUTHENTIC_CLOSE_UNAVAILABLE:{contract}");
                            await jobs.SaveQuoteAsync(acquired, ct).ConfigureAwait(false);
                            quote = acquired.Quote;
                        }
                        quotes.Add(quote!);
                    }
                    VerifiedExpiryFixing.RequireOffsetDelivery(source.Snapshot,quotes,job.BoundaryUtc);
                    var previous = await jobs.ReadPreviousSealAsync(current.Id,job.ValueDate,ct).ConfigureAwait(false);
                    var result = previous is null
                        ? await commands.RepairEndOfDayAsync(source.Snapshot,source.EventId,job.ValueDate,job.BoundaryUtc,quotes.ToArray(),ct).ConfigureAwait(false)
                        : await commands.RepairEndOfDayWithBaselineAsync(source.Snapshot,source.EventId,job.ValueDate,job.BoundaryUtc,quotes.ToArray(),previous,ct).ConfigureAwait(false);
                    // A lost reply may follow a successful commit. Always check durable evidence before retrying.
                    seal = await positions.ReadSealAsync(current.Id, job.ValueDate, job.BoundaryUtc.AddDays(-2), job.BoundaryUtc, ct).ConfigureAwait(false);
                    if (!IsQualified(seal, job.ValueDate, job.BoundaryUtc, options.MaximumCloseObservationAge))
                        throw new InvalidOperationException(result.Success ? "EOD.RECOVERY.SEAL_UNQUALIFIED" : result.ErrorMessage);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { failure = error.Message; }
        await jobs.FinishAsync(job, clock.GetUtcNow(), failure, token).ConfigureAwait(false);
        return true;
    }

    public static bool IsQualified(StrategyPositionSnapshot? seal, DateOnly date, DateTime boundary, TimeSpan maxAge)
        => seal is { Phase: StrategyPositionPhase.EndOfDay } && seal.ValueDate == date && seal.CloseBoundaryUtc == boundary
            && ClosingBookArchive.Qualifies(seal,boundary,maxAge);

    bool IsQualified(StoredPositionClosingQuote? quote, PositionClosingInstrument instrument, DateOnly date, DateTime boundary)
        => quote is not null && quote.ContractId==instrument.ContractId && quote.InstrumentId==instrument.InstrumentId
            && quote.ValueDate==date && quote.SourceSequence>0 && quote.ReceiveTimestampNanoseconds>=quote.EventTimestampNanoseconds
            && quote.EventTimestampNanoseconds<=(boundary.Ticks-DateTime.UnixEpoch.Ticks)*100
            && (instrument.IsOption && quote.ExpiryFixing?.Covers(quote,boundary)==true || quote.ExpiryFixing is null
                && !(instrument.ExpiryConvention is {} expiry && expiry.ExpirationUtc.UtcDateTime<=boundary)
                && (boundary-quote.ObservedAtUtc<=options.MaximumCloseObservationAge || quote.Book?.Covers(boundary)==true)
                && quote.Ask>0 && quote.AskSize>0 && (quote.HasBid ? quote.Bid>0 && quote.BidSize>0 && quote.Bid<=quote.Ask : instrument.IsOption && quote.Bid==0 && quote.BidSize==0));
}
