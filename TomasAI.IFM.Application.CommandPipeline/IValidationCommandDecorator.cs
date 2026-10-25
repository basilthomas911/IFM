using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.CommandPipeline
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The generic state type identifies the DI decorator binding and is part of the published contract.")]
    public interface IValidationCommandDecorator<TState> where TState : IBoundedContextState
    {
        void Validate(ICommand command);
    }
}
