using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Builds idempotent Fund settlement, commission and closing realized-P&amp;L requests.</summary>
public static class BrokerExecutionAccountingModel
{
    /// <summary>
    /// Creates one settlement and one commission posting per confirmed fill. Closing orders additionally
    /// create a realized-P&amp;L posting after an exact opening signed settlement is supplied for every leg.
    /// </summary>
    public static LedgerPostingBatchRequest Create(BrokerExecutionAccountingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var configuration = input.Configuration ?? throw new ArgumentException("Financial posting configuration is required.", nameof(input));
        var order = input.Order ?? throw new ArgumentException("Trade Order is required.", nameof(input));
        ValidateConfirmation(input, configuration, order);
        var settlementRule = RequireRule(configuration, LedgerTransactionKind.TradeSettlement, true);
        var commissionRule = RequireRule(configuration, LedgerTransactionKind.Commission, true);
        var realizedRule = order.PositionType == TradeOrderPositionType.Closing
            ? RequireRule(configuration, LedgerTransactionKind.RealizedPnl, true)
            : null;
        var legs = order.Components.SelectMany(x => x.Legs).ToDictionary(x => x.TradeLegId);
        var items = new List<LedgerPostingRequest>(input.Fills.Length * 2 + 1);
        decimal closingSignedSettlement = 0;
        decimal openingSignedSettlement = 0;
        var accountedOpeningLegs = new HashSet<Guid>();
        foreach (var fill in input.Fills.OrderBy(x => x.ExecutionFillId))
        {
            if (fill.ExecutionFillId == Guid.Empty || fill.ExecutionAttemptId == Guid.Empty ||
                fill.SignedQuantity == 0 || fill.Price <= 0 || fill.Commission < 0 ||
                fill.FilledAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(fill.ExternalExecutionId) ||
                !legs.TryGetValue(fill.TradeLegId, out var leg) || !string.Equals(leg.ContractId,fill.ContractId,StringComparison.Ordinal) ||
                leg.CashMultiplier <= 0 || Math.Sign(leg.SignedQuantity) != Math.Sign(fill.SignedQuantity) ||
                Math.Abs(fill.SignedQuantity) > Math.Abs(leg.SignedQuantity))
                throw new ArgumentException($"Fill {fill.ExecutionFillId} does not match the approved order leg.", nameof(input));

            var signedSettlement = Money(fill.Price * fill.SignedQuantity * leg.CashMultiplier);
            closingSignedSettlement += signedSettlement;
            var source = Source(order, fill);
            var movement = Movement(input.MovementReference, fill, input.ConfirmedAtUtc);
            items.Add(Request(configuration, order, fill, LedgerTransactionKind.TradeSettlement,
                signedSettlement, settlementRule, source, movement, "Confirmed emulator trade settlement"));
            if (fill.Commission > 0)
                items.Add(Request(configuration, order, fill, LedgerTransactionKind.Commission,
                    Money(fill.Commission), commissionRule, source with { FillId = $"{fill.ExternalExecutionId}:commission" },
                    movement with { SourceReference = $"{input.MovementReference}:commission:{fill.ExternalExecutionId}" },
                    "Confirmed emulator commission"));

            if (order.PositionType == TradeOrderPositionType.Closing)
            {
                if (input.OpeningSignedSettlementByLeg is null ||
                    !input.OpeningSignedSettlementByLeg.TryGetValue(fill.TradeLegId, out var opening))
                    throw new ArgumentException($"Opening basis for closing leg {fill.TradeLegId} is required.", nameof(input));
                // The supplied basis is a per-leg total, not a per-fill amount.
                // Broker execution fragmentation must not multiply the opening cost.
                if (accountedOpeningLegs.Add(fill.TradeLegId))
                    openingSignedSettlement += Money(opening);
            }
        }
        if (order.PositionType == TradeOrderPositionType.Closing)
            AddRealizedPnl(input, configuration, order, items, realizedRule!, openingSignedSettlement, closingSignedSettlement);
        var reference = $"BrokerExecution:{order.Id.Format()}:{input.Fills[0].ExecutionAttemptId:N}";
        return new LedgerPostingBatchRequest
        {
            BookId = configuration.BookId,
            Items = [.. items],
            BatchSourceReference = reference,
            ManifestHash = FinancialCanonicalHash.Compute(items)
        };
    }

    /// <summary>Requires a matching approved fund/order and confirmed nonempty UTC fill evidence.</summary>
    static void ValidateConfirmation(BrokerExecutionAccountingInput input,
        FinancialPostingConfiguration configuration, TradeOrderDefinition order)
    {
        if (configuration.BookId <= 0 || configuration.FundId != order.Id.FundId || !order.Id.IsValid ||
            input.Fills.Length == 0 || string.IsNullOrWhiteSpace(input.MovementReference) ||
            input.ConfirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Confirmed broker accounting identity and evidence are incomplete.", nameof(input));
    }

    /// <summary>Realized PnL is ?(opening signed settlement + closing signed settlement), rounded to currency precision; a zero amount still clears unrealized PnL.</summary>
    private static void AddRealizedPnl(BrokerExecutionAccountingInput input,
        FinancialPostingConfiguration configuration, TradeOrderDefinition order, List<LedgerPostingRequest> items,
        LedgerPostingRule realizedRule, decimal openingSignedSettlement, decimal closingSignedSettlement)
    {
        var realized = Money(-(openingSignedSettlement + closingSignedSettlement));
        var first = input.Fills.OrderBy(x => x.ExecutionFillId).First();
        items.Add(Request(configuration, order, first, LedgerTransactionKind.RealizedPnl,
            realized, realizedRule, Source(order, first) with { FillId = $"{first.ExecutionAttemptId:N}:realized" },
            Movement(input.MovementReference, first, input.ConfirmedAtUtc) with { SourceReference = $"{input.MovementReference}:realized" },
            "Realized P&L from confirmed closing fills"));
    }

    private static LedgerPostingRule RequireRule(FinancialPostingConfiguration configuration,
        LedgerTransactionKind kind, bool confirmedMovement)
    {
        var candidates = configuration.Rules.Where(x => x.Kind == kind).ToArray();
        if (candidates.Length != 1 || candidates[0].RequiresConfirmedMovement != confirmedMovement)
            throw new InvalidOperationException($"Exactly one confirmed-movement {kind} posting rule is required.");
        return candidates[0];
    }

    private static LedgerPostingRequest Request(FinancialPostingConfiguration c, TradeOrderDefinition order,
        ExecutionFillEvidence fill, LedgerTransactionKind kind, decimal amount, LedgerPostingRule rule,
        LedgerSourceReference source, LedgerMovementEvidence movement, string description) => new()
        {
            BookId = c.BookId,
            FundId = c.FundId,
            TransactionKind = kind,
            AccountingDate = DateOnly.FromDateTime(fill.FilledAtUtc),
            ValueDate = order.ValueDate,
            SettlementDate = DateOnly.FromDateTime(fill.FilledAtUtc),
            Currency = "USD",
            Amount = amount,
            Description = description,
            Source = source,
            PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
            Authority = c.Authority,
            MovementEvidence = movement
        };

    private static LedgerSourceReference Source(TradeOrderDefinition order, ExecutionFillEvidence fill) => new()
    {
        System = "IBKR-Emulator",
        SourceEntityId = new OrderExecutionId(order.Id, fill.ExecutionAttemptId).Format(),
        SourceEventId = fill.ExecutionFillId,
        SourceSequence = 1,
        SourceContentHash = Hash(fill.ExternalExecutionId, fill.ContractId, fill.SignedQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture), fill.Price.ToString(System.Globalization.CultureInfo.InvariantCulture), fill.Commission.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        OccurredAtUtc = fill.FilledAtUtc,
        OrderId = order.Id.OrderId,
        TradeId = order.Components.Single(x => x.ComponentId == fill.ComponentId).ReservedTradeId,
        FillId = fill.ExternalExecutionId
    };

    private static LedgerMovementEvidence Movement(string reference, ExecutionFillEvidence fill, DateTime confirmedAtUtc) => new()
    {
        Status = MovementStatus.Confirmed,
        SourceReference = $"{reference}:{fill.ExternalExecutionId}",
        ObservedAtUtc = fill.FilledAtUtc,
        ReceivedAtUtc = confirmedAtUtc,
        ValidUntilUtc = confirmedAtUtc.AddDays(1),
        ContentHash = Hash(reference, fill.ExternalExecutionId, fill.Price.ToString(System.Globalization.CultureInfo.InvariantCulture), fill.SignedQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture))
    };

    private static decimal Money(decimal value)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.ToEven);
        if (rounded != value) throw new ArgumentException("Emulator financial amounts must resolve exactly to USD cents.",nameof(value));
        return rounded;
    }

    private static string Hash(params string[] fields) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', fields))));
}
