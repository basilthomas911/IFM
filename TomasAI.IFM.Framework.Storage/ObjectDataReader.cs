using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage
{
    public abstract class ObjectDataReader<TResult> : IObjectReader<TResult>
    {
        IDataReader _dataReader;
        IObject<TResult> _objectRecord;
        Dictionary<Type, object> _resultTypeMap;
        Dictionary<string, PropertyInfo> _propertyInfoMap;

        /// <summary>
        /// create
        /// </summary>
        /// <param name="dataReader"></param>
        public ObjectDataReader(IDataReader dataReader,  Dictionary<Type, object> resultTypeMap = null)
        {
            if (dataReader == null)
                throw new ArgumentException("ObjectDataReader: dataReader parameter is null");
            if (dataReader.IsClosed)
                throw new InvalidOperationException("ObjectDataReader: dataReader is not open");
            _dataReader = dataReader;
            _resultTypeMap = resultTypeMap;
            _objectRecord = new ObjectDataRecord<TResult>(dataReader);
            _propertyInfoMap = [];
            foreach (var propInfo in typeof(TResult).GetProperties())
                _propertyInfoMap.TryAdd(propInfo.Name, propInfo);
        }

        public bool Read() => _dataReader.Read();
        public abstract Task<bool> ReadAsync();

        public List<TResult> ReadAll()
        {
            return this.GetResultTypeMap() switch
            {
                IEnumerable < IObjectPropertyTypeMap < TResult >> o => ReadAllFromPropertyMap(o),
                IEnumerable<IObjectParameterTypeMap<TResult>> o => ReadAllFromParameterMap(o),
                _ => new List<TResult>()
            };

            List <TResult>  ReadAllFromPropertyMap(IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap)
            {
                var resultSet = new List<TResult>();
                while (this.Read())
                    resultSet.Add(this.Get(propertyMap));
                return resultSet;
            }

            List<TResult> ReadAllFromParameterMap(IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap)
            {
                var resultSet = new List<TResult>();
                var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                while (this.Read())
                    resultSet.Add(this.Get(parameterTypeMaps));
                return resultSet;
            }
        }

        public IReadOnlyList<TResult> ReadAllAsImmutable()
        {
            return this.GetResultTypeMap() switch
            {
                IEnumerable<IObjectPropertyTypeMap<TResult>> o => ReadAllFromPropertyMap(o),
                IEnumerable<IObjectParameterTypeMap<TResult>> o => ReadAllFromParameterMap(o),
                _ => new List<TResult>()
            };

            IReadOnlyList<TResult> ReadAllFromPropertyMap(IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap)
            {
                var resultSet = ImmutableArray.CreateBuilder<TResult>();
                while (this.Read())
                    resultSet.Add(this.Get(propertyMap));
                return resultSet;
            }

            IReadOnlyList<TResult> ReadAllFromParameterMap(IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap)
            {
                var resultSet = ImmutableArray.CreateBuilder<TResult>();
                var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                while (this.Read())
                    resultSet.Add(this.Get(parameterTypeMaps));
                return resultSet;
            }
        }

        public List<TResult> ReadAll(Func<IObjectReader<TResult>, TResult> resultTypeMapper)
        {
            var resultSet = new List<TResult>();
            if (resultTypeMapper != null)
                while (this.Read())
                    resultSet.Add(resultTypeMapper(this));
            return resultSet;
        }

        public List<TResultOut> ReadAll<TResultOut>(Func<TResult, TResultOut> resultMapper)
        {
            var resultSet = new List<TResultOut>();
            if (resultMapper != null)
            {
                switch (this.GetResultTypeMap())
                {
                    case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                        while (this.Read())
                            resultSet.Add(resultMapper(this.Get(propertyMap)));
                        break;
                    case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                        var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                        while (this.Read())
                            resultSet.Add(resultMapper(this.Get(parameterTypeMaps)));
                        break;
                }
            }
            return resultSet;
        }

        public TResult ReadSingle()
        {
            var result = default(TResult);
            switch (this.GetResultTypeMap())
            {
                case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                    if (this.Read())
                        result = this.Get(propertyMap);
                    break;
                case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                    var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                    if (this.Read())
                        result = this.Get(parameterTypeMaps);
                    break;
            }
            return result;
        }

        public TResult ReadSingle(Func<IObjectReader<TResult>, TResult> resultTypeMapper) => _dataReader.Read() ? resultTypeMapper(this) : default(TResult);

        public TResultOut ReadSingle<TResultOut>(Func<TResult, TResultOut> resultMapper)
        {
            var result = default(TResultOut);
            if (resultMapper != null)
            {
                switch (this.GetResultTypeMap())
                {
                    case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                        if (this.Read())
                            result = (resultMapper(this.Get(propertyMap)));
                        break;
                    case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                        var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                        if (this.Read())
                            result = resultMapper(this.Get(parameterTypeMaps));
                        break;
                }
            }
            return result;
        }

        public TScalar ReadScalar<TScalar>(string columnName) where TScalar : struct 
            => _dataReader.Read() ? (typeof(TScalar).IsEnum ? this.Get(columnName).AsEnum<TScalar>() : this.Get(columnName).As<TScalar>()) : default(TScalar);

        public TScalar ReadScalar<TScalar>() where TScalar : struct
            => _dataReader.Read() ? (typeof(TScalar).IsEnum ? this.Get(0).AsEnum<TScalar>() : this.Get(0).As<TScalar>()) : default(TScalar);

        public async Task<List<TResult>> ReadAllAsync()
        {
            var resultSet = new List<TResult>();
            switch (this.GetResultTypeMap())
            {
                case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                    while (await this.ReadAsync())
                        resultSet.Add(this.Get(propertyMap));
                    break;
                case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                    var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                    while (await this.ReadAsync())
                        resultSet.Add(this.Get(parameterTypeMaps));
                    break;
            }
            return resultSet;
        }

        public async Task<List<TResult>> ReadAllAsync(Func<IObjectReader<TResult>, TResult> resultTypeMapper)
        {
            var resultSet = new List<TResult>();
            if (resultTypeMapper != null)
                while (await this.ReadAsync())
                    resultSet.Add(resultTypeMapper(this));
            return resultSet;
        }

        public async Task<List<TResultOut>> ReadAllAsync<TResultOut>(Func<TResult, TResultOut> resultMapper)
        {
            var resultSet = new List<TResultOut>();
            if (resultMapper != null)
            {
                switch (this.GetResultTypeMap())
                {
                    case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                        while (await this.ReadAsync())
                            resultSet.Add(resultMapper(this.Get(propertyMap)));
                        break;
                    case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                        var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                        while (await this.ReadAsync())
                            resultSet.Add(resultMapper(this.Get(parameterTypeMaps)));
                        break;
                }
            }
            return resultSet;
        }

        public async Task<List<TResultOut>> ReadRowsAsync<TResultOut>(Func<TResult, TResultOut> resultMapper, int rowCount)
        {
            var resultSet = new List<TResultOut>();
            if (resultMapper != null)
            {
                switch (this.GetResultTypeMap())
                {
                    case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                        while (await this.ReadAsync())
                        {
                            resultSet.Add(resultMapper(this.Get(propertyMap)));
                            if (resultSet.Count >= rowCount)
                                break;
                        }
                        break;
                    case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                        var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                        while (await this.ReadAsync())
                        {
                            resultSet.Add(resultMapper(this.Get(parameterTypeMaps)));
                            if (resultSet.Count >= rowCount)
                                break;
                        }
                        break;
                }
            }
            return resultSet;
        }

        public async Task<TResult> ReadSingleAsync()
        {
            var result = default(TResult);
            switch (this.GetResultTypeMap())
            {
                case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                    if (await this.ReadAsync())
                        result = this.Get(propertyMap);
                    break;
                case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                    var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                    if (await this.ReadAsync())
                        result = this.Get(parameterTypeMaps);
                    break;
            }
            return result;
        }

        public async Task<TResult> ReadSingleAsync(Func<IObjectReader<TResult>, TResult> resultTypeMapper) => await this.ReadAsync() ? resultTypeMapper(this) : default(TResult);

        public async Task<TResultOut> ReadSingleAsync<TResultOut>(Func<TResult, TResultOut> resultMapper)
        {
            var result = default(TResultOut);
            if (resultMapper != null)
            {
                switch (this.GetResultTypeMap())
                {
                    case IEnumerable<IObjectPropertyTypeMap<TResult>> propertyMap:
                        if (await this.ReadAsync())
                            result = (resultMapper(this.Get(propertyMap)));
                        break;
                    case IEnumerable<IObjectParameterTypeMap<TResult>> parameterMap:
                        var parameterTypeMaps = parameterMap.OrderBy(e => e.Index).ToArray();
                        if (await this.ReadAsync())
                            result = resultMapper(this.Get(parameterTypeMaps));
                        break;
                }
            }
            return result;
        }

        public async Task<TScalar> ReadScalarAsync<TScalar>(string columnName) where TScalar : struct
            => await this.ReadAsync() ? (typeof(TScalar).IsEnum ? this.Get(columnName).AsEnum<TScalar>() : this.Get(columnName).As<TScalar>()) : default(TScalar);

        public async Task<TScalar> ReadScalarAsync<TScalar>() where TScalar : struct
            => await this.ReadAsync() ? (typeof(TScalar).IsEnum ? this.Get(0).AsEnum<TScalar>() : this.Get(0).As<TScalar>()) : default(TScalar);


        /// <summary>
        /// return object record that contains field value
        /// </summary>
        /// <param name="fieldName">data reader field name</param>
        /// <returns></returns>
        public IObject<TResult> Get(string fieldName) => _objectRecord.SetFieldId(fieldName);

        /// <summary>
        /// return object record for value at this field index
        /// </summary>
        /// <param name="fieldId"></param>
        /// <returns></returns>
        public IObject<TResult> Get(int fieldId) => _objectRecord.SetFieldId(fieldId);

        /// <summary>
        /// return object record for value at this field index
        /// </summary>
        /// <param name="fieldId"></param>
        /// <returns></returns>
        public IObject<TResult> Get<TField>(Expression<Func<TField, string>> fieldNameExpr) => _objectRecord.SetFieldId(GetPropertyName(fieldNameExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader string value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>string reader value</returns>
        public string Get(Expression<Func<TResult, string>> resultPropertyExpr) => _objectRecord.Get<string>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader boolean value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>boolean reader value</returns>
        public bool Get(Expression<Func<TResult, bool>> resultPropertyExpr) => _objectRecord.Get<bool>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable boolean value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable boolean reader value</returns>
        public bool? Get(Expression<Func<TResult, bool?>> resultPropertyExpr) => _objectRecord.Get<bool?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>integer reader value</returns>
        public int Get(Expression<Func<TResult, int>> resultPropertyExpr) => _objectRecord.Get<int>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable integer reader value</returns>
        public int? Get(Expression<Func<TResult, int?>> resultPropertyExpr) =>  _objectRecord.Get<int?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader short integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>short integer reader value</returns>
        public short Get(Expression<Func<TResult, short>> resultPropertyExpr) => _objectRecord.Get<short>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable short integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable short integer reader value</returns>
        public short? Get(Expression<Func<TResult, short?>> resultPropertyExpr) => _objectRecord.Get<short?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader long integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>long integer reader value</returns>
        public long Get(Expression<Func<TResult, long>> resultPropertyExpr) => _objectRecord.Get<long>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable long integer value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable long integer reader value</returns>
        public long? Get(Expression<Func<TResult, long?>> resultPropertyExpr) => _objectRecord.Get<long?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader float value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>float reader value</returns>
        public float Get(Expression<Func<TResult, float>> resultPropertyExpr) => _objectRecord.Get<float>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable float value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable float reader value</returns>
        public float? Get(Expression<Func<TResult, float?>> resultPropertyExpr) => _objectRecord.Get<float?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader float value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>float reader value</returns>
        public double Get(Expression<Func<TResult, double>> resultPropertyExpr) => _objectRecord.Get<double>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable double value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable double reader value</returns>
        public double? Get(Expression<Func<TResult, double?>> resultPropertyExpr) => _objectRecord.Get<double?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader decimal value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>decimal reader value</returns>
        public decimal Get(Expression<Func<TResult, decimal>> resultPropertyExpr) => _objectRecord.Get<decimal>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable decimal value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable decimal reader value</returns>
        public decimal? Get(Expression<Func<TResult, decimal?>> resultPropertyExpr) => _objectRecord.Get<decimal?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader datetime value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>datetime reader value</returns>
        public DateTime Get(Expression<Func<TResult, DateTime>> resultPropertyExpr) => _objectRecord.Get<DateTime>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable datetime value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable datetime reader value</returns>
        public DateTime? Get(Expression<Func<TResult, DateTime?>> resultPropertyExpr) => _objectRecord.Get<DateTime?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader timespan value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>timespan reader value</returns>
        public TimeSpan Get(Expression<Func<TResult, TimeSpan>> resultPropertyExpr) => _objectRecord.Get<TimeSpan>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable timespan value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable timespan reader value</returns>
        public TimeSpan? Get(Expression<Func<TResult, TimeSpan?>> resultPropertyExpr) => _objectRecord.Get<TimeSpan?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader guid value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>guid reader value</returns>
        public Guid Get(Expression<Func<TResult, Guid>> resultPropertyExpr) => _objectRecord.Get<Guid>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader nullable guid value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>nullable guid reader value</returns>
        public Guid? Get(Expression<Func<TResult, Guid?>> resultPropertyExpr) => _objectRecord.Get<Guid?>(GetPropertyName(resultPropertyExpr));

        /// <summary>
        /// evaluate property expression and return mapped data reader enum value
        /// </summary>
        /// <param name="resultPropertyExpr">result property expression</param>
        /// <returns>enum reader value</returns>
        public TEnum Get<TEnum>(Expression<Func<TResult, TEnum>> resultPropertyExpr) where TEnum: struct
            => _objectRecord.SetFieldId(GetPropertyName(resultPropertyExpr)).AsEnum<TEnum>();


        /// <summary>
        /// return materialised result mapped from query result fields to object properties
        /// </summary>
        /// <param name="propertyTypeMaps"></param>
        /// <returns></returns>
        public TResult Get(IEnumerable<IObjectPropertyTypeMap<TResult>> propertyTypeMaps)
        {
            var errorMessages = new List<string>();
            var result = Activator.CreateInstance<TResult>();
            this.ReadValues();
            foreach (var typeMap in propertyTypeMaps)
            {
                // set property from field value...
                try
                {
                    var resultObject = _objectRecord.SetFieldId(typeMap.FieldName);
                    var propertyValue = typeMap.AsTypeOf == null ? resultObject.Value: typeMap.AsTypeOf(resultObject);
                    _propertyInfoMap[typeMap.PropertyName].SetValue(result, propertyValue);
                }
                catch { errorMessages.Add($"Unable to set property '{typeMap.PropertyName}' from field '{typeMap.FieldName}'"); continue; }
            }
            if (errorMessages.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                errorMessages.ForEach(errMsg => sb.AppendLine(errMsg));
                throw new InvalidOperationException($"ObjectDataReader.Get: {sb}");
            }
            return result;
        }

        /// <summary>
        /// return materialised result mapped from query result fields to object constructor parameters
        /// </summary>
        /// <param name="paramTypeMaps"></param>
        /// <returns></returns>
        public TResult Get(IObjectParameterTypeMap<TResult>[] paramTypeMaps)
        {
            var errorMessages = new List<string>();
            var resultParams = new object[paramTypeMaps.Length];
            this.ReadValues();
            for (var index=0; index < paramTypeMaps.Length;  index++)
            {
                // set parameter value...
                var typeMap = paramTypeMaps[index];
                try
                {
                    var resultObject = _objectRecord.SetFieldId(typeMap.FieldName);
                    var fieldValue = default(object) switch {
                        _ when typeMap.AsTypeOf == null => resultObject.Value,
                        _ =>  typeMap.AsTypeOf(resultObject)
                    };
                    resultParams[index] = fieldValue;
                }
                catch 
                { 
                    errorMessages.Add($"Unable to read value for field '{typeMap.FieldName}'"); 
                    continue; 
                }
            }
            if (errorMessages.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                errorMessages.ForEach(errMsg => sb.AppendLine(errMsg));
                throw new InvalidOperationException($"ObjectDataReader.Get: result type '{typeof(TResult).Name}' {sb}");
            }
            try
            {
                return (TResult)Activator.CreateInstance(typeof(TResult), resultParams);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                throw new InvalidOperationException($"ObjectDataReader.Get: result type '{typeof(TResult).Name}' {ex.Message}");
            }
        }

        void ReadValues() => _objectRecord.ReadValues();

        /// <summary>
        /// return result mapper type
        /// </summary>
        /// <returns></returns>
        public object GetResultTypeMap()
        {
            if (_resultTypeMap == null)
                throw new InvalidOperationException($"ObjectDataReader.GetResultTypeMap: result type map is empty for '{typeof(TResult).Name}'");
            if (!_resultTypeMap.ContainsKey(typeof(TResult)))
                throw new InvalidOperationException($"ObjectDataReader.GetResultTypeMap: result type map does not contain result type '{typeof(TResult).Name}'");
            var dbMap = _resultTypeMap[typeof(TResult)] as DbMap<TResult>;
            if (dbMap == null)
                throw new InvalidOperationException($"ObjectDataReader.GetResultTypeMap: result type map does not contain mapped entity DbMap<{typeof(TResult).Name}>");
            var resultTypeMap = dbMap.GetResultTypeMap();
            if (resultTypeMap == null)
                throw new InvalidOperationException($"ObjectDataReader.GetResultTypeMap: result type map does not contain mapped entity DbMap<{typeof(TResult).Name}>");
            return resultTypeMap;
        }

        /// <summary>
        /// return property name from property expression
        /// </summary>
        /// <param name="propertyExpr"></param>
        /// <returns></returns>
        string GetPropertyName(LambdaExpression lambda)
        {
            if (lambda == null)
                throw new ArgumentException($"ObjectDataReader.GetPropertyName: parameter MUST be a property from '{typeof(TResult).Name}'");
            var memberExpr = lambda.Body as MemberExpression;
            if (memberExpr == null)
                throw new InvalidOperationException($"ObjectDataReader.GetPropertyName: parameter MUST be a property from '{typeof(TResult).Name}'");
            return memberExpr.Member.Name;
        }

        public void Dispose()
        {
            _objectRecord = null;
            _resultTypeMap = null;
            _propertyInfoMap = null;
        }
    }

}
