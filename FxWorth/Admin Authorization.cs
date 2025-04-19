using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FxWorth
{
    public partial class Admin_Authorization : Form
    {
        public string EnteredEmail => emailTextBox.Text;
        public string EnteredPassword => passwordTextBox.Text;

        public Admin_Authorization()
        {
            InitializeComponent();
            this.AcceptButton = btnLogin;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(EnteredEmail) || string.IsNullOrWhiteSpace(EnteredPassword))
            {
                MessageBox.Show("Please enter both email and password.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void Admin_Authorization_Load(object sender, EventArgs e)
        {

        }
    }
}
