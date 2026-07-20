using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class OptionLeg : IDataValidation, IOptionLeg
{
    static IValidator<OptionLeg>? _validator;

    public OptionLeg(
        int orderId,
        int tradeId,
        string contractId,
        int quantity,
        decimal strikePrice,
        OptionType optionLegType,
        OptionLegAction optionLegAction,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
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
        _validator ??= new OptionLegValidator();
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

    public OptionTradeLegReadModel ToDataModel()
        => new  (
            orderId: OrderId,
            tradeId: TradeId,
            contractId:  ContractId,
            quantity:  Quantity,
            strikePrice: StrikePrice,
            optionLegType: OptionLegType,
            optionLegAction: OptionLegAction,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy
        );
}

public class OptionLegValidator : AbstractValidator<OptionLeg>
{
    public OptionLegValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OptionLeg.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("OptionLeg.TradeId is zero or negative");
        RuleFor(x => x.ContractId).NotEmpty().WithMessage("OptionLeg.ContractId is empty");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("OptionLeg.Quantity is zero or negative");
        RuleFor(x => x.StrikePrice).GreaterThan(0m).WithMessage("SpreadTrade.StrikePrice is zero or negative");
    }
}

public static class OptionLegReadModelExtension
{
    public static OptionLeg ToOptionLeg(
        this OptionTradeLegReadModel e,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    => new (
        orderId: e.OrderId,
        tradeId: e.TradeId,
        contractId: e.ContractId,
        quantity: e.Quantity,
        strikePrice: e.StrikePrice,
        optionLegType: e.OptionLegType,
        optionLegAction: e.OptionLegAction,
        createdOn: createdOn,
        createdBy: createdBy,
        updatedOn: updatedOn,
        updatedBy: updatedBy);
}
