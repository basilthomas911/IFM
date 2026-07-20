using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.UnitTests.TestData;
using NSubstitute;
using System.Data;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class ObjectDataRecordTests
    {
        [Fact]
        public void CreateObjectDataRecordOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(2);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetName(1).Returns("age");
            mockDataRecord.GetFieldType(1).Returns(typeof(int));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
        }

        [Fact]
        public void CreateObjectDataRecordWithNullDataRecord()
        {
            var act = () => { var odRecord = new ObjectDataRecord<TestParameterEntity>(null); };
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SetFieldIdOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var dataObject = odRecord.SetFieldId(0);
            dataObject.Should().NotBeNull();
        }

        [Fact]
        public void SetFieldIdWithInvalidId()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { var dataObject = odRecord.SetFieldId(23); };
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void SetFieldIdWithNegativeId()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { var dataObject = odRecord.SetFieldId(-23); };
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void SetFieldIdWithValidFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var dataObject = odRecord.SetFieldId("name");
            dataObject.Should().NotBeNull();
        }

        [Fact]
        public void SetFieldIdWithNullFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { var dataObject = odRecord.SetFieldId(null); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetFieldIdWithEmptyFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { var dataObject = odRecord.SetFieldId(""); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetFieldIdWithBlankFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { var dataObject = odRecord.SetFieldId("   "); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ValueOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue("porsche911", 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().NotBeNull();
            odRecord.SetFieldId(0).Value.Should().Be("porsche911");
        }

        [Fact]
        public void ValueWithDbNull()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().BeNull();
        }

        [Fact]
        public void ValueWithDefaultString()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(string));
        }

        [Fact]
        public void ValueWithDefaultInt()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(int));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(int));
        }

        [Fact]
        public void ValueWithDefaultLong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(long));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(long));
        }

        [Fact]
        public void ValueWithDefaultShort()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(short));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(short));
        }

        [Fact]
        public void ValueWithDefaultDouble()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(double));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(double));
        }

        [Fact]
        public void ValueWithDefaultFloat()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(float));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(float));
        }

        [Fact]
        public void ValueWithDefaultDecimal()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(decimal));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(decimal));
        }

        [Fact]
        public void ValueWithDefaultDateTime()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(DateTime));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(DateTime));
        }

        [Fact]
        public void ValueWithDefaultULong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(ulong));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(ulong));
        }

        [Fact]
        public void ValueWithDefaultTimeSpan()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(TimeSpan));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            odRecord.SetFieldId(0).Value.Should().Be(default(TimeSpan));
        }

        [Fact]
        public void ValueWithInvalidId()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.When(x => x.GetValues(Arg.Any<object[]>()))
                .Do(ci => ci.Arg<object[]>().SetValue(DBNull.Value, 0));
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            odRecord.ReadValues();
            var act = () => { odRecord.Value.Should().BeNull(); };
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void AsWithInvalidId()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.As<string>(); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AsWithUnknownValueType()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.As<TestParameterEntity>(); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AsOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var stringValue = odRecord.SetFieldId(0).As<string>();
            stringValue.Should().NotBeNull();
            stringValue.Should().Be("porsche911");
        }

        [Fact]
        public void AsWithNullString()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns(default(string));
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var stringValue = odRecord.SetFieldId(0).As<string>();
            stringValue.Should().BeNull();
        }

        [Fact]
        public void AsWithBoolean()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("booleanValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(bool));
            mockDataRecord.GetBoolean(0).Returns(true);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var booleanValue = odRecord.SetFieldId(0).As<bool>();
            booleanValue.Should().BeTrue();
            var nullBooleanValue = odRecord.SetFieldId(0).As<bool?>();
            nullBooleanValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullBoolean()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("booleanValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(bool));
            mockDataRecord.GetBoolean(0).Returns(default(bool));
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var booleanValue = odRecord.SetFieldId(0).As<bool>();
            booleanValue.Should().Be(default(bool));
            var nullBooleanValue = odRecord.SetFieldId(0).As<bool?>();
            nullBooleanValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithInteger()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("integerValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(int));
            mockDataRecord.GetInt32(0).Returns(1234);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var integerValue = odRecord.SetFieldId(0).As<int>();
            integerValue.Should().Be(1234);
            var nullIntegerValue = odRecord.SetFieldId(0).As<int?>();
            nullIntegerValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullInteger()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("integerValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(int));
            mockDataRecord.GetInt32(0).Returns(1234);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var integerValue = odRecord.SetFieldId(0).As<int>();
            integerValue.Should().Be(default(int));
            var nullIntegerValue = odRecord.SetFieldId(0).As<int?>();
            nullIntegerValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithShort()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("shortValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(short));
            mockDataRecord.GetInt16(0).Returns((short)1234);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var shortValue = odRecord.SetFieldId(0).As<short>();
            shortValue.Should().Be(1234);
            var nullShortValue = odRecord.SetFieldId(0).As<short?>();
            nullShortValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullShort()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("shortValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(short));
            mockDataRecord.GetInt16(0).Returns((short)1234);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var shortValue = odRecord.SetFieldId(0).As<short>();
            shortValue.Should().Be(default(short));
            var nullShortValue = odRecord.SetFieldId(0).As<short?>();
            nullShortValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithLong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("longValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(long));
            mockDataRecord.GetInt64(0).Returns(1234L);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var longValue = odRecord.SetFieldId(0).As<long>();
            longValue.Should().Be(1234);
            var nullLongValue = odRecord.SetFieldId(0).As<long?>();
            nullLongValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullLong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("longValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(long));
            mockDataRecord.GetInt64(0).Returns(1234L);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var longValue = odRecord.SetFieldId(0).As<long>();
            longValue.Should().Be(default(long));
            var nullLongValue = odRecord.SetFieldId(0).As<long?>();
            nullLongValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithUnsignedLong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("ulongValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(ulong));
            mockDataRecord.GetInt64(0).Returns(1234L);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var ulongValue = odRecord.SetFieldId(0).As<ulong>();
            ulongValue.Should().Be(1234ul);
            var nullULongValue = odRecord.SetFieldId(0).As<ulong?>();
            nullULongValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullUnsignedLong()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("ulongValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(ulong));
            mockDataRecord.GetInt64(0).Returns(1234L);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var ulongValue = odRecord.SetFieldId(0).As<ulong>();
            ulongValue.Should().Be(default(ulong));
            var nullULongValue = odRecord.SetFieldId(0).As<ulong?>();
            nullULongValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithDouble()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("doubleValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(double));
            mockDataRecord.GetDouble(0).Returns(1234.56);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var doubleValue = odRecord.SetFieldId(0).As<double>();
            doubleValue.Should().Be(1234.56);
            var nullDoubleValue = odRecord.SetFieldId(0).As<double?>();
            nullDoubleValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullDouble()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("doubleValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(double));
            mockDataRecord.GetDouble(0).Returns(1234.56);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var doubleValue = odRecord.SetFieldId(0).As<double>();
            doubleValue.Should().Be(default(double));
            var nullDoubleValue = odRecord.SetFieldId(0).As<double?>();
            nullDoubleValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithFloat()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("floatValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(float));
            mockDataRecord.GetFloat(0).Returns(1234.56f);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var floatValue = odRecord.SetFieldId(0).As<float>();
            floatValue.Should().Be(1234.56f);
            var nullFloatValue = odRecord.SetFieldId(0).As<float?>();
            nullFloatValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullFloat()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("floatValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(float));
            mockDataRecord.GetFloat(0).Returns(1234.56f);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var floatValue = odRecord.SetFieldId(0).As<float>();
            floatValue.Should().Be(default(float));
            var nullFloatValue = odRecord.SetFieldId(0).As<float?>();
            nullFloatValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithDecimal()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("decimalValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(decimal));
            mockDataRecord.GetDecimal(0).Returns(1234.56m);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var decimalValue = odRecord.SetFieldId(0).As<decimal>();
            decimalValue.Should().Be(1234.56m);
            var nullDecimalValue = odRecord.SetFieldId(0).As<decimal?>();
            nullDecimalValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullDecimal()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("decimalValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(decimal));
            mockDataRecord.GetDecimal(0).Returns(1234.56m);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var decimalValue = odRecord.SetFieldId(0).As<decimal>();
            decimalValue.Should().Be(default(decimal));
            var nullDecimalValue = odRecord.SetFieldId(0).As<decimal?>();
            nullDecimalValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithDateTime()
        {
            var dateTime = DateTime.Now;
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("datetimeValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(DateTime));
            mockDataRecord.GetDateTime(0).Returns(dateTime);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var datetimeValue = odRecord.SetFieldId(0).As<DateTime>();
            datetimeValue.Should().Be(dateTime);
            var nullDateTimeValue = odRecord.SetFieldId(0).As<DateTime?>();
            nullDateTimeValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullDateTime()
        {
            var dateTime = DateTime.Now;
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("datetimeValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(DateTime));
            mockDataRecord.GetDateTime(0).Returns(dateTime);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var datetimeValue = odRecord.SetFieldId(0).As<DateTime>();
            datetimeValue.Should().Be(default(DateTime));
            var nullDateTimeValue = odRecord.SetFieldId(0).As<DateTime?>();
            nullDateTimeValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void AsWithTimeSpan()
        {
            var timeSpan = new TimeSpan(1, 10, 33, 56, 567);
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("timespanValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(object));
            mockDataRecord.GetValue(0).Returns("1:10:33:56.567");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var timespanValue = odRecord.SetFieldId(0).As<TimeSpan>();
            timespanValue.Should().Be(timeSpan);
            var nullTimeSpanValue = odRecord.SetFieldId(0).As<TimeSpan?>();
            nullTimeSpanValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullTimeSpan()
        {
            var timeSpan = new TimeSpan(1, 10, 33, 56, 567);
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("timespanValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(object));
            mockDataRecord.GetValue(0).Returns(null);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var timespanValue = odRecord.SetFieldId(0).As<TimeSpan>();
            timespanValue.Should().Be(default(TimeSpan));
            var nullTimeSpanValue = odRecord.SetFieldId(0).As<TimeSpan?>();
            nullTimeSpanValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithGuid()
        {
            var guid = Guid.NewGuid();
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("guidValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(object));
            mockDataRecord.GetGuid(0).Returns(guid);
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var guidValue = odRecord.SetFieldId(0).As<Guid>();
            guidValue.Should().Be(guid);
            var nullGuidValue = odRecord.SetFieldId(0).As<Guid?>();
            nullGuidValue.HasValue.Should().BeTrue();
        }

        [Fact]
        public void AsWithNullGuid()
        {
            var guid = Guid.NewGuid();
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("guidValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(object));
            mockDataRecord.GetGuid(0).Returns(guid);
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var guidValue = odRecord.SetFieldId(0).As<Guid>();
            guidValue.Should().Be(default(Guid));
            var nullGuidValue = odRecord.SetFieldId(0).As<Guid?>();
            nullGuidValue.HasValue.Should().BeFalse();
        }

        [Fact]
        public void GetOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var stringValue = odRecord.SetFieldId(0).Get<string>("name");
            stringValue.Should().NotBeNull();
            stringValue.Should().Be("porsche911");
        }

        [Fact]
        public void GetWithNullFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.SetFieldId(0).Get<string>(null); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetWithBlankFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.SetFieldId(0).Get<string>("   "); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetWithEmptyFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.SetFieldId(0).Get<string>(""); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetWithUnknownFieldName()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("name");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("porsche911");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var stringValue = odRecord.SetFieldId(0).Get<string>("funkyTown"); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AsEnumOk()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("enumValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("Start");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var enumValue = odRecord.SetFieldId(0).AsEnum<TestEnumValue>();
            enumValue.Should().Be(TestEnumValue.Start);
        }

        [Fact]
        public void AsEnumWithNullValue()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("enumValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns(default(string));
            mockDataRecord.IsDBNull(0).Returns(true);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var enumValue = odRecord.SetFieldId(0).AsEnum<TestEnumValue>(); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AsEnumWithInvalidValue()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("enumValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("samson");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var enumValue = odRecord.SetFieldId(0).AsEnum<TestEnumValue>(); };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AsEnumWithInvalidFieldId()
        {
            var mockDataRecord = Substitute.For<IDataRecord>();
            mockDataRecord.FieldCount.Returns(1);
            mockDataRecord.GetName(0).Returns("enumValue");
            mockDataRecord.GetFieldType(0).Returns(typeof(string));
            mockDataRecord.GetString(0).Returns("samson");
            mockDataRecord.IsDBNull(0).Returns(false);
            var odRecord = new ObjectDataRecord<TestParameterEntity>(mockDataRecord);
            var act = () => { var enumValue = odRecord.AsEnum<TestEnumValue>(); };
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
