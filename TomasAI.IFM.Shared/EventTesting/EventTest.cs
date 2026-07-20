using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventTesting
{
    public class EventTest
    {
        public EventTest Given(params IEvent[] events)
        {
            return this;
        }

        public EventTest When(ICommand command)
        {
            return this;
        }

        public EventTest Then(params IEvent[] events)
        {
            return this;
        }

    }



}
