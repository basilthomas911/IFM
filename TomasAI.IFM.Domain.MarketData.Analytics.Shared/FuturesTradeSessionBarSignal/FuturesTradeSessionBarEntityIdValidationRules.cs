using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Validates a futures analytics observation coordinator identity.</summary>
public sealed class FuturesTradeSessionBarEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesTradeSessionBarEntityId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied entity identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarEntityId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarEntityId>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
        }
    }
}
