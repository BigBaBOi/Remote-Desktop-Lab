using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using remoteServer.Network;
using remoteServer.Services;

namespace remoteServer
{
    /// <summary>
    /// Giao diện chính của Server.
    /// Quản lý danh sách Client kết nối và cung cấp các công cụ quản trị (Gửi file, Cấu hình DB, Đăng ký).
    /// </summary>
    public partial class server : Form
    {
        private AsyncTcpListener _listener;
        private System.ComponentModel.IContainer components = null;

        public server()
        {
            InitializeComponent();
            Load += Server_Load;
        }

        /// <summary>
        /// Sự kiện khi Form tải xong: Khởi động bộ lắng nghe kết nối (Listener) trên Port 8888.
        /// </summary>
        private async void Server_Load(object sender, EventArgs e)
        {
            // Tự động bắt đầu lắng nghe khi mở ứng dụng
            _listener = new AsyncTcpListener(IPAddress.Any, 8888);
            _listener.OnClientConnected += OnClientConnected;
            _listener.Start();
            Text = "Remote Server Running on Port 8888";

            // Kiểm tra kết nối CSDL bất đồng bộ (không block UI)
            await CheckDatabaseConnectionAsync();
        }

        private async Task CheckDatabaseConnectionAsync()
        {
            try
            {
                // Khởi tạo DatabaseService trên luồng nền
                await Task.Run(() => new DatabaseService());

                if (!DatabaseService.IsDatabaseAvailable)
                {
                    Invoke(new Action(() => ShowDatabaseErrorWarning()));
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => MessageBox.Show($"Lỗi khi kiểm tra Database: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        private void ShowDatabaseErrorWarning()
        {
            DialogResult result = MessageBox.Show(
                "Không thể kết nối tới MySQL. Vui lòng kiểm tra dịch vụ MySQL đã được bật và cấu hình Database trong 'Cấu hình Database'.\n\nBạn có muốn mở bảng 'Cấu hình Database' không?\n- Chọn 'Yes' để mở Cấu hình.\n- Chọn 'No' để thử lại kết nối.\n- Chọn 'Cancel' để tiếp tục chạy chế độ Offline.",
                "Lỗi kết nối MySQL",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                using (var configForm = new remoteServer.Services.DatabaseConfigForm())
                {
                    configForm.ShowDialog(this);
                }
                // Sau khi đóng cấu hình, thử kiểm tra lại
                _ = CheckDatabaseConnectionAsync();
            }
            else if (result == DialogResult.No)
            {
                // Thử lại
                _ = CheckDatabaseConnectionAsync();
            }
            else
            {
                // Cancel -> Chạy offline
                Shared.Utils.Logger.Log("Người dùng chọn chạy chế độ Offline do lỗi MySQL.");
            }
        }

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
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(500, 300); // Tăng kích thước form
            this.Text = "Server";

            // Client List Box (Left Side)
            var lblClients = new Label { Text = "Danh sách Client:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
            this.Controls.Add(lblClients);

            _lstClients = new ListBox { Location = new System.Drawing.Point(10, 30), Size = new System.Drawing.Size(250, 200) };
            this.Controls.Add(_lstClients);

            // Right Side Buttons Panel
            int btnX = 280;
            int btnY = 30;
            int btnGap = 40;

            // Config Button
            var btnConfig = new Button();
            btnConfig.Text = "Cấu hình Database";
            btnConfig.AutoSize = true;
            btnConfig.Location = new System.Drawing.Point(btnX, btnY);
            btnConfig.Click += (s, e) =>
            {
                new remoteServer.Services.DatabaseConfigForm().ShowDialog();
            };
            this.Controls.Add(btnConfig);

            // Open Received Files Button
            var btnOpenFiles = new Button();
            btnOpenFiles.Text = "Mở thư mục nhận File";
            btnOpenFiles.AutoSize = true;
            btnOpenFiles.Location = new System.Drawing.Point(btnX, btnY + btnGap);
            btnOpenFiles.Click += (s, e) =>
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");
                System.IO.Directory.CreateDirectory(path); // Đảm bảo thư mục tồn tại
                System.Diagnostics.Process.Start("explorer.exe", path);
            };
            this.Controls.Add(btnOpenFiles);

            // Send File Button
            var btnSendFile = new Button();
            btnSendFile.Text = "Gửi File cho Client";
            btnSendFile.AutoSize = true;
            btnSendFile.Location = new System.Drawing.Point(btnX, btnY + btnGap * 2);
            btnSendFile.Click += (s, e) =>
            {
                // ... (Send File Logic) ...
                if (_lstClients.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn một Client trong danh sách để gửi file.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var session = _lstClients.SelectedItem as ClientSession;

                using (var ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        Task.Run(() => session.SendFileAsync(ofd.FileName));
                        MessageBox.Show($"Đang gửi file {System.IO.Path.GetFileName(ofd.FileName)}...", "Thông báo");
                    }
                }
            };
            this.Controls.Add(btnSendFile);

            var btnRegister = new Button();
            btnRegister.Text = "Đăng ký Tài khoản Mới";
            btnRegister.AutoSize = true;
            btnRegister.Location = new System.Drawing.Point(btnX, btnY + btnGap * 3); // Moved up from * 4
            btnRegister.Click += (s, e) => ShowRegisterDialog();
            this.Controls.Add(btnRegister);

            // Context Menu for Sending File (Keep for convenience)
            var ctxMenu = new ContextMenuStrip();
            var itemSendFile = new ToolStripMenuItem("Gửi File");
            itemSendFile.Click += (s, e) => btnSendFile.PerformClick(); // Reuse logic
            ctxMenu.Items.Add(itemSendFile);
            _lstClients.ContextMenuStrip = ctxMenu;
        }

        private ListBox _lstClients;
        private System.Collections.Generic.List<ClientSession> _sessions = new System.Collections.Generic.List<ClientSession>();

        private void OnClientConnected(ClientSession session)
        {
            Invoke(new Action(() =>
            {
                _sessions.Add(session);
                _lstClients.Items.Add(session); // DisplayMember needed? ClientSession.ToString() returns class name by default
                // Let's override ToString? Or wrapper?
                // For now, let's just add it. We need a DisplayMember.
                // Wait, ClientSession needs a ToString override to look good.
            }));

            // Clean up when client disconnects? (Not implemented in Session yet, but good enough for now)
        }
        private void ShowRegisterDialog()
        {
            using (var form = new remoteServer.Forms.RegisterForm())
            {
                form.ShowDialog(this);
            }
        }
    }
}
