namespace TomasAI.IFM.Application.Storage.EventSourceDb;

internal sealed class AtomicPreparationLane
{
    public object Sync { get; } = new();
    public Task Tail { get; set; } = Task.CompletedTask;
    public int Users { get; set; }
    public bool Retired { get; set; }
}

