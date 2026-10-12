using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>A posted cash outflow can settle this named component of the exact consumed reservation.</summary>
public enum CapacityFundingComponent { Undefined = 0, SettlementCash = 1, EntryFees = 2, MarginFunding = 3 }
