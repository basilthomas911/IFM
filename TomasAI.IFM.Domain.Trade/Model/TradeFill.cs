using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeFill : IDataValidation, ITradeFill
{
    static IValidator<TradeFill>? _validator;

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public DateTime FillDate { get; private set; }
    public int FillQuantity { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }

    public TradeFill(
        int orderId,
        int tradeId,
        DateTime fillDate,
        int fillQuantity,
        DateTime createdOn,
        string createdBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        FillDate = fillDate;
        FillQuantity = fillQuantity;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        _validator = _validator ?? new TradeFillValidator();
        this.Validate(_validator);
    }

    public TradeFill(TradeFillReadModel e):this(
        orderId: e.OrderId,
        tradeId: e.TradeId,
        fillDate: e.FillDate,
        fillQuantity: e.FillQuantity,
        createdOn: e.CreatedOn,
        createdBy: e.CreatedBy)
    {
    }

    public TradeFillReadModel ToViewModel()
    {
        var tradeFill = new TradeFillReadModel(OrderId, TradeId, FillDate, FillQuantity, CreatedOn, CreatedBy);
        return tradeFill;
    }

}

public class TradeFillValidator : AbstractValidator<TradeFill>
{
    public TradeFillValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("TradeFill.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeFill.TradeId is zero or negative");
        RuleFor(x => x.FillDate).Must(e => e != DateTime.MinValue && e != DateTime.MaxValue).WithMessage("TradeFill.FillDate is invalid date");
        RuleFor(x => x.FillQuantity).GreaterThan(0).WithMessage("TradeFill.FillQuantity is zero or negative");
    }
}
