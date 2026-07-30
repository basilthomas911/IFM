using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IDurableReplayQueue
{
    Task StartAsync(string eventProjectorName, TimeSpan replayInterval, CancellationToken cancellationToken = default);
    Task StopAsync(string eventProjectorName,  CancellationToken cancellationToken = default);
    void Enqueue(string eventProjectorName, IEvent domainEvent, CancellationToken cancellationToken = default);
    Task DequeueAsync(string eventProjectorName, Func<IEvent, Task> processMessageFunc, CancellationToken cancellationToken = default);
    void SetMaxAttemptsReachedAction(string eventProjectorName, Func<IEvent, Task> maxAttemptsReachedFunc, bool overwrite = true);
    void SetMaxReplayAttemps(string eventProjectorName, int maxReplayAttemps, bool overwrite = true);
    int GetMaxReplayAttemps(string eventProjectorName);
}
