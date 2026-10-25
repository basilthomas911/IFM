using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public enum CapacityChangeKind { Undefined = 0, Consume = 1, RecordWorking = 2, RecordFill = 3, MarkSubmissionUnknown = 4, RequestCancel = 5, ConfirmCancel = 6, ReleaseUnconsumed = 7, ExpireUnconsumed = 8, RecordPositionClose = 9 }
