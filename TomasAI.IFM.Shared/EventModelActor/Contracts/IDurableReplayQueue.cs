using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IDurableReplayQueue
{
    Task StartAsync(string durableReplayQueueName, TimeSpan replayInterval, CancellationToken cancellationToken = default);
    Task StopAsync(string durableReplayQueueName,  CancellationToken cancellationToken = default);
    void Enqueue(string durableReplayQueueName, IEvent domainEvent, CancellationToken cancellationToken = default);
    Task DequeueAsync(string durableReplayQueueName, Func<IEvent, Task> processMessageFunc, CancellationToken cancellationToken = default);
    void SetMaxAttemptsReachedAction(string durableReplayQueueName, Func<IEvent, Task> maxAttemptsReachedFunc, bool overwrite = true);
    void SetMaxReplayAttemps(string durableReplayQueueName, int maxReplayAttemps, bool overwrite = true);
    int GetMaxReplayAttemps(string durableReplayQueueName);
}
