using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ControlExtension
    {
        public static void InvokeAsync(this Control control, Action controlAction)
            => controlAction();
    }
}
