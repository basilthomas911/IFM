using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ProgressBarExtension
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);

        extension(ProgressBar pBar)
        {
            public void SetState(int state)
            {
                SendMessage(pBar.Handle, 1040, (IntPtr)state, IntPtr.Zero);
            }
        }
    }
}
