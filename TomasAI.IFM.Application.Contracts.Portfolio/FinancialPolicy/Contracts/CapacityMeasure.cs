using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public enum CapacityMeasure { Undefined = 0, SettlementCash = 1, LossCharge = 2, Margin = 3, GrossNotional = 4, GrossContracts = 5, PositionSlots = 6, Delta = 7, Gamma = 8, Vega = 9 }
