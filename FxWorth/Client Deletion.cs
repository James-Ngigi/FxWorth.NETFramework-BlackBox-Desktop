using System;
using System.Windows.Forms;
using System.Resources.Extensions;

namespace FxWorth
{
    public partial class Remove_Token : Form
    {
        public Remove_Token(string appId, string token)
        {
            InitializeComponent();

            this.Delete_Token_LBL.Text = string.Format("The Client associated with the \"{0}\" Token, \n is about to be deleted from the Client List. Delete?", token);

            // Subscribe to the FormClosing event
            this.FormClosing += Remove_Token_FormClosing;
        }

        private void Yes_BTN_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Yes; // User confirmed removal
            this.Close(); // Close after setting the result
        }

        private void No_BTN_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.No; // User cancelled removal
            this.Close();
        }

        private void Remove_Token_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If the DialogResult is not explicitly set, set it to None
            if (this.DialogResult != DialogResult.Yes && this.DialogResult != DialogResult.No)
            {
                this.DialogResult = DialogResult.None;
            }
        }
    }
}