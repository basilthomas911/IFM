using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Unique identifier for a futures Traders Dynamic Index (TDI) signal composed of a contract identifier,
/// a value date, and a timestamp component for intraday distinction.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot
/// separated format: ContractId.yyyy-MM-dd.HH:mm:ss. Use <see cref="Create(string, DateOnly, TimeOnly)"/> when
/// explicit construction improves clarity.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTdiSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the TDI signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Timestamp (intraday time component) linked to the signal generation.</summary>
    [Key(2)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Sampling period represented by this signal.</summary>
    [Key(3)]
    public TimeFrameType TimePeriod { get; init; }

    /// <summary>Stable identifier of the calculation contract.</summary>
    [Key(4)]
    public string ConfigurationId { get; init; } = FuturesTdiConfiguration.StandardConfigurationId;

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesTdiSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesTdiSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesTdiSignalId(string contractId, DateOnly valueDate, TimeOnly timestamp)
        : this(contractId, valueDate, TimeFrameType.OneMinute, timestamp)
    {
    }

    /// <summary>Initializes a configuration-aware intraday TDI signal identifier.</summary>
    public FuturesTdiSignalId(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        ConfigurationId = configurationId;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesTdiSignalId Create(string contractId, DateOnly valueDate, TimeOnly timestamp)
        => new(contractId, valueDate, timestamp);

    public static FuturesTdiSignalId Create(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
        => new(contractId, valueDate, timePeriod, timestamp, configurationId);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyy-MM-dd.HH:mm:ss
    /// </summary>
    public string Format() => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}.{Timestamp:HH:mm:ss}.{ConfigurationId}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Provides validation rules for <see cref="FuturesTdiSignalId"/> instances using FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// This class defines validation constraints to ensure that a <see cref="FuturesTdiSignalId"/> is properly
/// constructed with valid data before being used in domain operations or persistence scenarios.
/// </para>
/// <para>
/// Validation rules include:
/// <list type="bullet">
/// <item><description>ContractId must not be null or empty</description></item>
/// <item><description>ValueDate must be greater than DateOnly.MinValue</description></item>
/// <item><description>ValueDate must be less than DateOnly.MaxValue</description></item>
/// <item><description>Timestamp must be greater than TimeOnly.MinValue</description></item>
/// <item><description>Timestamp must be less than TimeOnly.MaxValue</description></item>
/// </list>
/// </para>
/// <para>
/// Use the <see cref="Execute"/> method to validate an instance and retrieve any validation errors.
/// </para>
/// </remarks>
public class FuturesTdiSignalIdValidationRules : BaseValidationRules, IValidationRules<FuturesTdiSignalId>
{
    public const string InstanceErrorMessage = "FuturesTdiSignalId instance is null";
    public const string ContractIdErrorMessage = "FuturesTdiSignalId: ContractId is required";
    public const string ValueDateMinErrorMessage = "FuturesTdiSignalId: ValueDate must be greater than DateOnly.MinValue";
    public const string ValueDateMaxErrorMessage = "FuturesTdiSignalId: ValueDate must be less than DateOnly.MaxValue";
    public const string TimestampMinErrorMessage = "FuturesTdiSignalId: Timestamp must be greater than TimeOnly.MinValue";
    public const string TimestampMaxErrorMessage = "FuturesTdiSignalId: Timestamp must be less than TimeOnly.MaxValue";

    /// <summary>
    /// Validates the specified <see cref="FuturesTdiSignalId"/> instance against defined rules.
    /// </summary>
    /// <param name="tdiSignalId">The TDI signal identifier instance to validate.</param>
    /// <returns>
    /// An array of <see cref="ValidationError"/> instances describing any validation failures.
    /// Returns an empty array if validation succeeds.
    /// </returns>
    static readonly FuturesTdiSignalIdValidator Validator = new();

    public ValidationError[] Execute(FuturesTdiSignalId tdiSignalId) => Validate(tdiSignalId, Validator);

    class FuturesTdiSignalIdValidator : AbstractValidator<FuturesTdiSignalId>
    {
        public FuturesTdiSignalIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage(TimestampMinErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage(TimestampMaxErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesTdiSignalId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTdiSignalId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
