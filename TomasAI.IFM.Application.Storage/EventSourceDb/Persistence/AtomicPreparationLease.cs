namespace TomasAI.IFM.Application.Storage.EventSourceDb;

internal sealed record AtomicPreparationLease(
        AtomicPreparationLane Lane,
        Task Predecessor,
        TaskCompletionSource Release);

