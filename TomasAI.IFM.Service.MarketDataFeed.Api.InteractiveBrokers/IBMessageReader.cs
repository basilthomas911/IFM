using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IBApi;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class IBMessageReader
    {
        private readonly EClientSocket _clientSocket;
        private readonly EReaderSignal _readerSignal;
        private Thread _readerThread;

        public IBMessageReader(EClientSocket clientSocket, EReaderSignal readerSignal)
        {
            _clientSocket = clientSocket;
            _readerSignal = readerSignal;
        }

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
                    _readerThread = null;
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
                if (_readerThread != null && _readerThread.ThreadState != ThreadState.Aborted)
                {
                    _readerThread.Abort();
                    _readerThread = null;
                }
            }
            catch { }
        }
    }
}
