using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;

namespace remoteServer.Services
{
    public class DatabaseConfigForm : Form
    {
        private TextBox txtIp;
        private TextBox txtUser;
        private TextBox txtPass;
        private TextBox txtDb;
        private Button btnSave;
        private Button btnCancel;

        public DatabaseConfigForm()
        {
            Text = "Cấu hình Kết nối Database";
            Size = new Size(450, 350);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(20);
            layout.RowCount = 5;
            layout.ColumnCount = 2;

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // IP Address
            var lblIp = new Label { Text = "IP Máy SQL:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, AutoSize = false };
            layout.Controls.Add(lblIp, 0, 0);
            txtIp = new TextBox { Text = "127.0.0.1", Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
            layout.Controls.Add(txtIp, 1, 0);

            // Database Name
            var lblDb = new Label { Text = "Tên Database:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, AutoSize = false };
            layout.Controls.Add(lblDb, 0, 1);
            txtDb = new TextBox { Text = "RemoteDesktopDB", Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
            layout.Controls.Add(txtDb, 1, 1);

            // Username
            var lblUser = new Label { Text = "User (root):", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, AutoSize = false };
            layout.Controls.Add(lblUser, 0, 2);
            txtUser = new TextBox { Text = "root", Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
            layout.Controls.Add(txtUser, 1, 2);

            // Password
            var lblPass = new Label { Text = "Mật khẩu:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, AutoSize = false };
            layout.Controls.Add(lblPass, 0, 3);
            txtPass = new TextBox { PasswordChar = '*', Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
            layout.Controls.Add(txtPass, 1, 3);

            // Buttons
            var pnlButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 20, 0, 0) };
            btnCancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Height = 35, Width = 80 };
            btnSave = new Button { Text = "Lưu & Kết nối", DialogResult = DialogResult.OK, AutoSize = true, Height = 35, MinimumSize = new Size(100, 35) };

            btnSave.Click += BtnSave_Click;

            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnSave);
            layout.Controls.Add(pnlButtons, 0, 4);
            layout.SetColumnSpan(pnlButtons, 2);

            Controls.Add(layout);
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            LoadExistingConfig();
        }

        private void LoadExistingConfig()
        {
            // Try to parse existing config roughly to fill boxes
            string configPath = AppDomain.CurrentDomain.BaseDirectory + "db_config.txt";
            if (File.Exists(configPath))
            {
                string connStr = File.ReadAllText(configPath);
                // Simple parsing logic (not robust but helpful)
                foreach (var part in connStr.Split(';'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().ToLower();
                        string val = kv[1].Trim();
                        if (key == "server") txtIp.Text = val;
                        if (key == "database") txtDb.Text = val;
                        if (key == "uid") txtUser.Text = val;
                        // Password might be complex, skip for security or show placeholder
                    }
                }
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            btnSave.Enabled = false;
            btnCancel.Enabled = false;
            btnSave.Text = "Đang thử kết nối...";

            try
            {
                // Build Connection String
                string connStr = $"Server={txtIp.Text};Database={txtDb.Text};Uid={txtUser.Text};Pwd={txtPass.Text};Connection Timeout=5";

                // Test connection async before saving
                await Task.Run(() =>
                {
                    using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connStr))
                    {
                        conn.Open(); // Will throw if invalid
                    }
                });

                // If success, save to file
                string configPath = AppDomain.CurrentDomain.BaseDirectory + "db_config.txt";
                File.WriteAllText(configPath, connStr);

                // Reload Service (async friendly)
                await Task.Run(() => DatabaseService.ReloadConnectionString());

                MessageBox.Show("Đã lưu cấu hình và kết nối thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết nối đến Database:\n{ex.Message}\n\nVui lòng kiểm tra lại IP, User, Pass.", "Lỗi Kết Nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
                btnCancel.Enabled = true;
                btnSave.Text = "Lưu & Kết nối";
            }
        }
    }
}
