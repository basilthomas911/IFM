using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Describes an absolute end-of-day valuation observed for one canonical Portfolio trade.</summary>
public sealed record PortfolioTradeValuationRequest(int FundId, int OrderId, int TradeId, DateOnly ValueDate,
    decimal AbsoluteUnrealizedPnl, string Description, Guid SourceEventId, long SourceSequence, DateTime ObservedAtUtc);

/// <summary>Defines the Portfolio-owned boundary for posting trade valuations.</summary>
public interface IPortfolioTradeValuationApi
{
    /// <summary>Posts an absolute unrealized trade valuation through the Portfolio General Ledger.</summary>
    /// <param name="request">The canonical trade valuation and its source evidence.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The completed ledger event identifier.</returns>
    ValueTask<ServiceResult<Guid>> PostAsync(PortfolioTradeValuationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Builds canonical valuation postings from trade end-of-day evidence.</summary>
public sealed class PortfolioTradeValuationApi : IPortfolioTradeValuationApi
{
    readonly IFinancialQueryStore _financialQueries;
    readonly IDbContextFactory _databases;
    readonly IActorService _actors;

    /// <summary>Initializes a Portfolio trade valuation service.</summary>
    /// <param name="financialQueries">The authoritative financial configuration query store.</param>
    /// <param name="databases">The application database context factory.</param>
    /// <param name="actors">The actor service used to post the ledger command.</param>
    public PortfolioTradeValuationApi(IFinancialQueryStore financialQueries, IDbContextFactory databases,
        IActorService actors)
    {
        _financialQueries = financialQueries;
        _databases = databases;
        _actors = actors;
    }

    /// <inheritdoc />
    public async ValueTask<ServiceResult<Guid>> PostAsync(PortfolioTradeValuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FundId <= 0 || request.OrderId <= 0 || request.TradeId <= 0 ||
            request.SourceEventId == Guid.Empty || request.SourceSequence < 0 ||
            request.ObservedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Canonical trade identity, source evidence, and UTC observation time are required.");

        var order = await _databases.PortfolioDb.GetOrderAsync(request.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("PORTFOLIO_VALUATION.ORDER_NOT_FOUND");
        if (order.FundId != request.FundId)
            throw new InvalidOperationException("PORTFOLIO_VALUATION.ORDER_FUND_MISMATCH");

        var access = new FinancialAccess("PortfolioTradeValuation", ["PortfolioAdministrator"]);
        var scope = new FinancialReadScope
        {
            PortfolioId = order.PortfolioId,
            FundId = request.FundId,
            Access = access
        };
        var selected = await _financialQueries.ReadAsync(scope,
            new GetFinancialPostingConfigurationRequest(request.ValueDate), cancellationToken).ConfigureAwait(false);
        if (selected.Status != FinancialReadStatus.Found || selected.Value is null || !selected.Value.PeriodOpen)
            throw new InvalidOperationException("PORTFOLIO_VALUATION.CONFIGURATION_UNAVAILABLE");
        var rules = selected.Value.Rules.Where(static candidate =>
            candidate.Kind == LedgerTransactionKind.Valuation).ToArray();
        if (rules.Length != 1 || rules[0].RequiresConfirmedMovement)
            throw new InvalidOperationException("PORTFOLIO_VALUATION.RULE_INVALID");
        var rule = rules[0];

        var operationId = DeterministicId("portfolio-trade-valuation", request.SourceEventId.ToString("N"));
        var body = new LedgerPostingRequest
        {
            BookId = selected.Value.BookId,
            FundId = request.FundId,
            TransactionKind = LedgerTransactionKind.Valuation,
            AccountingDate = request.ValueDate,
            ValueDate = request.ValueDate,
            Currency = "USD",
            Amount = request.AbsoluteUnrealizedPnl,
            Description = request.Description,
            Source = new()
            {
                System = "TradeEndOfDay",
                SourceEntityId = $"{request.OrderId}:{request.TradeId}",
                SourceEventId = request.SourceEventId,
                SourceSequence = request.SourceSequence,
                SourceContentHash = FinancialCanonicalHash.Compute(request),
                OccurredAtUtc = request.ObservedAtUtc,
                OrderId = request.OrderId,
                TradeId = request.TradeId
            },
            PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
            Authority = selected.Value.Authority
        };
        var command = new PostFundTransactionCommand
        {
            CommandId = DeterministicId("post-portfolio-trade-valuation", request.SourceEventId.ToString("N")),
            Subject = new(ActorType.Command, PostFundTransactionCommand.Actor, PostFundTransactionCommand.Verb,
                new LedgerPortfolioId(order.PortfolioId).Format()),
            EntityId = new(order.PortfolioId),
            OperationId = operationId,
            PortfolioId = order.PortfolioId,
            CorrelationId = request.SourceEventId,
            CausationId = request.SourceEventId,
            RequestedAtUtc = request.ObservedAtUtc,
            ExpiresAtUtc = request.ObservedAtUtc.AddDays(1),
            ExpectedFinancialRevision = selected.FinancialRevision,
            Body = body,
            Access = access
        };
        command = command with { InputSha256 = FinancialCanonicalHash.Request(command) };
        return await _actors.SendAsync<PostFundTransactionCommand, LedgerPortfolioId>(
            command, command.EntityId, cancellationToken).ConfigureAwait(false);
    }

    static Guid DeterministicId(string purpose, string identity) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{identity}"))[..16]);
}
