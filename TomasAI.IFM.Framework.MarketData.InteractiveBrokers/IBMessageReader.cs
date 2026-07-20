using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IBApi;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers
{
    public class IBMessageReader
    {
        readonly EClientSocket _clientSocket;
        readonly EReaderSignal _readerSignal;
        Thread? _readerThread;

        public IBMessageReader(EClientSocket clientSocket, EReaderSignal readerSignal)
        {
            _clientSocket =  IsArgumentNull.Set(clientSocket);
            _readerSignal = IsArgumentNull.Set(readerSignal);
        }

        public bool isActive => _clientSocket.IsConnected() && _readerThread is not null;

        public void Start()
        {
            // create reader to consume messages from TWS and store in queue...
            var reader = new EReader(_clientSocket, _readerSignal);
            reader.Start();

            // create additional thread to fetch messages and process via wrapper functions...
            _readerThread = new Thread(() => {
                try
                {
                    while (_clientSocket.IsConnected())
                    {
                        _readerSignal.waitForSignal();
                        reader.processMsgs();
                    }
                }
                catch
                {
                    Stop();
                    return;
                }
            });
            _readerThread.Priority = ThreadPriority.Highest;
            _readerThread.Start();
        }

        public void Stop()
        {
            try
            {
                if (_readerThread is not null && _readerThread.ThreadState != ThreadState.Aborted)
                {
                    _readerThread.Abort();
                    _readerThread = null;
                }
            }
            catch { }
        }
    }
}
