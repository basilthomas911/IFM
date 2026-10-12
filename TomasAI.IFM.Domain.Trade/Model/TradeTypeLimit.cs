using TomasAI.IFM.Domain.Trade.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeTypeLimit : IDataValidation, ITradeTypeLimit
{
    static readonly IValidator<TradeTypeLimit> _validator = new TradeTypeLimitValidator();
    public int TradeId { get; private set; }
    public TradeType TradeType { get; private set; }
    public decimal MaxLossLimit { get; private set; }
    public decimal MinProfitLimit { get; private set; }
    public decimal MaxProfitLimit { get; private set; }

    public TradeTypeLimit(int tradeId, TradeType tradeType, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        MaxLossLimit = maxLossLimit;
        MinProfitLimit = minProfitLimit;
        MaxProfitLimit = maxProfitLimit;
        this.Validate(_validator);
    }

    public TradeTypeLimitReadModel ToViewModel() => new(tradeId: TradeId, tradeType: TradeType, maxLossLimit: MaxLossLimit, minProfitLimit: MinProfitLimit, maxProfitLimit: MaxProfitLimit);
}
