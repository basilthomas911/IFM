using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

public enum OptionExerciseStyle { Unknown = 0, European = 1, American = 2 }
