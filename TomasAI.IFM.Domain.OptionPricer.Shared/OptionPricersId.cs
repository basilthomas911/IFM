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

    public class OptionPricersId
    {
        private readonly OptionStyle _optionStyle;
        private readonly OptionType _optionType;
        private readonly int _daysToMaturity;

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
            => HashCode.Combine(OptionStyle, OptionType, DaysToMaturity);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3875", Justification = "Value identity operators are intentional and tested together with Equals and GetHashCode.")]
        public static bool operator ==(OptionPricersId? e1, OptionPricersId? e2)
            => ReferenceEquals(e1, e2) || (e1 is not null && e2 is not null && e1.OptionStyle == e2.OptionStyle && e1.OptionType == e2.OptionType && e1.DaysToMaturity == e2.DaysToMaturity);

        public static bool operator !=(OptionPricersId? e1, OptionPricersId? e2) => !(e1 == e2);

        public override bool Equals(object? obj) => obj is OptionPricersId other && this == other;

    }
}
