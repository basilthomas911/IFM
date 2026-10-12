using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
public sealed record OfflineFillSimulationOptions
{
    public bool Enabled { get; init; } = true;
    public TimeSpan CompletionTime { get; init; } = TimeSpan.FromSeconds(30);
    public int MaximumUnitsPerFill { get; init; } = 3;
    public int RandomSeed { get; init; } = 1;
}
