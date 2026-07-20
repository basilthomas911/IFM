using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public record UpdateReadModelParameter(
        DomainEventCollection DomainEvents)
    {
    }
}
