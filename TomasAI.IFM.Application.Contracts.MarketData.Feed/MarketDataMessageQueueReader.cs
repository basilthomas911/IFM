using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public class MarketDataMessageQueueReader<TMessage>
    {
        private readonly ConcurrentQueue<TMessage> _messageQueue;


        public MarketDataMessageQueueReader()
        {
            _messageQueue = new ConcurrentQueue<TMessage>();
        }

        public void Enqueue(TMessage message)
        {
            if (message is not null)
                _messageQueue.Enqueue(message);
        }

        public void Close(Action<ICollection<TMessage>> onMessageAction)
            
        {
            ArgumentNullException.ThrowIfNull(onMessageAction);
            ReadAllMessagesFromQueue(onMessageAction);
        }

        public void ReadMessageQueue(Action<ICollection<TMessage>> onMessageAction)
            
        {
            Close(onMessageAction);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Callback boundary isolates subscriber failures so subsequent market messages can continue.")]
        private void ReadAllMessagesFromQueue(Action<ICollection<TMessage>> onMessageAction)
        {
            var messages = new List<TMessage>(GetMessagesFromQueue());
            if (messages.Count > 0)
            {
                try { onMessageAction(messages); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("Market data subscriber failed: {0}", ex); }
            }
            return;

            IEnumerable<TMessage> GetMessagesFromQueue()
            {
                while (!_messageQueue.IsEmpty)
                {
                    var message = default(TMessage);
                    if (_messageQueue.TryDequeue(out message))
                        yield return message;
                }
            }
        }
    }
}