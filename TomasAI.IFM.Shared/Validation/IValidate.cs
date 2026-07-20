using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Validation
{
    public interface IValidate<TCommand> where TCommand:ICommand
    {
        void ValidateCommand(TCommand command);
    }
}
