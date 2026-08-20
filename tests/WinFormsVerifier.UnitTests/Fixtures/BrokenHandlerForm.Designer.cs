namespace UnitTests.Fixtures;

public class BrokenHandlerForm
{
    private System.Windows.Forms.Button button1;

    private void InitializeComponent()
    {
        this.button1 = new System.Windows.Forms.Button();
        this.button1.Name = "button1";
        this.button1.Click += new System.EventHandler(this.btnNonExistent_Click);
    }
}
