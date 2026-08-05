using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ListViewExtension
    {
        public static void SetDoubleBuffered(this ListView listView, bool value)
        {
            listView.GetType()
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(listView, value);
        }
    }
}
