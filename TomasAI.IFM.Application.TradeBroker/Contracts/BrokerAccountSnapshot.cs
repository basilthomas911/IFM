namespace TomasAI.IFM.Application.TradeBroker.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "This published actor message uses arrays as its existing serialized payload contract.")]
public sealed record BrokerAccountSnapshot(string AccountAlias, string Currency, decimal CashBalance, decimal AvailableFunds,
    bool Complete, bool NewRiskAllowed, long Generation, DateTime AsOfUtc, BrokerAccountPosition[] Positions);
