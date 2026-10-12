using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Test store that survives construction of another ledger in the same process.</summary>
public sealed class InMemoryEmulatorLedgerStore : IEmulatorLedgerStore
{
    private EmulatorLedgerCheckpoint? _checkpoint;
    public EmulatorLedgerCheckpoint? Load() => _checkpoint;
    public void Save(EmulatorLedgerCheckpoint checkpoint) => _checkpoint = checkpoint;
}
