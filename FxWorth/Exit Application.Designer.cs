namespace FxWorth
{
    partial class Exit_Application
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Exit_Application));
            this.Exit_FxWorth_LBL = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.No_BTN = new System.Windows.Forms.Button();
            this.Yes_BTN = new System.Windows.Forms.Button();
            this.Switch_Off_PictureBox = new System.Windows.Forms.PictureBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Switch_Off_PictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // Exit_FxWorth_LBL
            // 
            this.Exit_FxWorth_LBL.AutoSize = true;
            this.Exit_FxWorth_LBL.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Exit_FxWorth_LBL.Location = new System.Drawing.Point(115, 53);
            this.Exit_FxWorth_LBL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Exit_FxWorth_LBL.Name = "Exit_FxWorth_LBL";
            this.Exit_FxWorth_LBL.Size = new System.Drawing.Size(291, 25);
            this.Exit_FxWorth_LBL.TabIndex = 2;
            this.Exit_FxWorth_LBL.Text = "You\'re hitting the Exit button, yeah?";
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
            this.panel1.Size = new System.Drawing.Size(458, 63);
            this.panel1.TabIndex = 3;
            // 
            // No_BTN
            // 
            this.No_BTN.BackColor = System.Drawing.SystemColors.ControlLight;
            this.No_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.No_BTN.DialogResult = System.Windows.Forms.DialogResult.No;
            this.No_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.No_BTN.FlatAppearance.BorderSize = 0;
            this.No_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.No_BTN.Location = new System.Drawing.Point(275, 15);
            this.No_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.No_BTN.Name = "No_BTN";
            this.No_BTN.Size = new System.Drawing.Size(120, 33);
            this.No_BTN.TabIndex = 25;
            this.No_BTN.Text = "  Nope";
            this.No_BTN.UseVisualStyleBackColor = false;
            // 
            // Yes_BTN
            // 
            this.Yes_BTN.BackColor = System.Drawing.SystemColors.ControlLight;
            this.Yes_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Yes_BTN.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.Yes_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Yes_BTN.FlatAppearance.BorderSize = 0;
            this.Yes_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Yes_BTN.Location = new System.Drawing.Point(75, 15);
            this.Yes_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Yes_BTN.Name = "Yes_BTN";
            this.Yes_BTN.Size = new System.Drawing.Size(120, 33);
            this.Yes_BTN.TabIndex = 20;
            this.Yes_BTN.Text = "  Yeah";
            this.Yes_BTN.UseVisualStyleBackColor = false;
            // 
            // Switch_Off_PictureBox
            // 
            this.Switch_Off_PictureBox.Image = ((System.Drawing.Image)(resources.GetObject("Switch_Off_PictureBox.Image")));
            this.Switch_Off_PictureBox.Location = new System.Drawing.Point(37, 31);
            this.Switch_Off_PictureBox.Margin = new System.Windows.Forms.Padding(4);
            this.Switch_Off_PictureBox.Name = "Switch_Off_PictureBox";
            this.Switch_Off_PictureBox.Size = new System.Drawing.Size(58, 58);
            this.Switch_Off_PictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.Switch_Off_PictureBox.TabIndex = 26;
            this.Switch_Off_PictureBox.TabStop = false;
            // 
            // Exit_Application
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(458, 184);
            this.Controls.Add(this.Switch_Off_PictureBox);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Exit_FxWorth_LBL);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Exit_Application";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Exiting FxWorth";
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.Switch_Off_PictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label Exit_FxWorth_LBL;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Yes_BTN;
        private System.Windows.Forms.Button No_BTN;
        private System.Windows.Forms.PictureBox Switch_Off_PictureBox;
    }
}