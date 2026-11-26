namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
/// <summary>Completed exact quote arrival, including zero-match results; retained across restart.</summary>
public sealed record EmulatorQuoteReceipt(EmulatorQuote Quote, int MatchedOrders);
/// <summary>One accepted quote-set match, scoped to the order revision, so partial-fill replay cannot fill again.</summary>
public sealed record EmulatorMatchReceipt(string Key, string BrokerOrderId, int Revision, EmulatorQuote[] Quotes, int FilledUnits);
