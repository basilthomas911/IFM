using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ControlExtension
    {

        extension(Control control)
        {
            public void InvokeAsync(Action controlAction)
                => controlAction();
        }
    }
}
