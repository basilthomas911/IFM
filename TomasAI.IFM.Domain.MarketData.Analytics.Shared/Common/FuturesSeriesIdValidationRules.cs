using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates a roll-aware futures continuation identity.</summary>
public sealed class FuturesSeriesIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesSeriesId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied continuation identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesSeriesId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesSeriesId>
    {
        public Validator()
        {
            RuleFor(x => x.RootSymbol).NotEmpty();
            RuleFor(x => x.RollRuleId).NotEmpty();
            RuleFor(x => x.AdjustmentRuleId).NotEmpty();
            RuleFor(x => x.Revision).GreaterThan((ushort)0);
        }
    }
}
