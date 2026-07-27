using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing.ViewModels
{
    public record CommandLogReadModel(
        Guid CommandId, 
        string StreamId,
        BoundedContextName AggregateName,
        string CommandName,
        DateTime CommandTimestamp,
        string CommandData)
    {
    }
}
