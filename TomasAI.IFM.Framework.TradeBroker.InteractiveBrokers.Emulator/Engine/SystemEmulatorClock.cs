using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>The runtime UTC clock; tests can supply a manually advanced clock.</summary>
public sealed class SystemEmulatorClock(TimeProvider? timeProvider = null) : IEmulatorClock
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Returns the injected server business time; default Development uses system UTC.</summary>
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
}
