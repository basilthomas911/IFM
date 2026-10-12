using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeFill : IDataValidation, ITradeFill
{
    static readonly IValidator<TradeFill> _validator = new TradeFillValidator();
    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public DateTime FillDate { get; private set; }
    public int FillQuantity { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }

    public TradeFill(int orderId, int tradeId, DateTime fillDate, int fillQuantity, DateTime createdOn, string createdBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        FillDate = fillDate;
        FillQuantity = fillQuantity;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        this.Validate(_validator);
    }

    public TradeFill(TradeFillReadModel e) : this(orderId: (e ?? throw new ArgumentNullException(nameof(e))).OrderId, tradeId: e.TradeId, fillDate: e.FillDate, fillQuantity: e.FillQuantity, createdOn: e.CreatedOn, createdBy: e.CreatedBy)
    {
    }

    public TradeFillReadModel ToViewModel()
    {
        var tradeFill = new TradeFillReadModel(OrderId, TradeId, FillDate, FillQuantity, CreatedOn, CreatedBy);
        return tradeFill;
    }
}
