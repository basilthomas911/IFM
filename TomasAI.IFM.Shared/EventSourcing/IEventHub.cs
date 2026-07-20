using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventHub
    {
        Task<bool> ExecuteAsync(DomainEventCollection domainEvents);
    }

    public interface IEventHub<TBoundedContext> : IEventHub where TBoundedContext : IBoundedContext
    {
    }
}
