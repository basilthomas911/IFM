using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BDDTests
{
    public record ExceptionEvent : DomainEvent
    {
        public Exception Exception { get; init; }
    }
}

