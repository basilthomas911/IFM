using System;
using System.Collections.Generic;
using System.Linq;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    /// <summary>
    /// streamid collection
    /// </summary>
    public class StreamIdCollection : IStreamIdCollection
    {
        private static Dictionary<int, string> _streamIds;

        /// <summary>
        /// streamId collection constructor
        /// </summary>
        public StreamIdCollection() => _streamIds = new Dictionary<int, string>();

        /// <summary>
        /// return stream id from contract id
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public int this[string contractId] => GetStreamIdFromContractId(contractId);

        /// <summary>
        /// streamId collection item count
        /// </summary>
        public int Count => _streamIds.Count;

        /// <summary>
        /// add contract id to stream id collection and return unique stream id
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public int Add(string contractId)
        {
            lock (_streamIds)
            {
                var streamId = this[contractId];
                if (streamId == -1)
                {
                    streamId = Math.Abs(contractId.GetHashCode());
                    _streamIds.Add(streamId, contractId);
                }
                return streamId;
            }
        }

        /// <summary>
        /// remove all stream id's from collection
        /// </summary>
        public void Clear()
        {
            lock (_streamIds)
            {
                _streamIds.Clear();
            }
        }

        /// <summary>
        /// return true if stream id exists in colllection
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public bool Exists(int  streamId)
        {
            lock (_streamIds)
            {
                return _streamIds.ContainsKey(streamId);
            }
        }

        /// <summary>
        /// remove stream id from collection
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public void Remove(int  streamId)
        {
            lock (_streamIds)
            {
                if (_streamIds.ContainsKey(streamId))
                    _streamIds.Remove(streamId);
            }
        }

        /// <summary>
        /// return stream id if contract id exist in collection else return -1
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        private int GetStreamIdFromContractId(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId)) return -1;
            lock (_streamIds)
            {
                return _streamIds.Any(e => e.Value == contractId)
                    ? _streamIds.Single(e => e.Value == contractId).Key
                    : -1;
            }
        }

    }
}
