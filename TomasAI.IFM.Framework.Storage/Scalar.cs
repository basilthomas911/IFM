using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public record struct Scalar<TScalar>(TScalar Value) where TScalar : struct
    {
    }
}
