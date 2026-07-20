using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class ProbabilityValueCollection : List<ProbabilityValue[]>
    {
        private ICollection<OptionSpreadResult> _csParams;

        public  ProbabilityValueCollection()
        {
        }

        public ProbabilityValueCollection(ICollection<OptionSpreadResult> csParams)
        {
            _csParams = csParams;
        }

        //public List<double> SpreadValues => GetSpreadValues(_csParams.ElementAt(0).ShortValues, _csParams.ElementAt(0).LongValues).OrderByDescending(e => e).Where(e => e >= 0.0).ToList();
        public List<double> SpreadValues => GetSpreadValues(_csParams.ElementAt(0).ShortValues, _csParams.ElementAt(0).LongValues).ToList();

        public SpreadDistribution SetForwardPrice(OptionType optionType, int expiryDays, int tradingDays, int lossFactor, double spreadDelta, decimal netSpread)
        {
            var meanPrice = Convert.ToDouble(netSpread);
            var skewDelta = spreadDelta * Math.Sqrt((double)expiryDays / (double)tradingDays);
            try
            {
                meanPrice = SpreadValues.Average(e => e);
                meanPrice = meanPrice <= 0.0 ? Convert.ToDouble(netSpread) : meanPrice;
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

        IEnumerable<double> GetSpreadValues(List<double[]> shortOptionValues, List<double[]> longOptionValues)
        {
            var sortedShortOptionValues = GetOptionValues(shortOptionValues).OrderBy(e => e).ToList();
            var sortedLongOptionValues = GetOptionValues(longOptionValues).OrderBy(e => e).ToList();
            for (var j = 0; j < sortedShortOptionValues.Count; j++)
            {
                var shortOptionValue = sortedShortOptionValues[j];
                var longOptionValue = sortedLongOptionValues[j];
                yield return shortOptionValue - longOptionValue;
            }
             
            IEnumerable<double> GetOptionValues(List<double[]> optionValues)
            {
                for (var j = 0; j < optionValues.Count; j++)
                    for (var k = 0; k < optionValues[j].Length; k++)
                        yield return GetOptionValue(optionValues[j][k]);
            }

            double GetOptionValue(double optionValue)
            {
                optionValue = double.IsInfinity(optionValue) || double.IsNaN(optionValue) ? 0 : optionValue;
                optionValue = optionValue < 0.000001 ? 0 : optionValue;
                return optionValue;
            }
        }

    }
}
