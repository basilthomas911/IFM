using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class OptionLeg : IDataValidation, IOptionLeg
{
    static readonly IValidator<OptionLeg> _validator = new OptionLegValidator();
    public OptionLeg(int orderId, int tradeId, string contractId, int quantity, decimal strikePrice, OptionType optionLegType, OptionLegAction optionLegAction, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ContractId = contractId;
        Quantity = quantity;
        StrikePrice = strikePrice;
        OptionLegType = optionLegType;
        OptionLegAction = optionLegAction;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
        this.Validate(_validator);
    }

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public string ContractId { get; private set; }
    public int Quantity { get; private set; }
    public decimal StrikePrice { get; private set; }
    public OptionType OptionLegType { get; private set; }
    public OptionLegAction OptionLegAction { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime UpdatedOn { get; private set; }
    public string UpdatedBy { get; private set; }

    public OptionTradeLegReadModel ToDataModel() => new(orderId: OrderId, tradeId: TradeId, contractId: ContractId, quantity: Quantity, strikePrice: StrikePrice, optionLegType: OptionLegType, optionLegAction: OptionLegAction, createdOn: CreatedOn, createdBy: CreatedBy, updatedOn: UpdatedOn, updatedBy: UpdatedBy);
}
