using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TomasAI.IFM.Extensions
{
    public static class DataGridViewRowCollectionExtension
    {
    
        public static DataGridViewRow GetSpreadTypeRow(this DataGridViewRowCollection rows, string spreadTypeName)
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
