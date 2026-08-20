namespace SampleApp;

partial class BadLayoutForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Button btnOverlap1;
    private System.Windows.Forms.Button btnOverlap2;
    private System.Windows.Forms.TextBox txtDuplicateTab1;
    private System.Windows.Forms.TextBox txtDuplicateTab2;
    private System.Windows.Forms.Button btnDockFillAnchor;
    private System.Windows.Forms.Button button1;
    private System.Windows.Forms.TextBox textBox2;
    private System.Windows.Forms.Label lblCustomFont;
    private System.Windows.Forms.Button btnDead;

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
        this.btnOverlap1 = new System.Windows.Forms.Button();
        this.btnOverlap2 = new System.Windows.Forms.Button();
        this.txtDuplicateTab1 = new System.Windows.Forms.TextBox();
        this.txtDuplicateTab2 = new System.Windows.Forms.TextBox();
        this.btnDockFillAnchor = new System.Windows.Forms.Button();
        this.button1 = new System.Windows.Forms.Button();
        this.textBox2 = new System.Windows.Forms.TextBox();
        this.lblCustomFont = new System.Windows.Forms.Label();
        this.btnDead = new System.Windows.Forms.Button();
        this.SuspendLayout();

        // WF010: Overlapping controls
        // btnOverlap1
        this.btnOverlap1.Location = new System.Drawing.Point(20, 20);
        this.btnOverlap1.Name = "btnOverlap1";
        this.btnOverlap1.Size = new System.Drawing.Size(100, 30);
        this.btnOverlap1.TabIndex = 0;
        this.btnOverlap1.Text = "Nút 1";

        // btnOverlap2 (Overlaps with btnOverlap1)
        this.btnOverlap2.Location = new System.Drawing.Point(50, 30);
        this.btnOverlap2.Name = "btnOverlap2";
        this.btnOverlap2.Size = new System.Drawing.Size(100, 30);
        this.btnOverlap2.TabIndex = 1;
        this.btnOverlap2.Text = "Nút 2 (Đè lên 1)";

        // WF020: Duplicate TabIndex = 2
        // txtDuplicateTab1
        this.txtDuplicateTab1.Location = new System.Drawing.Point(20, 70);
        this.txtDuplicateTab1.Name = "txtDuplicateTab1";
        this.txtDuplicateTab1.Size = new System.Drawing.Size(120, 23);
        this.txtDuplicateTab1.TabIndex = 2;

        // txtDuplicateTab2
        this.txtDuplicateTab2.Location = new System.Drawing.Point(150, 70);
        this.txtDuplicateTab2.Name = "txtDuplicateTab2";
        this.txtDuplicateTab2.Size = new System.Drawing.Size(120, 23);
        this.txtDuplicateTab2.TabIndex = 2;

        // WF030: Dock = Fill with non-default Anchor
        // btnDockFillAnchor
        this.btnDockFillAnchor.Dock = System.Windows.Forms.DockStyle.Fill;
        this.btnDockFillAnchor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.btnDockFillAnchor.Location = new System.Drawing.Point(0, 0);
        this.btnDockFillAnchor.Name = "btnDockFillAnchor";
        this.btnDockFillAnchor.Size = new System.Drawing.Size(400, 300);
        this.btnDockFillAnchor.TabIndex = 3;
        this.btnDockFillAnchor.Text = "Dock Fill and Anchor conflict";

        // WF040 & WF041: Default name, missing AccessibleName, empty text
        // button1
        this.button1.Location = new System.Drawing.Point(20, 110);
        this.button1.Name = "button1";
        this.button1.Size = new System.Drawing.Size(80, 25);
        this.button1.TabIndex = 4;
        this.button1.Text = "";

        // WF041: Default name textBox2
        // textBox2
        this.textBox2.Location = new System.Drawing.Point(110, 110);
        this.textBox2.Name = "textBox2";
        this.textBox2.Size = new System.Drawing.Size(100, 23);
        this.textBox2.TabIndex = 5;

        // WF050: Hardcoded font
        // lblCustomFont
        this.lblCustomFont.AutoSize = true;
        this.lblCustomFont.Font = new System.Drawing.Font("Courier New", 14F);
        this.lblCustomFont.Location = new System.Drawing.Point(20, 150);
        this.lblCustomFont.Name = "lblCustomFont";
        this.lblCustomFont.Size = new System.Drawing.Size(120, 20);
        this.lblCustomFont.TabIndex = 6;
        this.lblCustomFont.Text = "Custom Font";

        // WF060: btnDead is instantiated but NOT added to Controls.Add
        this.btnDead.Location = new System.Drawing.Point(20, 180);
        this.btnDead.Name = "btnDead";
        this.btnDead.Size = new System.Drawing.Size(100, 30);
        this.btnDead.TabIndex = 7;
        this.btnDead.Text = "Dead button";

        // BadLayoutForm
        // WF051: AutoScaleMode = None
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.ClientSize = new System.Drawing.Size(400, 300);
        this.Controls.Add(this.btnOverlap1);
        this.Controls.Add(this.btnOverlap2);
        this.Controls.Add(this.txtDuplicateTab1);
        this.Controls.Add(this.txtDuplicateTab2);
        this.Controls.Add(this.button1);
        this.Controls.Add(this.textBox2);
        this.Controls.Add(this.lblCustomFont);
        this.Name = "BadLayoutForm";
        this.Text = "Form có lỗi thiết kế";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
