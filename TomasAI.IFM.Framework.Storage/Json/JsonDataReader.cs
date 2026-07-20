using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;

namespace TomasAI.IFM.Framework.Storage.Json
{
    public interface IJsonDataReader : IDataReader
    {
    }

    public class JsonDataReader<TData> : IJsonDataReader
    {
        private List<string[]> _rows;
        private int _cursor;
        private List<PropertyInfo> _propertyInfo;
        private Dictionary<string, int> _propertyIndex;
        private Dictionary<Type, Func<int, object>> _valueMap;

        public JsonDataReader(IStringReader stringReader)
        {
            _propertyInfo = [.. typeof(TData).GetProperties()];
            _propertyIndex = [];
            for (var index = 0; index < _propertyInfo.Count; index++)
                _propertyIndex.Add(_propertyInfo[index].Name, index);
            _valueMap = new Dictionary<Type, Func<int, object>>
            {
                {typeof(string), id => IsDBNull(id) ? default! : GetString(id) },
                {typeof(bool), id => IsDBNull(id) ? default : GetBoolean(id) },
                {typeof(bool?), id => IsDBNull(id) ? default(bool?)! : GetBoolean(id) },
                {typeof(int), id => IsDBNull(id) ? default : GetInt32(id) },
                {typeof(int?), id => IsDBNull(id) ? default(int?)! : GetInt32(id) },
                {typeof(short), id => IsDBNull(id) ? default : GetInt16(id) },
                {typeof(short?), id => IsDBNull(id) ? default(short?)! : GetInt16(id) },
                {typeof(long), id => IsDBNull(id) ? default : GetInt64(id) },
                {typeof(long?), id => IsDBNull(id) ? default(long?)! : GetInt64(id) },
                {typeof(ulong), id => IsDBNull(id) ? default : Convert.ToUInt64( GetInt64(id)) },
                {typeof(ulong?), id => IsDBNull(id) ? default(ulong?)! : Convert.ToUInt64( GetInt64(id)) },
                {typeof(double), id => IsDBNull(id) ? default : GetDouble(id) },
                {typeof(double?), id => IsDBNull(id) ? default(double?)! : GetDouble(id) },
                {typeof(float), id => IsDBNull(id) ? default : GetFloat(id) },
                {typeof(float?), id => IsDBNull(id) ? default(float?)! : GetFloat(id) },
                {typeof(decimal), id => IsDBNull(id) ? default : GetDecimal(id) },
                {typeof(decimal?), id => IsDBNull(id) ? default(decimal?)! : GetDecimal(id) },
                {typeof(DateTime), id => IsDBNull(id) ? default : GetDateTime(id) },
                {typeof(DateTime?), id => IsDBNull(id) ? default(DateTime?)! : GetDateTime(id) },
                {typeof(Guid), id => IsDBNull(id) ? default : GetGuid(id) },
                {typeof(Guid?), id => IsDBNull(id) ? default(Guid?)! : GetGuid(id) }
            };

            _rows = [];
            var jsonData = stringReader.ReadToEndAsync().Result;
            if (!string.IsNullOrWhiteSpace(jsonData))
                SplitRowData(jsonData);
            _cursor = -1;
            return;

            void SplitRowData(string jsonData)
            {
                var rows = JsonConvert.DeserializeObject<List<TData>>(jsonData);
                foreach (var row in rows!)
                {
                    var cols = new List<string>();
                    for (var index = 0; index < _propertyInfo.Count; index++)
                    {
                        var value = _propertyInfo[index].GetValue(row);
                        var colValue = value is null
                            ? string.Empty
                            : $"{value}";
                        cols.Add(colValue);
                    }
                    _rows.Add(cols.ToArray());
                }
            }

        }

        public object this[int i] => GetValue(i);

        public object this[string name] => GetValue(GetOrdinal(name));

        public int Depth => 0;

        public bool IsClosed => false;

        public int RecordsAffected => 0;

        public int FieldCount => _propertyInfo.Count;

        public void Close()
        {
        }

        public void Dispose()
        {
        }

        public bool GetBoolean(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (bool.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetBoolean: unable to parse '{fieldValue}'");
        }

        public byte GetByte(int i)
        {
            throw new NotImplementedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }

        public string GetDataTypeName(int i)
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType.Name;
        }

        public DateTime GetDateTime(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (DateTime.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDateTime: unable to parse '{fieldValue}'");
        }

        public decimal GetDecimal(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (decimal.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDecimal: unable to parse '{fieldValue}'");
        }

        public double GetDouble(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (double.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDouble: unable to parse '{fieldValue}'");
        }

        public Type GetFieldType(int i) => _propertyInfo[i].PropertyType;

        public float GetFloat(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (float.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetFloat: unable to parse '{fieldValue}'");
        }

        public Guid GetGuid(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (Guid.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetGuid: unable to parse '{fieldValue}'");
        }

        public short GetInt16(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (short.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt16: unable to parse '{fieldValue}'");
        }

        public int GetInt32(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (int.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt32: unable to parse '{fieldValue}'");
        }

        public long GetInt64(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (long.TryParse(fieldValue, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt64: unable to parse '{fieldValue}'");
        }

        public string GetName(int i) => _propertyInfo[i].Name;

        public int GetOrdinal(string name) => _propertyIndex.ContainsKey(name) ? _propertyIndex[name] : -1;

        public DataTable GetSchemaTable()
        {
            var dataTable = new DataTable("JsonDataReader");
            foreach (var pi in _propertyInfo)
                dataTable.Columns.Add(new DataColumn(pi.Name, pi.PropertyType));
            return dataTable;
        }

        public string GetString(int i)
        {
            ValidIndex(i);
            return _rows[_cursor][i];
        }

        public object GetValue(int i)
        {
            ValidIndex(i);
            var pi = _propertyInfo[i];
            return _valueMap[pi.PropertyType](i);
        }

        public int GetValues(object[] values)
        {
            for (var index = 0; index < values.Length; index++)
                values[index] = GetValue(index);
            return values.Length;
        }

        public bool IsDBNull(int i)
        {
            ValidIndex(i);
            try
            {
                return string.IsNullOrWhiteSpace(_rows[_cursor][i]);
            }
            catch { }
            return true;
        }

        public bool NextResult()
        {
            throw new NotImplementedException();
        }

        public bool Read() => ++_cursor < _rows.Count;

        private void ValidIndex(int index)
        {
            if (index < 0 || index >= FieldCount)
                throw new IndexOutOfRangeException("JsonDataReader invalid field index value");
            if (_cursor < 0 || _cursor >= _rows.Count)
                throw new IndexOutOfRangeException("JsonDataReader invalid cursor");
        }
    }
}
