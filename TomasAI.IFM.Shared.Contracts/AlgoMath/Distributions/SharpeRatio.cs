using System;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.Distributions;

namespace TomasAI.IFM.Shared.AlgoMath.Distributions
{
    public class SharpeRatio
    {

        public static void Estimate(IEnumerable<double> assetReturns, IEnumerable<double> benchmarkReturns)
        {
            // This compatibility entry point has never produced a result; no estimator is configured here.
        }

        private SharpeRatio(IEnumerable<double> assetReturns, IEnumerable<double> benchmarkReturns)
        {
            // Instances are not exposed; retained for compatibility with the original implementation.
        }
    }
}
