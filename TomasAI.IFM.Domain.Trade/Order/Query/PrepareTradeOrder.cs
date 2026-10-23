using System.Globalization;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Query;

public static class PrepareTradeOrder
{
    public static async ValueTask ExecuteAsync(this PrepareTradeOrderQuery query, ITradeOrderQueryContext context, CancellationToken token)
    {
        ServiceResult<TradeOrderPreparation> response;
        try { response = new ServiceOk<TradeOrderPreparation>(await PrepareAsync(query.Request, context, token).ConfigureAwait(false)); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { response = new ServiceFailed<TradeOrderPreparation>(400, $"TradeOrder.Preparation: {error.Message}"); }
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, response).ConfigureAwait(false);
    }

    static async Task<TradeOrderPreparation> PrepareAsync(TradeOrderPreparationRequest request, ITradeOrderQueryContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        var draft = request.Draft ?? throw new ArgumentException("Order draft is required.");
        if (request.PortfolioId <= 0 || string.IsNullOrWhiteSpace(request.UnderlyingContractId)
            || request.UnderlyingContractId.Length > 256 || request.SelectedQuoteInputs.Length > 262144
            || draft.Components.Length != 1 || draft.Components[0].Legs.Length is < 1 or > 4
            || draft.SetupTrade is not { FundId: > 0 } setup)
            throw new ArgumentException("A single scoped component is required.");
        var underlying = await context.DbFactory.SecuritiesDb.GetFuturesContractAsync(request.UnderlyingContractId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The underlying contract definition is unavailable.");
        var component = draft.Components[0].ToTradeComponent();
        var resolved = new List<TradeLegDefinition>();
        decimal? tick = null;
        foreach (var leg in component.Legs)
        {
            if (leg.AssetFamily == TradeAssetFamily.Futures)
            {
                if (leg.ContractId != underlying.ContractId) throw new ArgumentException("Unexpected futures contract.");
                if (!decimal.TryParse(underlying.Multiplier, NumberStyles.Number, CultureInfo.InvariantCulture, out var multiplier) || multiplier <= 0)
                    throw new InvalidOperationException("Underlying multiplier is unavailable.");
                resolved.Add(leg with { CashMultiplier = multiplier }); tick = underlying.TickSize;
            }
            else
            {
                var option = await context.DbFactory.SecuritiesDb.GetFuturesOptionContractAsync(leg.ContractId, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Option definition is unavailable: {leg.ContractId}");
                if (option.UnderlyingContractId != underlying.ContractId || option.ExpirationUtc is null)
                    throw new ArgumentException("The selected option does not resolve to the requested underlying and expiry.");
                if (!decimal.TryParse(option.Multiplier, NumberStyles.Number, CultureInfo.InvariantCulture, out var multiplier) || multiplier <= 0)
                    throw new InvalidOperationException("Option multiplier is unavailable.");
                var right = option.OptionRight.ToString();
                if (right is not ("Call" or "Put")) throw new InvalidOperationException("Option right is unavailable.");
                resolved.Add(leg with { CashMultiplier = multiplier, Strike = (decimal)option.StrikePrice,
                    PutCall = right == "Call" ? (byte)1 : (byte)2, Expiry = DateOnly.FromDateTime(option.ExpirationUtc.Value.UtcDateTime) });

            }
        }
        tick = ManualOrderEntryPolicy.Tick(underlying.Symbol, component.StrategyKind == TradeStrategyKind.FuturesOutright, tick, context.PreparationConfiguration);
        var now = context.PreparationClock.GetUtcNow().UtcDateTime;
        var session = context.MarketSessionAuthority?.Current ?? throw new InvalidOperationException("Server market session is unavailable.");
        var fund = await context.DbFactory.PortfolioDb.GetFundAsync(setup.FundId, token).ConfigureAwait(false);
        if (fund?.PortfolioId != request.PortfolioId) throw new ArgumentException("The Fund does not belong to the requested Portfolio.");
        CatalogKey deployment = default!; var horizon = "Manual";
        var development = string.Equals(context.PreparationConfiguration?["DOTNET_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.Ordinal)
            || string.Equals(context.PreparationConfiguration?["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.Ordinal);
        if (!development || draft.BrokerEnvironment != PortfolioBrokerEnvironment.Emulator)
        {
            var family = component.StrategyKind == TradeStrategyKind.FuturesOutright ? "Futures" : component.StrategyKind == TradeStrategyKind.IronCondor ? "IronCondor" : "VerticalSpread";
            var assignments = await context.DbFactory.PortfolioDb.GetAssignmentsAsync(request.PortfolioId, setup.FundId, fund!.FundMandateVersion, 200, token).ConfigureAwait(false);
            var assignment = assignments.Where(a => a.IsEffectiveAt(now) && a.TradeStrategyFamily?.CatalogDeployment is not null
                && (a.UnderlyingUniverse.Contains(underlying.Symbol, StringComparer.OrdinalIgnoreCase) || a.UnderlyingUniverse.Contains(underlying.ContractId, StringComparer.OrdinalIgnoreCase))
                && a.TradeFamily.Contains(family, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Priority).ThenByDescending(a => a.AssignmentVersion).FirstOrDefault()
                ?? throw new InvalidOperationException("No effective deployment is assigned to this Fund and product.");
            deployment = assignment.TradeStrategyFamily!.CatalogDeployment!; horizon = assignment.DecisionHorizon;
        }
        var emulator = draft.BrokerEnvironment == PortfolioBrokerEnvironment.Emulator;
        var alias = emulator ? context.PreparationConfiguration?["TradeBroker:Emulator:AccountAlias"]
            ?? context.PreparationConfiguration?["TradeBroker:AccountAlias"] ?? "IFM-EMULATOR-PAPER"
            : context.PreparationConfiguration?["TradeBroker:AccountAlias"];
        if (!string.Equals(alias, draft.BrokerAccountAlias, StringComparison.Ordinal)) throw new ArgumentException("Unexpected broker account.");
        var configuredFee = emulator ? context.PreparationConfiguration?["TradeBroker:Emulator:PerLegCommission"]
            : context.PreparationConfiguration?[$"TradeBroker:PreparationFees:{alias}:PerLegCommission"];
        if (!emulator && configuredFee is null) throw new InvalidOperationException("The configured broker route has no confirmation fee policy.");
        if (underlying.Currency != "USD") throw new InvalidOperationException("This order preparation fee policy requires USD contracts.");
        var fee = configuredFee is null ? .65m : decimal.TryParse(configuredFee, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedFee)
            ? parsedFee : throw new InvalidOperationException("The emulator fee configuration is invalid.");
        var intent = request with { Draft = draft with { Components = [(component with { Legs = resolved.ToArray() }).ToPortfolioComponent()] } };
        return TradeOrderPreparationCompute.Calculate(intent, underlying, session.OperationalValueDate, now, tick.Value, fee,
            deployment, horizon, emulator ? "Emulator per-leg contract fee; estimated, USD" : "Configured account per-leg contract fee; estimated, USD");
    }
}
