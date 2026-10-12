using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates the discriminator and payload of a market-series identity.</summary>
public sealed class MarketSeriesIdentityValidationRules
    : BaseValidationRules, IValidationStructRules<MarketSeriesIdentity>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied market-series identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(MarketSeriesIdentity value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketSeriesIdentity>
    {
        public Validator()
        {
            RuleFor(x => x).Must(IsValid).WithMessage(
                "MarketSeriesIdentity must contain exactly the payload selected by Kind.");
        }

        static bool IsValid(MarketSeriesIdentity value) => value.Kind switch
        {
            MarketSeriesIdentityKind.Contract =>
                !string.IsNullOrWhiteSpace(value.ContractId) && value.FuturesSeriesId is null,
            MarketSeriesIdentityKind.FuturesContinuation =>
                string.IsNullOrEmpty(value.ContractId)
                && value.FuturesSeriesId is { } series
                && new FuturesSeriesIdValidationRules().Execute(series).Length == 0,
            _ => false
        };
    }
}
