using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Validation;

/// <summary>Validates shared order definitions and execution evidence without state or I/O.</summary>
public static class TradeOrderDefinitionValidation
{
    private static readonly OrderRules Orders = new();
    private static readonly FillRules Fills = new();

    /// <summary>Returns every structural order error, including nested component and leg errors.</summary>
    public static string[] Validate(TradeOrderDefinition? order) => order is null
        ? ["Order is required."]
        : [.. Orders.Validate(order).Errors.Select(static error => error.ErrorMessage)];

    /// <summary>Appends all shared order definition failures to the caller's errors.</summary>
    public static List<ValidationError> ValidateTradeOrderDefinition(this List<ValidationError> errors, TradeOrderDefinition? order)
    {
        errors.AddRange(Validate(order).Select(static error => new ValidationError(error)));
        return errors;
    }

    /// <summary>Appends all normalized execution fill failures, handling a missing payload explicitly.</summary>
    public static List<ValidationError> ValidateExecutionFill(this List<ValidationError> errors, ExecutionFillEvidence? fill)
    {
        if (fill is null) errors.Add(new("Fill is required."));
        else errors.AddRange(Fills.Validate(fill).Errors.Select(static error => new ValidationError(error.ErrorMessage)));
        return errors;
    }

    private static readonly EstablishedTradeRules Trades = new();

    /// <summary>Appends all established Trade evidence errors, including missing nested fills and legs.</summary>
    public static List<ValidationError> ValidateEstablishedTrade(this List<ValidationError> errors, EstablishedTradeDefinition? trade)
    {
        if (trade is null) errors.Add(new("Trade is required."));
        else errors.AddRange(Trades.Validate(trade).Errors.Select(static error => new ValidationError(error.ErrorMessage)));
        return errors;
    }

    private sealed class EstablishedTradeRules : AbstractValidator<EstablishedTradeDefinition>
    {
        public EstablishedTradeRules()
        {
            RuleFor(trade => trade.SchemaVersion).InclusiveBetween((ushort)1, (ushort)3);
            RuleFor(trade => trade.Id).Must(id => id.IsValid);
            RuleFor(trade => trade.AssetFamily).IsInEnum().NotEqual(TradeAssetFamily.Unknown);
            RuleFor(trade => trade.StrategyKind).IsInEnum().NotEqual(TradeStrategyKind.Unknown);
            RuleFor(trade => trade.SourceComponentId).NotEmpty();
            RuleFor(trade => trade.ExecutionAttemptId).NotEmpty();
            RuleFor(trade => trade.Status).IsInEnum();
            RuleFor(trade => trade.Legs).NotEmpty();
            When(trade => trade.Legs is not null, () =>
            {
                RuleForEach(trade => trade.Legs).NotNull().SetValidator(new LegRules());
                RuleForEach(trade => trade.Legs).ChildRules(leg =>
                {
                    leg.RuleFor(value => value.TradeLegId).NotEmpty();
                    leg.RuleFor(value => value.ContractId).NotEmpty();
                    leg.RuleFor(value => value.SignedQuantity).NotEqual(0);
                });
            });
            RuleFor(trade => trade.OriginalFills).NotEmpty();
            When(trade => trade.OriginalFills is not null, () => RuleForEach(trade => trade.OriginalFills).NotNull().SetValidator(new FillRules()));
            RuleFor(trade => trade.OpeningCommission).GreaterThanOrEqualTo(0);
            RuleFor(trade => trade.EstablishedAtUtc).Must(time => time.Kind == DateTimeKind.Utc);
            RuleFor(trade => trade.EvidenceRevision).GreaterThanOrEqualTo(0);
            RuleFor(trade => trade.ClosingFills).NotNull();
            When(trade => trade.ClosingFills is not null, () => RuleForEach(trade => trade.ClosingFills).NotNull().SetValidator(new FillRules()));
            RuleFor(trade => trade.ClosedAtUtc).Must(time => time is null || time.Value.Kind == DateTimeKind.Utc);
        }
    }

    private sealed class OrderRules : AbstractValidator<TradeOrderDefinition>
    {
        public OrderRules()
        {
            RuleFor(order => order).Custom((order, context) =>
            {
                foreach (var error in TradeOrderDefinitionChecks.Validate(order)) context.AddFailure(error);
            });
            RuleFor(order => order.SchemaVersion).InclusiveBetween((ushort)1, (ushort)4);
            RuleFor(order => order.Status).IsInEnum();
            RuleFor(order => order.PositionType).IsInEnum();
            RuleFor(order => order.Origin).NotNull();
            RuleFor(order => order.DefinitionHash).NotNull();
            RuleFor(order => order.BoundExecutionAttemptId).Must(id => id is null || id != Guid.Empty);
            RuleFor(order => order.BoundExecutionChannel).Must(channel => channel is null || Enum.IsDefined(channel.Value));
            RuleFor(order => order.ExecutionBoundAtUtc).Must(time => time is null || time.Value.Kind == DateTimeKind.Utc);
            RuleFor(order => order.BrokerAccountAlias).NotNull();
            RuleFor(order => order.BrokerEnvironment).IsInEnum();
            RuleFor(order => order.MicroExecutionProfileId).NotNull();
            RuleFor(order => order.MicroExecutionProfileVersion).GreaterThanOrEqualTo(0);
            RuleFor(order => order.MicroExecutionProfileHash).NotNull();
            RuleFor(order => order.AccountPromotionApprovalReference).NotNull();
            RuleFor(order => order.RequiredCapital).GreaterThanOrEqualTo(0);
            RuleFor(order => order.MaximumLoss).GreaterThanOrEqualTo(0);
            RuleFor(order => order.BrokerOrderType).IsInEnum();
            RuleFor(order => order.BrokerAlgorithm).IsInEnum();
            RuleFor(order => order.TimeInForce).NotEmpty();
            RuleFor(order => order.AlgorithmPace).NotEmpty();
            When(order => order.Components is not null, () =>
                RuleForEach(order => order.Components).SetValidator(new ComponentRules()));
        }
    }

    private sealed class ComponentRules : AbstractValidator<TradeOrderComponentDefinition>
    {
        public ComponentRules()
        {
            RuleFor(component => component.StrategyKind).IsInEnum();
            RuleFor(component => component.TickIncrement).Must(tick => tick is null || tick > 0);
            RuleFor(component => component).Must(component => component.MinimumSignedNetDebitLimit is null ||
                component.MaximumSignedNetDebitLimit is null || component.MinimumSignedNetDebitLimit <= component.MaximumSignedNetDebitLimit)
                .WithMessage("MinimumSignedNetDebitLimit cannot exceed MaximumSignedNetDebitLimit.");
            When(component => component.Legs is not null, () =>
                RuleForEach(component => component.Legs).SetValidator(new LegRules()));
        }
    }

    private sealed class LegRules : AbstractValidator<TradeLegDefinition>
    {
        public LegRules()
        {
            RuleFor(leg => leg.AssetFamily).IsInEnum();
            RuleFor(leg => leg.ContractKey).NotNull();
            RuleFor(leg => leg.Expiry).Must(expiry => expiry is null || expiry != default(DateOnly));
            RuleFor(leg => leg.Strike).Must(strike => strike is null || strike > 0);
            RuleFor(leg => leg.PutCall).Must(putCall => putCall is null || putCall is 0 or 1);
            RuleFor(leg => leg.CashMultiplier).GreaterThanOrEqualTo(0);
        }
    }

    private sealed class FillRules : AbstractValidator<ExecutionFillEvidence>
    {
        public FillRules()
        {
            RuleFor(fill => fill.ExecutionFillId).NotEmpty();
            RuleFor(fill => fill.ExecutionAttemptId).NotEmpty();
            RuleFor(fill => fill.ComponentId).NotEmpty();
            RuleFor(fill => fill.TradeLegId).NotEmpty();
            RuleFor(fill => fill.SignedQuantity).NotEqual(0);
            RuleFor(fill => fill.Price).GreaterThan(0);
            RuleFor(fill => fill.Commission).GreaterThanOrEqualTo(0);
            RuleFor(fill => fill.FilledAtUtc).Must(time => time.Kind == DateTimeKind.Utc);
            RuleFor(fill => fill.ExternalExecutionId).NotNull();
            RuleFor(fill => fill.ContractId).NotEmpty();
        }
    }
}
