using System.Collections.Immutable;
using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents a view model for a fund order, encapsulating details such as the fund, order, trade, and maturity dates,
/// as well as metadata about the order's creation and updates.
/// </summary>
/// <remarks>This record is designed to provide a structured representation of a fund order, including its unique
/// identifiers, status, associated contract, and audit information. It also includes derived properties for validation
/// and composite identification.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderReadModel
{
    [Key(0)]
    public int FundId { get; init; }
    [Key(1)]
    public int OrderId { get; init; }   
    [Key(2)]
    public DateTime OrderDate { get; init; }
    [Key(3)]
    public OrderStatus OrderStatus { get; init; }
    [Key(4)]
    public string BaseContractId { get; init; }
    [Key(5)]
    public DateOnly TradeDate { get; init; }
    [Key(6)]
    public DateOnly MaturityDate { get; init; }
    [Key(7)]
    public string Reference { get; init; }
    [Key(8)]
    public DateTime CreatedOn { get; init; }
    [Key(9)]
    public string CreatedBy { get; init; }
    [Key(10)]
    public DateTime? UpdatedOn { get; init; }
    [Key(11)]
    public string UpdatedBy { get; init; }


    public FundOrderReadModel(
        int fundId, 
        int orderId,
        DateTime orderDate,
        OrderStatus orderStatus,
        string baseContractId,
        DateOnly tradeDate,
        DateOnly maturityDate,
        string reference,
        DateTime createdOn,
        string createdBy,
        DateTime? updatedOn,
        string updatedBy)
    {
        FundId = fundId;
        OrderId = orderId;
        OrderDate = orderDate;
        OrderStatus = orderStatus;
        BaseContractId = baseContractId;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        Reference = reference;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
    }

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0;
    [JsonIgnore]
    [IgnoreMember]
    public FundOrderId Id => new (FundId, OrderId);
    [JsonIgnore]
    [IgnoreMember]
    List<FundOrderTradeReadModel>? _trades;
    [JsonProperty]
    [IgnoreMember]
    public ImmutableArray<FundOrderTradeReadModel> Trades => _trades == null ? [] : [.. _trades];
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Adds a new trade to the collection of fund order trades.
    /// </summary>
    /// <remarks>If the collection of trades is uninitialized, it will be initialized before adding the
    /// trade.</remarks>
    /// <param name="fundOrderTrade">The trade to add to the collection. Cannot be <see langword="null"/>.</param>
    public void Add(FundOrderTradeReadModel fundOrderTrade)
    {
        _trades ??= [];
        _trades.Add(fundOrderTrade);
    }

}

/// <summary>Intrinsic validation rules for command payloads carrying a fund order.</summary>
public sealed class FundOrderValidationRules : BaseValidationRules, IValidationRules<FundOrderReadModel>
{
    static readonly FundOrderValidator Validator = new();

    public ValidationError[] Execute(FundOrderReadModel fundOrder)
        => Validate(fundOrder, Validator);

    sealed class FundOrderValidator : AbstractValidator<FundOrderReadModel>
    {
        public FundOrderValidator()
        {
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundOrder.FundId is zero or negative");
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundOrder.OrderId is zero or negative");
            RuleFor(x => x.OrderDate).Must(IsValidDateTime).WithMessage("FundOrder.OrderDate is not a valid date");
            RuleFor(x => x.OrderStatus).Must(Enum.IsDefined).WithMessage("FundOrder.OrderStatus is invalid");
            RuleFor(x => x.BaseContractId).NotEmpty().WithMessage("FundOrder.BaseContractId is empty");
            RuleFor(x => x.TradeDate).Must(IsValidDate).WithMessage("FundOrder.TradeDate is invalid");
            RuleFor(x => x.MaturityDate).Must(IsValidDate).WithMessage("FundOrder.MaturityDate is invalid");
            RuleFor(x => x.MaturityDate)
                .GreaterThanOrEqualTo(x => x.TradeDate)
                .When(x => IsValidDate(x.TradeDate) && IsValidDate(x.MaturityDate))
                .WithMessage("FundOrder.MaturityDate must not precede TradeDate");
            RuleFor(x => x.Reference).NotNull().WithMessage("FundOrder.Reference is null");
            RuleFor(x => x.CreatedOn).Must(IsValidDateTime).WithMessage("FundOrder.CreatedOn is invalid");
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage("FundOrder.CreatedBy is empty");
            RuleFor(x => x.UpdatedOn)
                .Must(value => value is null || IsValidDateTime(value.Value))
                .WithMessage("FundOrder.UpdatedOn is invalid");
            RuleFor(x => x.UpdatedBy).NotNull().WithMessage("FundOrder.UpdatedBy is null");
            RuleFor(x => x.UpdatedBy)
                .NotEmpty()
                .When(x => x.UpdatedOn.HasValue)
                .WithMessage("FundOrder.UpdatedBy is empty when UpdatedOn is set");
        }

        static bool IsValidDateTime(DateTime value)
            => value > DateTime.MinValue && value < DateTime.MaxValue;

        static bool IsValidDate(DateOnly value)
            => value > DateOnly.MinValue && value < DateOnly.MaxValue;

        public override ValidationResult Validate(ValidationContext<FundOrderReadModel> context)
        {
            if (context.InstanceToValidate is null)
                return new ValidationResult([new ValidationFailure("FundOrder", "FundOrder instance is null")]);
            return base.Validate(context);
        }
    }
}

/// <summary>Adapts intrinsic fund-order rules to the aggregate command-error list.</summary>
public static class FundOrderReadModelValidationExtensions
{
    static readonly FundOrderValidationRules Rules = new();

    public static List<ValidationError> ValidateFundOrder(
        this List<ValidationError> validationErrors,
        FundOrderReadModel? fundOrder)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        validationErrors.AddRange(Rules.Execute(fundOrder!));
        return validationErrors;
    }
}
