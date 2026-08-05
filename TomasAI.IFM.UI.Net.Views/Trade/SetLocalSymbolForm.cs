using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Views.Trade
{
    public partial class SetLocalSymbolForm : Form
    {

        public string LocalSymbol => txtLocalSymbol.Text;

        public SetLocalSymbolForm(string localSymbol)
        {
            InitializeComponent();
            txtLocalSymbol.Text = localSymbol ?? string.Empty;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            txtLocalSymbol.Text = null;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
