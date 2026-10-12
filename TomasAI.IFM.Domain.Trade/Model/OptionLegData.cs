using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using MathNet.Numerics;

namespace TomasAI.IFM.Domain.Trade.Model;
public class OptionLegData : IDataValidation, IOptionLegData
{
    static readonly IValidator<OptionLegData> _validator = new OptionLegDataValidator();
    public OptionLegData(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy)
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
        this.Validate(_validator);
    }

    public OptionLegData(TradePositionEntityId key, OptionTradeLegDataReadModel model, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : this(key.OrderId, key.TradeId, key.TradeType, key.ValueDate, key.DaysToExpiry, key.TradeStatus, (model ?? throw new ArgumentNullException(nameof(model))).OptionLeg?.ContractId ?? string.Empty, model.BidPrice, model.AskPrice, model.ImpliedVolatility, model.Delta, model.Gamma, model.Theta, model.Vega, model.Rho, createdOn, createdBy, updatedOn, updatedBy)
    {
        OptionLeg = model.OptionLeg;
    }

    public int OrderId { get; private set; }
    public int TradeId { get; private set; }
    public TradeType TradeType { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public int DaysToExpiry { get; private set; }
    public TradeStatus TradeStatus { get; private set; }
    public string OptionLegId { get; private set; }
    public OptionType OptionType { get; }
    public OptionLegAction OptionLegAction { get; }
    public int Quantity { get; }
    public decimal StrikePrice { get; }
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

    public OptionLegData SetOptionLeg(OptionTradeLegReadModel optionLeg)
    {
        OptionLeg = optionLeg;
        return this;
    }

    public OptionTradeLegDataReadModel ToDataModel() => new OptionTradeLegDataReadModel(orderId: OrderId, tradeId: TradeId, tradeType: TradeType, valueDate: ValueDate, daysToExpiry: DaysToExpiry, tradeStatus: TradeStatus, optionLegId: OptionLegId, bidPrice: BidPrice, askPrice: AskPrice, impliedVolatility: ImpliedVolatility, delta: Delta, gamma: Gamma, theta: Theta, vega: Vega, rho: Rho, createdOn: CreatedOn, createdBy: CreatedBy, updatedOn: UpdatedOn, updatedBy: UpdatedBy).SetOptionLeg(OptionLeg);
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
