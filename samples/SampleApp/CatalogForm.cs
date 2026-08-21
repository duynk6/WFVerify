namespace SampleApp;

/// <summary>
/// Fixture cho các tool tương tác chưa có nơi kiểm thử: TreeView (expand/select),
/// ListBox nhiều mục (select theo index/tên + scroll_into_view), CheckBox (toggle),
/// DateTimePicker và TextBox (focus/send_keys).
/// </summary>
public partial class CatalogForm : Form
{
    public CatalogForm()
    {
        InitializeComponent();
        BuildTree();
        BuildProducts();
    }

    private void BuildTree()
    {
        var electronics = new TreeNode("Điện tử");
        electronics.Nodes.Add(new TreeNode("Điện thoại"));
        electronics.Nodes.Add(new TreeNode("Máy tính bảng"));
        electronics.Nodes.Add(new TreeNode("Laptop"));

        var household = new TreeNode("Gia dụng");
        household.Nodes.Add(new TreeNode("Nồi cơm điện"));
        household.Nodes.Add(new TreeNode("Máy lọc nước"));

        tvCategories.Nodes.Add(electronics);
        tvCategories.Nodes.Add(household);
        tvCategories.CollapseAll();
    }

    private void BuildProducts()
    {
        lstProducts.BeginUpdate();
        lstProducts.Items.Clear();
        for (int i = 1; i <= 60; i++)
        {
            lstProducts.Items.Add($"Sản phẩm {i:D2}");
        }
        lstProducts.EndUpdate();
    }

    private void tvCategories_AfterSelect(object sender, TreeViewEventArgs e)
    {
        lblSelection.Text = $"Danh mục: {e.Node?.Text}";
    }

    private void lstProducts_SelectedIndexChanged(object sender, EventArgs e)
    {
        lblSelection.Text = $"Sản phẩm: {lstProducts.SelectedItem}";
    }

    private void chkActiveOnly_CheckedChanged(object sender, EventArgs e)
    {
        lblSelection.Text = chkActiveOnly.Checked ? "Bộ lọc: đang hoạt động" : "Bộ lọc: tất cả";
    }
}
