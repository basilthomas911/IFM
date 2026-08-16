using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Unique identifier for a Futures MACD (Moving Average Convergence Divergence) signal composed of a contract identifier,
/// a value date, all three EMA periods, and a timestamp component for intraday distinction.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot
/// separated format: ContractId.yyyyMMdd.TimePeriod.SignalEma.FastEma.SlowEma.HH:mm:ss.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeFrameType TimePeriod { get; init; }
    [Key(3)] public int SignalEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSignalEmaPeriod;
    [Key(4)] public TimeOnly Timestamp { get; init; }
    [Key(5)] public int FastEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalFastEmaPeriod;
    [Key(6)] public int SlowEmaPeriod { get; init; } = FuturesMacdConfiguration.ConventionalSlowEmaPeriod;

    [IgnoreMember]
    [Obsolete("Use SignalEmaPeriod, FastEmaPeriod, and SlowEmaPeriod.")]
    public int PeriodLength => SignalEmaPeriod;

    [IgnoreMember]
    public FuturesMacdConfiguration Configuration
        => new(SignalEmaPeriod, FastEmaPeriod, SlowEmaPeriod);

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
    /// <param name="signalEmaPeriod">Signal-line EMA period.</param>
    /// <param name="fastEmaPeriod">Fast EMA period.</param>
    /// <param name="slowEmaPeriod">Slow EMA period.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesMacdSignalId(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        TimeOnly timestamp)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;
        Timestamp = timestamp;
    }

    /// <summary>Creates a conventional MACD identity with a configurable signal EMA period.</summary>
    public FuturesMacdSignalId(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        TimeOnly timestamp)
        : this(
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod,
            timestamp)
    {
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesMacdSignalId Create(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, timestamp);

    public static FuturesMacdSignalId Create(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, signalEmaPeriod, timestamp);

    /// <summary>
    /// Formats the identifier into a stable string key containing all three EMA periods.
    /// </summary>
    public string Format() => string.Create(
        null,
        stackalloc char[128],
        $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{SignalEmaPeriod}.{FastEmaPeriod}.{SlowEmaPeriod}.{Timestamp:HH:mm:ss}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>
    /// Converts this identifier to a corresponding <see cref="FuturesMacdSignalEntityId"/> instance
    /// </summary>
    /// <returns></returns>
    public FuturesMacdSignalEntityId ToEntityId()
        => new(ContractId, ValueDate, TimePeriod, SignalEmaPeriod, FastEmaPeriod, SlowEmaPeriod);

    public FuturesMacdDailySignalEntityId ToDailyEntityId()
        => new(ContractId, TimePeriod, SignalEmaPeriod, FastEmaPeriod, SlowEmaPeriod);
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

    static readonly FuturesMacdSignalIdValidator Validator = new();

    public ValidationError[] Execute(FuturesMacdSignalId macdSignalId) => Validate(macdSignalId, Validator);

    class FuturesMacdSignalIdValidator : AbstractValidator<FuturesMacdSignalId>
    {
        public FuturesMacdSignalIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage(TimestampMinErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage(TimestampMaxErrorMessage);
            RuleFor(x => x.SignalEmaPeriod).GreaterThan(0);
            RuleFor(x => x.FastEmaPeriod).GreaterThan(0);
            RuleFor(x => x.SlowEmaPeriod).GreaterThan(0);
            RuleFor(x => x).Must(x => x.FastEmaPeriod < x.SlowEmaPeriod)
                .WithMessage("FuturesMacdSignalId: FastEmaPeriod must be less than SlowEmaPeriod");
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


