using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserves the existing externally referenced type identity.")]
    public class SpreadPricerCollection : ISpreadPricerCollection
    {
        private static readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        private readonly ConcurrentQueue<ISpreadPricer> _spreadPricers;

        public int Count => _spreadPricers.Count;

        public SpreadPricerCollection()
        {
            _spreadPricers = new ConcurrentQueue<ISpreadPricer>();
        }

        /// <summary>
        /// fill collection of spread pricers
        /// </summary>
        /// <param name="optionPricers"></param>
        public SpreadPricerCollection(ISpreadPricer[] spreadPricers)
        {
            _spreadPricers = new ConcurrentQueue<ISpreadPricer>();
            if (spreadPricers != null && spreadPricers.Length > 0)
                foreach (var optionPricer in spreadPricers)
                    _spreadPricers.Enqueue(optionPricer);
        }

        public ISpreadPricer? this[int optionPricerId] => _spreadPricers.SingleOrDefault(e => e.Id == optionPricerId);

        public List<ISpreadPricer> ToList()
            => _spreadPricers.ToList();

        /// <summary>
        /// add spread pricer to collection
        /// </summary>
        /// <param name="spreadPricer"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
        public void Add(ISpreadPricer spreadPricer)
        {
            ArgumentNullException.ThrowIfNull(spreadPricer);
            if (_spreadPricers.Any(e => e.Id == spreadPricer.Id))
                throw new InvalidOperationException($"SpreadPricerCollection.Add: device id {spreadPricer.DeviceId} already exists in this collection");
            _spreadPricers.Enqueue(spreadPricer);
        }

        /// <summary>
        /// remove spread pricer by option pricer id
        /// </summary>
        /// <param name="optionPricerId"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
        public void Remove(int optionPricerId)
        {
            var spreadPricer = _spreadPricers.SingleOrDefault(e => e.Id == optionPricerId);
            if (spreadPricer == null)
                throw new InvalidOperationException($"SpreadPricerCollection.Add: spread pricer - {optionPricerId} does not exist in this collection");
            if (_spreadPricers.TryDequeue(out spreadPricer))
                spreadPricer.Dispose();
        }

        /// <summary>
        /// clear option pricers
        /// </summary>
        public void Clear()
        {
            var optionPricerIds = new List<int>(_spreadPricers.Select(e => e.Id));
            optionPricerIds.ForEach(e => Remove(e));
        }

        /// <summary>
        /// get next available option pricer
        /// </summary>
        /// <returns></returns>
        public ISpreadPricer Next()
        {
            // find an option pricer that is available for pricing...
            var spreadPricer = default(ISpreadPricer);
            do
            {
                if (_spreadPricers.TryDequeue(out spreadPricer))
                        break;
                _resetEvent.WaitOne();
            } while (spreadPricer == null);
            return spreadPricer;
        }

        /// <summary>
        /// reset option pricer busy flag
        /// </summary>
        /// <param name="spreadPricer"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
        public void Release(ISpreadPricer spreadPricer)
        {
            _spreadPricers.Enqueue(spreadPricer);
            _resetEvent.Set();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) Clear();
        }

        public bool Exists(OptionPricerId optionPricerId)
            => _spreadPricers.Any(e => e.OptionPricerId == optionPricerId);
    }
}
