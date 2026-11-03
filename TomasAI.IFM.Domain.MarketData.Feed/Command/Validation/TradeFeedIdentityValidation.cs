using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Validation;

/// <summary>Checks the Trade identity used for live feed requests.</summary>
public static class TradeFeedIdentityValidation
{

    extension<TErrors>(TErrors errors)
        where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Appends intrinsic Trade ID and subject mismatch errors without throwing.</summary>
        public TErrors ValidateTradeFeedIdentity(
            TomasAI.IFM.Domain.Trade.Shared.TradeEntityId entityId,
            ActorSubject subject,
            string commandName)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (!entityId.IsValid)
                errors.Add(new($"{commandName}.EntityId requires positive PortfolioId, FundId, OrderId, and TradeId."));
            if (!StringComparer.Ordinal.Equals(subject.EntityId, entityId.Format()))
                errors.Add(new($"{commandName}.Subject.EntityId must match EntityId."));
            return errors;
        }
    }

}
