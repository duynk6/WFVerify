namespace SampleApp;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label lblUsername;
    private System.Windows.Forms.Label lblPassword;
    private System.Windows.Forms.TextBox txtUsername;
    private System.Windows.Forms.TextBox txtPassword;
    private System.Windows.Forms.Button btnLogin;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.CheckBox chkRemember;

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
        this.lblUsername = new System.Windows.Forms.Label();
        this.lblPassword = new System.Windows.Forms.Label();
        this.txtUsername = new System.Windows.Forms.TextBox();
        this.txtPassword = new System.Windows.Forms.TextBox();
        this.btnLogin = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.chkRemember = new System.Windows.Forms.CheckBox();
        this.SuspendLayout();

        // lblUsername
        this.lblUsername.AutoSize = true;
        this.lblUsername.Location = new System.Drawing.Point(30, 30);
        this.lblUsername.Name = "lblUsername";
        this.lblUsername.Size = new System.Drawing.Size(89, 15);
        this.lblUsername.TabIndex = 0;
        this.lblUsername.Text = "Tên đăng nhập:";

        // txtUsername
        this.txtUsername.Location = new System.Drawing.Point(130, 27);
        this.txtUsername.Name = "txtUsername";
        this.txtUsername.AccessibleName = "Tên đăng nhập";
        this.txtUsername.Size = new System.Drawing.Size(200, 23);
        this.txtUsername.TabIndex = 1;

        // lblPassword
        this.lblPassword.AutoSize = true;
        this.lblPassword.Location = new System.Drawing.Point(30, 70);
        this.lblPassword.Name = "lblPassword";
        this.lblPassword.Size = new System.Drawing.Size(59, 15);
        this.lblPassword.TabIndex = 2;
        this.lblPassword.Text = "Mật khẩu:";

        // txtPassword
        this.txtPassword.Location = new System.Drawing.Point(130, 67);
        this.txtPassword.Name = "txtPassword";
        this.txtPassword.AccessibleName = "Mật khẩu";
        this.txtPassword.PasswordChar = '*';
        this.txtPassword.Size = new System.Drawing.Size(200, 23);
        this.txtPassword.TabIndex = 3;

        // chkRemember
        this.chkRemember.AutoSize = true;
        this.chkRemember.Location = new System.Drawing.Point(130, 105);
        this.chkRemember.Name = "chkRemember";
        this.chkRemember.Size = new System.Drawing.Size(104, 19);
        this.chkRemember.TabIndex = 4;
        this.chkRemember.Text = "Ghi nhớ tôi";
        this.chkRemember.UseVisualStyleBackColor = true;

        // btnLogin
        this.btnLogin.Location = new System.Drawing.Point(130, 140);
        this.btnLogin.Name = "btnLogin";
        this.btnLogin.AccessibleName = "Đăng nhập";
        this.btnLogin.Size = new System.Drawing.Size(95, 30);
        this.btnLogin.TabIndex = 5;
        this.btnLogin.Text = "Đăng nhập";
        this.btnLogin.UseVisualStyleBackColor = true;
        this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);

        // btnCancel
        this.btnCancel.Location = new System.Drawing.Point(235, 140);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.AccessibleName = "Hủy bỏ";
        this.btnCancel.Size = new System.Drawing.Size(95, 30);
        this.btnCancel.TabIndex = 6;
        this.btnCancel.Text = "Hủy";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

        // LoginForm
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(380, 200);
        this.Controls.Add(this.lblUsername);
        this.Controls.Add(this.txtUsername);
        this.Controls.Add(this.lblPassword);
        this.Controls.Add(this.txtPassword);
        this.Controls.Add(this.chkRemember);
        this.Controls.Add(this.btnLogin);
        this.Controls.Add(this.btnCancel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.Name = "LoginForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Đăng nhập hệ thống";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
