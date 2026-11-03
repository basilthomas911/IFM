using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Extensions
{
    public static class DataGridViewRowCollectionExtension
    {

        extension(DataGridViewRowCollection rows)
        {
            public DataGridViewRow? GetSpreadTypeRow(string spreadTypeName)
            {
                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    if ($"{row.Cells[0].Value}".ToLower() == spreadTypeName.ToLower())
                        return row;
                }
                return default;
            }
        }
    }
}
