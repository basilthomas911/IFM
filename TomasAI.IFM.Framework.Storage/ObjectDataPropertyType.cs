using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataPropertyType<TResult> : IObjectPropertyType<TResult>
    {
        List<IObjectPropertyTypeMap<TResult>> _propertyTypeMaps;

        /// <summary>
        /// create property type
        /// </summary>
        public ObjectDataPropertyType()
        {
            _propertyTypeMaps = new List<IObjectPropertyTypeMap<TResult>>();
        }

        public ICollection<IObjectPropertyTypeMap<TResult>> PropertyTypeMaps => _propertyTypeMaps;

        /// <summary>
        /// map property to field name
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="propertyExpr">property expression</param>
        /// <returns></returns>
        public IObjectPropertyType<TResult> Set(
            Expression<Func<TResult, object>> propertyExpr, string fieldName)
        {
            _propertyTypeMaps
                .Add(new ObjectDataPropertyTypeMap<TResult>(propertyExpr, fieldName));
            return this;
        }

        /// <summary>
        /// map property with function that converts value of property by type e.g. an Enum
        /// </summary>
        /// <param name="propertyExpr"></param>
        /// <param name="asTypeOf"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public IObjectPropertyType<TResult> Set(
            Expression<Func<TResult, Enum>> propertyExpr, Func<IObject<TResult>, object> asTypeOf, string fieldName)
        {
            _propertyTypeMaps
                .Add(new ObjectDataPropertyTypeMap<TResult>(propertyExpr, asTypeOf, fieldName));
            return this;
        }

    }
}
