using TomasAI.IFM.Domain.Trade.Shared;
using System.Globalization;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;
/// <summary>Shared WinForms/Rust broker-preview calculations. Contains no controls or service calls.</summary>
public sealed record BrokerTradePreviewPresentation(IReadOnlyDictionary<string,string> Fields,decimal? OrderPrice)
{
    /// <summary>Displays submitted contracts without inventing live quotes for stored legs.</summary>
    public static BrokerTradeLegData[] StoredLegs(TradeOrderComponentDefinition component)=>component.Legs.Select(leg=>new BrokerTradeLegData(leg.ContractId,leg.AssetFamily==TradeAssetFamily.Futures?$"{(leg.SignedQuantity<0?"-":"+")}Future":leg.SignedQuantity<0?(leg.PutCall==1?"-SC":"-SP"):(leg.PutCall==1?"+LC":"+LP"),leg.Strike,null,null,null,leg.PutCall==1,Multiplier:leg.CashMultiplier,IsFuture:leg.AssetFamily==TradeAssetFamily.Futures)).ToArray();
    public static BrokerTradePreviewPresentation Calculate(IReadOnlyList<BrokerTradeLegData> legs,IReadOnlyDictionary<string,int> quantities,int quantity,decimal? editedOrderPrice,BrokerTradeInitializationResult? data)
    {
        if(quantity<=0||legs.Any(x=>!quantities.TryGetValue(x.ContractId,out var q)||q<=0))throw new ArgumentException("Positive quantities are required");
        var b=new Builder();Populate(b,legs,quantities,quantity,editedOrderPrice,data);return new(b.Fields,b.OrderPrice);
    }
    sealed class Builder
    {
        public Dictionary<string,string> Fields {get;}=new(StringComparer.Ordinal);
        public decimal? OrderPrice {get;set;}
        public void SetField(string caption,decimal? value)=>SetField(caption,value?.ToString("N2")??"Unavailable");
        public void SetField(string caption,string value)=>Fields[caption]=value;
    }
    static void Populate(Builder b,IReadOnlyList<BrokerTradeLegData> legs,IReadOnlyDictionary<string,int> quantities,int quantity,decimal? editedOrderPrice,BrokerTradeInitializationResult? data)
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
                var future = legs[0];
                var referencePrice = data?.UnderlyingPrice?.ClosePrice;
                b.SetField("Value source", "Stored EOD reference - no bid/ask");
                if (data?.UnderlyingPrice is { } eod) b.SetField("As of", $"EOD {eod.ValueDate:yyyy-MM-dd}");
                b.SetField("Validation", "Reference price only - market quote required");
                if (referencePrice is > 0)
                {
                    b.SetField("Net mid", ((decimal)referencePrice.Value).ToString("N2"));
                    b.OrderPrice = (editedOrderPrice ?? future.Sign * (decimal)referencePrice.Value);
                }
                // An operator-entered limit remains valid even when no stored reference price exists.
                if (editedOrderPrice is { } enteredPrice)
                    b.OrderPrice = enteredPrice;
                if (future.Multiplier.HasValue) b.SetField("Net delta", future.Sign * quantities[future.ContractId] * future.Multiplier.Value);
                b.SetField("Net vega", 0m);
                b.SetField("Max profit", "Price dependent"); b.SetField("Max loss", "Price dependent");
                return;
            }
            var valid = legs.Count > 0 && legs.All(x => x.Sign != 0 && x.Bid.HasValue && x.Ask.HasValue);
            var balanced = legs.Select(x => quantities[x.ContractId]).Distinct().Count() <= 1;
            b.SetField("Validation", !valid ? "Select priced strategy legs" : !balanced ? "Unequal leg quantities - strategy changed" : data?.Errors.Length > 0 ? "Stored data incomplete" : "Preview - approval required");
            if (!valid) return;
            // Quotes remain per strategy unit; economics and exposures include actual leg quantities.
            var netBid = legs.Sum(x => x.Sign * (x.Sign > 0 ? x.Bid!.Value : x.Ask!.Value) * quantities[x.ContractId]) / quantity;
            var netAsk = legs.Sum(x => x.Sign * (x.Sign > 0 ? x.Ask!.Value : x.Bid!.Value) * quantities[x.ContractId]) / quantity;
            b.SetField("Net bid", netBid); b.SetField("Net ask", netAsk); b.SetField("Net mid", (netBid + netAsk) / 2);
            var tick = 0.05m;
            b.OrderPrice = (editedOrderPrice ?? Math.Round((netBid + netAsk) / 2 / tick, MidpointRounding.AwayFromZero) * tick);
            if (legs.All(x => x.Delta.HasValue && x.Multiplier.HasValue)) b.SetField("Net delta", legs.Sum(x => x.Sign * x.Delta!.Value * quantities[x.ContractId] * (double)x.Multiplier!.Value).ToString("0.####"));
            if (legs.All(x => x.Vega.HasValue && x.Multiplier.HasValue)) b.SetField("Net vega", legs.Sum(x => x.Sign * x.Vega!.Value * quantities[x.ContractId] * (double)x.Multiplier!.Value).ToString("0.####"));
            foreach (var call in new[] { true, false })
            {
                var pair = legs.Where(x => !x.IsFuture && x.IsCall == call).ToArray();
                if (pair.Length == 2 && pair.All(x => x.Strike.HasValue) && pair.Sum(x => x.Sign) == 0)
                    b.SetField(call ? "Call width" : "Put width", Math.Abs(pair[0].Strike!.Value - pair[1].Strike!.Value));
                if (pair.Length > 0 && pair.All(x => x.Multiplier.HasValue))
                    b.SetField(call ? "Call spread" : "Put spread", -pair.Sum(x => x.Sign * (x.Bid!.Value + x.Ask!.Value) / 2 * quantities[x.ContractId] * x.Multiplier!.Value));
            }
            if (legs.Any(x => x.IsFuture) || legs.Any(x => !x.Multiplier.HasValue || !x.Strike.HasValue)) return;
            var cost = balanced && legs.Select(x => x.Multiplier).Distinct().Count() == 1 && b.OrderPrice is { } price
                ? price * quantity * legs[0].Multiplier!.Value
                : legs.Sum(x => x.Sign * (x.Bid!.Value + x.Ask!.Value) / 2 * quantities[x.ContractId] * x.Multiplier!.Value);
            b.SetField("Est. credit", -cost);
            var profits = legs.Select(x => x.Strike!.Value).Append(0m).Distinct().Select(spot =>
                legs.Sum(x => x.Sign * quantities[x.ContractId] * x.Multiplier!.Value * Math.Max(x.IsCall ? spot - x.Strike!.Value : x.Strike!.Value - spot, 0m)) - cost).ToArray();
            var slope = legs.Where(x => x.IsCall).Sum(x => x.Sign * quantities[x.ContractId] * x.Multiplier!.Value);
            decimal? maxProfit = slope > 0 ? null : profits.Max();
            decimal? maxLoss = slope < 0 ? null : Math.Max(0, -profits.Min());
            b.SetField("Max profit", maxProfit is null ? "Unlimited" : maxProfit.Value.ToString("N2"));
            b.SetField("Max loss", maxLoss is null ? "Unlimited" : maxLoss.Value.ToString("N2"));
            b.SetField("Max return", maxLoss is > 0 && maxProfit.HasValue ? (maxProfit.Value / maxLoss.Value).ToString("P2") : "Unavailable");
    }
}
