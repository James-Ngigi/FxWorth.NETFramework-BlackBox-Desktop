namespace FxWorth
{
    partial class NullClients
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NullClients));
            this.panel1 = new System.Windows.Forms.Panel();
            this.Okay_BTN = new System.Windows.Forms.Button();
            this.Error_PictureBox = new System.Windows.Forms.PictureBox();
            this.Token_LBL = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Error_PictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.Okay_BTN);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 111);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(558, 63);
            this.panel1.TabIndex = 25;
            // 
            // Okay_BTN
            // 
            this.Okay_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Okay_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Okay_BTN.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Okay_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Okay_BTN.FlatAppearance.BorderSize = 0;
            this.Okay_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Okay_BTN.Location = new System.Drawing.Point(214, 14);
            this.Okay_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Okay_BTN.Name = "Okay_BTN";
            this.Okay_BTN.Size = new System.Drawing.Size(130, 35);
            this.Okay_BTN.TabIndex = 20;
            this.Okay_BTN.Text = "Okay";
            this.Okay_BTN.UseVisualStyleBackColor = false;
            // 
            // Error_PictureBox
            // 
            this.Error_PictureBox.Image = ((System.Drawing.Image)(resources.GetObject("Error_PictureBox.Image")));
            this.Error_PictureBox.Location = new System.Drawing.Point(40, 32);
            this.Error_PictureBox.Margin = new System.Windows.Forms.Padding(4);
            this.Error_PictureBox.Name = "Error_PictureBox";
            this.Error_PictureBox.Size = new System.Drawing.Size(60, 55);
            this.Error_PictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.Error_PictureBox.TabIndex = 25;
            this.Error_PictureBox.TabStop = false;
            // 
            // Token_LBL
            // 
            this.Token_LBL.AutoSize = true;
            this.Token_LBL.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Token_LBL.Location = new System.Drawing.Point(131, 27);
            this.Token_LBL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Token_LBL.Name = "Token_LBL";
            this.Token_LBL.Size = new System.Drawing.Size(342, 75);
            this.Token_LBL.TabIndex = 26;
            this.Token_LBL.Text = "You need atleast an \r\nAPI Account Token n\' an Account ID\r\nto automate your trades" +
    " on this platform.";
            this.Token_LBL.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // NullClients
            // 
            this.AcceptButton = this.Okay_BTN;
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.Okay_BTN;
            this.ClientSize = new System.Drawing.Size(558, 174);
            this.Controls.Add(this.Token_LBL);
            this.Controls.Add(this.Error_PictureBox);
            this.Controls.Add(this.panel1);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NullClients";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Client Registration";
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.Error_PictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Okay_BTN;
        private System.Windows.Forms.PictureBox Error_PictureBox;
        private System.Windows.Forms.Label Token_LBL;
    }
}