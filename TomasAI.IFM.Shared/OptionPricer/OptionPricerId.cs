using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionPricerId
    {
        private OptionStyle _optionStyle;
        private OptionType _optionType;
        private int _deviceId;
        private int _spreadPaths;
        private int _volatilityPaths;
        private int _daysToMaturity;
        private int _maxBatchSize;

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

        public static bool operator ==(OptionPricerId e1, OptionPricerId e2)
            => e1.OptionStyle == e2.OptionStyle
            && e1.OptionType == e2.OptionType
            && e1.SpreadPaths == e2.SpreadPaths
            && e1.VolatilityPaths == e2.VolatilityPaths
            && e1.DeviceId == e2.DeviceId
            && e1.DaysToMaturity == e2.DaysToMaturity
            && e1.MaxBatchSize == e2.MaxBatchSize;

        public static bool operator !=(OptionPricerId e1, OptionPricerId e2)
            => !(e1.OptionStyle == e2.OptionStyle
            && e1.OptionType == e2.OptionType
            && e1.SpreadPaths == e2.SpreadPaths
            && e1.VolatilityPaths == e2.VolatilityPaths
            && e1.DeviceId == e2.DeviceId
            && e1.DaysToMaturity == e2.DaysToMaturity
            && e1.MaxBatchSize == e2.MaxBatchSize);

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }

    public class OptionPricersId
    {
        private OptionStyle _optionStyle;
        private OptionType _optionType;
        private int _daysToMaturity;

        public OptionStyle OptionStyle => _optionStyle;
        public OptionType OptionType => _optionType;
        public int DaysToMaturity => _daysToMaturity;

        public OptionPricersId(
            OptionStyle optionStyle,
            OptionType optionType,
            int daysToMaturity)
        {
            _optionStyle = optionStyle;
            _optionType = optionType;
            _daysToMaturity = daysToMaturity;
        }

        public override string ToString()
            => $"{_optionStyle}|{_optionType} DaysToMaturity: {_daysToMaturity}";

        public override int GetHashCode()
            => $"{this}".GetHashCode();

        public static bool operator ==(OptionPricersId e1, OptionPricersId e2)
            => e1.OptionStyle == e2.OptionStyle
            && e1.OptionType == e2.OptionType
            && e1.DaysToMaturity == e2.DaysToMaturity;

        public static bool operator !=(OptionPricersId e1, OptionPricersId e2)
            => !(e1.OptionStyle == e2.OptionStyle
            && e1.OptionType == e2.OptionType
            && e1.DaysToMaturity == e2.DaysToMaturity);

        public override bool Equals(object obj)
            => obj != null
                && ((OptionPricersId)obj).OptionStyle == this.OptionStyle
                && ((OptionPricersId)obj).OptionType == this.OptionType
                && ((OptionPricersId)obj).DaysToMaturity == this.DaysToMaturity;

    }
}
