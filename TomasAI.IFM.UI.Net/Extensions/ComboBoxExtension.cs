using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ComboBoxExtension
    {
        public static void SetSelectedIndex(this ComboBox ddl, int selectedValue)
        {
            ddl.SelectedIndex = -1;
            for (var index = 0; index < ddl.Items.Count; index++)
            {
                var ddlValue = (int)ddl.Items[index]!;
                if (ddlValue == selectedValue)
                {
                    ddl.SelectedIndex = index;
                    break;
                }
            }
        }
    }
}
