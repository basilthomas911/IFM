using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Events
{
    public record CreateFundTransactionFailEvent : ErrorEvent
    {
    }
}
