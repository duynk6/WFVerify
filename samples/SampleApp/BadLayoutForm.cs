namespace SampleApp;

public partial class BadLayoutForm : Form
{
    public BadLayoutForm()
    {
        InitializeComponent();
    }

    // WF002: Orphaned event handler method (never wired in Designer)
    private void btnOrphan_Click(object sender, EventArgs e)
    {
        MessageBox.Show("This handler is never wired to any control!");
    }
}
