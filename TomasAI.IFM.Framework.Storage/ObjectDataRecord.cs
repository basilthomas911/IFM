using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using QLNet;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataRecord<TResult> : IObject<TResult>
    {
        IDataRecord _dataRecord;
        Dictionary<string, int> _fieldIdMap;
        Dictionary<Type, Func<int, object>> _valueMap;
        Dictionary<Type, Func<object>> _defaultValueMap;
        int _fieldId;
        string _fieldName;
        object[] _fieldValues;
        Type[] _fieldTypes;

        /// <summary>
        /// create object data record from data record
        /// </summary>
        /// <param name="dataReader"></param>
        public ObjectDataRecord(IDataRecord dataRecord)
        {
            if (dataRecord == null)
                throw new ArgumentException("ObjectDataRecord: empty dataRecord parameter passed to constructor");
            _dataRecord = dataRecord;
            _fieldId = -1;
            _fieldName = null;
            _fieldValues = new object[dataRecord.FieldCount];

            // map all field names to field id's...
            _fieldIdMap = new Dictionary<string, int>();
            _fieldTypes = new Type[dataRecord.FieldCount];
            for (var fieldId= 0; fieldId < dataRecord.FieldCount; fieldId++)
            {
                _fieldTypes[fieldId] = dataRecord.GetFieldType(fieldId);
                var fieldName = dataRecord.GetName(fieldId).ToLower();
                if (!_fieldIdMap.ContainsKey(fieldName))
                    _fieldIdMap.Add(fieldName, fieldId);
            }

            // map all value record values we can return...
            _valueMap = new Dictionary<Type, Func<int, object>>
            {
                {typeof(string), id => dataRecord.IsDBNull(id) ? default(string) : dataRecord.GetString(id) },
                {typeof(bool), id => dataRecord.IsDBNull(id) ? default(bool) : dataRecord.GetBoolean(id) },
                {typeof(bool?), id => dataRecord.IsDBNull(id) ? default(bool?) : dataRecord.GetBoolean(id) },
                {typeof(int), id => dataRecord.IsDBNull(id) ? default(int) : dataRecord.GetInt32(id) },
                {typeof(int?), id => dataRecord.IsDBNull(id) ? default(int?) : dataRecord.GetInt32(id) },
                {typeof(short), id => dataRecord.IsDBNull(id) ? default(short) : dataRecord.GetInt16(id) },
                {typeof(short?), id => dataRecord.IsDBNull(id) ? default(short?) : dataRecord.GetInt16(id) },
                {typeof(long), id => dataRecord.IsDBNull(id) ? default(long) : dataRecord.GetInt64(id) },
                {typeof(long?), id => dataRecord.IsDBNull(id) ? default(long?) : dataRecord.GetInt64(id) },
                {typeof(ulong), id => dataRecord.IsDBNull(id) ? default(ulong) : Convert.ToUInt64( dataRecord.GetInt64(id)) },
                {typeof(ulong?), id => dataRecord.IsDBNull(id) ? default(ulong?) : Convert.ToUInt64( dataRecord.GetInt64(id)) },
                {typeof(double), id => dataRecord.IsDBNull(id) ? default(double) : dataRecord.GetDouble(id) },
                {typeof(double?), id => dataRecord.IsDBNull(id) ? default(double?) : dataRecord.GetDouble(id) },
                {typeof(float), id => dataRecord.IsDBNull(id) ? default(float) : dataRecord.GetFloat(id) },
                {typeof(float?), id => dataRecord.IsDBNull(id) ? default(float?) : dataRecord.GetFloat(id) },
                {typeof(decimal), id => dataRecord.IsDBNull(id) ? default(decimal) : dataRecord.GetDecimal(id) },
                {typeof(decimal?), id => dataRecord.IsDBNull(id) ? default(decimal?) : dataRecord.GetDecimal(id) },
                {typeof(DateTime), id => dataRecord.IsDBNull(id) ? default(DateTime) : dataRecord.GetDateTime(id) },
                {typeof(DateTime?), id => dataRecord.IsDBNull(id) ? default(DateTime?) : dataRecord.GetDateTime(id) },
                {typeof(TimeSpan), id => GetTimeSpan(id) },
                {typeof(TimeSpan?), id => GetTimeSpan(id) },
                {typeof(Guid), id => dataRecord.IsDBNull(id) ? default(Guid) : dataRecord.GetGuid(id) },
                {typeof(Guid?), id => dataRecord.IsDBNull(id) ? default(Guid?) : dataRecord.GetGuid(id) },
                {typeof(byte[]), id => dataRecord.IsDBNull(id) ? default(byte[]) : dataRecord.GetGuid(id) },
            };

            _defaultValueMap = new Dictionary<Type, Func< object>>
            {
                {typeof(string), () => default(string) },
                {typeof(int), () => default(int) },
                {typeof(long), () =>  default(long) },
                {typeof(short), () => default(short) },
               {typeof(double), () => default(double) },
                {typeof(float), () => default(float) },
                {typeof(decimal), () => default(decimal) },
                {typeof(DateTime), () => default(DateTime) },
                {typeof(DateTime?), () => default(DateTime?) },
                {typeof(TimeSpan), () => default(TimeSpan) },
                {typeof(ulong), () => default(ulong) },
                {typeof(byte[]), () => default(byte[]) }
            };
        }

        /// <summary>
        /// return field value as object
        /// </summary>
        public object Value
        {
            get
            {
                var value = _fieldValues[_fieldId];
                if (value is DBNull)
                {
                    var valueType = _fieldTypes[_fieldId];
                    value = _defaultValueMap.ContainsKey(valueType) ? _defaultValueMap[valueType]() : null;
                }
                return value;
            }
        }

        /// <summary>
        /// read all values from current position in data reader
        /// </summary>
        public void ReadValues() => _dataRecord.GetValues(_fieldValues);

        /// <summary>
        /// set field id from field name
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <returns></returns>
        public IObject<TResult> SetFieldId(string fieldName)
        {
            try
            {
                _fieldName = fieldName.ToLower();
                _fieldId = _fieldIdMap[_fieldName];
                return this;
            }
            catch
            {
                _fieldId = -1;
                _fieldName = null;
                var errorMessage = $"ObjectDataRecord: Unable to set field id for fieldName";
                throw new InvalidOperationException(errorMessage);
            }
        }

        /// <summary>
        /// set field id to column index
        /// </summary>
        /// <param name="fieldId"></param>
        /// <returns></returns>
        public IObject<TResult> SetFieldId(int fieldId)
        {
            if (fieldId >= 0 && fieldId < _dataRecord.FieldCount)
            {
                _fieldId = fieldId;
                return this;
            }
            _fieldId = -1;
            var errorMessage = $"ObjectDataRecord: invalid field index";
            throw new IndexOutOfRangeException(errorMessage);
        }

        /// <summary>
        /// return field value as selected value type
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public TValue As<TValue>()
        {
            var fieldName = "??";
            if (_fieldId >= 0 
                && _fieldId < _dataRecord.FieldCount
                && _valueMap.ContainsKey(typeof(TValue)))
            {
                fieldName = _dataRecord.GetName(_fieldId);
                var value = (TValue)_valueMap[typeof(TValue)](_fieldId);
                _fieldId = -1;
                return value;
            }
            _fieldId = -1;
            var errorMessage = $"ObjectDataRecord.As: Unable to convert field: {fieldName} to value type: {typeof(TValue).Name}";
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// return field value by field name
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public TValue Get<TValue>(string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(fieldName) && _fieldIdMap.ContainsKey(fieldName.ToLower()))
            {
                var fieldId = _fieldIdMap[fieldName.ToLower()];
                return (TValue)_valueMap[typeof(TValue)](fieldId);
            }
            fieldName = fieldName ?? "??";
            var errorMessage = $"ObjectDataRecord: Unable to convert field '{fieldName}' to value type '{typeof(TValue).Name}'";
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// return field value as selected enum type
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <returns></returns>
        public TEnum AsEnum<TEnum>() where TEnum:struct
        {
            var fieldName = "??";
            if (_fieldId >= 0 && _fieldId < _dataRecord.FieldCount)
            {
                TEnum enumValue;
                var enumStringValue = _dataRecord.IsDBNull(_fieldId)
                    ? string.Empty : _dataRecord.GetString(_fieldId);
                if (Enum.TryParse<TEnum>(enumStringValue, out enumValue))
                    return enumValue;
                fieldName = _dataRecord.GetName(_fieldId);
            }
            _fieldId = -1;
            var errorMessage = $"ObjectDataRecord.AsEnum: Unable to convert field: {fieldName} to enum type: {typeof(TEnum).Name}";
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// return field value guid
        /// </summary>
        /// <returns>guid value</returns>
        public Guid AsGuid()
        {
            var fieldName = "??";
            if (_fieldId >= 0 && _fieldId < _dataRecord.FieldCount)
            {
                var guidValue = Guid.Empty;
                if (_dataRecord.IsDBNull(_fieldId)) return guidValue;
                var guidStringValue = _dataRecord.GetString(_fieldId);
                if (string.IsNullOrWhiteSpace(guidStringValue)) return guidValue;
                if (Guid.TryParse(guidStringValue, out guidValue))
                    return guidValue;
                fieldName = _dataRecord.GetName(_fieldId);
            }
            _fieldId = -1;
            var errorMessage = $"ObjectDataRecord.AsGuid: Unable to convert field: {fieldName} to guid";
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// return field value as byte array
        /// </summary>
        /// <returns>guid value</returns>
        public byte[] AsBinary()
        {
            var fieldName = "??";
            if (_fieldId >= 0 && _fieldId < _dataRecord.FieldCount)
            {
                fieldName = _dataRecord.GetName(_fieldId);
                var byteArray = default(byte[]);
                if (_dataRecord.IsDBNull(_fieldId)) 
                    return byteArray;
                var byteArrayLength = _dataRecord.GetBytes(_fieldId, 0, byteArray, 0, 0);
                if (byteArrayLength == 0) 
                    return byteArray;
                byteArray = new byte[byteArrayLength];
                _dataRecord.GetBytes(_fieldId, 0, byteArray, 0, (int )byteArrayLength);
                return byteArray;
            }
            _fieldId = -1;
            var errorMessage = $"ObjectDataRecord.AsBinary: Unable to convert field: {fieldName} to byte array";
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// read time span formtted as:
        /// HH:mm:ss.nnn
        /// </summary>
        /// <param name="tickTimeString"></param>
        /// <returns></returns>
        TimeSpan GetTimeSpan(int fieldId)
        {
            if (_dataRecord.IsDBNull(fieldId))
                return default(TimeSpan);
            var timeSpanValue = _dataRecord.GetValue(fieldId);
            var tickTimeSpan = new TimeSpan(0, 0, 0, 0, 0);
            if (timeSpanValue is string)
            {
                var tickTimeString = timeSpanValue as string;
                if (!string.IsNullOrWhiteSpace(tickTimeString))
                {
                    var tickTime = tickTimeString.Split(new string[] { ":", "." }, StringSplitOptions.RemoveEmptyEntries);
                    if (tickTime.Length == 5)
                    {
                        tickTimeSpan = new TimeSpan(
                            days: int.Parse(tickTime[0]),
                            hours: int.Parse(tickTime[1]),
                            minutes: int.Parse(tickTime[2]),
                            seconds: int.Parse(tickTime[3]),
                            milliseconds: int.Parse(tickTime[4]) );
                    }
                }
            }
            return tickTimeSpan;
        }
    }
}
