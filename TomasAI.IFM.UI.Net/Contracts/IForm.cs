using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace TomasAI.IFM.Contracts
{
    public interface IForm<TWindowForm> where TWindowForm : Form
    {
    }
}
