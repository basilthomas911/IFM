using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataParameterType<TResult> : IObjectParameterType<TResult>
    {
        List<IObjectParameterTypeMap<TResult>> _parameterTypeMaps;

        /// <summary>
        /// public properties
        /// </summary>
        public ICollection<IObjectParameterTypeMap<TResult>> ParameterTypeMaps => _parameterTypeMaps;

        /// <summary>
        /// create parameter type
        /// </summary>
        public ObjectDataParameterType()
        {
            _parameterTypeMaps = new List<IObjectParameterTypeMap<TResult>>();
        }
    
        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(string fieldName, int index = -1)
            => MapFieldNameToProperty(fieldName, index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(string fieldName, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(fieldName, index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, string>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, int>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, int>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, int?>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, int?>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);


        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, DateTime>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, DateTime>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);
        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, DateTime?>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, DateTime?>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, TimeSpan>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, TimeSpan>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);


        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, string>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, bool>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, bool>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);


   
        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, long>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, long>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, double>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, double>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, double?>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, double?>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, decimal>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, decimal>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, decimal?>> fieldNameExpr, int index = -1)
             => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, decimal?>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, Enum>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        /// <summary>
        /// set field name to property
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="asTypeOf">function to convert field value to another type</param>
        /// <param name="index">result parameter index</param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, Enum>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// return guid property
        /// </summary>
        /// <param name="fieldNameExpr"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, Guid>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        public IObjectParameterType<TResult> Set(Expression<Func<TResult, Guid>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// return byte[] property
        /// </summary>
        /// <param name="fieldNameExpr"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public IObjectParameterType<TResult> Set(Expression<Func<TResult, byte[]>> fieldNameExpr, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index);

        public IObjectParameterType<TResult> Set(Expression<Func<TResult, byte[]>> fieldNameExpr, Func<IObject<TResult>, object> asTypeOf, int index = -1)
            => MapFieldNameToProperty(GetFieldName(fieldNameExpr), index, asTypeOf);

        /// <summary>
        /// map field name to property
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="index"></param>
        ObjectDataParameterType<TResult> MapFieldNameToProperty(string fieldName, int index)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("ObjectDataParameterType.Set: fieldName parameter is empty");
            var resultType = typeof(TResult);
            index = GetParameterTypeIndex(resultType, index);
            _parameterTypeMaps.Add(new ObjectDataParameterTypeMap<TResult>(fieldName, index));
            return this;
        }

        /// <summary>
        /// map field name to property
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="index"></param>
        /// <param name="asTypeOf"></param>
        ObjectDataParameterType<TResult> MapFieldNameToProperty(string fieldName, int index, Func<IObject<TResult>, object> asTypeOf)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("ObjectDataParameterType.Set: fieldName parameter is empty");
            if (asTypeOf == null)
                throw new ArgumentException("ObjectDataParameterType.Set: asTypeOf parameter is empty");
            var resultType = typeof(TResult);
            index = GetParameterTypeIndex(resultType, index);
            _parameterTypeMaps.Add(new ObjectDataParameterTypeMap<TResult>(fieldName, index, asTypeOf));
            return this;
        }

        /// <summary>
        /// return parameter type index
        /// </summary>
        /// <param name="resultType"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        int GetParameterTypeIndex(Type resultType, int index)
        {
            var maxParams = 0;
            foreach (var constructor in resultType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                if (constructor.GetParameters().Length > maxParams)
                    maxParams = constructor.GetParameters().Length;
            if (index >= maxParams)
                throw new InvalidOperationException($"ObjectDataParameterType.GetParameterTypeIndex: maximum number of parameters exceeded for '{resultType.Name}'");
            if (index < 0)
                index = _parameterTypeMaps.Count;
            return index;
        }

        /// <summary>
        /// return field name from property expression
        /// </summary>
        /// <param name="propertyExpr"></param>
        /// <returns></returns>
        string GetFieldName(LambdaExpression lambda)
        {
            var memberExpr = default(MemberExpression);
            switch (lambda.Body.NodeType)
            {
                case ExpressionType.Convert:
                    memberExpr = ((UnaryExpression)lambda.Body).Operand as MemberExpression;
                    break;
                case ExpressionType.MemberAccess:
                    memberExpr = lambda.Body as MemberExpression;
                    break;
            }
            if (memberExpr != null)
                return memberExpr.Member.Name;
            throw new InvalidOperationException($"ObjectDataPropertyTypeMap.GetPropertyName: parameter MUST be a property from '{typeof(TResult).Name}'");
        }
    }
}
