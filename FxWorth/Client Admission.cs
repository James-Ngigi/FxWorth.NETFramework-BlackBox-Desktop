using System;
using System.Windows.Forms;
using System.Globalization;
using System.Resources.Extensions;

namespace FxWorth
{
    public partial class Adding_New_Token : Form
    {
        public Adding_New_Token()
        {
            InitializeComponent();
        }

        public decimal EnteredProfitTarget
        {
            get
            {
                if (decimal.TryParse(ProfitTargetTXT.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal target))
                {
                    return target;
                }
                return 0m;
            }
        }

        private void Cut_TSMI_Click(object sender, EventArgs e)
        {
            if (tokenTextBox.Focused)
            {
                Clipboard.SetText(tokenTextBox.Text);
                tokenTextBox.Text = string.Empty;
            }

            if (appTextBox.Focused)
            {
                Clipboard.SetText(appTextBox.Text);
                appTextBox.Text = string.Empty;
            }
        }

        private void Copy_TSMI_Click(object sender, EventArgs e)
        {
            if (tokenTextBox.Focused)
            {
                Clipboard.SetText(tokenTextBox.Text);
            }

            if (appTextBox.Focused)
            {
                Clipboard.SetText(appTextBox.Text);
            }
        }

        private void Paste_TSMI_Click(object sender, EventArgs e)
        {
            if (tokenTextBox.Focused)
            {
                tokenTextBox.Text = Clipboard.GetText();
            }

            if (appTextBox.Focused)
            {
                appTextBox.Text = Clipboard.GetText();
            }
        }
    }
}