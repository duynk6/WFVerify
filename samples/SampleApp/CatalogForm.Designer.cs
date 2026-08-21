namespace SampleApp;

partial class CatalogForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TreeView tvCategories;
    private System.Windows.Forms.ListBox lstProducts;
    private System.Windows.Forms.CheckBox chkActiveOnly;
    private System.Windows.Forms.DateTimePicker dtpFromDate;
    private System.Windows.Forms.TextBox txtSearch;
    private System.Windows.Forms.Label lblSelection;

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
        this.tvCategories = new System.Windows.Forms.TreeView();
        this.lstProducts = new System.Windows.Forms.ListBox();
        this.chkActiveOnly = new System.Windows.Forms.CheckBox();
        this.dtpFromDate = new System.Windows.Forms.DateTimePicker();
        this.txtSearch = new System.Windows.Forms.TextBox();
        this.lblSelection = new System.Windows.Forms.Label();
        this.SuspendLayout();
        //
        // tvCategories
        //
        this.tvCategories.Location = new System.Drawing.Point(12, 12);
        this.tvCategories.Name = "tvCategories";
        this.tvCategories.AccessibleName = "Cây danh mục";
        this.tvCategories.Size = new System.Drawing.Size(260, 300);
        this.tvCategories.TabIndex = 0;
        this.tvCategories.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvCategories_AfterSelect);
        //
        // lstProducts
        //
        this.lstProducts.FormattingEnabled = true;
        this.lstProducts.ItemHeight = 15;
        this.lstProducts.Location = new System.Drawing.Point(288, 12);
        this.lstProducts.Name = "lstProducts";
        this.lstProducts.AccessibleName = "Danh sách sản phẩm";
        this.lstProducts.Size = new System.Drawing.Size(280, 304);
        this.lstProducts.TabIndex = 1;
        this.lstProducts.SelectedIndexChanged += new System.EventHandler(this.lstProducts_SelectedIndexChanged);
        //
        // chkActiveOnly
        //
        this.chkActiveOnly.AutoSize = true;
        this.chkActiveOnly.Location = new System.Drawing.Point(12, 328);
        this.chkActiveOnly.Name = "chkActiveOnly";
        this.chkActiveOnly.AccessibleName = "Chỉ hiện đang hoạt động";
        this.chkActiveOnly.Size = new System.Drawing.Size(160, 19);
        this.chkActiveOnly.TabIndex = 2;
        this.chkActiveOnly.Text = "Chỉ hiện đang hoạt động";
        this.chkActiveOnly.UseVisualStyleBackColor = true;
        this.chkActiveOnly.CheckedChanged += new System.EventHandler(this.chkActiveOnly_CheckedChanged);
        //
        // dtpFromDate
        //
        this.dtpFromDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
        this.dtpFromDate.Location = new System.Drawing.Point(288, 328);
        this.dtpFromDate.Name = "dtpFromDate";
        this.dtpFromDate.AccessibleName = "Ngày bắt đầu";
        this.dtpFromDate.Size = new System.Drawing.Size(130, 23);
        this.dtpFromDate.TabIndex = 3;
        //
        // txtSearch
        //
        this.txtSearch.Location = new System.Drawing.Point(438, 328);
        this.txtSearch.Name = "txtSearch";
        this.txtSearch.AccessibleName = "Từ khoá tìm kiếm";
        this.txtSearch.Size = new System.Drawing.Size(130, 23);
        this.txtSearch.TabIndex = 4;
        //
        // lblSelection
        //
        this.lblSelection.AutoSize = true;
        this.lblSelection.Location = new System.Drawing.Point(12, 364);
        this.lblSelection.Name = "lblSelection";
        this.lblSelection.Size = new System.Drawing.Size(100, 15);
        this.lblSelection.TabIndex = 5;
        this.lblSelection.Text = "Chưa chọn gì";
        //
        // CatalogForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(584, 401);
        this.Controls.Add(this.tvCategories);
        this.Controls.Add(this.lstProducts);
        this.Controls.Add(this.chkActiveOnly);
        this.Controls.Add(this.dtpFromDate);
        this.Controls.Add(this.txtSearch);
        this.Controls.Add(this.lblSelection);
        this.Name = "CatalogForm";
        this.Text = "Danh mục sản phẩm";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
