using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Validates a VX front/back stream identity.</summary>
public sealed class FuturesVxTermStructureSignalEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesVxTermStructureSignalEntityId>
{
    static readonly Validator Rules = new();
    /// <inheritdoc />
    public ValidationError[] Execute(FuturesVxTermStructureSignalEntityId value) => Validate(value, Rules);
    sealed class Validator : AbstractValidator<FuturesVxTermStructureSignalEntityId>
    {
        public Validator()
        {
            RuleFor(x => x.ValueDate).NotEqual(default(DateOnly));
            RuleFor(x => x.FrontContractId).NotEmpty();
            RuleFor(x => x.BackContractId).NotEmpty().NotEqual(x => x.FrontContractId);
            RuleFor(x => x.ConfigurationId).NotEmpty();
        }
    }
}
