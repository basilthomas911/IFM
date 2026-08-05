using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public class ProbabilityValueCollection : List<ProbabilityValue[]>
    {
        readonly ICollection<OptionSpreadResult>? _csParams;
        double[]? _spreadValues;

        public  ProbabilityValueCollection()
        {
        }

        public ProbabilityValueCollection(ICollection<OptionSpreadResult> csParams)
        {
            _csParams = csParams;
        }

        //public List<double> SpreadValues => GetSpreadValues(_csParams.ElementAt(0).ShortValues, _csParams.ElementAt(0).LongValues).OrderByDescending(e => e).Where(e => e >= 0.0).ToList();
        public List<double> SpreadValues => new(Values);

        /// <summary>
        /// Gets the normalized, independently sorted spread values. The calculation is
        /// materialized once because forward-price and loss calculations consume the same data.
        /// </summary>
        public IReadOnlyList<double> Values => _spreadValues ??= CreateSpreadValues();

        public SpreadDistribution SetForwardPrice(OptionType optionType, int expiryDays, int tradingDays, int lossFactor, double spreadDelta, decimal netSpread)
        {
            var meanPrice = Convert.ToDouble(netSpread);
            var skewDelta = spreadDelta * Math.Sqrt((double)expiryDays / (double)tradingDays);
            try
            {
                var values = Values;
                if (values.Count > 0)
                {
                    var total = 0.0;
                    for (var index = 0; index < values.Count; index++)
                        total += values[index];
                    meanPrice = total / values.Count;
                    meanPrice = meanPrice <= 0.0 ? Convert.ToDouble(netSpread) : meanPrice;
                }
            }
            catch
            {
                meanPrice = Convert.ToDouble(netSpread);
            }

            var forwardPrice = lossFactor == 1
                ? meanPrice * (1 + (skewDelta * 2.0))
                : meanPrice * (1 - (skewDelta * 2.0));

            return new SpreadDistribution(expiryDays,  forwardPrice);
        }

        double[] CreateSpreadValues()
        {
            var spread = _csParams?.FirstOrDefault();
            if (spread is null)
                return [];

            var shortValues = FlattenAndNormalize(spread.ShortValues);
            var longValues = FlattenAndNormalize(spread.LongValues);
            if (shortValues.Length != longValues.Length)
                throw new InvalidOperationException("Short and long option simulation counts must match.");

            Array.Sort(shortValues);
            Array.Sort(longValues);
            for (var index = 0; index < shortValues.Length; index++)
                shortValues[index] -= longValues[index];
            return shortValues;

            static double[] FlattenAndNormalize(List<double[]> optionValues)
            {
                var count = 0;
                for (var index = 0; index < optionValues.Count; index++)
                    count += optionValues[index].Length;

                var result = new double[count];
                var resultIndex = 0;
                for (var outer = 0; outer < optionValues.Count; outer++)
                {
                    var source = optionValues[outer];
                    for (var inner = 0; inner < source.Length; inner++)
                    {
                        var value = source[inner];
                        result[resultIndex++] = !double.IsFinite(value) || value < 0.000001 ? 0 : value;
                    }
                }

                return result;
            }
        }

    }
}
