using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface IOptionLegData
{
    int TradeId { get; }
    TradeType TradeType { get; }
    DateOnly ValueDate { get; }
    int DaysToExpiry { get; }
    TradeStatus TradeStatus { get; }
    string OptionLegId { get; }
    OptionType OptionType { get; }
    OptionLegAction OptionLegAction { get; }
    int Quantity { get; }
    decimal StrikePrice { get; }
    decimal BidPrice { get; }
    decimal AskPrice { get; }
    double ImpliedVolatility { get; }
    double Delta { get; }
    double Gamma { get; }
    double Theta { get; }
    double Vega { get; }
    double Rho { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }

    OptionTradeLegDataReadModel ToDataModel();
    double GetOTMProbability(double assetPrice);
}
