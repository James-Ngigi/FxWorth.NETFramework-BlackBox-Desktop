namespace FxWorth
{
    partial class Subscriber_Fetch
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Subscriber_Fetch));
            this.FxWorth_Subscriber_Fetching_Service_Tab = new System.Windows.Forms.TabControl();
            this.Paid_Client_Tab = new System.Windows.Forms.TabPage();
            this.dataGridView_Paid = new System.Windows.Forms.DataGridView();
            this.Api_Tokens_Paid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Profit_Targets_Paid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Clear_n_Fetch_Paid_BTN = new System.Windows.Forms.Button();
            this.Extract_Paid_BTN = new System.Windows.Forms.Button();
            this.Trial_Client_Tab = new System.Windows.Forms.TabPage();
            this.dataGridView_Trial = new System.Windows.Forms.DataGridView();
            this.Api_Tokens_Trial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Profit_Targets_Trial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel2 = new System.Windows.Forms.Panel();
            this.Clear_n_Fetch_Trial_BTN = new System.Windows.Forms.Button();
            this.Extract_Trial_BTN = new System.Windows.Forms.Button();
            this.OTH_CR_Client_Tab = new System.Windows.Forms.TabPage();
            this.dataGridView_OTH_Real = new System.Windows.Forms.DataGridView();
            this.OTH_CR_Api_Tokens = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OTH_CR_Profit_Targets = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel3 = new System.Windows.Forms.Panel();
            this.Clear_n_Fetch_OTH_Real_BTN = new System.Windows.Forms.Button();
            this.Extract_OTH_Real_BTN = new System.Windows.Forms.Button();
            this.OTH_VR_Client_Tab = new System.Windows.Forms.TabPage();
            this.dataGridView_OTH_Virtual = new System.Windows.Forms.DataGridView();
            this.OTH_VR_Api_Tokens = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OTH_VR_Profit_Targets = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel4 = new System.Windows.Forms.Panel();
            this.Clear_n_Fetch_OTH_Virtual_BTN = new System.Windows.Forms.Button();
            this.Extract_OTH_Virtual_BTN = new System.Windows.Forms.Button();
            this.FxWorth_Subscriber_Fetching_Service_Tab.SuspendLayout();
            this.Paid_Client_Tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Paid)).BeginInit();
            this.panel1.SuspendLayout();
            this.Trial_Client_Tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Trial)).BeginInit();
            this.panel2.SuspendLayout();
            this.OTH_CR_Client_Tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_OTH_Real)).BeginInit();
            this.panel3.SuspendLayout();
            this.OTH_VR_Client_Tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_OTH_Virtual)).BeginInit();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // FxWorth_Subscriber_Fetching_Service_Tab
            // 
            this.FxWorth_Subscriber_Fetching_Service_Tab.Controls.Add(this.Paid_Client_Tab);
            this.FxWorth_Subscriber_Fetching_Service_Tab.Controls.Add(this.Trial_Client_Tab);
            this.FxWorth_Subscriber_Fetching_Service_Tab.Controls.Add(this.OTH_CR_Client_Tab);
            this.FxWorth_Subscriber_Fetching_Service_Tab.Controls.Add(this.OTH_VR_Client_Tab);
            this.FxWorth_Subscriber_Fetching_Service_Tab.Location = new System.Drawing.Point(10, 9);
            this.FxWorth_Subscriber_Fetching_Service_Tab.Name = "FxWorth_Subscriber_Fetching_Service_Tab";
            this.FxWorth_Subscriber_Fetching_Service_Tab.SelectedIndex = 0;
            this.FxWorth_Subscriber_Fetching_Service_Tab.Size = new System.Drawing.Size(850, 508);
            this.FxWorth_Subscriber_Fetching_Service_Tab.TabIndex = 0;
            // 
            // Paid_Client_Tab
            // 
            this.Paid_Client_Tab.Controls.Add(this.dataGridView_Paid);
            this.Paid_Client_Tab.Controls.Add(this.panel1);
            this.Paid_Client_Tab.Location = new System.Drawing.Point(4, 34);
            this.Paid_Client_Tab.Name = "Paid_Client_Tab";
            this.Paid_Client_Tab.Padding = new System.Windows.Forms.Padding(3);
            this.Paid_Client_Tab.Size = new System.Drawing.Size(842, 470);
            this.Paid_Client_Tab.TabIndex = 0;
            this.Paid_Client_Tab.Text = "Paid Subscribers";
            this.Paid_Client_Tab.UseVisualStyleBackColor = true;
            // 
            // dataGridView_Paid
            // 
            this.dataGridView_Paid.AllowUserToOrderColumns = true;
            this.dataGridView_Paid.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView_Paid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridView_Paid.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Raised;
            this.dataGridView_Paid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_Paid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Api_Tokens_Paid,
            this.Profit_Targets_Paid});
            this.dataGridView_Paid.GridColor = System.Drawing.SystemColors.Window;
            this.dataGridView_Paid.Location = new System.Drawing.Point(26, 25);
            this.dataGridView_Paid.Name = "dataGridView_Paid";
            this.dataGridView_Paid.RowHeadersWidth = 62;
            this.dataGridView_Paid.RowTemplate.Height = 28;
            this.dataGridView_Paid.Size = new System.Drawing.Size(791, 377);
            this.dataGridView_Paid.TabIndex = 23;
            // 
            // Api_Tokens_Paid
            // 
            this.Api_Tokens_Paid.HeaderText = "Paid Subscriber Api Tokens";
            this.Api_Tokens_Paid.MinimumWidth = 8;
            this.Api_Tokens_Paid.Name = "Api_Tokens_Paid";
            this.Api_Tokens_Paid.ReadOnly = true;
            this.Api_Tokens_Paid.Width = 365;
            // 
            // Profit_Targets_Paid
            // 
            this.Profit_Targets_Paid.HeaderText = "Profit Targets Assigned";
            this.Profit_Targets_Paid.MinimumWidth = 8;
            this.Profit_Targets_Paid.Name = "Profit_Targets_Paid";
            this.Profit_Targets_Paid.Width = 362;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.GhostWhite;
            this.panel1.Controls.Add(this.Clear_n_Fetch_Paid_BTN);
            this.panel1.Controls.Add(this.Extract_Paid_BTN);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(3, 407);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(836, 60);
            this.panel1.TabIndex = 22;
            // 
            // Clear_n_Fetch_Paid_BTN
            // 
            this.Clear_n_Fetch_Paid_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Clear_n_Fetch_Paid_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Clear_n_Fetch_Paid_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Clear_n_Fetch_Paid_BTN.FlatAppearance.BorderSize = 0;
            this.Clear_n_Fetch_Paid_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Clear_n_Fetch_Paid_BTN.Location = new System.Drawing.Point(135, 14);
            this.Clear_n_Fetch_Paid_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Clear_n_Fetch_Paid_BTN.Name = "Clear_n_Fetch_Paid_BTN";
            this.Clear_n_Fetch_Paid_BTN.Size = new System.Drawing.Size(185, 32);
            this.Clear_n_Fetch_Paid_BTN.TabIndex = 3;
            this.Clear_n_Fetch_Paid_BTN.Text = "Clear n Fetch";
            this.Clear_n_Fetch_Paid_BTN.UseVisualStyleBackColor = false;
            // 
            // Extract_Paid_BTN
            // 
            this.Extract_Paid_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Extract_Paid_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Extract_Paid_BTN.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Extract_Paid_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Extract_Paid_BTN.FlatAppearance.BorderSize = 0;
            this.Extract_Paid_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Extract_Paid_BTN.Location = new System.Drawing.Point(507, 14);
            this.Extract_Paid_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Extract_Paid_BTN.Name = "Extract_Paid_BTN";
            this.Extract_Paid_BTN.Size = new System.Drawing.Size(185, 32);
            this.Extract_Paid_BTN.TabIndex = 4;
            this.Extract_Paid_BTN.Text = "Extract";
            this.Extract_Paid_BTN.UseVisualStyleBackColor = false;
            // 
            // Trial_Client_Tab
            // 
            this.Trial_Client_Tab.Controls.Add(this.dataGridView_Trial);
            this.Trial_Client_Tab.Controls.Add(this.panel2);
            this.Trial_Client_Tab.Location = new System.Drawing.Point(4, 34);
            this.Trial_Client_Tab.Name = "Trial_Client_Tab";
            this.Trial_Client_Tab.Padding = new System.Windows.Forms.Padding(3);
            this.Trial_Client_Tab.Size = new System.Drawing.Size(842, 470);
            this.Trial_Client_Tab.TabIndex = 1;
            this.Trial_Client_Tab.Text = "Trial Subscribers";
            this.Trial_Client_Tab.UseVisualStyleBackColor = true;
            // 
            // dataGridView_Trial
            // 
            this.dataGridView_Trial.AllowUserToOrderColumns = true;
            this.dataGridView_Trial.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView_Trial.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridView_Trial.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Raised;
            this.dataGridView_Trial.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_Trial.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Api_Tokens_Trial,
            this.Profit_Targets_Trial});
            this.dataGridView_Trial.GridColor = System.Drawing.SystemColors.Window;
            this.dataGridView_Trial.Location = new System.Drawing.Point(26, 25);
            this.dataGridView_Trial.Name = "dataGridView_Trial";
            this.dataGridView_Trial.RowHeadersWidth = 62;
            this.dataGridView_Trial.RowTemplate.Height = 28;
            this.dataGridView_Trial.Size = new System.Drawing.Size(791, 377);
            this.dataGridView_Trial.TabIndex = 24;
            // 
            // Api_Tokens_Trial
            // 
            this.Api_Tokens_Trial.HeaderText = "Trial Subscriber Api Tokens";
            this.Api_Tokens_Trial.MinimumWidth = 8;
            this.Api_Tokens_Trial.Name = "Api_Tokens_Trial";
            this.Api_Tokens_Trial.ReadOnly = true;
            this.Api_Tokens_Trial.Width = 365;
            // 
            // Profit_Targets_Trial
            // 
            this.Profit_Targets_Trial.HeaderText = "Profit Targets Assigned";
            this.Profit_Targets_Trial.MinimumWidth = 8;
            this.Profit_Targets_Trial.Name = "Profit_Targets_Trial";
            this.Profit_Targets_Trial.Width = 362;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.GhostWhite;
            this.panel2.Controls.Add(this.Clear_n_Fetch_Trial_BTN);
            this.panel2.Controls.Add(this.Extract_Trial_BTN);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(3, 407);
            this.panel2.Margin = new System.Windows.Forms.Padding(4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(836, 60);
            this.panel2.TabIndex = 25;
            // 
            // Clear_n_Fetch_Trial_BTN
            // 
            this.Clear_n_Fetch_Trial_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Clear_n_Fetch_Trial_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Clear_n_Fetch_Trial_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Clear_n_Fetch_Trial_BTN.FlatAppearance.BorderSize = 0;
            this.Clear_n_Fetch_Trial_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Clear_n_Fetch_Trial_BTN.Location = new System.Drawing.Point(135, 12);
            this.Clear_n_Fetch_Trial_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Clear_n_Fetch_Trial_BTN.Name = "Clear_n_Fetch_Trial_BTN";
            this.Clear_n_Fetch_Trial_BTN.Size = new System.Drawing.Size(185, 32);
            this.Clear_n_Fetch_Trial_BTN.TabIndex = 3;
            this.Clear_n_Fetch_Trial_BTN.Text = "Clear n Fetch";
            this.Clear_n_Fetch_Trial_BTN.UseVisualStyleBackColor = false;
            // 
            // Extract_Trial_BTN
            // 
            this.Extract_Trial_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Extract_Trial_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Extract_Trial_BTN.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Extract_Trial_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Extract_Trial_BTN.FlatAppearance.BorderSize = 0;
            this.Extract_Trial_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Extract_Trial_BTN.Location = new System.Drawing.Point(507, 12);
            this.Extract_Trial_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Extract_Trial_BTN.Name = "Extract_Trial_BTN";
            this.Extract_Trial_BTN.Size = new System.Drawing.Size(185, 32);
            this.Extract_Trial_BTN.TabIndex = 4;
            this.Extract_Trial_BTN.Text = "Extract";
            this.Extract_Trial_BTN.UseVisualStyleBackColor = false;
            // 
            // OTH_CR_Client_Tab
            // 
            this.OTH_CR_Client_Tab.Controls.Add(this.dataGridView_OTH_Real);
            this.OTH_CR_Client_Tab.Controls.Add(this.panel3);
            this.OTH_CR_Client_Tab.Location = new System.Drawing.Point(4, 34);
            this.OTH_CR_Client_Tab.Name = "OTH_CR_Client_Tab";
            this.OTH_CR_Client_Tab.Padding = new System.Windows.Forms.Padding(3);
            this.OTH_CR_Client_Tab.Size = new System.Drawing.Size(842, 470);
            this.OTH_CR_Client_Tab.TabIndex = 2;
            this.OTH_CR_Client_Tab.Text = "CRxx On the House";
            this.OTH_CR_Client_Tab.UseVisualStyleBackColor = true;
            // 
            // dataGridView_OTH_Real
            // 
            this.dataGridView_OTH_Real.AllowUserToOrderColumns = true;
            this.dataGridView_OTH_Real.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView_OTH_Real.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridView_OTH_Real.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Raised;
            this.dataGridView_OTH_Real.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_OTH_Real.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.OTH_CR_Api_Tokens,
            this.OTH_CR_Profit_Targets});
            this.dataGridView_OTH_Real.GridColor = System.Drawing.SystemColors.Window;
            this.dataGridView_OTH_Real.Location = new System.Drawing.Point(26, 25);
            this.dataGridView_OTH_Real.Name = "dataGridView_OTH_Real";
            this.dataGridView_OTH_Real.RowHeadersWidth = 62;
            this.dataGridView_OTH_Real.RowTemplate.Height = 28;
            this.dataGridView_OTH_Real.Size = new System.Drawing.Size(791, 377);
            this.dataGridView_OTH_Real.TabIndex = 24;
            // 
            // OTH_CR_Api_Tokens
            // 
            this.OTH_CR_Api_Tokens.HeaderText = "O.T.H Real Acc Subscriber Api Tokens";
            this.OTH_CR_Api_Tokens.MinimumWidth = 8;
            this.OTH_CR_Api_Tokens.Name = "OTH_CR_Api_Tokens";
            this.OTH_CR_Api_Tokens.ReadOnly = true;
            this.OTH_CR_Api_Tokens.Width = 365;
            // 
            // OTH_CR_Profit_Targets
            // 
            this.OTH_CR_Profit_Targets.HeaderText = "Profit Targets Assigned";
            this.OTH_CR_Profit_Targets.MinimumWidth = 8;
            this.OTH_CR_Profit_Targets.Name = "OTH_CR_Profit_Targets";
            this.OTH_CR_Profit_Targets.Width = 362;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.GhostWhite;
            this.panel3.Controls.Add(this.Clear_n_Fetch_OTH_Real_BTN);
            this.panel3.Controls.Add(this.Extract_OTH_Real_BTN);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel3.Location = new System.Drawing.Point(3, 407);
            this.panel3.Margin = new System.Windows.Forms.Padding(4);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(836, 60);
            this.panel3.TabIndex = 23;
            // 
            // Clear_n_Fetch_OTH_Real_BTN
            // 
            this.Clear_n_Fetch_OTH_Real_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Clear_n_Fetch_OTH_Real_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Clear_n_Fetch_OTH_Real_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Clear_n_Fetch_OTH_Real_BTN.FlatAppearance.BorderSize = 0;
            this.Clear_n_Fetch_OTH_Real_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Clear_n_Fetch_OTH_Real_BTN.Location = new System.Drawing.Point(135, 12);
            this.Clear_n_Fetch_OTH_Real_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Clear_n_Fetch_OTH_Real_BTN.Name = "Clear_n_Fetch_OTH_Real_BTN";
            this.Clear_n_Fetch_OTH_Real_BTN.Size = new System.Drawing.Size(185, 32);
            this.Clear_n_Fetch_OTH_Real_BTN.TabIndex = 3;
            this.Clear_n_Fetch_OTH_Real_BTN.Text = "Clear n Fetch";
            this.Clear_n_Fetch_OTH_Real_BTN.UseVisualStyleBackColor = false;
            // 
            // Extract_OTH_Real_BTN
            // 
            this.Extract_OTH_Real_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Extract_OTH_Real_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Extract_OTH_Real_BTN.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Extract_OTH_Real_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Extract_OTH_Real_BTN.FlatAppearance.BorderSize = 0;
            this.Extract_OTH_Real_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Extract_OTH_Real_BTN.Location = new System.Drawing.Point(507, 12);
            this.Extract_OTH_Real_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Extract_OTH_Real_BTN.Name = "Extract_OTH_Real_BTN";
            this.Extract_OTH_Real_BTN.Size = new System.Drawing.Size(185, 32);
            this.Extract_OTH_Real_BTN.TabIndex = 4;
            this.Extract_OTH_Real_BTN.Text = "Extract";
            this.Extract_OTH_Real_BTN.UseVisualStyleBackColor = false;
            // 
            // OTH_VR_Client_Tab
            // 
            this.OTH_VR_Client_Tab.Controls.Add(this.dataGridView_OTH_Virtual);
            this.OTH_VR_Client_Tab.Controls.Add(this.panel4);
            this.OTH_VR_Client_Tab.Location = new System.Drawing.Point(4, 34);
            this.OTH_VR_Client_Tab.Name = "OTH_VR_Client_Tab";
            this.OTH_VR_Client_Tab.Padding = new System.Windows.Forms.Padding(3);
            this.OTH_VR_Client_Tab.Size = new System.Drawing.Size(842, 470);
            this.OTH_VR_Client_Tab.TabIndex = 3;
            this.OTH_VR_Client_Tab.Text = "VRxx On the House";
            this.OTH_VR_Client_Tab.UseVisualStyleBackColor = true;
            // 
            // dataGridView_OTH_Virtual
            // 
            this.dataGridView_OTH_Virtual.AllowUserToOrderColumns = true;
            this.dataGridView_OTH_Virtual.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView_OTH_Virtual.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridView_OTH_Virtual.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Raised;
            this.dataGridView_OTH_Virtual.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_OTH_Virtual.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.OTH_VR_Api_Tokens,
            this.OTH_VR_Profit_Targets});
            this.dataGridView_OTH_Virtual.GridColor = System.Drawing.SystemColors.Window;
            this.dataGridView_OTH_Virtual.Location = new System.Drawing.Point(26, 25);
            this.dataGridView_OTH_Virtual.Name = "dataGridView_OTH_Virtual";
            this.dataGridView_OTH_Virtual.RowHeadersWidth = 62;
            this.dataGridView_OTH_Virtual.RowTemplate.Height = 28;
            this.dataGridView_OTH_Virtual.Size = new System.Drawing.Size(791, 377);
            this.dataGridView_OTH_Virtual.TabIndex = 24;
            // 
            // OTH_VR_Api_Tokens
            // 
            this.OTH_VR_Api_Tokens.HeaderText = "O.T.H Virtual Acc Subscriber Api Tokens";
            this.OTH_VR_Api_Tokens.MinimumWidth = 8;
            this.OTH_VR_Api_Tokens.Name = "OTH_VR_Api_Tokens";
            this.OTH_VR_Api_Tokens.ReadOnly = true;
            this.OTH_VR_Api_Tokens.Width = 365;
            // 
            // OTH_VR_Profit_Targets
            // 
            this.OTH_VR_Profit_Targets.HeaderText = "Profit Targets Assigned";
            this.OTH_VR_Profit_Targets.MinimumWidth = 8;
            this.OTH_VR_Profit_Targets.Name = "OTH_VR_Profit_Targets";
            this.OTH_VR_Profit_Targets.Width = 362;
            // 
            // panel4
            // 
            this.panel4.BackColor = System.Drawing.Color.GhostWhite;
            this.panel4.Controls.Add(this.Clear_n_Fetch_OTH_Virtual_BTN);
            this.panel4.Controls.Add(this.Extract_OTH_Virtual_BTN);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel4.Location = new System.Drawing.Point(3, 407);
            this.panel4.Margin = new System.Windows.Forms.Padding(4);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(836, 60);
            this.panel4.TabIndex = 23;
            // 
            // Clear_n_Fetch_OTH_Virtual_BTN
            // 
            this.Clear_n_Fetch_OTH_Virtual_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Clear_n_Fetch_OTH_Virtual_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Clear_n_Fetch_OTH_Virtual_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Clear_n_Fetch_OTH_Virtual_BTN.FlatAppearance.BorderSize = 0;
            this.Clear_n_Fetch_OTH_Virtual_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Clear_n_Fetch_OTH_Virtual_BTN.Location = new System.Drawing.Point(135, 12);
            this.Clear_n_Fetch_OTH_Virtual_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Clear_n_Fetch_OTH_Virtual_BTN.Name = "Clear_n_Fetch_OTH_Virtual_BTN";
            this.Clear_n_Fetch_OTH_Virtual_BTN.Size = new System.Drawing.Size(185, 32);
            this.Clear_n_Fetch_OTH_Virtual_BTN.TabIndex = 3;
            this.Clear_n_Fetch_OTH_Virtual_BTN.Text = "Clear n Fetch";
            this.Clear_n_Fetch_OTH_Virtual_BTN.UseVisualStyleBackColor = false;
            // 
            // Extract_OTH_Virtual_BTN
            // 
            this.Extract_OTH_Virtual_BTN.BackColor = System.Drawing.SystemColors.MenuBar;
            this.Extract_OTH_Virtual_BTN.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Extract_OTH_Virtual_BTN.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Extract_OTH_Virtual_BTN.FlatAppearance.BorderColor = System.Drawing.SystemColors.InactiveBorder;
            this.Extract_OTH_Virtual_BTN.FlatAppearance.BorderSize = 0;
            this.Extract_OTH_Virtual_BTN.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Extract_OTH_Virtual_BTN.Location = new System.Drawing.Point(507, 12);
            this.Extract_OTH_Virtual_BTN.Margin = new System.Windows.Forms.Padding(4);
            this.Extract_OTH_Virtual_BTN.Name = "Extract_OTH_Virtual_BTN";
            this.Extract_OTH_Virtual_BTN.Size = new System.Drawing.Size(185, 32);
            this.Extract_OTH_Virtual_BTN.TabIndex = 4;
            this.Extract_OTH_Virtual_BTN.Text = "Extract";
            this.Extract_OTH_Virtual_BTN.UseVisualStyleBackColor = false;
            // 
            // Subscriber_Fetch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(870, 527);
            this.Controls.Add(this.FxWorth_Subscriber_Fetching_Service_Tab);
            this.Font = new System.Drawing.Font("Ebrima", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Subscriber_Fetch";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Subscriber Fetch";
            this.FxWorth_Subscriber_Fetching_Service_Tab.ResumeLayout(false);
            this.Paid_Client_Tab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Paid)).EndInit();
            this.panel1.ResumeLayout(false);
            this.Trial_Client_Tab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Trial)).EndInit();
            this.panel2.ResumeLayout(false);
            this.OTH_CR_Client_Tab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_OTH_Real)).EndInit();
            this.panel3.ResumeLayout(false);
            this.OTH_VR_Client_Tab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_OTH_Virtual)).EndInit();
            this.panel4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl FxWorth_Subscriber_Fetching_Service_Tab;
        private System.Windows.Forms.TabPage Paid_Client_Tab;
        private System.Windows.Forms.TabPage Trial_Client_Tab;
        private System.Windows.Forms.TabPage OTH_CR_Client_Tab;
        private System.Windows.Forms.TabPage OTH_VR_Client_Tab;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button Clear_n_Fetch_Paid_BTN;
        private System.Windows.Forms.Button Extract_Paid_BTN;
        private System.Windows.Forms.DataGridView dataGridView_Paid;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button Clear_n_Fetch_Trial_BTN;
        private System.Windows.Forms.Button Extract_Trial_BTN;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button Clear_n_Fetch_OTH_Real_BTN;
        private System.Windows.Forms.Button Extract_OTH_Real_BTN;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button Clear_n_Fetch_OTH_Virtual_BTN;
        private System.Windows.Forms.Button Extract_OTH_Virtual_BTN;
        private System.Windows.Forms.DataGridView dataGridView_Trial;
        private System.Windows.Forms.DataGridView dataGridView_OTH_Real;
        private System.Windows.Forms.DataGridView dataGridView_OTH_Virtual;
        private System.Windows.Forms.DataGridViewTextBoxColumn Api_Tokens_Paid;
        private System.Windows.Forms.DataGridViewTextBoxColumn Profit_Targets_Paid;
        private System.Windows.Forms.DataGridViewTextBoxColumn Api_Tokens_Trial;
        private System.Windows.Forms.DataGridViewTextBoxColumn Profit_Targets_Trial;
        private System.Windows.Forms.DataGridViewTextBoxColumn OTH_CR_Api_Tokens;
        private System.Windows.Forms.DataGridViewTextBoxColumn OTH_CR_Profit_Targets;
        private System.Windows.Forms.DataGridViewTextBoxColumn OTH_VR_Api_Tokens;
        private System.Windows.Forms.DataGridViewTextBoxColumn OTH_VR_Profit_Targets;
    }
}