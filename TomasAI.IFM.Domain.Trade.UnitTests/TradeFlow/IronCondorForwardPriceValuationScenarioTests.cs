using System.Globalization;
using System.Text;
using System.Text.Json;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;
using Xunit.Abstractions;
using SessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

/// <summary>Complete consistent model valuations, rather than independently assigned prices, deltas or PnL.</summary>
public sealed class IronCondorForwardPriceValuationScenarioTests(ITestOutputHelper output)
{
    static readonly DateTime EntryAt = new(2026,9,21,16,0,0,DateTimeKind.Utc);
    static readonly DateTime BaselineAt = new(2026,10,19,21,0,0,DateTimeKind.Utc);
    static readonly DateTime CurrentAt = new(2026,10,20,16,0,0,DateTimeKind.Utc);
    static readonly DateTime ExpiryAt = new(2026,11,19,21,0,0,DateTimeKind.Utc);
    const decimal Multiplier = 50m;
    const int Quantity = 1;
    const double Rate = .04;
    const double BaseVolatility = .20;
    const string Currency = "USD";
    static readonly decimal[] Strikes = [5850,5800,6150,6200];

    public static IEnumerable<object[]> Cases()
    {
        yield return ["Large profit",6000d,.08,1];
        yield return ["Small profit",6000d,.19,1];
        yield return ["Flat",6000d,FlatVolatility(),0];
        yield return ["Small loss",6000d,.21,-1];
        yield return ["Large loss",6600d,.20,-1];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Five_pnl_cases_use_consistent_leg_marks_deltas_and_prior_session_basis(string name,double future,double volatility,int direction)
    {
        var row = await Evaluate(name,future,volatility);
        if (direction == 0) Assert.InRange(Math.Abs(row.DailyPnlCurrency),0m,.000001m);
        else Assert.Equal(direction,Math.Sign(row.DailyPnlCurrency));
        if (name.StartsWith("Large",StringComparison.Ordinal)) Assert.True(Math.Abs(row.DailyPnlCurrency) > 500m);
        if (name.StartsWith("Small",StringComparison.Ordinal)) Assert.InRange(Math.Abs(row.DailyPnlCurrency),.01m,100m);
        output.WriteLine(JsonSerializer.Serialize(row));
    }

    [Fact]
    public async Task Adverse_call_move_recommends_exit_before_the_actual_thousand_dollar_budget()
    {
        var row = await Evaluate("Adverse call near 700 actual loss", 7207d, .20);
        var actualTradePnl = (row.EntryCredit - row.CurrentCloseCost) * Multiplier * Quantity;
        Assert.InRange(actualTradePnl, -701m, -699m);
        Assert.True(row.ForwardPnlCurrency < actualTradePnl);
        Assert.True(row.ForwardLossCurrency <= -1000m);
        Assert.True(row.ExitRecommended);
        output.WriteLine($"Actual trade PnL: {actualTradePnl}; projected PnL: {row.ForwardPnlCurrency}; forward price: {row.ForwardPrice}");
    }

    [Fact]
    public async Task Export_full_precision_scenario_and_leg_valuation_evidence()
    {
        var rows = new List<Scenario>();
        foreach (var item in Cases()) rows.Add(await Evaluate((string)item[0],(double)item[1],(double)item[2]));
        var directory = Directory.CreateDirectory(Path.Combine(Workspace(),".artifacts","forward-price-coherent-valuations")).FullName;
        var evidence = new { Provenance = "Computed European upfront-premium futures-option fair values; not historical/provider quotes or executions",
            Model = OptionCalculator.Version, Engine = rows[0].Legs[0].Current.EngineVersion,
            EntryAt, BaselineAt, CurrentAt, ExpiryAt,
            EntryFuturesPrice = 6000d, EntryImpliedVolatility = BaseVolatility,
            BaselineFuturesPrice = 6000d, BaselineImpliedVolatility = BaseVolatility,
            ForwardModel = "IronCondor/DeltaTimeStrength020RawSum/v2",
            AmplificationStrength = IronCondorForwardPriceCalculation.AmplificationStrength,
            ForwardRemainingCalendarDays = DateOnly.FromDateTime(ExpiryAt).DayNumber-DateOnly.FromDateTime(CurrentAt).DayNumber,
            ForwardOpeningCalendarDays = DateOnly.FromDateTime(ExpiryAt).DayNumber-DateOnly.FromDateTime(EntryAt).DayNumber,
            ConfiguredLossBudgetUSD = 1000m, AnnualContinuousRate = Rate, DayCount = "Actual365Fixed",
            Currency, Multiplier, Quantity, Commission = 0, Slippage = 0,
            ComparisonBasis = "CurrentCloseCost and ForwardPrice are points per complete condor. SignedCurrentPositionPrice is the negative holding value. Difference = raw ForwardPrice - current close cost, not a forecast error against an observed future outcome.",
            Scenarios = rows };
        await File.WriteAllTextAsync(Path.Combine(directory,"scenarios.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions { WriteIndented=true }));
        var csv = new StringBuilder("Scenario,FuturesPrice,ImpliedVolatility,EntryCreditPoints,BaselineCloseCostPoints,CurrentCloseCostPoints,SignedCurrentPositionPricePoints,DailyPnlPoints,DailyPnlUSD,PutForwardPricePoints,CallForwardPricePoints,ForwardPricePoints,ForwardMinusCurrentPoints,ForwardPriceScaledUSD,ForwardPnlUSD,ForwardLossUSD,ForwardLossRatio,ForwardLossHeadroomUSD,RiskCalculationVersion,ExitRecommended\n");
        foreach (var r in rows) csv.AppendLine(FormattableString.Invariant($"{r.Name},{r.FuturesPrice:R},{r.Volatility:R},{r.EntryCredit},{r.BaselineCloseCost},{r.CurrentCloseCost},{r.SignedCurrentPositionPrice},{r.DailyPnlPoints},{r.DailyPnlCurrency},{r.PutForwardPrice},{r.CallForwardPrice},{r.ForwardPrice},{r.ForwardMinusCurrent},{r.ForwardPriceScaledCurrency},{r.ForwardPnlCurrency},{r.ForwardLossCurrency},{r.ForwardLossRatio:R},{r.ForwardLossHeadroom},{r.RiskCalculationVersion},{r.ExitRecommended}"));
        await File.WriteAllTextAsync(Path.Combine(directory,"scenarios.csv"),csv.ToString());
        var legsCsv = new StringBuilder("Scenario,Role,Strike,SignedQuantity,EntryPrice,BaselinePrice,CurrentPrice,CurrentDelta,CurrentFuturesPrice,CurrentIV,CurrentYearsToExpiry,Rate\n");
        foreach (var r in rows) foreach (var l in r.Legs)
            legsCsv.AppendLine(FormattableString.Invariant($"{r.Name},{l.Role},{l.Strike},{l.SignedQuantity},{l.Entry.Price},{l.Baseline.Price},{l.Current.Price},{l.Current.Delta:R},{r.FuturesPrice:R},{r.Volatility:R},{l.Current.TimeToExpiry:R},{Rate:R}"));
        await File.WriteAllTextAsync(Path.Combine(directory,"legs.csv"),legsCsv.ToString());
        output.WriteLine($"Full-precision evidence: {directory}");
    }

    static async Task<Scenario> Evaluate(string name,double future,double volatility)
    {
        var entry = Value(6000,BaseVolatility,EntryAt);
        var baseline = Value(6000,BaseVolatility,BaselineAt);
        var current = Value(future,volatility,CurrentAt);
        decimal Cost(Valuation[] values) => values[0].Price-values[1].Price+values[2].Price-values[3].Price;
        var entryCost = Cost(entry); var baselineCost = Cost(baseline); var currentCost = Cost(current);
        var discount = (decimal)Math.Exp(-Rate * (ExpiryAt-CurrentAt).TotalDays/365d);
        Assert.InRange(current[0].Price-current[1].Price,0m,50m*discount);
        Assert.InRange(current[2].Price-current[3].Price,0m,50m*discount);
        Assert.InRange(currentCost,0m,50m*discount);
        var legs = Enumerable.Range(0,4).Select(i => new StrategyPositionLeg {
            TradeLegId = Guid.Parse($"00000000-0000-0000-0000-{i+1:D12}"),ContractId = $"MODEL-{(i<2?"PUT":"CALL")}-{Strikes[i]}",
            PutCall = (byte)(i<2?2:1),Strike = Strikes[i],Expiry = DateOnly.FromDateTime(ExpiryAt),
            SignedQuantity = i%2==0 ? -Quantity : Quantity,OpeningPrice = entry[i].Price,CurrentPrice = current[i].Price,
            AssetFamily = TradeAssetFamily.FuturesOption,LastPriceAtUtc = CurrentAt }).ToArray();
        var cumulative = legs.Sum(l => (l.CurrentPrice-l.OpeningPrice)*l.SignedQuantity);
        var openingPnl = Enumerable.Range(0,4).Sum(i => (baseline[i].Price-entry[i].Price)*legs[i].SignedQuantity);
        var dailyPnl = cumulative-openingPnl;
        Assert.Equal((baselineCost-currentCost)*Quantity,dailyPnl);
        var date = DateOnly.FromDateTime(CurrentAt);
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(new TradeEntityId(1,2,3,700001),TradeStrategyKind.IronCondor),
            StrategyKind = TradeStrategyKind.IronCondor,Phase = StrategyPositionPhase.MarkToMarket,Legs = legs,
            ValueDate = date,AsOfUtc = CurrentAt,PositionSequence = 1,RouteGeneration = 1,IsOpen = true,
            MarketValue = -currentCost*Quantity,UnrealizedPnl = cumulative,DailyOpeningPnl = openingPnl,DailyPnlPoints = dailyPnl };
        var id = new SessionId(position.Id,date);
        var request = new CalculateIronCondorForwardPriceCommand { CommandId=Guid.NewGuid(),SourceEventId=Guid.NewGuid(),
            EntityId=id,Position=position,RequestedAtUtc=CurrentAt,
            Subject=new(ActorType.Function,CalculateIronCondorForwardPriceCommand.Actor,CalculateIronCondorForwardPriceCommand.Verb,id.Format()),
            IronCondorForwardPriceInputs = new() { TradeDate=DateOnly.FromDateTime(EntryAt),MaturityDate=DateOnly.FromDateTime(ExpiryAt),
                OptionLegs=legs.Select((leg,i) => new IronCondorForwardPriceLeg(leg.TradeLegId,leg.ContractId,i>=2,leg.SignedQuantity,
                    (double)current[i].Price,current[i].Delta,current[i].Price,Multiplier,Currency)).ToArray() } };
        var price = IronCondorForwardPriceCalculation.Calculate(request);
        Assert.Equal(price.ForwardPrice,IronCondorLegacyValueInitializers.CalculateForwardPrice(price.PutForwardPricePoints,price.CallForwardPricePoints));
        var completed = new FunctionEventContext<CalculateIronCondorForwardPriceCommand>(typeof(IronCondorForwardPriceCalculatedEvent),request,price).Complete(TimeProvider.System).Completed!;
        var planRequest = IronCondorForwardPriceTestScenario.PlanRequest(request,completed) with { RequestedAtUtc=CurrentAt };
        planRequest = planRequest with { IronCondorTradePlanInputs=planRequest.IronCondorTradePlanInputs! with {
            DailyRiskInputs=planRequest.IronCondorTradePlanInputs!.DailyRiskInputs! with { Currency=Currency } } };
        var context = Substitute.For<IIronCondorTradePlanFunctionContext>(); context.Algorithm.Returns(new IronCondorTradePlanAlgorithm());
        var result = await planRequest.ExecuteAsync(new IronCondorTradePlanFunctionState().Prepare(planRequest),context,input=>input.Complete(TimeProvider.System),default);
        var plan = result.Completed!.Plan; var detail = plan.IronCondorTradePlanSnapshot!;
        Assert.True(detail.IsComplete,detail.ForwardPriceCalculationError);
        Assert.Equal(dailyPnl*Multiplier,detail.DailyPnl);
        Assert.Equal(price.ForwardPrice,plan.ForwardTradePrice);
        var forwardPnl = (entryCost-price.ForwardPrice)*Multiplier*Quantity;
        Assert.Equal(entryCost,detail.IronCondorForwardRisk!.EntryCreditPoints);
        Assert.Equal(forwardPnl,detail.IronCondorForwardRisk.ForwardPnl);
        Assert.Equal(Math.Min(0m,forwardPnl),detail.IronCondorForwardRisk.ForwardLoss);
        Assert.Equal(forwardPnl,plan.ForwardPnl);
        Assert.Equal(IronCondorForwardRisk.EntryCreditCalculationVersion,detail.CalculationVersion);
        Assert.Equal(forwardPnl <= -1000m,plan.RequiresExit);
        if (name == "Large loss") Assert.True(forwardPnl < cumulative * Multiplier,
            "This adverse call-side fixture must project a greater loss than its actual whole-trade loss.");
        if (plan.RequiresExit) Assert.True((position.UnrealizedPnl+position.RealizedPnl)*Multiplier > -1000m,
            "The amplified projection must trigger this fixture before actual whole-trade PnL reaches the configured budget.");
        var restored = MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(MessagePackSerializer.Serialize(plan));
        Assert.Equal(detail.IronCondorForwardRisk,restored.IronCondorTradePlanSnapshot!.IronCondorForwardRisk! with {
            IronCondorForwardPrice=detail.IronCondorForwardRisk.IronCondorForwardPrice });
        return new(name,future,volatility,entryCost,baselineCost,currentCost,-currentCost,dailyPnl,dailyPnl*Multiplier,
            price.PutForwardPricePoints,price.CallForwardPricePoints,price.ForwardPrice,price.ForwardPrice-currentCost,
            price.ForwardPrice*Multiplier*Quantity,detail.IronCondorForwardRisk.ForwardPnl!.Value,
            detail.IronCondorForwardRisk.ForwardLoss,detail.IronCondorForwardRisk.ForwardLossRatio,
            detail.IronCondorForwardRisk.ForwardLossHeadroom,detail.IronCondorForwardRisk.CalculationVersion,plan.RequiresExit,
            legs.Select((l,i) => new Leg($"{(l.SignedQuantity<0?"Short":"Long")} {(i<2?"put":"call")}",l.Strike!.Value,l.SignedQuantity,entry[i],baseline[i],current[i])).ToArray());
    }

    static Valuation[] Value(double future,double volatility,DateTime at)
    {
        var calculator = new OptionCalculator();
        return Strikes.Select((strike,i) => {
            var years=(ExpiryAt-at).TotalDays/365d;
            var request = new OptionPricingRequest(UnderlyingKind.Futures,ExerciseKind.European,PremiumKind.PaidUpfront,
                i<2?OptionSide.Put:OptionSide.Call,future,(double)strike,years,Rate);
            var result=calculator.PriceAndDelta(request,volatility);
            Assert.True(result.Success,$"{strike}: {result.Failure}");
            var value=result.Value!.Value;
            // Verify put/call parity at each strike using the same conventions.
            var opposite=calculator.TheoreticalPrice(request with { Side=i<2?OptionSide.Call:OptionSide.Put },volatility);
            Assert.True(opposite.Success);
            var call=i<2?opposite.Price!.Value:value.Price;
            var put=i<2?value.Price:opposite.Price!.Value;
            Assert.InRange(Math.Abs(call-put-Math.Exp(-Rate*years)*(future-(double)strike)),0,1e-8);
            return new Valuation((decimal)value.Price,value.Delta,years,result.EngineVersion);
        }).ToArray();
    }

    static double FlatVolatility()
    {
        decimal Cost(Valuation[] p)=>p[0].Price-p[1].Price+p[2].Price-p[3].Price;
        var target=Cost(Value(6000,BaseVolatility,BaselineAt));
        var low=.01; var high=1d;
        for(var i=0;i<80;i++) { var mid=(low+high)/2;
            if(Cost(Value(6000,mid,CurrentAt))<target) low=mid; else high=mid; }
        return (low+high)/2;
    }

    static string Workspace()
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        while(folder is not null && !Directory.Exists(Path.Combine(folder.FullName,".git"))) folder=folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    public sealed record Valuation(decimal Price,double Delta,double TimeToExpiry,string EngineVersion);
    public sealed record Leg(string Role,decimal Strike,int SignedQuantity,Valuation Entry,Valuation Baseline,Valuation Current);
    public sealed record Scenario(string Name,double FuturesPrice,double Volatility,decimal EntryCredit,decimal BaselineCloseCost,
        decimal CurrentCloseCost,decimal SignedCurrentPositionPrice,decimal DailyPnlPoints,decimal DailyPnlCurrency,
        decimal PutForwardPrice,decimal CallForwardPrice,decimal ForwardPrice,decimal ForwardMinusCurrent,
        decimal ForwardPriceScaledCurrency,decimal ForwardPnlCurrency,decimal ForwardLossCurrency,double ForwardLossRatio,
        decimal ForwardLossHeadroom,string RiskCalculationVersion,bool ExitRecommended,Leg[] Legs);
}
