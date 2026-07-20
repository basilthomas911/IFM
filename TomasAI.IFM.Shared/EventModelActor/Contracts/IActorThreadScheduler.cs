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
    void Stop();
    bool WriteData(in NatsMsg<byte[]> message);

    bool IsRunning { get; }
}
