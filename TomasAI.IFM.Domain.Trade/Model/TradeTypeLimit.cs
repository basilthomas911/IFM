using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeTypeLimit : IDataValidation, ITradeTypeLimit
{
    static IValidator<TradeTypeLimit>? _validator;

    public int TradeId { get; private set; }
    public TradeType TradeType { get; private set; }
    public decimal MaxLossLimit { get; private set; }
    public decimal MinProfitLimit { get; private set; }
    public decimal MaxProfitLimit { get; private set; }

    public TradeTypeLimit(
        int tradeId,
        TradeType tradeType,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal maxProfitLimit)
   {
        TradeId = tradeId;
        TradeType = tradeType;
        MaxLossLimit = maxLossLimit;
        MinProfitLimit = minProfitLimit;
        MaxProfitLimit = maxProfitLimit;
        _validator = _validator ?? new TradeTypeLimitValidator();
        this.Validate(_validator);
    }

    public TradeTypeLimitReadModel ToViewModel()
        => new(
            tradeId: TradeId,
            tradeType: TradeType,
            maxLossLimit: MaxLossLimit,
            minProfitLimit: MinProfitLimit,
            maxProfitLimit: MaxProfitLimit
        );
}

public class TradeTypeLimitValidator : AbstractValidator<TradeTypeLimit>
{
    public TradeTypeLimitValidator()
    {
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeTypeLimit.TradeId is zero or negative");
    }
}
