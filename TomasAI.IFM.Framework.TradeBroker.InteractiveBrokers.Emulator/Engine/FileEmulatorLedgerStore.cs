using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

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
