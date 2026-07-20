using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;
using NSubstitute;
using System.Data;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataReaderTests
    {
        [Fact]
        public void CreateObjectDataReaderOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
        }

        [Fact]
        public void CreateObjectDataReaderWithNullDataReader()
        {
            var act = () => { var odReader = new TestObjectDataReader<TestParameterEntity>(null); };
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateObjectDataReaderWithClosedDataReader()
        {
            var act = () => {
                var mockDataReader = Substitute.For<IDataReader>();
                mockDataReader.IsClosed.Returns(true);
                var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
            };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(2);
            mockDataReader.GetName(0).Returns("name");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetName(1).Returns("age");
            mockDataReader.GetFieldType(1).Returns(typeof(int));

            var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
            var o = odReader.Get("name");
            o.Should().NotBeNull();
        }

        [Fact]
        public void GetWithInvalidFieldName()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(2);
            mockDataReader.GetName(0).Returns("name");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetName(1).Returns("age");
            mockDataReader.GetFieldType(1).Returns(typeof(int));

            var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
            var act = () => { var o = odReader.Get("samson"); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetWithValidIndex()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(2);
            mockDataReader.GetName(0).Returns("name");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetName(1).Returns("age");
            mockDataReader.GetFieldType(1).Returns(typeof(int));

            var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
            var o = odReader.Get(0);
            o.Should().NotBeNull();
        }

        [Fact]
        public void GetWithInvalidFieldIndex()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(2);
            mockDataReader.GetName(0).Returns("name");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetName(1).Returns("age");
            mockDataReader.GetFieldType(1).Returns(typeof(int));

            var odReader = new TestObjectDataReader<TestParameterEntity>(mockDataReader);
            var act = () => { var o = odReader.Get(4); };
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void GetStringValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("StringValue");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetString(0).Returns("Rain In Spain");
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var stringValue = odReader.Get(e => e.StringValue);
            stringValue.Should().NotBeNull();
            stringValue.Should().Be("Rain In Spain");
        }

        [Fact]
        public void GetNullStringValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("StringValue");
            mockDataReader.GetFieldType(0).Returns(typeof(string));
            mockDataReader.GetString(0).Returns(default(string));
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var stringValue = odReader.Get(e => e.StringValue);
            stringValue.Should().BeNull();
        }

        [Fact]
        public void GetBooleanValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("BooleanValue");
            mockDataReader.GetFieldType(0).Returns(typeof(bool));
            mockDataReader.GetBoolean(0).Returns(true);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var booleanValue = odReader.Get(e => e.BooleanValue);
            booleanValue.Should().BeTrue();
        }

        [Fact]
        public void GetNullBooleanValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullBooleanValue");
            mockDataReader.GetFieldType(0).Returns(typeof(bool?));
            mockDataReader.GetBoolean(0).Returns(true);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullBooleanValue = odReader.Get(e => e.NullBooleanValue);
            nullBooleanValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetIntValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("IntValue");
            mockDataReader.GetFieldType(0).Returns(typeof(int));
            mockDataReader.GetInt32(0).Returns(23);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var intValue = odReader.Get(e => e.IntValue);
            intValue.Should().Be(23);
        }

        [Fact]
        public void GetNullIntValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullIntValue");
            mockDataReader.GetFieldType(0).Returns(typeof(int?));
            mockDataReader.GetInt32(0).Returns(23);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullIntValue = odReader.Get(e => e.NullIntValue);
            nullIntValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetShortValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("ShortValue");
            mockDataReader.GetFieldType(0).Returns(typeof(short));
            mockDataReader.GetInt16(0).Returns((short)46);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var shortValue = odReader.Get(e => e.ShortValue);
            shortValue.Should().Be(46);
        }

        [Fact]
        public void GetNullShortValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullShortValue");
            mockDataReader.GetFieldType(0).Returns(typeof(short?));
            mockDataReader.GetInt16(0).Returns((short)46);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullShortValue = odReader.Get(e => e.NullShortValue);
            nullShortValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetLongValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("LongValue");
            mockDataReader.GetFieldType(0).Returns(typeof(long));
            mockDataReader.GetInt64(0).Returns((long)1279);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var longValue = odReader.Get(e => e.LongValue);
            longValue.Should().Be(1279);
        }

        [Fact]
        public void GetNullLongValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullLongValue");
            mockDataReader.GetFieldType(0).Returns(typeof(long?));
            mockDataReader.GetInt64(0).Returns((long)1279);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullLongValue = odReader.Get(e => e.NullLongValue);
            nullLongValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetDecimalValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("DecimalValue");
            mockDataReader.GetFieldType(0).Returns(typeof(decimal));
            mockDataReader.GetDecimal(0).Returns(1279.34m);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var decimalValue = odReader.Get(e => e.DecimalValue);
            decimalValue.Should().Be(1279.34m);
        }

        [Fact]
        public void GetNullDecimalValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullDecimalValue");
            mockDataReader.GetFieldType(0).Returns(typeof(decimal?));
            mockDataReader.GetDecimal(0).Returns(1279.34m);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullDecimalValue = odReader.Get(e => e.NullDecimalValue);
            nullDecimalValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetFloatValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("FloatValue");
            mockDataReader.GetFieldType(0).Returns(typeof(float));
            mockDataReader.GetFloat(0).Returns(1279.34f);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var floatValue = odReader.Get(e => e.FloatValue);
            floatValue.Should().Be(1279.34f);
        }

        [Fact]
        public void GetNullFloatValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullFloatValue");
            mockDataReader.GetFieldType(0).Returns(typeof(float?));
            mockDataReader.GetFloat(0).Returns(1279.34f);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullFloatValue = odReader.Get(e => e.NullFloatValue);
            nullFloatValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetDoubleValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("DoubleValue");
            mockDataReader.GetFieldType(0).Returns(typeof(double));
            mockDataReader.GetDouble(0).Returns(3456.7896);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var doubleValue = odReader.Get(e => e.DoubleValue);
            doubleValue.Should().Be(3456.7896);
        }

        [Fact]
        public void GetNullDoubleValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullDoubleValue");
            mockDataReader.GetFieldType(0).Returns(typeof(double?));
            mockDataReader.GetDouble(0).Returns(3456.7896);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullDoubleValue = odReader.Get(e => e.NullDoubleValue);
            nullDoubleValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetDateTimeValueOk()
        {
            var dateTime = DateTime.Now;
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("DateTimeValue");
            mockDataReader.GetFieldType(0).Returns(typeof(DateTime));
            mockDataReader.GetDateTime(0).Returns(dateTime);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var dateTimeValue = odReader.Get(e => e.DateTimeValue);
            dateTimeValue.Should().Be(dateTime);
        }

        [Fact]
        public void GetNullDateTimeValueOk()
        {
            var dateTime = DateTime.Now;
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullDateTimeValue");
            mockDataReader.GetFieldType(0).Returns(typeof(double?));
            mockDataReader.GetDateTime(0).Returns(dateTime);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullDateTimeValue = odReader.Get(e => e.NullDateTimeValue);
            nullDateTimeValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetTimeSpanValueOk()
        {
            var timeSpan = new TimeSpan(1, 10, 59, 23, 234);
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("TimeSpanValue");
            mockDataReader.GetFieldType(0).Returns(typeof(TimeSpan));
            mockDataReader.GetValue(0).Returns("1:10:59:23.234");
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var timeSpanValue = odReader.Get(e => e.TimeSpanValue);
            timeSpanValue.Should().Be(timeSpan);
        }

        [Fact]
        public void GetNullTimeSpanValueOk()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullTimeSpanValue");
            mockDataReader.GetFieldType(0).Returns(typeof(TimeSpan?));
            mockDataReader.GetValue(0).Returns("1:10:59:23.234");
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullTimeSpanValue = odReader.Get(e => e.NullTimeSpanValue);
            nullTimeSpanValue.HasValue.Should().BeTrue();
            nullTimeSpanValue.Should().Be(default(TimeSpan));
        }

        [Fact]
        public void GetGuidValueOk()
        {
            var newGuidValue = Guid.NewGuid();
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("GuidValue");
            mockDataReader.GetFieldType(0).Returns(typeof(Guid));
            mockDataReader.GetGuid(0).Returns(newGuidValue);
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var guidValue = odReader.Get(e => e.GuidValue);
            guidValue.Should().Be(newGuidValue);
        }

        [Fact]
        public void GetNullGuidValueOk()
        {
            var newGuidValue = Guid.NewGuid();
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("NullGuidValue");
            mockDataReader.GetFieldType(0).Returns(typeof(Guid?));
            mockDataReader.GetValue(0).Returns(newGuidValue);
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var nullGuidValue = odReader.Get(e => e.NullGuidValue);
            nullGuidValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetEnumValueOk()
        {
            var testEnumValue = TestEnumValue.Start;
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("EnumValue");
            mockDataReader.GetFieldType(0).Returns(typeof(Enum));
            mockDataReader.GetString(0).Returns($"{TestEnumValue.Start}");
            mockDataReader.IsDBNull(0).Returns(false);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var enumValue = odReader.Get(e => e.EnumValue);
            enumValue.Should().Be(testEnumValue);
        }

        [Fact]
        public void GetNullEnumValue()
        {
            var mockDataReader = Substitute.For<IDataReader>();
            mockDataReader.FieldCount.Returns(1);
            mockDataReader.GetName(0).Returns("EnumValue");
            mockDataReader.GetFieldType(0).Returns(typeof(Enum));
            mockDataReader.GetString(0).Returns($"{TestEnumValue.Start}");
            mockDataReader.IsDBNull(0).Returns(true);

            var odReader = new TestObjectDataReader<DataReaderEntity>(mockDataReader);
            var act = () => { var enumValue = odReader.Get(e => e.EnumValue); };
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
