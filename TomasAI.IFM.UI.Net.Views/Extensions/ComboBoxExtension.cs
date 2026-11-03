using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class ComboBoxExtension
    {

        extension(ComboBox ddl)
        {
            public void SetSelectedIndex(int selectedValue)
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
}
