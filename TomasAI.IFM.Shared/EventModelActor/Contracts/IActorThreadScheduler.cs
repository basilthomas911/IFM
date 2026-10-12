using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for scheduling and managing the execution of actor threads.
/// </summary>
/// <remarks>This interface provides methods to start and stop the scheduler, enqueue messages for processing, and
/// check the scheduler's running state. It is designed to support actor-based concurrency models.</remarks>
public interface IActorThreadScheduler
{
    void Start(IActorThreadQueue threadQueue);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    void Stop();
    bool WriteData(IActorMessage message);

    bool IsRunning { get; }
}
