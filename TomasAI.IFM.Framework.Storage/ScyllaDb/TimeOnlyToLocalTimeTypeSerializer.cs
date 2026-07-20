using Cassandra.Serialization;
using Cassandra;
using System;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb
{
    internal class TimeOnlyToLocalTimeTypeSerializer : TypeSerializer<TimeOnly>
    {
        private static readonly TypeSerializer<LocalTime> serializer = TypeSerializer.PrimitiveLocalTimeSerializer;

        public override ColumnTypeCode CqlType
        {
            get { return ColumnTypeCode.Time; }
        }

        public TimeOnlyToLocalTimeTypeSerializer() { }

        public override TimeOnly Deserialize(ushort protocolVersion, byte[] buffer,
             int offset, int length, IColumnInfo typeInfo)
        {
            var result = serializer.Deserialize(protocolVersion, buffer,
                    offset, length, typeInfo);
            return new TimeOnly(result.Hour, result.Minute, result.Second, result.AsMilliseconds(), result.AsMicroseconds());
        }

        public override byte[] Serialize(ushort protocolVersion, TimeOnly value)
        {
            return serializer.Serialize(protocolVersion,
                new LocalTime(value.Hour, value.Minute, value.Second, value.Millisecond * 1000000));
        }
    }
}