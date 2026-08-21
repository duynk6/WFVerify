namespace SampleApp;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.MenuStrip menuStrip;
    private System.Windows.Forms.ToolStripMenuItem menuFile;
    private System.Windows.Forms.ToolStripMenuItem menuOrders;
    private System.Windows.Forms.ToolStripMenuItem menuExit;
    private System.Windows.Forms.ToolStripMenuItem menuHelp;
    private System.Windows.Forms.ToolStripMenuItem menuAbout;
    private System.Windows.Forms.TabControl tabControl;
    private System.Windows.Forms.TabPage tabOrders;
    private System.Windows.Forms.TabPage tabSlow;
    private System.Windows.Forms.TabPage tabAbout;
    private System.Windows.Forms.DataGridView dgOrders;
    private System.Windows.Forms.DataGridViewTextBoxColumn colId;
    private System.Windows.Forms.DataGridViewTextBoxColumn colCustomer;
    private System.Windows.Forms.DataGridViewTextBoxColumn colDate;
    private System.Windows.Forms.DataGridViewTextBoxColumn colTotal;
    private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
    private System.Windows.Forms.DataGridViewTextBoxColumn colNotes;
    private System.Windows.Forms.ComboBox cboStatus;
    private System.Windows.Forms.Button btnFilter;
    private System.Windows.Forms.Label lblFilterResult;
    private System.Windows.Forms.Button btnSlowTask;
    private System.Windows.Forms.Label lblSlowStatus;
    private System.Windows.Forms.Label lblAboutText;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.menuStrip = new System.Windows.Forms.MenuStrip();
        this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
        this.menuOrders = new System.Windows.Forms.ToolStripMenuItem();
        this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
        this.menuHelp = new System.Windows.Forms.ToolStripMenuItem();
        this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
        this.tabControl = new System.Windows.Forms.TabControl();
        this.tabOrders = new System.Windows.Forms.TabPage();
        this.dgOrders = new System.Windows.Forms.DataGridView();
        this.colId = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colCustomer = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colTotal = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colNotes = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.cboStatus = new System.Windows.Forms.ComboBox();
        this.btnFilter = new System.Windows.Forms.Button();
        this.lblFilterResult = new System.Windows.Forms.Label();
        this.tabSlow = new System.Windows.Forms.TabPage();
        this.btnSlowTask = new System.Windows.Forms.Button();
        this.lblSlowStatus = new System.Windows.Forms.Label();
        this.tabAbout = new System.Windows.Forms.TabPage();
        this.lblAboutText = new System.Windows.Forms.Label();
        this.menuStrip.SuspendLayout();
        this.tabControl.SuspendLayout();
        this.tabOrders.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.dgOrders)).BeginInit();
        this.tabSlow.SuspendLayout();
        this.tabAbout.SuspendLayout();
        this.SuspendLayout();

        // menuStrip
        this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFile,
            this.menuHelp});
        this.menuStrip.Location = new System.Drawing.Point(0, 0);
        this.menuStrip.Name = "menuStrip";
        this.menuStrip.Size = new System.Drawing.Size(900, 24);
        this.menuStrip.TabIndex = 0;
        this.menuStrip.Text = "menuStrip";

        // menuFile
        this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuOrders,
            this.menuExit});
        this.menuFile.Name = "menuFile";
        this.menuFile.Size = new System.Drawing.Size(37, 20);
        this.menuFile.Text = "File";

        // menuOrders
        this.menuOrders.Name = "menuOrders";
        this.menuOrders.Size = new System.Drawing.Size(126, 22);
        this.menuOrders.Text = "Đơn hàng";
        this.menuOrders.Click += new System.EventHandler(this.menuOrders_Click);

        // menuExit
        this.menuExit.Name = "menuExit";
        this.menuExit.Size = new System.Drawing.Size(126, 22);
        this.menuExit.Text = "Thoát";
        this.menuExit.Click += new System.EventHandler(this.menuExit_Click);

        // menuHelp
        this.menuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAbout});
        this.menuHelp.Name = "menuHelp";
        this.menuHelp.Size = new System.Drawing.Size(62, 20);
        this.menuHelp.Text = "Trợ giúp";

        // menuAbout
        this.menuAbout.Name = "menuAbout";
        this.menuAbout.Size = new System.Drawing.Size(125, 22);
        this.menuAbout.Text = "Giới thiệu";
        this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);

        // tabControl
        this.tabControl.Controls.Add(this.tabOrders);
        this.tabControl.Controls.Add(this.tabSlow);
        this.tabControl.Controls.Add(this.tabAbout);
        this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tabControl.Location = new System.Drawing.Point(0, 24);
        this.tabControl.Name = "tabControl";
        this.tabControl.SelectedIndex = 0;
        this.tabControl.Size = new System.Drawing.Size(900, 536);
        this.tabControl.TabIndex = 1;

        // tabOrders
        this.tabOrders.Controls.Add(this.cboStatus);
        this.tabOrders.Controls.Add(this.btnFilter);
        this.tabOrders.Controls.Add(this.lblFilterResult);
        this.tabOrders.Controls.Add(this.dgOrders);
        this.tabOrders.Location = new System.Drawing.Point(4, 24);
        this.tabOrders.Name = "tabOrders";
        this.tabOrders.Padding = new System.Windows.Forms.Padding(3);
        this.tabOrders.Size = new System.Drawing.Size(892, 508);
        this.tabOrders.TabIndex = 0;
        this.tabOrders.Text = "Quản lý đơn hàng";
        this.tabOrders.UseVisualStyleBackColor = true;

        // cboStatus
        this.cboStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboStatus.FormattingEnabled = true;
        this.cboStatus.Items.AddRange(new object[] {
            "Tất cả",
            "Chờ xử lý",
            "Đang giao",
            "Hoàn thành"});
        this.cboStatus.Location = new System.Drawing.Point(10, 12);
        this.cboStatus.Name = "cboStatus";
        this.cboStatus.AccessibleName = "Trạng thái";
        this.cboStatus.Size = new System.Drawing.Size(150, 23);
        this.cboStatus.TabIndex = 0;

        // btnFilter
        this.btnFilter.Location = new System.Drawing.Point(170, 10);
        this.btnFilter.Name = "btnFilter";
        this.btnFilter.AccessibleName = "Lọc dữ liệu";
        this.btnFilter.Size = new System.Drawing.Size(80, 26);
        this.btnFilter.TabIndex = 1;
        this.btnFilter.Text = "Lọc";
        this.btnFilter.UseVisualStyleBackColor = true;
        this.btnFilter.Click += new System.EventHandler(this.btnFilter_Click);

        // lblFilterResult
        this.lblFilterResult.AutoSize = true;
        this.lblFilterResult.Location = new System.Drawing.Point(270, 16);
        this.lblFilterResult.Name = "lblFilterResult";
        this.lblFilterResult.Size = new System.Drawing.Size(95, 15);
        this.lblFilterResult.TabIndex = 2;
        this.lblFilterResult.Text = "Hiển thị: Tất cả";

        // dgOrders
        this.dgOrders.AllowUserToAddRows = false;
        this.dgOrders.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.dgOrders.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dgOrders.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colId,
            this.colCustomer,
            this.colDate,
            this.colTotal,
            this.colStatus,
            this.colNotes});
        this.dgOrders.Location = new System.Drawing.Point(10, 45);
        this.dgOrders.Name = "dgOrders";
        this.dgOrders.AccessibleName = "Danh sách đơn hàng";
        this.dgOrders.Size = new System.Drawing.Size(870, 450);
        this.dgOrders.TabIndex = 3;

        // Columns
        this.colId.HeaderText = "Mã ĐH";
        this.colId.Name = "colId";
        this.colId.Width = 80;

        this.colCustomer.HeaderText = "Khách hàng";
        this.colCustomer.Name = "colCustomer";
        this.colCustomer.Width = 150;

        this.colDate.HeaderText = "Ngày đặt";
        this.colDate.Name = "colDate";
        this.colDate.Width = 100;

        this.colTotal.HeaderText = "Tổng tiền";
        this.colTotal.Name = "colTotal";
        this.colTotal.Width = 120;

        this.colStatus.HeaderText = "Trạng thái";
        this.colStatus.Name = "colStatus";
        this.colStatus.Width = 110;

        this.colNotes.HeaderText = "Ghi chú";
        this.colNotes.Name = "colNotes";
        this.colNotes.Width = 250;

        // tabSlow
        this.tabSlow.Controls.Add(this.btnSlowTask);
        this.tabSlow.Controls.Add(this.lblSlowStatus);
        this.tabSlow.Location = new System.Drawing.Point(4, 24);
        this.tabSlow.Name = "tabSlow";
        this.tabSlow.Padding = new System.Windows.Forms.Padding(3);
        this.tabSlow.Size = new System.Drawing.Size(892, 508);
        this.tabSlow.TabIndex = 1;
        this.tabSlow.Text = "Tác vụ chậm";
        this.tabSlow.UseVisualStyleBackColor = true;

        // btnSlowTask
        this.btnSlowTask.Location = new System.Drawing.Point(30, 30);
        this.btnSlowTask.Name = "btnSlowTask";
        this.btnSlowTask.AccessibleName = "Chạy tác vụ chậm";
        this.btnSlowTask.Size = new System.Drawing.Size(180, 35);
        this.btnSlowTask.TabIndex = 0;
        this.btnSlowTask.Text = "Chạy tác vụ 3 giây";
        this.btnSlowTask.UseVisualStyleBackColor = true;
        this.btnSlowTask.Click += new System.EventHandler(this.btnSlowTask_Click);

        // lblSlowStatus
        this.lblSlowStatus.AutoSize = true;
        this.lblSlowStatus.Location = new System.Drawing.Point(30, 80);
        this.lblSlowStatus.Name = "lblSlowStatus";
        this.lblSlowStatus.Size = new System.Drawing.Size(95, 15);
        this.lblSlowStatus.TabIndex = 1;
        this.lblSlowStatus.Text = "Trạng thái: Sẵn sàng";

        // tabAbout
        this.tabAbout.Controls.Add(this.lblAboutText);
        this.tabAbout.Location = new System.Drawing.Point(4, 24);
        this.tabAbout.Name = "tabAbout";
        this.tabAbout.Size = new System.Drawing.Size(892, 508);
        this.tabAbout.TabIndex = 2;
        this.tabAbout.Text = "Giới thiệu";
        this.tabAbout.UseVisualStyleBackColor = true;

        // lblAboutText
        this.lblAboutText.Location = new System.Drawing.Point(30, 30);
        this.lblAboutText.Name = "lblAboutText";
        this.lblAboutText.Size = new System.Drawing.Size(600, 100);
        this.lblAboutText.TabIndex = 0;
        this.lblAboutText.Text = "WinForms Verifier Fixture Application\nPhiên bản 1.0.0\nĐược sử dụng cho kiểm thử tự động hóa và phân tích tĩnh.";

        // MainForm
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(900, 560);
        this.Controls.Add(this.tabControl);
        this.Controls.Add(this.menuStrip);
        this.MainMenuStrip = this.menuStrip;
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Hệ thống Quản lý Đơn hàng";
        this.menuStrip.ResumeLayout(false);
        this.menuStrip.PerformLayout();
        this.tabControl.ResumeLayout(false);
        this.tabOrders.ResumeLayout(false);
        this.tabOrders.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.dgOrders)).EndInit();
        this.tabSlow.ResumeLayout(false);
        this.tabSlow.PerformLayout();
        this.tabAbout.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
