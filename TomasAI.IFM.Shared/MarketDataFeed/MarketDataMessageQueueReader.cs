using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class MarketDataMessageQueueReader<TMessage>
    {
        private ConcurrentQueue<TMessage> _messageQueue;


        public MarketDataMessageQueueReader()
        {
            _messageQueue = new ConcurrentQueue<TMessage>();
        }

        public void Enqueue(TMessage message)
        {
            if (message != null)
                _messageQueue.Enqueue(message);
        }

        public void Close(Action<ICollection<TMessage>> onMessageAction)
            => ReadAllMessagesFromQueue(onMessageAction);

        public void ReadMessageQueue(Action<ICollection<TMessage>> onMessageAction)
            => ReadAllMessagesFromQueue(onMessageAction);

        private void ReadAllMessagesFromQueue(Action<ICollection<TMessage>> onMessageAction)
        {
            var messages = new List<TMessage>(GetMessagesFromQueue());
            if (messages.Count > 0)
            {
                try { onMessageAction(messages); }
                catch { }
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