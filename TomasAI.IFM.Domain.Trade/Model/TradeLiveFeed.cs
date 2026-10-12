using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeLiveFeed : IDataValidation, ITradeLiveFeed
{
    static readonly IValidator<TradeLiveFeed> _validator = new TradeLiveFeedValidator();
    public TradeLiveFeed(int orderId, int tradeId, bool liveFeed)
    {
        OrderId = orderId;
        TradeId = tradeId;
        LiveFeed = liveFeed;
        this.Validate(_validator);
    }

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public bool LiveFeed { get; private set; }

    public TradeLiveFeedReadModel ToViewModel() => new(orderId: OrderId, tradeId: TradeId, liveFeed: LiveFeed);
}
