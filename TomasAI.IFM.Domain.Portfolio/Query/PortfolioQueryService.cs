using System.Globalization;
using System.Text;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Side-effect-free handler used by PortfolioQueryActor; it reads PortfolioDb projections only.</summary>
public sealed class PortfolioQueryService(IPortfolioDbReadContext db, PortfolioFundStrategyResolver resolver) : IPortfolioQueryApi
{
    readonly IPortfolioDbReadContext _db = db ?? throw new ArgumentNullException(nameof(db));
    readonly PortfolioFundStrategyResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public async Task<ServiceResult<PortfolioReadModel>> GetPortfolioAsync(int portfolioId, long? version = null, CancellationToken cancellationToken = default)
    {
        var item = await _db.GetPortfolioAsync(Positive(portfolioId), cancellationToken).ConfigureAwait(false);
        return item is null || version is { } v && item.PortfolioVersion != v ? NotFound<PortfolioReadModel>("Portfolio/version was not found.") : new ServiceOk<PortfolioReadModel>(item);
    }

    public async Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default)
    {
        var value = await _db.GetPortfolioRevisionAsync(Positive(portfolioId), cancellationToken).ConfigureAwait(false);
        return value is null ? NotFound<PortfolioAggregateRevision>("Portfolio revision was not found.") :
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = value.PortfolioId, Revision = value.AggregateRevision, SourceEventId = value.SourceEventId });
    }

    public async Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        if (state is null or PortfolioOperatingState.Unknown) return Invalid<PortfolioPage<PortfolioReadModel>>("OperatingState is required for a bounded Portfolio page.");
        try
        {
            var after = PortfolioPageToken.DecodeInteger(pageToken);
            var bucket = after <= 0 ? 0 : (after - 1) / 1000;
            var items = await _db.GetPortfoliosByStateAsync(state.Value, bucket, after, Page(pageSize), cancellationToken).ConfigureAwait(false);
            return new ServiceOk<PortfolioPage<PortfolioReadModel>>(new() { Items = [.. items], PageSize = pageSize, NextPageToken = items.Count == pageSize ? PortfolioPageToken.EncodeInteger(items[^1].PortfolioId) : null });
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException) { return Invalid<PortfolioPage<PortfolioReadModel>>(ex.Message); }
    }

    public async Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default)
    {
        var item = await _db.GetFundAsync(Positive(fundId), cancellationToken).ConfigureAwait(false);
        return item is null || item.PortfolioId != Positive(portfolioId) || version is { } v && item.FundMandateVersion != v ? NotFound<FundMandateReadModel>("Fund mandate/version was not found in the Portfolio.") : new ServiceOk<FundMandateReadModel>(item);
    }

    public async Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default)
    {
        var value = await _db.GetFundRevisionAsync(Positive(fundId), cancellationToken).ConfigureAwait(false);
        return value is null || value.PortfolioId != Positive(portfolioId) ? NotFound<PortfolioAggregateRevision>("Fund revision was not found in the Portfolio.") :
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = value.PortfolioId, FundId = value.FundId, Revision = value.AggregateRevision, SourceEventId = value.SourceEventId });
    }

    public async Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var after = PortfolioPageToken.DecodeInteger(pageToken);
            var items = await _db.GetFundsByPortfolioAsync(Positive(portfolioId), after, Page(pageSize), cancellationToken).ConfigureAwait(false);
            var filtered = state is null ? items : items.Where(x => x.OperatingState == state).ToArray();
            return new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [.. filtered], PageSize = pageSize, NextPageToken = items.Count == pageSize ? PortfolioPageToken.EncodeInteger(items[^1].FundId) : null });
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException) { return Invalid<PortfolioPage<FundMandateReadModel>>(ex.Message); }
    }

    public async Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        Utc(asOfUtc);
        var value = await _db.GetCurrentRiskEnvelopeAsync(Positive(portfolioId), Positive(fundId), cancellationToken).ConfigureAwait(false);
        return value is null || !(value.EffectiveFromUtc <= asOfUtc && asOfUtc < value.ExpiresAtUtc) ? NotFound<FundRiskEnvelopeReadModel>("A current Fund risk envelope was not found.") : new ServiceOk<FundRiskEnvelopeReadModel>(value);
    }

    public async Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default)
    {
        var value = await _db.GetCurrentAllocationAsync(Positive(portfolioId), Positive(fundId), cancellationToken).ConfigureAwait(false);
        return value is null ? NotFound<FundAllocationReadModel>("A current Fund allocation was not found.") : new ServiceOk<FundAllocationReadModel>(value);
    }

    public async Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default) =>
        new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([.. await _db.GetAssignmentsAsync(Positive(portfolioId), Positive(fundId), Positive(mandateVersion), 200, cancellationToken).ConfigureAwait(false)]);

    public async Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var portfolio = await _db.GetPortfolioAsync(Positive(portfolioId), cancellationToken).ConfigureAwait(false) ?? throw new PortfolioResolutionException("PortfolioMissing", "Portfolio was not found.");
            var financialPolicy = await _db.GetActivePolicyAsync(portfolioId, cancellationToken).ConfigureAwait(false) ?? throw new PortfolioResolutionException("FinancialPolicyMissing", "The Portfolio has no projected Active financial policy.");
            var funds = await _db.GetActiveFundsAsync(portfolioId, tradingYear, decisionHorizon, asOfUtc, 2, cancellationToken).ConfigureAwait(false);
            var eligible = funds.Where(x => x.UnderlyingUniverse.Contains(underlyingRoot, StringComparer.OrdinalIgnoreCase) && x.EligibleAssetTypes.Contains(assetType, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (eligible.Length == 0) throw new PortfolioResolutionException("ActiveFundMissing", "No matching active Fund was found.");
            var fund = eligible[0];
            var allocation = await _db.GetCurrentAllocationAsync(portfolioId, fund.FundId, cancellationToken).ConfigureAwait(false);
            var envelope = await _db.GetCurrentRiskEnvelopeAsync(portfolioId, fund.FundId, cancellationToken).ConfigureAwait(false);
            var assignments = await _db.GetAssignmentsAsync(portfolioId, fund.FundId, fund.FundMandateVersion, 200, cancellationToken).ConfigureAwait(false);
            return new ServiceOk<PortfolioFundStrategySnapshot>(_resolver.Resolve(workflowId, workflowRevision, correlationId, portfolio, financialPolicy, eligible, allocation is null ? [] : [allocation], envelope is null ? [] : [envelope], assignments, tradingYear, decisionHorizon, underlyingRoot, assetType, asOfUtc));
        }
        catch (PortfolioResolutionException ex)
        {
            var code = ex.ReasonCode.Contains("Ambiguous", StringComparison.Ordinal) ? PortfolioErrorCodes.ConfigurationAmbiguous : PortfolioErrorCodes.ConfigurationMissing;
            return new ServiceFailed<PortfolioFundStrategySnapshot>(code, $"{ex.ReasonCode}: {ex.Message}");
        }
    }

    public async Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        await _db.GetOrderAsync(Positive(orderId), cancellationToken).ConfigureAwait(false) is { } value ? new ServiceOk<FundOrderProjectionReadModel>(value) : NotFound<FundOrderProjectionReadModel>("FundOrder was not found.");

    public async Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default) =>
        await _db.GetTradeAsync(Positive(tradeId), cancellationToken).ConfigureAwait(false) is { } value ? new ServiceOk<FundOrderTradeProjectionReadModel>(value) : NotFound<FundOrderTradeProjectionReadModel>("FundOrderTrade was not found.");

    public async Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default) =>
        new ServiceOk<FundCompositionWorkflowProjectionReadModel[]>([.. await _db.GetCompositionsAsync(workflowId, 200, cancellationToken).ConfigureAwait(false)]);

    public async Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var before = PortfolioPageToken.DecodeTimestamp(pageToken) ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            var items = await _db.GetOrdersAsync(Positive(portfolioId), Positive(fundId), orderMonth, before, Page(pageSize), cancellationToken).ConfigureAwait(false);
            return new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new() { Items = [.. items], PageSize = pageSize, NextPageToken = items.Count == pageSize ? PortfolioPageToken.EncodeTimestamp(items[^1].CreatedOnUtc) : null });
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException) { return Invalid<PortfolioPage<FundOrderProjectionReadModel>>(ex.Message); }
    }

    public async Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageToken is not null) PortfolioPageToken.DecodeInteger(pageToken);
            var items = await _db.GetOrderTradesAsync(Positive(orderId), Page(pageSize), cancellationToken).ConfigureAwait(false);
            return new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(new() { Items = [.. items], PageSize = pageSize });
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException) { return Invalid<PortfolioPage<FundOrderTradeProjectionReadModel>>(ex.Message); }
    }

    public async Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        Utc(asOfUtc);
        var funds = await _db.GetFundsByPortfolioAsync(Positive(portfolioId), 0, 200, cancellationToken).ConfigureAwait(false);
        var rows = new List<PortfolioFundStrategyReferenceCombination>();
        foreach (var fund in funds.OrderBy(x => x.FundId))
        foreach (var assignment in await _db.GetAssignmentsAsync(portfolioId, fund.FundId, fund.FundMandateVersion, 200, cancellationToken).ConfigureAwait(false))
        foreach (var root in assignment.UnderlyingUniverse.Order(StringComparer.Ordinal))
            rows.Add(new()
            {
                PortfolioId = portfolioId, PortfolioVersion = assignment.PortfolioVersion, FundId = fund.FundId, FundMandateVersion = fund.FundMandateVersion,
                TradingYear = fund.TradingYear, DecisionHorizon = fund.DecisionHorizon, UnderlyingRoot = root, AssetType = assignment.AssetType,
                TradeFamily = assignment.TradeFamily, TradeTemplateId = assignment.TradeTemplateId, TradeTemplateVersion = assignment.TradeTemplateVersion,
                TradeSelectionHintProfileId = assignment.TradeSelectionHintProfileId, TradeSelectionHintProfileVersion = assignment.TradeSelectionHintProfileVersion,
                OrderCompositionProfileId = assignment.OrderCompositionProfileId, OrderCompositionProfileVersion = assignment.OrderCompositionProfileVersion,
                CurrentlyEligible = fund.OperatingState == FundOperatingState.Active && assignment.IsEffectiveAt(asOfUtc),
                ReasonCode = fund.OperatingState == FundOperatingState.Active && assignment.IsEffectiveAt(asOfUtc) ? "Eligible" : "InactiveOrNotEffective",
            });
        return new ServiceOk<PortfolioFundStrategyReferenceCombination[]>([.. rows]);
    }

    public async Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) =>
        await _db.GetPolicyAsync(Positive(policyId), policyVersion, cancellationToken).ConfigureAwait(false) is { } value
            ? new ServiceOk<PortfolioFinancialPolicyReadModel>(value)
            : NotFound<PortfolioFinancialPolicyReadModel>("Financial policy/version was not found.");

    public async Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default)
    {
        var items = await _db.GetPoliciesAsync(Positive(portfolioId), Page(pageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceOk<PortfolioPage<PortfolioFinancialPolicyReadModel>>(new() { Items = [.. items], PageSize = pageSize });
    }

    public async Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) =>
        await _db.GetActivePolicyAsync(Positive(portfolioId), cancellationToken).ConfigureAwait(false) is { } value
            ? new ServiceOk<PortfolioFinancialPolicyReadModel>(value)
            : NotFound<PortfolioFinancialPolicyReadModel>("Active financial policy was not found.");

    static ServiceFailed<T> NotFound<T>(string message) => new(PortfolioErrorCodes.NotFound, message);
    static ServiceFailed<T> Invalid<T>(string message) => new(PortfolioErrorCodes.ValidationFailed, message);
    static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    static int Page(int value) => value is >= 1 and <= 200 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Page size must be 1..200.");
    static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC."); }
}

public static class PortfolioPageToken
{
    public static string EncodeInteger(int value) => Encode($"i:{value.ToString(CultureInfo.InvariantCulture)}");
    public static int DecodeInteger(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        var value = Decode(token);
        return value.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(value.AsSpan(2), CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : throw new FormatException("Page token is invalid.");
    }
    public static string EncodeTimestamp(DateTime value) { Utc(value); return Encode($"t:{value.Ticks.ToString(CultureInfo.InvariantCulture)}"); }
    public static DateTime? DecodeTimestamp(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var value = Decode(token);
        return value.StartsWith("t:", StringComparison.Ordinal) && long.TryParse(value.AsSpan(2), CultureInfo.InvariantCulture, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : throw new FormatException("Page token is invalid.");
    }
    static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    static string Decode(string value) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); } catch (Exception ex) when (ex is FormatException or ArgumentException) { throw new FormatException("Page token is invalid.", ex); } }
    static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC."); }
}
