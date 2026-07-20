using System;
using System.Threading.Tasks;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataBulkCopyContext : IObjectBulkCopyContext
    {
        IObjectRepository _db;
        string _tableName;
        IDataReader _sourceDataReader;

        public ObjectDataBulkCopyContext(IObjectRepository db, DataTable bulkCopyDataTable)
        {
            _db = db;
            _tableName = bulkCopyDataTable.TableName;
            _sourceDataReader = bulkCopyDataTable.CreateDataReader();
        }

        /// <summary>
        /// bulk copy data from source data reader to database table 
        /// </summary>
        public void BulkCopy()
        {
            using (var conn = _db.CreateConnection().As<SqlConnection>(_db.ConnectionString))
            {
                conn.Open();
                try
                {
                    using (var bulkCopy = new SqlBulkCopy(conn as SqlConnection))
                    {
                        bulkCopy.DestinationTableName = _tableName;
                        for (var ordinal = 0; ordinal < _sourceDataReader.FieldCount; ordinal++)
                        {
                            var columnName = _sourceDataReader.GetName(ordinal);
                            bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping
                            {
                                DestinationColumn = columnName,
                                SourceColumn = columnName
                            });
                        }
                        bulkCopy.WriteToServer(_sourceDataReader);
                    }
                }
                catch (Exception ex)
                {
                    var errorMessage = "ObjectDataBulkCopyContext.BulkCopy: database update error";
                    throw new InvalidOperationException(errorMessage, ex);
                }
                conn.Close();
            }

        }

        /// <summary>
        /// call bulk copy asynchronously
        /// </summary>
        /// <returns></returns>
        public async Task BulkCopyAsync()
            => await Task.Run(() => this.BulkCopy());
    }
}
