using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1700", Justification = "This is a real persisted financial lifecycle state; retain its public name and numeric value.")]
public enum ReservationStatus { Undefined = 0, Reserved = 1, Consumed = 2, Working = 3, PartiallyFilled = 4, SubmissionUnknown = 5, CancelPending = 6, Filled = 7, Released = 8, Expired = 9 }
