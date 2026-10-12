using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;

namespace TomasAI.IFM.Framework.Storage.Json
{

    public class JsonDataReader<TData> : IJsonDataReader
    {
        private readonly List<string[]> _rows;
        private int _cursor;
        private readonly List<PropertyInfo> _propertyInfo;
        private readonly Dictionary<string, int> _propertyIndex;
        private readonly Dictionary<Type, Func<int, object>> _valueMap;

        public JsonDataReader(string jsonData)
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
                {typeof(Guid), id => IsDBNull(id) ? Guid.Empty : GetGuid(id) },
                {typeof(Guid?), id => IsDBNull(id) ? default(Guid?)! : GetGuid(id) }
            };

            _rows = [];
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Preserves the existing externally referenced identity and named API contract.")]
        public static async Task<JsonDataReader<TData>> CreateAsync(
            IStringReader stringReader,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stringReader);
            var jsonData = await stringReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return new JsonDataReader<TData>(jsonData);
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Rows are materialized managed values; there is no owned external resource.
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
            throw new NotSupportedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotSupportedException();
        }

        public char GetChar(int i)
        {
            throw new NotSupportedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotSupportedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotSupportedException();
        }

        public string GetDataTypeName(int i)
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType.Name;
        }

        public DateTime GetDateTime(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (DateTime.TryParse(fieldValue, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDateTime: unable to parse '{fieldValue}'");
        }

        public decimal GetDecimal(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (decimal.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDecimal: unable to parse '{fieldValue}'");
        }

        public double GetDouble(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (double.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetDouble: unable to parse '{fieldValue}'");
        }

        public Type GetFieldType(int i) => _propertyInfo[i].PropertyType;

        public float GetFloat(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (float.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
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
            if (short.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt16: unable to parse '{fieldValue}'");
        }

        public int GetInt32(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (int.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt32: unable to parse '{fieldValue}'");
        }

        public long GetInt64(int i)
        {
            var fieldValue = GetString(i) ?? string.Empty;
            if (long.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidCastException($"JsonDataReader.GetInt64: unable to parse '{fieldValue}'");
        }

        public string GetName(int i) => _propertyInfo[i].Name;

        public int GetOrdinal(string name) => _propertyIndex.TryGetValue(name, out var ordinal) ? ordinal : -1;

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
            ArgumentNullException.ThrowIfNull(values);
            for (var index = 0; index < values.Length; index++)
                values[index] = GetValue(index);
            return values.Length;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
        public bool IsDBNull(int i)
        {
            ValidIndex(i);
            try
            {
                return string.IsNullOrWhiteSpace(_rows[_cursor][i]);
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
            return true;
        }

        public bool NextResult()
        {
            throw new NotSupportedException();
        }

        public bool Read() => ++_cursor < _rows.Count;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2201", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0012", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S112", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
        private void ValidIndex(int index)
        {
            if (index < 0 || index >= FieldCount)
                throw new IndexOutOfRangeException("JsonDataReader invalid field index value");
            if (_cursor < 0 || _cursor >= _rows.Count)
                throw new IndexOutOfRangeException("JsonDataReader invalid cursor");
        }
    }
}
