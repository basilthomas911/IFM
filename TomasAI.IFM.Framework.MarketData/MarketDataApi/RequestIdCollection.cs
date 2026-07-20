using System;
using System.Collections.Generic;
using System.Linq;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    /// <summary>
    /// RequestId collection
    /// </summary>
    public class RequestIdCollection : IRequestIdCollection
    {
        static Dictionary<int, string> _requestIds = new();

        /// <summary>
        /// RequestId collection constructor
        /// </summary>
        public RequestIdCollection()
        {
        } 

        /// <summary>
        /// return request id from contract id
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public int this[string contractId] => GetRequestIdFromContractId(contractId);

        /// <summary>
        /// requestId collection item count
        /// </summary>
        public int Count => _requestIds.Count;

        /// <summary>
        /// add contract id to request id collection and return unique request id
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public int Add(string contractId)
        {
            lock (_requestIds)
            {
                var requestId = this[contractId];
                if (requestId == -1)
                {
                    requestId = Math.Abs(contractId.GetHashCode());
                    _requestIds.Add(requestId, contractId);
                }
                return requestId;
            }
        }

        /// <summary>
        /// remove all request id's from collection
        /// </summary>
        public void Clear()
        {
            lock (_requestIds)
            {
                _requestIds.Clear();
            }
        }

        /// <summary>
        /// return true if request id exists in colllection
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public bool Exists(int requestId)
        {
            lock (_requestIds)
            {
                return _requestIds.ContainsKey(requestId);
            }
        }

        /// <summary>
        /// remove request id from collection
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public void Remove(int requestId)
        {
            lock (_requestIds)
            {
                if (_requestIds.ContainsKey(requestId))
                    _requestIds.Remove(requestId);
            }
        }

        /// <summary>
        /// only needed for trade broker client api
        /// </summary>
        /// <param name="errorHandler"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void SetErrorHandler(Action<int, string> errorHandler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// return request id if contract id exist in collection else return -1
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        private int GetRequestIdFromContractId(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId)) return -1;
            lock (_requestIds)
            {
                return _requestIds.Any(e => e.Value == contractId)
                    ? _requestIds.Single(e => e.Value == contractId).Key
                    : -1;
            }
        }

    }
}
