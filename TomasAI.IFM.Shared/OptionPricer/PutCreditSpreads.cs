using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class PutCreditSpreads : List<double[]>
    {
        public PutCreditSpreads()
        {
        }

        public PutCreditSpreads(IEnumerable<double[]> collection):base(collection)
        {
        }
    }
}
