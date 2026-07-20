using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public class EventSource
    {
        long _eventSourceId;
        long _entityId;
        long _entityTypeId;
        long _eventSourceVersion;
        DateTime _eventSourceDate;

        public EventSource(
            long eventSourceId,
            long entityId,
            long entityTypeId,
            long eventSourceVersion,
            DateTime eventSourceDate)
        {
            _eventSourceId = eventSourceId;
            _entityId = entityId;
            _entityTypeId = entityTypeId;
            _eventSourceVersion = eventSourceVersion;
            _eventSourceDate = eventSourceDate;
        }

        public long EventSourceId => _eventSourceId;
        public long EntityId => _entityId;
        public long EntityTypeId => _entityTypeId;
        public long EventSourceVersion => _eventSourceVersion;
        public DateTime EventSourceDate => _eventSourceDate;

        public EventSource IncrementEventSourceVersion(DateTime eventSourceDate)
        {
            Interlocked.Increment(ref _eventSourceVersion);
            _eventSourceDate = eventSourceDate;
            return this;
        }

    }
}
