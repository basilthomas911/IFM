using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Unique identifier for a Futures MACD (Moving Average Convergence Divergence) signal composed of a contract identifier,
/// a value date, and a timestamp component for intraday distinction.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot
/// separated format: ContractId.yyyyMMdd.TimePeriod.HH:mm:ss. Use <see cref="Create(string, DateOnly, TimeOnly)"/> when
/// explicit construction improves clarity.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)] public int PeriodLength { get; init; }
    [Key(4)] public TimeOnly Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesMacdSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesMacdSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timePeriod">Futures time period type.</param>
    /// <param name="periodLength">The length of the time period.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesMacdSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesMacdSignalId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, periodLength, timestamp);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod.HH:mm:ss
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[96], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}.{Timestamp:HH:mm:ss}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>
    /// Converts this identifier to a corresponding <see cref="FuturesMacdSignalEntityId"/> instance
    /// </summary>
    /// <returns></returns>
    public FuturesMacdSignalEntityId ToEntityId() => new(ContractId, ValueDate, TimePeriod, PeriodLength);
    public FuturesMacdDailySignalEntityId ToDailyEntityId() => new(ContractId, TimePeriod, PeriodLength);
}

/// <summary>
/// Validation rules for <see cref="FuturesMacdSignalId"/>.
/// </summary>
public class FuturesMacdSignalIdValidationRules : BaseValidationRules, IValidationRules<FuturesMacdSignalId>
{
    public const string InstanceErrorMessage = "FuturesMacdSignalId instance is null";
    public const string ContractIdErrorMessage = "FuturesMacdSignalId: ContractId is required";
    public const string ValueDateMinErrorMessage = "FuturesMacdSignalId: ValueDate must be greater than DateOnly.MinValue";
    public const string ValueDateMaxErrorMessage = "FuturesMacdSignalId: ValueDate must be less than DateOnly.MaxValue";
    public const string TimestampMinErrorMessage = "FuturesMacdSignalId: Timestamp must be greater than TimeOnly.MinValue";
    public const string TimestampMaxErrorMessage = "FuturesMacdSignalId: Timestamp must be less than TimeOnly.MaxValue";

    public ValidationError[] Execute(FuturesMacdSignalId macdSignalId) => Validate(macdSignalId, new FuturesMacdSignalIdValidator());

    class FuturesMacdSignalIdValidator : AbstractValidator<FuturesMacdSignalId>
    {
        public FuturesMacdSignalIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage(TimestampMinErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage(TimestampMaxErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesMacdSignalId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesMacdSignalId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}


