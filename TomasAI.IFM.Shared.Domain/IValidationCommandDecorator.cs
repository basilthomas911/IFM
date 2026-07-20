using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain
{
    public interface IValidationCommandDecorator<TState> where TState: IBoundedContextState
    {
        void Validate(ICommand command);
    }
}
