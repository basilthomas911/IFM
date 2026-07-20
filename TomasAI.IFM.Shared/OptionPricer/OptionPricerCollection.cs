using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionPricerCollection : IOptionPricerCollection
    {
        private static AutoResetEvent _resetEvent = new AutoResetEvent(true);
        private static object _resetLock = new object();
        private ConcurrentQueue<IOptionPricer> _optionPricers;

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

        public IOptionPricer this[int optionPricerId] => _optionPricers.Where(e => e.Id == optionPricerId).SingleOrDefault();

        public List<IOptionPricer> ToList()
            => _optionPricers.ToList();

        /// <summary>
        /// add option pricer to collection
        /// </summary>
        /// <param name="optionPricer"></param>
        public void Add(IOptionPricer optionPricer)
        {
            if (_optionPricers.Any(e => e.Id == optionPricer.Id))
                throw new InvalidOperationException($"OptionPricerCollection.Add: device id {optionPricer.DeviceId} already exists in this collection");
            _optionPricers.Enqueue(optionPricer);
        }

        /// <summary>
        /// remove option pricer by option pricer id
        /// </summary>
        /// <param name="optionPricerId"></param>
        public void Remove(int optionPricerId)
        {
            var optionPricer = _optionPricers.Where(e => e.Id == optionPricerId).SingleOrDefault();
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
                if (!_optionPricers.IsEmpty)
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

        public void Dispose() => Clear();

        public bool Exists(OptionPricerId optionPricerId)
            => _optionPricers.Any(e => e.OptionPricerId == optionPricerId);

        public IOptionPricerCollection GetByOptionType(OptionType optionType)
            => new OptionPricerCollection(_optionPricers.Where(e => e.OptionPricerId.OptionType == optionType).ToArray());
        
    }
}
