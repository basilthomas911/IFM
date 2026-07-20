using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObject<TResult>
    {
        object Value { get; }

        TValue Get<TValue>(string fieldName);
        TValue As<TValue>();
        TEnum AsEnum<TEnum>() where TEnum : struct;
        Guid AsGuid();
        byte[] AsBinary();
        IObject<TResult> SetFieldId(string fieldName);
        IObject<TResult> SetFieldId(int fieldId);
        void ReadValues();
    }
}
