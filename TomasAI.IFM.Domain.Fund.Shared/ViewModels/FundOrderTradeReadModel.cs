using TomasAI.IFM.Domain.Trade.Shared;
using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade within a fund order,
/// including identifiers, trade metadata, and helper methods for contract parsing.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// derived members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeReadModel
{
    /// <summary>Fund identifier.</summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>Order identifier within the fund.</summary>
    [Key(1)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(2)]
    public int TradeId { get; init; }

    /// <summary>Strategy/type of the option trade.</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Trade execution date.</summary>
    [Key(4)]
    public DateOnly TradeDate { get; init; }

    /// <summary>Maturity/expiry date.</summary>
    [Key(5)]
    public DateOnly MaturityDate { get; init; }

    /// <summary>Lifecycle state of the trade.</summary>
    [Key(6)]
    public TradeState TradeState { get; init; }

    /// <summary>Trade action (Buy/Sell).</summary>
    [Key(7)]
    public TradeAction TradeAction { get; init; }

    /// <summary>Formatted reference string describing the structure (e.g., iron condor legs).</summary>
    [Key(8)]
    public string Reference { get; init; } = string.Empty;

    /// <summary>Indicates if this is the primary trade for the order.</summary>
    [Key(9)]
    public bool PrimaryTrade { get; init; }

    /// <summary>Base contract symbol used when parsing contract ids.</summary>
    [Key(10)]
    public string BaseContractSymbol { get; init; } = string.Empty;

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(11)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created the record.</summary>
    [Key(12)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last updated timestamp, if any.</summary>
    [Key(13)]
    public DateTime? UpdatedOn { get; init; }

    /// <summary>User or system that last updated the record.</summary>
    [Key(14)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public FundOrderTradeReadModel() { }

    /// <summary>
    /// Full constructor to initialize a fund order trade view model.
    /// </summary>
    public FundOrderTradeReadModel(
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeState tradeState,
        TradeAction tradeAction,
        string reference,
        bool primaryTrade,
        string baseContractSymbol,
        DateTime createdOn,
        string createdBy,
        DateTime? updatedOn,
        string updatedBy)
    {
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeState = tradeState;
        TradeAction = tradeAction;
        Reference = reference ?? string.Empty;
        PrimaryTrade = primaryTrade;
        BaseContractSymbol = baseContractSymbol ?? string.Empty;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>True when basic identifiers are set to positive values.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0 && TradeId > 0;

    /// <summary>Derived identifier for this fund-order-trade (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FundOrderTradeId Id => new(FundId, OrderId, TradeId);

    /// <summary>Returns a JSON string representation of this view model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Extracts underlying contract ids from the <see cref="Reference"/> string based on the trade type.
    /// Currently supports Iron Condor patterns (P:put strikes, C:call strikes).
    /// </summary>
    public string[] GetContractIds()
    {
        var contractIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(Reference))
        {
            switch (TradeType)
            {
                case TradeType.ShortIronCondor:
                case TradeType.LongIronCondor:
                    contractIds.AddRange(ParseIronCondorContractIds());
                    break;
            }
        }
        return contractIds.ToArray();
    }

    /// <summary>
    /// Parses the iron condor reference into individual option contract ids.
    /// </summary>
    private string[] ParseIronCondorContractIds()
    {
        var contractIds = new List<string>();
        var spreadLegs = Reference.ToUpper().Split(new[] { "X" }, StringSplitOptions.RemoveEmptyEntries);
        if (spreadLegs.Length == 2)
        {
            var putSpreadLeg = spreadLegs.Where(e => e.Contains("P")).SingleOrDefault();
            if (putSpreadLeg != null)
            {
                var putStrikes = putSpreadLeg.Replace("P", "").Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (putStrikes.Length == 2)
                {
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}P{putStrikes[0].Trim()}");
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}P{putStrikes[1].Trim()}");
                }
            }
            var callSpreadLeg = spreadLegs.Where(e => e.Contains("C")).SingleOrDefault();
            if (callSpreadLeg != null)
            {
                var callStrikes = callSpreadLeg.Replace("C", "").Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (callStrikes.Length == 2)
                {
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}C{callStrikes[0].Trim()}");
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}C{callStrikes[1].Trim()}");
                }
            }
        }
        return contractIds.ToArray();
    }
}

/// <summary>Intrinsic validation rules for command payloads carrying a fund-order trade.</summary>
public sealed class FundOrderTradeValidationRules : BaseValidationRules, IValidationRules<FundOrderTradeReadModel>
{
    static readonly FundOrderTradeValidator Validator = new();

    public ValidationError[] Execute(FundOrderTradeReadModel fundOrderTrade)
        => Validate(fundOrderTrade, Validator);

    sealed class FundOrderTradeValidator : AbstractValidator<FundOrderTradeReadModel>
    {
        public FundOrderTradeValidator()
        {
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundOrderTrade.FundId is zero or negative");
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundOrderTrade.OrderId is zero or negative");
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("FundOrderTrade.TradeId is zero or negative");
            RuleFor(x => x.TradeType)
                .Must(value => Enum.IsDefined(value) && value != TradeType.Unknown)
                .WithMessage("FundOrderTrade.TradeType is invalid");
            RuleFor(x => x.TradeDate).Must(IsValidDate).WithMessage("FundOrderTrade.TradeDate is invalid");
            RuleFor(x => x.MaturityDate).Must(IsValidDate).WithMessage("FundOrderTrade.MaturityDate is invalid");
            RuleFor(x => x.MaturityDate)
                .GreaterThanOrEqualTo(x => x.TradeDate)
                .When(x => IsValidDate(x.TradeDate) && IsValidDate(x.MaturityDate))
                .WithMessage("FundOrderTrade.MaturityDate must not precede TradeDate");
            RuleFor(x => x.TradeState).Must(Enum.IsDefined).WithMessage("FundOrderTrade.TradeState is invalid");
            RuleFor(x => x.TradeAction).Must(Enum.IsDefined).WithMessage("FundOrderTrade.TradeAction is invalid");
            RuleFor(x => x.Reference).NotEmpty().WithMessage("FundOrderTrade.Reference is empty");
            // PrimaryTrade uses the complete Boolean domain; both values are valid.
            RuleFor(x => x.BaseContractSymbol).NotEmpty().WithMessage("FundOrderTrade.BaseContractSymbol is empty");
            RuleFor(x => x.CreatedOn).Must(IsValidDateTime).WithMessage("FundOrderTrade.CreatedOn is invalid");
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage("FundOrderTrade.CreatedBy is empty");
            RuleFor(x => x.UpdatedOn)
                .Must(value => value is null || IsValidDateTime(value.Value))
                .WithMessage("FundOrderTrade.UpdatedOn is invalid");
            RuleFor(x => x.UpdatedBy).NotNull().WithMessage("FundOrderTrade.UpdatedBy is null");
            RuleFor(x => x.UpdatedBy)
                .NotEmpty()
                .When(x => x.UpdatedOn.HasValue)
                .WithMessage("FundOrderTrade.UpdatedBy is empty when UpdatedOn is set");
        }

        static bool IsValidDateTime(DateTime value)
            => value > DateTime.MinValue && value < DateTime.MaxValue;

        static bool IsValidDate(DateOnly value)
            => value > DateOnly.MinValue && value < DateOnly.MaxValue;

        public override ValidationResult Validate(ValidationContext<FundOrderTradeReadModel> context)
        {
            if (context.InstanceToValidate is null)
                return new ValidationResult([new ValidationFailure("FundOrderTrade", "FundOrderTrade instance is null")]);
            return base.Validate(context);
        }
    }
}

/// <summary>Adapts intrinsic fund-order-trade rules to the aggregate command-error list.</summary>
public static class FundOrderTradeReadModelValidationExtensions
{
    static readonly FundOrderTradeValidationRules Rules = new();

    public static List<ValidationError> ValidateFundOrderTrade(
        this List<ValidationError> validationErrors,
        FundOrderTradeReadModel? fundOrderTrade)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        validationErrors.AddRange(Rules.Execute(fundOrderTrade!));
        return validationErrors;
    }
}
