namespace SampleApp;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
        LoadOrderData();
    }

    private void LoadOrderData()
    {
        dgOrders.Rows.Clear();
        for (int i = 1; i <= 50; i++)
        {
            dgOrders.Rows.Add(
                $"DH{i:D4}",
                $"Khách hàng {i}",
                DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd"),
                $"{i * 150_000:N0} đ",
                i % 3 == 0 ? "Hoàn thành" : (i % 2 == 0 ? "Đang giao" : "Chờ xử lý"),
                $"Ghi chú đơn hàng số {i}");
        }

        cboStatus.SelectedIndex = 0;
    }

    private void menuExit_Click(object sender, EventArgs e)
    {
        Application.Exit();
    }

    private void menuAbout_Click(object sender, EventArgs e)
    {
        MessageBox.Show(
            "WinForms Verifier Sample App v1.0\nỨng dụng mẫu phục vụ kiểm thử MCP Server.",
            "Giới thiệu",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void btnSlowTask_Click(object sender, EventArgs e)
    {
        lblSlowStatus.Text = "Đang xử lý tác vụ nặng...";
        Application.DoEvents();
        Thread.Sleep(3000);
        lblSlowStatus.Text = "Tác vụ hoàn thành!";
    }

    private void btnFilter_Click(object sender, EventArgs e)
    {
        var status = cboStatus.SelectedItem?.ToString() ?? "";
        lblFilterResult.Text = $"Đã lọc theo: {status}";
    }
}
