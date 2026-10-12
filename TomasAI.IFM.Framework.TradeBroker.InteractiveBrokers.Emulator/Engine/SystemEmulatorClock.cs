using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>The runtime UTC clock; tests can supply a manually advanced clock.</summary>
public sealed class SystemEmulatorClock : IEmulatorClock { public DateTime UtcNow => DateTime.UtcNow; }
