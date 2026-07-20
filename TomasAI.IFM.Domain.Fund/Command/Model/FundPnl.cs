using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund pnl model
/// </summary>
public class FundPnl : IDataValidation
{

    /// <summary>
    /// fund pnl constructor
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="pnl"></param>
    public FundPnl(
        int fundId,
        DateOnly valueDate,
        int orderId,
        int tradeId,
        TradeType tradeType,
        decimal pnl)
    {
        FundId = fundId;
        ValueDate = valueDate;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        Pnl = pnl;
        _validator ??= new FundPnlValidator();
        this.Validate(_validator);
    }

    public int FundId { get; } 
    public DateOnly ValueDate { get; }
    public int OrderId { get; }
    public int TradeId { get; }
    public TradeType TradeType { get; } 
    public decimal Pnl { get; }
    readonly IValidator<FundPnl> _validator;

    /// <summary>
    /// create fund pnl from view model
    /// </summary>
    /// <param name="e"></param>
    public FundPnl(FundPnlReadModel e) : this(
        fundId: e.FundId,
        valueDate: e.ValueDate,
        orderId: e.OrderId,
        tradeId: e.TradeId,
        tradeType: e.TradeType,
        pnl: e.Pnl)
    {
    }

    /// <summary>
    /// convert pnl to view model
    /// </summary>
    /// <returns></returns>
    public FundPnlReadModel ToViewModel()
        => new (
            fundId: FundId,
            valueDate: ValueDate,
            orderId: OrderId,
            tradeId: TradeId,
            tradeType: TradeType,
            pnl: Pnl
        );

    public override string ToString() =>  $"{ValueDate:yyyy-MM-dd}: {OrderId}/{TradeId} = {Pnl:C}";
    
}

/// <summary>
/// fund pnl validator
/// </summary>
public class FundPnlValidator : AbstractValidator<FundPnl>
{
    public FundPnlValidator()
    {
        RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundPnl.FundId is zero or negative");
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundPnl.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("FundPnl.TradeId is zero or negative");
    }
}
