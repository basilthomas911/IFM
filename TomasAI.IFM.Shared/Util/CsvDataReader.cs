using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;

namespace TomasAI.IFM.Shared.Util
{
    public class CsvDataReader : StreamReader, IDataReader
    {
        private readonly Dictionary<string, int> _columns;
        private readonly List<string[]> _rows;
        private int _rowIndex;

        public CsvDataReader(Stream stream, bool firstRowColName = true)
            : base(stream)
        {
            _columns = new Dictionary<string, int>();
            _rows = new List<string[]>();
            ReadCsv(firstRowColName);
        }

        public CsvDataReader(string filename, bool firstRowColName = true)
            : base(filename)
        {
            _columns = new Dictionary<string, int>();
            _rows = new List<string[]>();
            ReadCsv(firstRowColName);
        }

        public object this[int i] => throw new NotImplementedException();

        public object this[string name] => throw new NotImplementedException();

        public bool IsEmpty => _rows.Count == 0;

        public int Depth => 0;

        public bool IsClosed => this.EndOfStream;

        public int RecordsAffected => 0;

        public int FieldCount => _columns.Count;

        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));

        public byte GetByte(int i) => throw new NotImplementedException();

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();

        public char GetChar(int i) => throw new NotImplementedException();

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotImplementedException();

        public IDataReader GetData(int i) => throw new NotImplementedException();

        public string GetDataTypeName(int i) => throw new NotImplementedException();

        public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));

        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));

        public double GetDouble(int i) => Convert.ToDouble(GetValue(i));

        public Type GetFieldType(int i) => throw new NotImplementedException();

        public float GetFloat(int i) => Convert.ToSingle(GetValue(i));

        public Guid GetGuid(int i) => throw new NotImplementedException();

        public short GetInt16(int i) => Convert.ToInt16(GetValue(i));

        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));

        public long GetInt64(int i) => Convert.ToInt64(GetValue(i));

        public string GetName(int i) => _columns.Where(e => e.Value == i).Select(e => e.Key).Single();

        public int GetOrdinal(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || _columns.Count == 0)
                return -1;
            return _columns[name];
        }

        public DataTable GetSchemaTable() => throw new NotImplementedException();

        public string GetString(int i) => GetValue(i)?.ToString();

        public object GetValue(int i)
        {
            if (_rowIndex < 0 || _rows.Count == 0)
                throw new InvalidOperationException("No rows found in Csv Data");
            if (_rowIndex >= _rows.Count)
                throw new IndexOutOfRangeException("Csv Data row index out of range");
            if  (i < 0 || i >= _columns.Count)
                throw new IndexOutOfRangeException("Csv Data column index out of range");
            return _rows[_rowIndex][i];
        }

        public int GetValues(object[] values)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = GetValue(i);
            return values.Length;
        }

        public bool IsDBNull(int i) => string.IsNullOrWhiteSpace(GetValue(i)?.ToString());

        public bool NextResult() => false;

        bool IDataReader.Read() => ++_rowIndex < _rows.Count;

        private void ReadCsv(bool firstRowColName)
        {
            var firstRow = ReadLine();
            if (string.IsNullOrWhiteSpace(firstRow))
                return;

            // read column names if in first row...
            if (firstRowColName)
            {
                SetColumnNames(firstRow);
                _rowIndex = 0;
            }
            else
                _rowIndex = -1;
            ReadAllRows();
            return;

            void ReadAllRows()
            {
                var rowData = default(string[]);
                while ((rowData = ReadRow(ReadLine())) != null)
                    _rows.Add(rowData);
            }

            string[] ReadRow(string rowData)
            {
                if (string.IsNullOrWhiteSpace(rowData))
                    return default(string[]);
                var pos = 0;
                var fieldData = new List<string>();
                while (pos < rowData.Length)
                {
                    string value;

                    // Special handling for quoted field
                    if (rowData[pos] == '"')
                    {
                        // Skip initial quote
                        pos++;

                        // Parse quoted value
                        int start = pos;
                        while (pos < rowData.Length)
                        {
                            // Test for quote character
                            if (rowData[pos] == '"')
                            {
                                // Found one
                                pos++;

                                // If two quotes together, keep one
                                // Otherwise, indicates end of value
                                if (pos >= rowData.Length || rowData[pos] != '"')
                                {
                                    pos--;
                                    break;
                                }
                            }
                            pos++;
                        }
                        value = rowData.Substring(start, pos - start);
                        value = value.Replace("\"\"", "\"");
                    }
                    else
                    {
                        // Parse unquoted value
                        int start = pos;
                        while (pos < rowData.Length && rowData[pos] != ',')
                            pos++;
                        value = rowData.Substring(start, pos - start);
                    }

                    // Add field to list
                    fieldData.Add(value);
    
                    // Eat up to and including next comma
                    while (pos < rowData.Length && rowData[pos] != ',')
                        pos++;
                    if (pos < rowData.Length)
                        pos++;
                }
                return fieldData.ToArray();
            }

            void SetColumnNames(string rowData)
            {
                var colNames = ReadRow(rowData);
                for (var fieldIndex = 0; fieldIndex < colNames.Length; fieldIndex++)
                    _columns.Add(colNames[fieldIndex], fieldIndex);
            }

        }
    }
}
