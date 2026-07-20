using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Unique identifier for a futures RSI signal composed of a contract identifier and a value date.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// format: ContractId.yyyy-MM-dd. Provides factory and formatting helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiDailySignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the RSI signal.</summary>
    [Key(1)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(2)]
    public int PeriodLength { get; init; }  

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesRsiDailySignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesRsiSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="timePeriod">The type of RSI signal.</param>
    /// <param name="periodLength"></param>
    public FuturesRsiDailySignalEntityId(string contractId, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="timePeriod">The type of RSI signal.</param>
    /// <param name="periodLength">The length of the RSI period.</param>
    public static FuturesRsiDailySignalEntityId Create(string contractId, TradeTimePeriodType timePeriod, int periodLength) 
        => new(contractId, timePeriod, periodLength);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.SignalType
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Validation rules for <see cref="FuturesRsiDailySignalEntityId"/>.
/// </summary>
public class FuturesRsiDailySignalEntityIdValidationRules : BaseValidationRules, IValidationRules<FuturesRsiDailySignalEntityId>
{
    public const string InstanceErrorMessage = "FuturesRsiSignalEntityId instance is null";
    public const string ContractIdErrorMessage = "FuturesRsiSignalEntityId: ContractId is required";
    public const string TimePeriodErrorMessage = "FuturesRsiSignalEntityId: TimePeriod must be a valid value other than None";

    public ValidationError[] Execute(FuturesRsiDailySignalEntityId rsiSignalEntityId) => Validate(rsiSignalEntityId, new FuturesRsiDailySignalEntityIdValidator());

    class FuturesRsiDailySignalEntityIdValidator : AbstractValidator<FuturesRsiDailySignalEntityId>
    {
        public FuturesRsiDailySignalEntityIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.PeriodLength).GreaterThan(0).WithMessage("FuturesRsiSignalEntityId: PeriodLength must be a positive integer");
            RuleFor(x => x.TimePeriod).IsInEnum().NotEqual(TradeTimePeriodType.None).WithMessage(TimePeriodErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesRsiDailySignalEntityId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesRsiDailySignalEntityId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}

