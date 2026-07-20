using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Framework.Storage
{ 
    public class DbMap<TEntity> : DbMapCollection<TEntity>
    {
        string _tblName;
        ObjectDataResultMapperType _resultMapperType;
        IEnumerable<IObjectPropertyTypeMap<TEntity>> _propertyTypeMaps;
        IEnumerable<IObjectParameterTypeMap<TEntity>> _parameterTypeMaps;

        public string Table => _tblName;
        public IEnumerable<IObjectPropertyTypeMap<TEntity>> PropertyTypeMaps => _propertyTypeMaps;
        public IEnumerable<IObjectParameterTypeMap<TEntity>> ParameterTypeMaps => _parameterTypeMaps;

        /// <summary>
        /// create entity map from property type map
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="propertyTypeMaps"></param>
        public DbMap(IObjectRepository repo, string tableName, IEnumerable<IObjectPropertyTypeMap<TEntity>> propertyTypeMaps)
            :base(repo)
        {
            if (propertyTypeMaps == null)
                throw new ArgumentException("DbMap: property type maps parameter is empty");
            SetDbMap(tableName, ObjectDataResultMapperType.Property);
            _propertyTypeMaps = propertyTypeMaps;
        }

        /// <summary>
        /// create entity map from parameter type map
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="parameterTypeMaps"></param>
        public DbMap(IObjectRepository repo, string tableName, IEnumerable<IObjectParameterTypeMap<TEntity>> parameterTypeMaps)
            :base(repo)
        {
            if (parameterTypeMaps == null)
                throw new ArgumentException("DbMap: parameter type maps parameter is empty");
            SetDbMap(tableName, ObjectDataResultMapperType.Parameter);
            _parameterTypeMaps = parameterTypeMaps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public object GetResultTypeMap()
            => _resultMapperType == ObjectDataResultMapperType.Parameter
                ? (object)_parameterTypeMaps : _propertyTypeMaps;

        /// <summary>
        /// create entity map from result mapper type
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="resultMapperType"></param>
        void SetDbMap(string tableName, ObjectDataResultMapperType resultMapperType)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("DbMap.SetDbMap: table name parameter is empty");
            SetDbMap(this);
            _tblName = tableName;
            _resultMapperType = resultMapperType;
        }
    }
}
