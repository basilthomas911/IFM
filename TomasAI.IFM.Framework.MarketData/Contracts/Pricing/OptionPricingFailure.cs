using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

/// <summary>Safe diagnostic data; failed pricing never supplies usable numeric output.</summary>
/// <param name="Code">The domain failure code.</param>
/// <param name="Input">The pricing input associated with the failure.</param>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="Detail">The diagnostic detail explaining the failure.</param>
/// <param name="Retryable">Whether the failure can be retried.</param>
[MessagePackObject]
public sealed record OptionPricingFailure(
    [property: Key(0)] string Code,
    [property: Key(1)] string Input,
    [property: Key(2)] string ContractId,
    [property: Key(3)] string Detail,
    [property: Key(4)] bool Retryable = false);
