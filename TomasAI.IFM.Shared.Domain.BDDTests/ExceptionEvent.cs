using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain.BDDTests
{
    public record ExceptionEvent : DomainEvent
    {
        public Exception Exception { get; init; } = new Exception("Unknown exception");
    }
}
