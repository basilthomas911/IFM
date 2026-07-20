using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain
{
    public interface IValidationDecoratorFactory
    {
        IValidationCommandDecorator<TState> GetDecorator<TState>() where TState : IBoundedContextState;
    }
}
