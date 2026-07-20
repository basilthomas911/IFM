using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionValues
    {
        private double[,] _value;
        private double[,] _assetPrice;
        private int _maxBatchSize;
        private int _maxDaysToMaturity;

        /// <summary>
        /// create option values object to hold value/asset price for complete credit spread batch
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="daysToMaturity"></param>
        public OptionValues(int batchSize, int daysToMaturity)
        {
            _value = new double[batchSize, daysToMaturity];
            _assetPrice = new double[batchSize, daysToMaturity];
            _maxBatchSize = batchSize;
            _maxDaysToMaturity = daysToMaturity;
        }

        /// <summary>
        /// set credit spread value
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="daysToMaturity"></param>
        /// <param name="value"></param>
        public OptionValues SetValues( int daysToMaturity, double[] values)
        {
            var batchSize = values.Length;
            if (batchSize < 0 || batchSize > _maxBatchSize)
                throw new InvalidOperationException("OptionValues.SetValues => Invalid batch size parameter");
            if (daysToMaturity < 0 || daysToMaturity > _maxDaysToMaturity)
                throw new InvalidOperationException("OptionValues.SetValues => Invalid days to maturity parameter");
            for(var i=0; i < batchSize; i++)
                _value[i, daysToMaturity] = values[i];
            return this;
        }

        /// <summary>
        /// get credit spread value
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="daysToMaturity"></param>
        /// <returns></returns>
        public double GetValue(int batchSize, int daysToMaturity)
        {
            if (batchSize < 0 || batchSize > _maxBatchSize)
                throw new InvalidOperationException("OptionValues.GetValue => Invalid batch size parameter");
            if (daysToMaturity < 0 || daysToMaturity > _maxDaysToMaturity)
                throw new InvalidOperationException("OptionValues.GetValue => Invalid days to maturity parameter");
            return _value[batchSize, daysToMaturity];
        }

        public IEnumerable<double[]> GetValues()
        {
            for (var i = 0; i < _maxBatchSize; i++)
            {
                var values = new double[_maxDaysToMaturity];
                for (var j = 0; j < _maxDaysToMaturity; j++)
                    values[j] = _value[i, j];
                yield return values;
            }
        }

        /// <summary>
        /// set credit spread asset price
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="daysToMaturity"></param>
        /// <param name="value"></param>
        public OptionValues SetAssetPrices(int daysToMaturity, double[] values)
        {
            var batchSize = values.Length;
            if (batchSize < 0 || batchSize > _maxBatchSize)
                throw new InvalidOperationException("OptionValues.SetAssetPrices => Invalid batch size parameter");
            if (daysToMaturity < 0 || daysToMaturity > _maxDaysToMaturity)
                throw new InvalidOperationException("OptionValues.SetAssetPrices => Invalid days to maturity parameter");
            for(var i=0; i < batchSize; i++)
                _assetPrice[i, daysToMaturity] = values[i];
            return this;
        }

        /// <summary>
        /// get credit spread asset price
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="daysToMaturity"></param>
        /// <returns></returns>
        public double GetAssetPrice(int batchSize, int daysToMaturity)
        {
            if (batchSize < 0 || batchSize > _maxBatchSize)
                throw new InvalidOperationException("OptionValues.GetAssetPrice => Invalid batch size parameter");
            if (daysToMaturity < 0 || daysToMaturity > _maxDaysToMaturity)
                throw new InvalidOperationException("OptionValues.GetAssetPrice => Invalid days to maturity parameter");
            return _assetPrice[batchSize, daysToMaturity];
        }
    }
}
