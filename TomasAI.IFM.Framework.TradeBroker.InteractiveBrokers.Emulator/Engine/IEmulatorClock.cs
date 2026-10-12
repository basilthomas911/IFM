using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>A testable UTC clock supplied to the deterministic matching engine.</summary>
public interface IEmulatorClock { DateTime UtcNow { get; } }
