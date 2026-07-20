using System;

namespace TomasAI.IFM.Shared.AlgoMath.Converters
{
    public static class SlopeToDegreesConverter
    {
        public static double Convert(double slope)
        {
            // calculate the arctangent of the slope (in radians)...
            var radians = Math.Atan(slope);

            // convert radians to degrees...
            var degrees =  radians * (180.0 / Math.PI);
            return degrees;
        }
    }
}
