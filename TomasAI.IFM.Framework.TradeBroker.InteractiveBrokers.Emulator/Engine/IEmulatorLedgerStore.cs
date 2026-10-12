using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Durable checkpoint contract used before synthetic callbacks are exposed.</summary>
public interface IEmulatorLedgerStore
{
    EmulatorLedgerCheckpoint? Load();
    void Save(EmulatorLedgerCheckpoint checkpoint);
}
