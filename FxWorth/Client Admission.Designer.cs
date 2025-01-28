namespace FxWorth
{
    partial class Adding_New_Token
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Adding_New_Token));
            this.Insert_Token_LBL = new System.Windows.Forms.Label();
            this.Insert_App_ID_LBL = new System.Windows.Forms.Label();
            this.tokenTextBox = new System.Windows.Forms.TextBox();
            this.Cut_Copy_Paste_CMS = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.Paste_TSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.Copy_TSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.Cut_TSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.appTextBox = new System.Windows.Forms.TextBox();
            this.Add_BTN = new System.Windows.Forms.Button();
            this.Cancel_BTN = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Cut_Copy_Paste_CMS.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // Insert_Token_LBL
            // 
            this.Insert_Token_LBL.AutoSize = true;
            this.Insert_Token_LBL.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.Insert_Token_LBL.Location = new System.Drawing.Point(33, 40);
            this.Insert_Token_LBL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Insert_Token_LBL.Name = "Insert_Token_LBL";
            this.Insert_Token_LBL.Size = new System.Drawing.Size(203, 21);
            this.Insert_Token_LBL.TabIndex = 0;
            this.Insert_Token_LBL.Text = "Insert Account Token   -";
            // 
            // Insert_App_ID_LBL
            // 
            this.Insert_App_ID_LBL.AutoSize = true;
            this.Insert_App_ID_LBL.Location = new System.Drawing.Point(33, 100);
            this.Insert_App_ID_LBL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Insert_App_ID_LBL.Name = "Insert_App_ID_LBL";
            this.Insert_App_ID_LBL.Size = new System.Drawing.Size(205, 21);
            this.Insert_App_ID_LBL.TabIndex = 0;
            this.Insert_App_ID_LBL.Text = "Insert Account ID          -";
            // 
            // tokenTextBox
            // 
            this.tokenTextBox.ContextMenuStrip = this.Cut_Copy_Paste_CMS;
            this.tokenTextBox.Location = new System.Drawing.Point(262, 37);
            this.tokenTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.tokenTextBox.Name = "tokenTextBox";
            this.tokenTextBox.Size = new System.Drawing.Size(260, 28);
            this.tokenTextBox.TabIndex = 1;
            // 
            // Cut_Copy_Paste_CMS
            // 
            this.Cut_Copy_Paste_CMS.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.Cut_Copy_Paste_CMS.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Paste_TSMI,
            this.Copy_TSMI,
            this.Cut_TSMI});
            this.Cut_Copy_Paste_CMS.Name = "Cut_Copy_Paste_CMS";
            this.Cut_Copy_Paste_CMS.Size = new System.Drawing.Size(135, 100);
            // 
            // Paste_TSMI
            // 
            this.Paste_TSMI.BackColor = System.Drawing.SystemColors.Window;
            this.Paste_TSMI.Font = new System.Drawing.Font("Ebrima", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Paste_TSMI.Image = ((System.Drawing.Image)(resources.GetObject("Paste_TSMI.Image")));
            this.Paste_TSMI.Name = "Paste_TSMI";
            this.Paste_TSMI.Size = new System.Drawing.Size(134, 32);
            this.Paste_TSMI.Text = "Paste";
            this.Paste_TSMI.Click += new System.EventHandler(this.Paste_TSMI_Click);
            // 
            // Copy_TSMI
            // 
            this.Copy_TSMI.BackColor = System.Drawing.SystemColors.Window;
            this.Copy_TSMI.Font = new System.Drawing.Font("Ebrima", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Copy_TSMI.Image = ((System.Drawing.Image)(resources.GetObject("Copy_TSMI.Image")));
            this.Copy_TSMI.Name = "Copy_TSMI";
            this.Copy_TSMI.Size = new System.Drawing.Size(134, 32);
            this.Copy_TSMI.Text = "Copy";
            this.Copy_TSMI.Click += new System.EventHandler(this.Copy_TSMI_Click);
            // 
            // Cut_TSMI
            // 
            this.Cut_TSMI.BackColor = System.Drawing.SystemColors.Window;
            this.Cut_TSMI.Font = new System.Drawing.Font("Ebrima", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Cut_TSMI.Image = ((System.Drawing.Image)(resources.GetObject("Cut_TSMI.Image")));
            this.Cut_TSMI.Name = "Cut_TSMI";
            this.Cut_TSMI.Size = new System.Drawing.Size(134, 32);
            this.Cut_TSMI.Text = "Cut";
            this.Cut_TSMI.Click += new System.EventHandler(this.Cut_TSMI_Click);
            // 
            // appTextBox
            // 
            this.appTextBox.ContextMenuStrip = this.Cut_Copy_Paste_CMS;
            this.appTextBox.Location = new System.Drawing.Point(262, 97);
            this.appTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.appTextBox.Name = "appTextBox";
            this.appTextBox.Size = new System.Drawing.Size(260, 28);
            this.appTextBox.TabIndex = 2;
            // 
            // Add_BTN
            // 
            this.Add_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Add_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Add_BTN.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Add_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Add_BTN.FlatAppearance.BorderSize = 0;
            this.Add_BTN.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.Add_BTN.Location = new System.Drawing.Point(265, 12);
            this.Add_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Add_BTN.Name = "Add_BTN";
            this.Add_BTN.Size = new System.Drawing.Size(115, 32);
            this.Add_BTN.TabIndex = 3;
            this.Add_BTN.Text = "+ Add";
            this.Add_BTN.UseVisualStyleBackColor = false;
            // 
            // Cancel_BTN
            // 
            this.Cancel_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Cancel_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Cancel_BTN.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Cancel_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Cancel_BTN.FlatAppearance.BorderSize = 0;
            this.Cancel_BTN.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.Cancel_BTN.Location = new System.Drawing.Point(405, 12);
            this.Cancel_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Cancel_BTN.Name = "Cancel_BTN";
            this.Cancel_BTN.Size = new System.Drawing.Size(115, 32);
            this.Cancel_BTN.TabIndex = 4;
            this.Cancel_BTN.Text = "Cancel";
            this.Cancel_BTN.UseVisualStyleBackColor = false;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.Add_BTN);
            this.panel1.Controls.Add(this.Cancel_BTN);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 147);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(563, 60);
            this.panel1.TabIndex = 21;
            // 
            // Adding_New_Token
            // 
            this.AcceptButton = this.Add_BTN;
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.Cancel_BTN;
            this.ClientSize = new System.Drawing.Size(563, 207);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.appTextBox);
            this.Controls.Add(this.tokenTextBox);
            this.Controls.Add(this.Insert_App_ID_LBL);
            this.Controls.Add(this.Insert_Token_LBL);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Eras Medium ITC", 9F);
            this.HelpButton = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Adding_New_Token";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Client Admission";
            this.Cut_Copy_Paste_CMS.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label Insert_Token_LBL;
        private System.Windows.Forms.Label Insert_App_ID_LBL;
        public System.Windows.Forms.TextBox tokenTextBox;
        public System.Windows.Forms.TextBox appTextBox;
        private System.Windows.Forms.Button Add_BTN;
        private System.Windows.Forms.Button Cancel_BTN;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ContextMenuStrip Cut_Copy_Paste_CMS;
        private System.Windows.Forms.ToolStripMenuItem Paste_TSMI;
        private System.Windows.Forms.ToolStripMenuItem Copy_TSMI;
        private System.Windows.Forms.ToolStripMenuItem Cut_TSMI;
    }
}