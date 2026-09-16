using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Application.TradeBroker.Mapping;

/// <summary>Explicit mapping between application and provider-neutral framework contracts.</summary>
public static class TradeBrokerMapper
{
    public static FrameworkMarketQuote ToFramework(BrokerMarketQuote value) => new(value.ContractId,
        value.Bid, value.Ask, value.BidSize, value.AskSize, value.MarketTimeUtc,
        value.SourceEpoch, value.SourceSequence);
    public static FrameworkOrderRequest ToFramework(BrokerOrderRequest x) => new(x.AccountAlias, x.BrokerOrderId, x.OperationId,
        x.ComponentId, (FrameworkOrderShape)x.Shape, x.IsClosing,
        [.. x.Legs.Select(l => new FrameworkOrderLeg(l.LegId, l.ContractId, l.SignedQuantity, l.Strike, l.Expiry, l.PutCall, l.CashMultiplier))],
        x.SignedNetDebitLimit, x.MinimumLimit, x.MaximumLimit, x.TickIncrement, x.ValidUntilUtc, x.ApprovalHash, x.ContractReferenceHash,
        x.RequiredCapital, x.MaximumLoss);

    public static FrameworkLimitUpdate ToFramework(BrokerLimitUpdate x) => new(x.AccountAlias, x.BrokerOrderId, x.OperationId, x.NewSignedNetDebitLimit, x.ExpectedRevision);
    public static FrameworkCancelRequest ToFramework(BrokerCancelRequest x) => new(x.AccountAlias, x.BrokerOrderId, x.OperationId, x.ExpectedRevision);
    public static BrokerDispatchReceipt ToApplication(FrameworkDispatchReceipt x) => new((BrokerDispatchOutcome)x.Outcome, x.OperationId, x.BrokerOrderId, x.Category, x.Detail, x.RecordedAtUtc);
    public static BrokerObservation ToApplication(FrameworkBrokerObservation x) => new()
    {
        Kind = (BrokerObservationKind)x.Kind, ObservationId = x.ObservationId, AccountAlias = x.AccountAlias,
        BrokerOrderId = x.BrokerOrderId, OperationId = x.OperationId, ComponentId = x.ComponentId, LegId = x.LegId,
        ContractId = x.ContractId, ExternalExecutionId = x.ExternalExecutionId, SignedQuantity = x.SignedQuantity,
        Price = x.Price, Commission = x.Commission, OrderRevision = x.OrderRevision, SourceEpoch = x.SourceEpoch,
        SourceSequence = x.SourceSequence, OccurredAtUtc = x.OccurredAtUtc, Category = x.Category, Detail = x.Detail
    };
    public static BrokerAccountSnapshot ToApplication(FrameworkAccountSnapshot x) => new(x.AccountAlias, x.Currency,
        x.CashBalance, x.AvailableFunds, x.Complete, x.NewRiskAllowed, x.Generation, x.AsOfUtc,
        [.. x.Positions.Select(p => new BrokerAccountPosition(p.ContractId, p.SignedQuantity, p.AveragePrice))]);
}
