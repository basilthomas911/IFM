using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

public sealed record EmulatorOrderCheckpoint(
    FrameworkOrderRequest Request,
    decimal Limit,
    int Revision,
    bool Filled,
    bool Cancelled,
    int FilledStrategyUnits = 0);
