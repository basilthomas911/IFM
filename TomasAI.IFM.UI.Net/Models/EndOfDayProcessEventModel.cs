using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models;

public class EndOfDayProcessEventModel(IEndOfDayProcessUIEventConsumer eodEventConsumer) : BaseModel<EndOfDayProcessEventModel>
{
    readonly IEndOfDayProcessUIEventConsumer _eventConsumer = IsArgumentNull.Set(eodEventConsumer);

    /// <summary>
    /// start listening for eod process notification events
    /// </summary>
    /// <param name="listenerAction"></param>
    public async ValueTask StartEndOfDayProcessListenerAsync( Func<IEvent, ValueTask> listenerAction) 
        => await ExecuteValueTaskAsync( () => _eventConsumer.StartAsync(listenerAction));

    /// <summary>
    /// stop listening for eod process notification events
    /// </summary>
    public async ValueTask StopEndOfDayProcessListenerAsync() 
        => await ExecuteValueTaskAsync( _eventConsumer.StopAsync );
    
}
