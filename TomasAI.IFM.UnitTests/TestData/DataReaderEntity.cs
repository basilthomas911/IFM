using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.UnitTests.TestData
{
    public class DataReaderEntity
    {
        public string StringValue { get; set; }
        public bool BooleanValue { get; set; }
        public bool? NullBooleanValue { get; set; }
        public int IntValue { get; set; }
        public int? NullIntValue { get; set; }
        public short ShortValue { get; set; }
        public short? NullShortValue { get; set; }
        public long LongValue { get; set; }
        public long? NullLongValue { get; set; }
        public decimal DecimalValue { get; set; }
        public decimal? NullDecimalValue { get; set; }
        public float FloatValue { get; set; }
        public float? NullFloatValue { get; set; }
        public double DoubleValue { get; set; }
        public double? NullDoubleValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public DateTime? NullDateTimeValue { get; set; }
        public TimeSpan TimeSpanValue { get; set; }
        public TimeSpan? NullTimeSpanValue { get; set; }
        public Guid GuidValue { get; set; }
        public Guid? NullGuidValue { get; set; }
        public TestEnumValue EnumValue { get; set; }
        public TestEnumValue? NullEnumValue { get; set; }
    }

    public enum TestEnumValue
    {
        Start,
        End
    }
}
