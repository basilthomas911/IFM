using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataPropertyTypeMap<TResult> :IObjectPropertyTypeMap<TResult>
    {
        string _fieldName;
        string _propertyName;
        Func<IObject<TResult>, object> _asTypeOf;

        public ObjectDataPropertyTypeMap(
            Expression<Func<TResult, object>> propertyExpr,
            string fieldName)
        {
            if (propertyExpr == null)
                throw new ArgumentException($"ObjectDataPropertyTypeMap: property expression MUST be a property from '{typeof(TResult).Name}'");
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("ObjectDataPropertyTypeMap: fieldName parameter is empty");
            _propertyName = GetPropertyName(propertyExpr);
            _fieldName = fieldName;
        }

        public ObjectDataPropertyTypeMap(
            Expression<Func<TResult, Enum>> propertyExpr,
            Func<IObject<TResult>, object> asTypeOf,
            string fieldName)
        {
            if (propertyExpr == null)
                throw new ArgumentException($"ObjectDataPropertyTypeMap: property expression MUST be a property from '{typeof(TResult).Name}'");
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("ObjectDataPropertyTypeMap: fieldName parameter is empty");
            _propertyName = GetPropertyName(propertyExpr);
            _fieldName = fieldName;
            _asTypeOf = asTypeOf;
        }

        public string FieldName => _fieldName;
        public string PropertyName => _propertyName;
        public Func<IObject<TResult>, object> AsTypeOf => _asTypeOf;

        /// <summary>
        /// return name of property
        /// </summary>
        /// <param name="propertyExpr"></param>
        /// <returns></returns>
        string GetPropertyName(LambdaExpression lambda)
        {
            var memberExpr = default(MemberExpression);
            switch(lambda.Body.NodeType)
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
