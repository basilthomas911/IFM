using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public enum MovementStatus { Undefined = 0, Pending = 1, Confirmed = 2, Cancelled = 3, Unknown = 4 }
