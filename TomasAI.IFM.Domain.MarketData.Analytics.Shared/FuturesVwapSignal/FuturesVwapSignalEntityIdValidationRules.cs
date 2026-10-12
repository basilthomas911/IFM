using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Validates a futures-session VWAP identity.</summary>
public sealed class FuturesVwapSignalEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesVwapSignalEntityId>
{
    static readonly Validator Rules = new();
    /// <inheritdoc />
    public ValidationError[] Execute(FuturesVwapSignalEntityId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesVwapSignalEntityId>
    {
        public Validator()
        {
            RuleFor(value => value.ContractId).NotEmpty();
            RuleFor(value => value.ValueDate).NotEqual(default(DateOnly));
            RuleFor(value => value.ConfigurationId).NotEmpty();
        }
    }
}
