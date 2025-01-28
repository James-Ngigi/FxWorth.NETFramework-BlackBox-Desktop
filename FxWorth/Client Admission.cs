using System;
using System.Windows.Forms;

namespace FxWorth
{
    public partial class Adding_New_Token : Form
    {
        public Adding_New_Token()
        {
            InitializeComponent();
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