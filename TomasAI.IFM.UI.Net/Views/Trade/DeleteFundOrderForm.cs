using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TomasAI.IFM.Views.Trade
{
    public partial class DeleteFundOrderForm : Form
    {
        public DeleteFundOrderForm(string caption)
        {
            InitializeComponent();
            btnNo.Select();
            txtDeleteFundOrder.Text = caption;
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Yes;
            this.Close();
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.No;
            this.Close();
        }
    }
}
