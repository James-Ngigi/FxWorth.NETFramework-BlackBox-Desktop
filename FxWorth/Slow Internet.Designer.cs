namespace FxWorth
{
    partial class No_Internet
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(No_Internet));
            this.Internet_Connection_LBL = new System.Windows.Forms.Label();
            this.Cloud_Update_PictureBox = new System.Windows.Forms.PictureBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Retry_BTN = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.Cloud_Update_PictureBox)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // Internet_Connection_LBL
            // 
            resources.ApplyResources(this.Internet_Connection_LBL, "Internet_Connection_LBL");
            this.Internet_Connection_LBL.Name = "Internet_Connection_LBL";
            // 
            // Cloud_Update_PictureBox
            // 
            resources.ApplyResources(this.Cloud_Update_PictureBox, "Cloud_Update_PictureBox");
            this.Cloud_Update_PictureBox.Name = "Cloud_Update_PictureBox";
            this.Cloud_Update_PictureBox.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.Retry_BTN);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // Retry_BTN
            // 
            this.Retry_BTN.BackColor = System.Drawing.SystemColors.Control;
            this.Retry_BTN.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            this.Retry_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.Retry_BTN.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.GradientInactiveCaption;
            resources.ApplyResources(this.Retry_BTN, "Retry_BTN");
            this.Retry_BTN.Name = "Retry_BTN";
            this.Retry_BTN.UseVisualStyleBackColor = false;
            this.Retry_BTN.UseWaitCursor = true;
            // 
            // No_Internet
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Cloud_Update_PictureBox);
            this.Controls.Add(this.Internet_Connection_LBL);
            this.DoubleBuffered = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "No_Internet";
            this.Opacity = 0.95D;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.Cloud_Update_PictureBox)).EndInit();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label Internet_Connection_LBL;
        private System.Windows.Forms.PictureBox Cloud_Update_PictureBox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Retry_BTN;
    }
}