using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.MarketData.OptionChainCache;

/// <summary>A prepared immutable payload and its monotonically increasing publication version.</summary>
public sealed record OptionChainPublication<T>(long Version, DateTimeOffset PublishedAtUtc, T Snapshot) where T : class;
