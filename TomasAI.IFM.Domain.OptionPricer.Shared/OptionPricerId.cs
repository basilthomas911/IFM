using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public class OptionPricerId
    {
        private readonly OptionStyle _optionStyle;
        private readonly OptionType _optionType;
        private readonly int _deviceId;
        private readonly int _spreadPaths;
        private readonly int _volatilityPaths;
        private readonly int _daysToMaturity;
        private readonly int _maxBatchSize;

        public OptionStyle OptionStyle => _optionStyle;
        public OptionType OptionType => _optionType;
        public int DeviceId => _deviceId;
        public int SpreadPaths => _spreadPaths;
        public int VolatilityPaths => _volatilityPaths;
        public int DaysToMaturity => _daysToMaturity;
        public int MaxBatchSize => _maxBatchSize;

        public OptionPricerId(
            OptionStyle optionStyle,
            OptionType optionType,
            int deviceId,
            int spreadPaths,
            int volatilityPaths,
            int daysToMaturity,
            int maxBatchSize)
        {
            _optionStyle = optionStyle;
            _optionType = optionType;
            _deviceId = deviceId;
            _spreadPaths = spreadPaths;
            _volatilityPaths = volatilityPaths;
            _daysToMaturity = daysToMaturity;
            _maxBatchSize = maxBatchSize;
        }

        public override string ToString()
            => $"DeviceId: {_deviceId} => {_optionStyle}|{_optionType} SpreadPaths: {_spreadPaths} DaysToMaturity: {_daysToMaturity} MaxBatchSize: {_maxBatchSize}";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3875", Justification = "Value identity operators are intentional and tested together with Equals and GetHashCode.")]
        public static bool operator ==(OptionPricerId? e1, OptionPricerId? e2)
            => ReferenceEquals(e1, e2) || (e1 is not null && e2 is not null && e1.OptionStyle == e2.OptionStyle && e1.OptionType == e2.OptionType && e1.SpreadPaths == e2.SpreadPaths && e1.VolatilityPaths == e2.VolatilityPaths && e1.DeviceId == e2.DeviceId && e1.DaysToMaturity == e2.DaysToMaturity && e1.MaxBatchSize == e2.MaxBatchSize);

        public static bool operator !=(OptionPricerId? e1, OptionPricerId? e2) => !(e1 == e2);

        public override bool Equals(object? obj) => obj is OptionPricerId other && this == other;

        public override int GetHashCode() => HashCode.Combine(OptionStyle, OptionType, SpreadPaths, VolatilityPaths, DeviceId, DaysToMaturity, MaxBatchSize);

    }
}
