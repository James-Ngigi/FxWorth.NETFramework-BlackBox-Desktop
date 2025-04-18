namespace FxWorth
{
    partial class Admin_Authorization
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Admin_Authorization));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.Email_LBL = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Login_BTN = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(202, 52);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(140, 134);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // Email_LBL
            // 
            this.Email_LBL.AutoSize = true;
            this.Email_LBL.Location = new System.Drawing.Point(33, 254);
            this.Email_LBL.Name = "Email_LBL";
            this.Email_LBL.Size = new System.Drawing.Size(168, 25);
            this.Email_LBL.TabIndex = 19;
            this.Email_LBL.Text = "Administartor Email";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(33, 319);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(146, 25);
            this.label1.TabIndex = 20;
            this.label1.Text = "Admin Password";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(234, 250);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(278, 32);
            this.textBox1.TabIndex = 21;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(234, 315);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(278, 32);
            this.textBox2.TabIndex = 22;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.Login_BTN);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 408);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(545, 83);
            this.panel1.TabIndex = 23;
            // 
            // Login_BTN
            // 
            this.Login_BTN.BackColor = System.Drawing.SystemColors.ControlLight;
            this.Login_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Login_BTN.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Login_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Login_BTN.FlatAppearance.BorderSize = 0;
            this.Login_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Login_BTN.Location = new System.Drawing.Point(197, 25);
            this.Login_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Login_BTN.Name = "Login_BTN";
            this.Login_BTN.Size = new System.Drawing.Size(150, 35);
            this.Login_BTN.TabIndex = 20;
            this.Login_BTN.Text = "Login";
            this.Login_BTN.UseVisualStyleBackColor = false;
            // 
            // Admin_Authorization
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(545, 491);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.Email_LBL);
            this.Controls.Add(this.pictureBox1);
            this.Font = new System.Drawing.Font("Ebrima", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Admin_Authorization";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Admin Login";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label Email_LBL;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Login_BTN;
    }
}