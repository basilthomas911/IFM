using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataParameterTypeMap<TResult> : IObjectParameterTypeMap<TResult>
    {
        string _fieldName;
        int _index;
        Func<IObject<TResult>, object> _asTypeOf;

        /// <summary>
        /// create parameter type map
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="index">result parameter index</param>
        public ObjectDataParameterTypeMap(string fieldName, int index, Func<IObject<TResult>, object> asTypeOf = null)
        {
            if (String.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("ObjectDataParameterTypeMap: constructor parameter fieldName is empty");
            if (index < 0)
                throw new ArgumentException("ObjectDataParameterTypeMap: constructor parameter index is negative");
            _fieldName = fieldName;
            _index = index;
            _asTypeOf = asTypeOf;
        }

        public string FieldName => _fieldName;
        public int Index => _index;
        public Func<IObject<TResult>, object> AsTypeOf => _asTypeOf;

    }
}
