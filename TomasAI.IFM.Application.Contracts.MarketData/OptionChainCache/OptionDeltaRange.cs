using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Absolute option delta magnitudes, each between zero and one, with a target inside the range.</summary>
[MessagePackObject]
public sealed record OptionDeltaRange([property: Key(0)] decimal Minimum,
    [property: Key(1)] decimal Target, [property: Key(2)] decimal Maximum)
{
    /// <summary>Validates ordered delta magnitudes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
    public void Validate()
    {
        if (Minimum < 0 || Maximum > 1 || Minimum > Target || Target > Maximum)
            throw new ArgumentException("Invalid absolute option delta range.");
    }
}
