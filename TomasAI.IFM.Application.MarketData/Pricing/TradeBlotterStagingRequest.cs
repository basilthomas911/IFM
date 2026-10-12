using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Selection settings are explicit; a deviation never silently changes the strategy.</summary>
public sealed record TradeBlotterStagingRequest(TradeBlotterStrategy Strategy, TradeBlotterDirection Direction,
    double TargetAbsoluteDelta, decimal WingWidth, double DeltaTolerance = 0.03,
    decimal WidthTolerance = 5m, TradeBlotterOptionRight VerticalRight = TradeBlotterOptionRight.Call);
