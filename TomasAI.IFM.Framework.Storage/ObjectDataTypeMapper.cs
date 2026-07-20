using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataTypeMapper<TEntity> : IObjectTypeMapper<TEntity>
    {
        readonly IObjectRepository _repo;
        readonly string _tableName;

        /// <summary>
        /// create object type mapper
        /// </summary>
        public ObjectDataTypeMapper(IObjectRepository repo, string tableName)
        {
            if (repo == null)
                throw new ArgumentException("ObjectDataTypeMapper: object repository parameter is empty");
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("ObjectDataTypeMapper: table name parameter is empty");
            _repo = repo;
            _tableName = tableName;
        }

        /// <summary>
        /// create mapped entity with properties to field names
        /// </summary>
        /// <param name="setPropertyTypes"></param>
        public DbMap<TEntity> Properties(Action<IObjectPropertyType<TEntity>> setPropertyTypes)
        {
            if (setPropertyTypes == null)
                throw new ArgumentException("ObjectDataTypeMapper.Properties: set property type function is empty");
            var objPropType = new ObjectDataPropertyType<TEntity>() as IObjectPropertyType<TEntity>;
            setPropertyTypes(objPropType);
            var propTypeMaps = objPropType.PropertyTypeMaps;
            var mappedEntity = new DbMap<TEntity>(_repo, _tableName, propTypeMaps);
            _repo.AddResultTypeMap(mappedEntity);
            return mappedEntity;
        }

        /// <summary>
        /// create mapped entity with constructor parameters to field position in result set
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="setParameterTypes"></param>
        public DbMap<TEntity> Parameters(Action<IObjectParameterType<TEntity>> setParameterTypes)
        {
            if (setParameterTypes == null)
                throw new ArgumentException("ObjectDataTypeMapper.Parameters: set parameter type function is empty");
            var paramType = new ObjectDataParameterType<TEntity>() as IObjectParameterType<TEntity>;
            setParameterTypes(paramType);
            var paramTypeMaps = paramType.ParameterTypeMaps;
            var mappedEntity = new DbMap<TEntity>(_repo, _tableName, paramTypeMaps);
            _repo.AddResultTypeMap(mappedEntity);
            return mappedEntity;
        }

    }
}
