using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain
{
    public interface IExceptionDecoratorFactory
    {
        object GetDecorator<TState>() where TState : IBoundedContextState;
    }
}
