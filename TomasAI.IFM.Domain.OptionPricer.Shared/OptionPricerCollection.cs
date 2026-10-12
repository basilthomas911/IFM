using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserves the existing externally referenced type identity.")]
    public class OptionPricerCollection : IOptionPricerCollection
    {
        private static readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        private readonly ConcurrentQueue<IOptionPricer> _optionPricers;

        public int Count => _optionPricers.Count;

        public OptionPricerCollection()
        {
            _optionPricers = new ConcurrentQueue<IOptionPricer>();
        }

        /// <summary>
        /// fill collection of option pricers
        /// </summary>
        /// <param name="optionPricers"></param>
        public OptionPricerCollection(IOptionPricer[] optionPricers)
        {
            _optionPricers = new ConcurrentQueue<IOptionPricer>();
            if (optionPricers != null && optionPricers.Length > 0)
                foreach (var optionPricer in optionPricers)
                    _optionPricers.Enqueue(optionPricer);
        }

        public IOptionPricer this[int optionPricerId] => _optionPricers.SingleOrDefault(e => e.Id == optionPricerId);

        public List<IOptionPricer> ToList()
            => _optionPricers.ToList();

        /// <summary>
        /// add option pricer to collection
        /// </summary>
        /// <param name="optionPricer"></param>
        public void Add(IOptionPricer optionPricer)
        {
            ArgumentNullException.ThrowIfNull(optionPricer);
            if (_optionPricers.Any(e => e.Id == optionPricer.Id))
                throw new InvalidOperationException($"OptionPricerCollection.Add: device id {optionPricer.DeviceId} already exists in this collection");
            _optionPricers.Enqueue(optionPricer);
        }

        /// <summary>
        /// remove option pricer by option pricer id
        /// </summary>
        /// <param name="optionPricerId"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
        public void Remove(int optionPricerId)
        {
            var optionPricer = _optionPricers.SingleOrDefault(e => e.Id == optionPricerId);
            if (optionPricer == null)
                throw new InvalidOperationException($"OptionPricerCollection.Add: option pricer - {optionPricerId} does not exist in this collection");
            if (_optionPricers.TryDequeue(out optionPricer))
                optionPricer.Dispose();
        }

        /// <summary>
        /// clear option pricers
        /// </summary>
        public void Clear()
        {
            var optionPricerIds = new List<int>();
            foreach (var optionPricer in _optionPricers)
                optionPricerIds.Add(optionPricer.Id);
            optionPricerIds.ForEach(e => Remove(e));
        }

        /// <summary>
        /// get next available option pricer
        /// </summary>
        /// <returns></returns>
        public IOptionPricer Next()
        {
            // find an option pricer that is available for pricing...
            var optionPricer = default(IOptionPricer);
            do
            {
                if (_optionPricers.TryDequeue(out optionPricer))
                        break;
                _resetEvent.WaitOne();
            } while (optionPricer == null);
            return optionPricer;
        }

        /// <summary>
        /// reset option pricer busy flag
        /// </summary>
        /// <param name="optionPricer"></param>
        public void Release(IOptionPricer optionPricer)
        {
            _optionPricers.Enqueue(optionPricer);
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
            => _optionPricers.Any(e => e.OptionPricerId == optionPricerId);

        public IOptionPricerCollection GetByOptionType(OptionType optionType)
            => new OptionPricerCollection(_optionPricers.Where(e => e.OptionPricerId.OptionType == optionType).ToArray());

    }
}
