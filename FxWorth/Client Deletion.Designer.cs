namespace FxWorth
{
    partial class Remove_Token
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Remove_Token));
            this.Question_Mark_PictureBox = new System.Windows.Forms.PictureBox();
            this.Delete_Token_LBL = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.No_BTN = new System.Windows.Forms.Button();
            this.Yes_BTN = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.Question_Mark_PictureBox)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // Question_Mark_PictureBox
            // 
            this.Question_Mark_PictureBox.Image = ((System.Drawing.Image)(resources.GetObject("Question_Mark_PictureBox.Image")));
            this.Question_Mark_PictureBox.Location = new System.Drawing.Point(36, 38);
            this.Question_Mark_PictureBox.Margin = new System.Windows.Forms.Padding(4);
            this.Question_Mark_PictureBox.Name = "Question_Mark_PictureBox";
            this.Question_Mark_PictureBox.Size = new System.Drawing.Size(50, 50);
            this.Question_Mark_PictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.Question_Mark_PictureBox.TabIndex = 0;
            this.Question_Mark_PictureBox.TabStop = false;
            // 
            // Delete_Token_LBL
            // 
            this.Delete_Token_LBL.AutoSize = true;
            this.Delete_Token_LBL.Location = new System.Drawing.Point(99, 45);
            this.Delete_Token_LBL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Delete_Token_LBL.Name = "Delete_Token_LBL";
            this.Delete_Token_LBL.Size = new System.Drawing.Size(498, 42);
            this.Delete_Token_LBL.TabIndex = 1;
            this.Delete_Token_LBL.Text = "The Client associated with the  \"XXXXXXXXXXXXXXX\" Token,\r\nis about to be deleted " +
    "from the Client List. Delete?";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.No_BTN);
            this.panel1.Controls.Add(this.Yes_BTN);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 121);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(613, 63);
            this.panel1.TabIndex = 22;
            // 
            // No_BTN
            // 
            this.No_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.No_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.No_BTN.DialogResult = System.Windows.Forms.DialogResult.No;
            this.No_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.No_BTN.FlatAppearance.BorderSize = 0;
            this.No_BTN.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.No_BTN.Location = new System.Drawing.Point(370, 15);
            this.No_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.No_BTN.Name = "No_BTN";
            this.No_BTN.Size = new System.Drawing.Size(129, 33);
            this.No_BTN.TabIndex = 24;
            this.No_BTN.Text = "Nope";
            this.No_BTN.UseVisualStyleBackColor = false;
            this.No_BTN.Click += new System.EventHandler(this.No_BTN_Click);
            // 
            // Yes_BTN
            // 
            this.Yes_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Yes_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Yes_BTN.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.Yes_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Yes_BTN.FlatAppearance.BorderSize = 0;
            this.Yes_BTN.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.Yes_BTN.Location = new System.Drawing.Point(120, 15);
            this.Yes_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Yes_BTN.Name = "Yes_BTN";
            this.Yes_BTN.Size = new System.Drawing.Size(129, 33);
            this.Yes_BTN.TabIndex = 19;
            this.Yes_BTN.Text = "Delete";
            this.Yes_BTN.UseVisualStyleBackColor = false;
            this.Yes_BTN.Click += new System.EventHandler(this.Yes_BTN_Click);
            // 
            // Remove_Token
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(613, 184);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Delete_Token_LBL);
            this.Controls.Add(this.Question_Mark_PictureBox);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Remove_Token";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Client Deletion";
            ((System.ComponentModel.ISupportInitialize)(this.Question_Mark_PictureBox)).EndInit();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox Question_Mark_PictureBox;
        private System.Windows.Forms.Label Delete_Token_LBL;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Yes_BTN;
        private System.Windows.Forms.Button No_BTN;
    }
}