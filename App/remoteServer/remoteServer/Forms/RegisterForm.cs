using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using remoteServer.Services;

namespace remoteServer.Forms
{
    /// <summary>
    /// Form đăng ký tài khoản mới (Dành cho Admin).
    /// </summary>
    public class RegisterForm : Form
    {
        private TextBox txtRegUser;
        private TextBox txtRegPass;
        private TextBox txtRegConfirm;
        private Button btnRegSubmit;
        private Button btnCancel;

        public RegisterForm()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            Text = "Đăng ký Tài khoản (Admin)";
            MinimumSize = new Size(400, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                Parent = this,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20),
                AutoSize = true
            };
            
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            
            // Username
            layout.Controls.Add(new Label { Text = "Tên đăng nhập:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            txtRegUser = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            layout.Controls.Add(txtRegUser, 1, 0);

            // Password
            layout.Controls.Add(new Label { Text = "Mật khẩu:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            txtRegPass = new TextBox { PasswordChar = '*', Anchor = AnchorStyles.Left | AnchorStyles.Right };
            layout.Controls.Add(txtRegPass, 1, 1);

            // Confirm Password
            layout.Controls.Add(new Label { Text = "Nhập lại MK:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            txtRegConfirm = new TextBox { PasswordChar = '*', Anchor = AnchorStyles.Left | AnchorStyles.Right };
            layout.Controls.Add(txtRegConfirm, 1, 2);

            // Buttons Logic
            var pnlButtons = new FlowLayoutPanel 
            { 
                FlowDirection = FlowDirection.RightToLeft, 
                AutoSize = true, 
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 20, 0, 0)
            };
            
            btnCancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(10, 0, 0, 0) };
            btnRegSubmit = new Button { Text = "Tạo tài khoản", AutoSize = true, DialogResult = DialogResult.None };
            
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnRegSubmit);
            
            layout.Controls.Add(pnlButtons, 0, 3);
            layout.SetColumnSpan(pnlButtons, 2);

            this.AcceptButton = btnRegSubmit;
            this.CancelButton = btnCancel;

            btnRegSubmit.Click += async (s, e) => await HandleRegisterAsync();
        }

        private async Task HandleRegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(txtRegUser.Text) || string.IsNullOrWhiteSpace(txtRegPass.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (txtRegPass.Text != txtRegConfirm.Text)
            {
                MessageBox.Show("Mật khẩu nhập lại không khớp.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnRegSubmit.Enabled = false;
            Text = "Đang xử lý...";

            // Gọi DatabaseService trực tiếp (Chạy async để không treo UI)
            await Task.Run(() => {
                var dbService = new DatabaseService();
                // Hash User Password
                string hash = DatabaseService.ComputeSha256Hash(txtRegPass.Text);
                
                string msg;
                if (dbService.RegisterUser(txtRegUser.Text, hash, out msg))
                {
                    Invoke(new Action(() => {
                        MessageBox.Show(msg, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }));
                }
                else
                {
                    Invoke(new Action(() => {
                            MessageBox.Show("Lỗi: " + msg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            btnRegSubmit.Enabled = true;
                            Text = "Đăng ký Tài khoản (Admin)";
                    }));
                }
            });
        }
    }
}
