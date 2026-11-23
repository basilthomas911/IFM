using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.Recovery;

/// <summary>Closing observations remain factual: stale quotes fail readiness rather than receiving a fabricated timestamp.</summary>
public sealed record OpenPositionEndOfDayOptions
{
    public TimeSpan MaximumCloseObservationAge { get; init; } = TimeSpan.FromMinutes(15);
}

public sealed record OpenPositionEndOfDayIssue(string PositionId, DateOnly ValueDate, string Code,
    string Detail, DateTime? LastMarketObservationUtc = null, DateTime? OldestLegObservationUtc = null);

public sealed record OpenPositionEndOfDayAudit(DateOnly ValueDate, DateTimeOffset AuditedAtUtc,
    int OpenPositions, int CatchUpAttempts, int Recovered, int NotRequired, int FailedPositions,
    OpenPositionEndOfDayIssue[] Issues);

public interface IOpenPositionEndOfDayStore
{
    Task<StrategyPositionSnapshot[]> ReadOpenAsync(StrategyPositionId? after, int pageSize, CancellationToken token);
    Task<StrategyPositionSnapshot?> LoadPositionAsync(StrategyPositionId id, CancellationToken token);
    Task<EstablishedTradeDefinition?> LoadTradeAsync(TradeEntityId id, CancellationToken token);
    Task<StrategyPositionSnapshot?> ReadSealAsync(StrategyPositionId id, DateOnly valueDate, DateTime fromUtc, DateTime toUtc, CancellationToken token);
}

/// <summary>Audits position owners directly; never enables feeds or submits orders; missing closing evidence is queued for durable recovery.</summary>
public sealed class OpenPositionEndOfDaySweep(IOpenPositionEndOfDayStore storage, IStrategyPositionCommandApi commands,
    IMarketSessionCalendar calendar, TimeProvider clock, OpenPositionEndOfDayOptions options, IPositionEndOfDayRecoveryQueue? recovery = null)
{
    public DateOnly LastCompletedValueDate()
    {
        var now = clock.GetUtcNow();
        var date = calendar.GetValueDate(now);
        while (!calendar.IsTradingDate(date) || calendar.GetSession(date).EndUtc > now) date = date.AddDays(-1);
        return date;
    }

    public async Task<OpenPositionEndOfDayAudit> RunAsync(CancellationToken token)
    {
        if (options.MaximumCloseObservationAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        var target = LastCompletedValueDate();
        var session = calendar.GetSession(target);
        var boundary = session.EndUtc.UtcDateTime;
        var issues = new List<OpenPositionEndOfDayIssue>();
        var scanned = 0; var attempted = 0; var recovered = 0; var notRequired = 0; var failed = 0;
        StrategyPositionId? after = null;
        while (true)
        {
            var page = await storage.ReadOpenAsync(after, 64, token).ConfigureAwait(false);
            if (page.Length == 0) break;
            foreach (var candidate in page)
            {
                token.ThrowIfCancellationRequested();
                scanned++;
                try
                {
                    // Re-read after paging: a concurrent close or live mark may have advanced this owner.
                    var current = await storage.LoadPositionAsync(candidate.Id, token).ConfigureAwait(false);
                    if (current is null) { Fail(candidate, "PositionMissing", "The open-position owner could not be reloaded."); continue; }
                    if (!current.IsOpen) { notRequired++; continue; }
                    var trade = await storage.LoadTradeAsync(current.Id.Trade, token).ConfigureAwait(false);
                    var openingDate = current.OpeningValueDate ?? trade?.TradeDate;
                    if (openingDate is null) { Fail(current, "OpeningEvidenceMissing", "Cannot establish whether this position existed in the completed session."); continue; }
                    if (openingDate > target) { notRequired++; continue; }
                    if (current.StrategyKind is not (TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread or TradeStrategyKind.FuturesOutright))
                    { Fail(current, "UnsupportedStrategy", "No EOD command owner for this open position."); continue; }
                    string? commandFailure = null;
                    if (current.LatestSealedValueDate < target)
                    {
                        // Persist recovery intent before a transport timeout or shutdown can interrupt the seal request.
                        if (recovery is not null) await recovery.ScheduleAsync(current.Id, target, boundary, token).ConfigureAwait(false);
                        attempted++;
                        try
                        {
                            var result = await commands.EndOfDayAsync(current.Id, current.StrategyKind, target, boundary, token).ConfigureAwait(false);
                            commandFailure = result.Success ? null : result.ErrorMessage;
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception error) { commandFailure = error.Message; }
                        var persisted = await storage.LoadPositionAsync(current.Id, token).ConfigureAwait(false);
                        if (persisted is null) { Fail(current, "PositionMissing", "The EOD command did not leave readable position evidence."); continue; }
                        if (!persisted.IsOpen) { notRequired++; continue; }
                        current = persisted;
                        if (current.LatestSealedValueDate >= target) recovered++;
                    }
                    var seal = await storage.ReadSealAsync(current.Id, target, session.StartUtc.UtcDateTime, boundary, token).ConfigureAwait(false);
                    if (current.LatestSealedValueDate < target || seal is null || seal.CloseBoundaryUtc != boundary)
                    {
                        if (recovery is not null) await recovery.ScheduleAsync(current.Id, target, boundary, token).ConfigureAwait(false);
                        Fail(current, "MissingSeal", commandFailure ?? "The completed session lacks a durable EOD row and confirmed close boundary."); continue;
                    }
                    var oldest = seal.Legs.Where(leg => leg.SignedQuantity != 0).Select(leg => leg.LastPriceAtUtc).DefaultIfEmpty(default).Min();
                    if (!ClosingBookArchive.Qualifies(seal,boundary,options.MaximumCloseObservationAge))
                    {
                        if (recovery is not null) await recovery.ScheduleAsync(current.Id, target, boundary, token).ConfigureAwait(false);
                        Fail(seal, "StaleCloseObservation", $"Closing observations require either a quote within {options.MaximumCloseObservationAge.TotalMinutes:g} minutes of close or a complete verified closing book; authentic quote recovery is pending.", oldest);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) { Fail(candidate, "AuditFailed", error.Message); }
            }
            var next = page[^1].Id;
            if (after == next) throw new InvalidDataException("Open-position EOD paging did not advance.");
            after = next;
        }
        return new(target, clock.GetUtcNow(), scanned, attempted, recovered, notRequired, failed, issues.ToArray());

        void Fail(StrategyPositionSnapshot position, string code, string detail, DateTime? oldest = null)
        {
            failed++;
            if (issues.Count < 100) issues.Add(new(position.Id.Format(), target, code,
                detail.Length > 1500 ? detail[..1500] : detail, position.LastMarketObservationUtc, oldest));
        }
    }
}
