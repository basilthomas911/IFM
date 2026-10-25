using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.CommandPipeline
{
    public interface IExceptionDecoratorFactory
    {
        object GetDecorator<TState>() where TState : IBoundedContextState;
    }
}
