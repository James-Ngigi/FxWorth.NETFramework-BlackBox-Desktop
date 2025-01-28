using System.Windows.Forms;

namespace FxWorth
{
    public partial class NullClients : Form
    {
        public NullClients(string message)
        {
            InitializeComponent();
            Token_LBL.Text = message;
            this.Refresh();

            // Ensure proper disposal when form closes
            this.FormClosing += (s, e) =>
            {
                Token_LBL.Dispose();
            };
        }
    }
}