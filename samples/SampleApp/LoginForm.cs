namespace SampleApp;

public partial class LoginForm : Form
{
    public LoginForm()
    {
        InitializeComponent();
    }

    private void btnLogin_Click(object sender, EventArgs e)
    {
        if (txtUsername.Text.Trim() == "admin" && txtPassword.Text == "123456")
        {
            var main = new MainForm();
            main.Show();
            this.Hide();
        }
        else
        {
            MessageBox.Show(
                "Sai tên đăng nhập hoặc mật khẩu! Vui lòng thử lại.",
                "Lỗi đăng nhập",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Application.Exit();
    }
}
