using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Durable checkpoint contract used before synthetic callbacks are exposed.</summary>
public interface IEmulatorLedgerStore
{
    EmulatorLedgerCheckpoint? Load();
    void Save(EmulatorLedgerCheckpoint checkpoint);
}

public sealed record EmulatorOrderCheckpoint(
    FrameworkOrderRequest Request,
    decimal Limit,
    int Revision,
    bool Filled,
    bool Cancelled,
    int FilledStrategyUnits = 0);
public sealed record EmulatorOperationCheckpoint(Guid OperationId, string Hash, FrameworkDispatchReceipt Receipt);
public sealed record EmulatorLedgerCheckpoint(decimal Cash, long Sequence, long Generation,
    EmulatorOrderCheckpoint[] Orders, EmulatorOperationCheckpoint[] Operations,
    FrameworkAccountPosition[] Positions, EmulatorJournalEntry[] Journal,
    FrameworkBrokerObservation[] Observations);

/// <summary>Test store that survives construction of another ledger in the same process.</summary>
public sealed class InMemoryEmulatorLedgerStore : IEmulatorLedgerStore
{
    private EmulatorLedgerCheckpoint? _checkpoint;
    public EmulatorLedgerCheckpoint? Load() => _checkpoint;
    public void Save(EmulatorLedgerCheckpoint checkpoint) => _checkpoint = checkpoint;
}

/// <summary>Atomic JSON checkpoint store for the development emulator ledger.</summary>
public sealed class FileEmulatorLedgerStore(string path) : IEmulatorLedgerStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);
    public EmulatorLedgerCheckpoint? Load() => File.Exists(path)
        ? JsonSerializer.Deserialize<EmulatorLedgerCheckpoint>(File.ReadAllBytes(path), Options)
        : null;

    public void Save(EmulatorLedgerCheckpoint checkpoint)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".new";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(checkpoint, Options));
        File.Move(temporary, fullPath, true);
    }
}
