using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using MathNet.Numerics;

namespace TomasAI.IFM.Domain.Trade.Model;

public class OptionLegData : IDataValidation, IOptionLegData
{
    static IValidator<OptionLegData>? _validator;
  
    public OptionLegData(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        string optionLegId,
        decimal bidPrice,
        decimal askPrice,
        double impliedVolatility,
        double delta,
        double gamma,
        double theta,
        double vega,
        double rho,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        OptionLegId = optionLegId;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        ImpliedVolatility = impliedVolatility;
        Delta = delta;
        Gamma = gamma;
        Theta = theta;
        Vega = vega;
        Rho = rho;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
        _validator = _validator ?? new OptionLegDataValidator();
        this.Validate(_validator);
    }

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public TradeType TradeType { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public int DaysToExpiry { get; private set; }
    public TradeStatus TradeStatus { get; private set; }
    public string OptionLegId { get; private set; }
    public OptionType OptionType { get; private set; }
    public OptionLegAction OptionLegAction { get; private set; }
    public int Quantity { get; private set; }
    public decimal StrikePrice { get; private set; }
    public decimal BidPrice { get; private set; }
    public decimal AskPrice { get; private set; }
    public double ImpliedVolatility { get; private set; }
    public double Delta { get; private set; }
    public double Gamma { get; private set; }
    public double Theta { get; private set; }
    public double Vega { get; private set; }
    public double Rho { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime UpdatedOn { get; private set; }
    public string UpdatedBy { get; private set; }
    public OptionTradeLegReadModel? OptionLeg { get; private set; }

    public OptionLegData(
        TradePositionEntityId key,
        OptionTradeLegDataReadModel model,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy):this(key.OrderId, key.TradeId, key.TradeType, key.ValueDate, key.DaysToExpiry, key.TradeStatus, 
            model.OptionLeg?.ContractId ?? string.Empty, model.BidPrice, model.AskPrice, model.ImpliedVolatility, model.Delta, model.Gamma, model.Theta,
            model.Vega, model.Rho, createdOn, createdBy, updatedOn, updatedBy)
    {
        OptionLeg = model.OptionLeg;
    }

    public OptionLegData SetOptionLeg(OptionTradeLegReadModel optionLeg)
    {
        OptionLeg = optionLeg;
        return this;
    }

    public OptionTradeLegDataReadModel ToDataModel()
        => new OptionTradeLegDataReadModel(
            orderId: OrderId,
            tradeId: TradeId,
            tradeType: TradeType,
            valueDate: ValueDate,
            daysToExpiry: DaysToExpiry,
            tradeStatus: TradeStatus,
            optionLegId: OptionLegId,
            bidPrice: BidPrice,
            askPrice: AskPrice,
            impliedVolatility: ImpliedVolatility,
            delta: Delta,
            gamma: Gamma,
            theta: Theta,
            vega: Vega,
            rho: Rho,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy
        ).SetOptionLeg(OptionLeg);

    public double GetOTMProbability(double assetPrice)
    {
        var strikePrice = Convert.ToDouble(StrikePrice);
        var zvalue = Math.Log(strikePrice / assetPrice) / (ImpliedVolatility * Math.Sqrt(Convert.ToDouble(DaysToExpiry) / 365.0));
        return OptionType switch
        {
            OptionType.Put => 1 - ExcelFunctions.NormSDist(zvalue),
            OptionType.Call => ExcelFunctions.NormSDist(zvalue),
            _ => 0,
        };
    }
}

public class OptionLegDataValidator : AbstractValidator<OptionLegData>
{
    public OptionLegDataValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OptionLegData.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("OptionLegData.TradeId is zero or negative");
        //RuleFor(x => x.ValueDate).Must().WithMessage("OptionLegData.ValueDate is empty");
    }
}

public static class OptionLegDataReadModelExtension
{
    public static OptionLegData ToOptionLegData(
        this OptionTradeLegDataReadModel e,
        TradePositionEntityId tradeDataKey,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    => new (
        key: tradeDataKey,
        model: e,
        createdOn: createdOn,
        createdBy: createdBy,
        updatedOn: updatedOn,
        updatedBy: updatedBy);
}
