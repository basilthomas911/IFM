using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Validates a deterministic futures analytics observation identity.</summary>
public sealed class FuturesTradeSessionBarIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesTradeSessionBarId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied observation identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarId>
    {
        public Validator() => RuleFor(x => x.Value).NotEmpty();
    }
}
