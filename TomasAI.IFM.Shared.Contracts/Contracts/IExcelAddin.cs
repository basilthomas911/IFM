using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Contracts
{
    public interface IExcelAddin
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1814", Justification = "Excel range interop uses rectangular two-dimensional arrays, not jagged arrays.")]
        string[,] GetCellRangeData(string cellRange);
    }
}
