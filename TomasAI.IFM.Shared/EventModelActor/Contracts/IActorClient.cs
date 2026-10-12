using System.Collections.Immutable;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IActorClient
{
    ValueTask TellAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask TellAsync(IEvent @event);
    ValueTask<TValue> AskAsync<TValue>(IQuery<TValue> query);
    ValueTask<ServiceResult<Guid>> AskAsync(ICommand<IActorEntityId> query);
}
