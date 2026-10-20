using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using System.Globalization;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;
/// <summary>Formats backend-computed broker preview totals. Contains no pricing or payoff calculations.</summary>
public sealed record BrokerTradePreviewPresentation(IReadOnlyDictionary<string,string> Fields,decimal? OrderPrice)
{
    /// <summary>Displays submitted contracts without inventing live quotes for stored legs.</summary>
    public static BrokerTradeLegData[] StoredLegs(TradeOrderComponentDefinition component)=>component.Legs.Select(leg=>new BrokerTradeLegData(leg.ContractId,leg.AssetFamily==TradeAssetFamily.Futures?$"{(leg.SignedQuantity<0?"-":"+")}Future":leg.SignedQuantity<0?(leg.PutCall==1?"-SC":"-SP"):(leg.PutCall==1?"+LC":"+LP"),leg.Strike,null,null,null,leg.PutCall==1,Multiplier:leg.CashMultiplier,IsFuture:leg.AssetFamily==TradeAssetFamily.Futures)).ToArray();
    /// <summary>Formats a backend preview and stored account context for existing controls.</summary>
    public static BrokerTradePreviewPresentation FromBackend(IReadOnlyList<BrokerTradeLegData> legs, TradeOrderPreview preview, BrokerTradeInitializationResult? data)
    {
        var b = new Builder { OrderPrice = preview.OrderPrice };
        Populate(b, legs, preview, data);
        return new(b.Fields, b.OrderPrice);
    }
    sealed class Builder
    {
        public Dictionary<string,string> Fields {get;}=new(StringComparer.Ordinal);
        public decimal? OrderPrice {get;set;}
        public void SetField(string caption,decimal? value)=>SetField(caption,value?.ToString("N2")??"Unavailable");
        public void SetField(string caption,string value)=>Fields[caption]=value;
    }
    static void Populate(Builder b,IReadOnlyList<BrokerTradeLegData> legs,TradeOrderPreview preview,BrokerTradeInitializationResult? data)
    {
            b.SetField("Value source", legs.Any(x => x.Frozen) ? "Frozen quotes / stored fund" : "Market Selection / stored fund");
            var asOf = legs.Where(x => x.QuoteAtUtc.HasValue).Select(x => x.QuoteAtUtc!.Value).ToArray();
            b.SetField("As of", asOf.Length > 0 ? EasternTime.FromUtc(asOf.Min()).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "Quote timestamp unavailable");
            b.SetField("Risk approval", data?.Order?.RiskAuthorization is null ? "Not requested" : "Stored approval - requires revalidation");
            b.SetField("Available funds", data?.Balances?.Value?.AvailableCash);
            b.SetField("Fund capacity", data?.RiskEnvelope?.AvailableCapital);
            b.SetField("Fund risk limit", data?.RiskEnvelope?.MaximumRiskPerTrade);
            b.SetField("Minimum profit", data?.Trade?.TradeLimit?.MinProfitTarget);
            b.SetField("Risk margin", data?.Trade?.TradeLimit?.RiskMargin);
            b.SetField("Buying power", "Broker margin assessment required");
            b.SetField("Est. fees", "Fee schedule unavailable");
            b.SetField("Worst approved limit", "Not approved for this selection");
            var usage = data?.CapacityUsage?.Value?.Scopes.Where(x => x.ScopeKind == CapacityScopeKind.Fund && x.ScopeKey == FinancialScopeKeys.Fund(data!.FundId) && x.Unit == CapacityUnit.Usd && x.Measure == CapacityMeasure.LossCharge).ToArray();
            if (usage is { Length: > 0 }) b.SetField("Reserved capital", usage.Sum(x => x.Held + x.Working));
            else b.SetField("Reserved capital", "Unavailable");
            if (legs.Count == 1 && legs[0].IsFuture)
            {
                b.SetField("Value source", "Stored EOD reference - no bid/ask");
                if (data?.UnderlyingPrice is { } eod) b.SetField("As of", $"EOD {eod.ValueDate:yyyy-MM-dd}");
                b.SetField("Validation", "Reference price only - market quote required");
                b.SetField("Net mid", preview.NetMid);
                b.SetField("Net delta", preview.NetDelta?.ToString("0.####") ?? "Unavailable");
                b.SetField("Net vega", preview.NetVega?.ToString("0.####") ?? "Unavailable");
                b.SetField("Max profit", "Price dependent"); b.SetField("Max loss", "Price dependent");
                return;
            }
            b.SetField("Validation", !preview.HasCompleteQuotes ? "Select priced strategy legs" : !preview.HasBalancedQuantities ? "Unequal leg quantities - strategy changed" : data?.Errors.Length > 0 ? "Stored data incomplete" : "Preview - approval required");
            if (!preview.HasCompleteQuotes) return;
            b.SetField("Net bid", preview.NetBid); b.SetField("Net ask", preview.NetAsk); b.SetField("Net mid", preview.NetMid);
            if (preview.NetDelta.HasValue) b.SetField("Net delta", preview.NetDelta.Value.ToString("0.####"));
            if (preview.NetVega.HasValue) b.SetField("Net vega", preview.NetVega.Value.ToString("0.####"));
            if (preview.CallWidth.HasValue) b.SetField("Call width", preview.CallWidth);
            if (preview.PutWidth.HasValue) b.SetField("Put width", preview.PutWidth);
            if (preview.CallSpreadValue.HasValue) b.SetField("Call spread", preview.CallSpreadValue);
            if (preview.PutSpreadValue.HasValue) b.SetField("Put spread", preview.PutSpreadValue);
            if (!preview.HasPayoffEstimate) return;
            b.SetField("Est. credit", preview.EstimatedCredit);
            b.SetField("Max profit", preview.HasUnlimitedProfit ? "Unlimited" : preview.MaximumProfit?.ToString("N2") ?? "Unavailable");
            b.SetField("Max loss", preview.HasUnlimitedLoss ? "Unlimited" : preview.MaximumLoss?.ToString("N2") ?? "Unavailable");
            b.SetField("Max return", preview.MaximumReturn?.ToString("P2") ?? "Unavailable");
    }
}
