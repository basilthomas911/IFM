using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.UI.Net.Contracts
{
    public interface IModel<TModel> where TModel : class
    {
        void OnError(Action<int, string> errorNotifier);
    }
}
