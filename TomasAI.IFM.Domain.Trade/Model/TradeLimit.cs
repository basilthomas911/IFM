using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeLimit : IDataValidation, ITradeLimit
{
    static IValidator<TradeLimit>? _validator;
 
    public TradeLimit(
        int tradeId,
        TradeType tradeType,
        decimal riskMargin,
        decimal maxProfit,
        decimal maxLoss,
        decimal maxReturn,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal maxProfitLimit,
        decimal minProfitTarget,
        decimal dailyProfitTarget,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        RiskMargin = riskMargin;
        MaxProfit = maxProfit;
        MaxLoss = maxLoss;
        MaxReturn = maxReturn;
        MaxLossLimit = maxLossLimit;
        MinProfitLimit = minProfitLimit;
        MaxProfitLimit = maxProfitLimit;
        MinProfitTarget = minProfitTarget;
        DailyProfitTarget = dailyProfitTarget;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
        _validator ??= new TradeLimitValidator();
        this.Validate(_validator);
    }

    public int TradeId { get; private set; }
    public TradeType TradeType { get; private set; }
    public decimal RiskMargin { get; private set; }
    public decimal MaxProfit { get; private set; }
    public decimal MaxLoss { get; private set; }
    public decimal MaxReturn { get; private set; }
    public decimal MaxLossLimit { get; private set; }
    public decimal MinProfitLimit { get; private set; }
    public decimal MaxProfitLimit { get; private set; }
    public decimal MinProfitTarget { get; private set; }
    public decimal DailyProfitTarget { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime UpdatedOn { get; private set; }
    public string UpdatedBy { get; private set; }

    public TradeLimit(TradeLimitReadModel e, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) :this(
        tradeId: e.TradeId,
        tradeType: e.TradeType,
        riskMargin: e.RiskMargin,
        maxProfit: e.MaxProfit,
        maxLoss: e.MaxLoss,
        maxReturn: e.MaxReturn,
        maxLossLimit: e.MaxLossLimit,
        minProfitLimit: e.MinProfitLimit,
        maxProfitLimit: e.MaxProfitLimit,
        minProfitTarget: e.MinProfitTarget,
        dailyProfitTarget: e.DailyProfitTarget,
        createdOn: createdOn,
        createdBy: createdBy,
        updatedOn: updatedOn,
        updatedBy: updatedBy)
    {
    }

    public TradeLimitReadModel ToViewModel()
        => new(
            tradeId: TradeId,
            tradeType: TradeType,
            riskMargin: RiskMargin,
            maxProfit: MaxProfit,
            maxLoss: MaxLoss,
            maxReturn: MaxReturn,
            maxLossLimit: MaxLossLimit,
            minProfitLimit: MinProfitLimit,
            maxProfitLimit: MaxProfitLimit,
            minProfitTarget: MinProfitTarget,
            dailyProfitTarget: DailyProfitTarget,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy
        );

    public ITradeLimit SetDailyProfitTarget(decimal dailyProfitTarget)
    {
        DailyProfitTarget = dailyProfitTarget;
        return this;
    }
}

public class TradeLimitValidator : AbstractValidator<TradeLimit>
{
    public TradeLimitValidator()
    {
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeLimit.TradeId is zero or negative");
    }
}
