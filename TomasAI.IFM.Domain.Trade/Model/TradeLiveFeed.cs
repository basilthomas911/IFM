using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeLiveFeed : IDataValidation, ITradeLiveFeed
{
    static IValidator<TradeLiveFeed>? _validator;

    public TradeLiveFeed(
        int orderId,
        int tradeId,
        bool liveFeed)
   {
        OrderId = orderId;
        TradeId = tradeId;
        LiveFeed = liveFeed;
        _validator = _validator ?? new TradeLiveFeedValidator();
        this.Validate(_validator);
    }

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public bool LiveFeed { get; private set; }

    public TradeLiveFeedReadModel ToViewModel()
        => new 
        (
            orderId: OrderId,
            tradeId: TradeId,
            liveFeed: LiveFeed
        );
}

public class TradeLiveFeedValidator : AbstractValidator<TradeLiveFeed>
{
    public TradeLiveFeedValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("TradeLiveFeed.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeLiveFeed.TradeId is zero or negative");
    }
}
