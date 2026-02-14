using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using remoteClient.Network;
using Shared.DTO;
using Shared.Protocol;
using Shared.Utils;
using System.Text;
using System.Security.Cryptography;

namespace remoteClient
{
    /// <summary>
    /// Form chính của ứng dụng Client (Remote Desktop Viewer).
    /// </summary>
    public partial class client : Form
    {
        private ClientConnection _connection;
        private TextBox txtIp;
        private TextBox txtUser;
        private TextBox txtPass;
        private Button btnConnect;
        private Label lblStatus;
        private PictureBox pbScreen; // Hiển thị màn hình remote
        
        // Thông tin màn hình remote để tính toán tỷ lệ tọa độ chuột
        private int _serverWidth;
        private int _serverHeight;
        private DateTime _lastMouseMove = DateTime.MinValue; // Dùng để throttle sự kiện chuột

        public client()
        {
            InitializeComponent();
            SetupUI();
            this.KeyPreview = true; // Cho phép Form bắt sự kiện phím trước khi Control con xử lý
            
            _connection = new ClientConnection();
            // Đăng ký sự kiện từ Connection
            _connection.OnError += (msg) => Invoke(new Action(() => lblStatus.Text = "Lỗi: " + msg));
            _connection.OnPacketReceived += OnPacketReceived;
        }

        /// <summary>
        /// Khởi tạo giao diện người dùng bằng code (thay vì Designer).
        /// </summary>
        private void SetupUI()
        {
            this.Size = new System.Drawing.Size(1024, 768);
            this.Text = "Remote Desktop Client - Điều khiển từ xa";

            // Panel chứa các control kết nối
            var panelControl = new Panel { Parent = this, Dock = DockStyle.Top, Height = 50 };

            var lblIp = new Label { Parent = panelControl, Top = 15, Left = 10, Width = 30, Text = "IP:" };
            txtIp = new TextBox { Parent = panelControl, Top = 12, Left = 40, Width = 100, Text = "127.0.0.1" };
            
            var lblUser = new Label { Parent = panelControl, Top = 15, Left = 150, Width = 40, Text = "User:" };
            txtUser = new TextBox { Parent = panelControl, Top = 12, Left = 190, Width = 80, Text = "admin" };
            
            var lblPass = new Label { Parent = panelControl, Top = 15, Left = 280, Width = 40, Text = "Pass:" };
            txtPass = new TextBox { Parent = panelControl, Top = 12, Left = 320, Width = 80, Text = "admin123", PasswordChar = '*' };
            
            btnConnect = new Button { Parent = panelControl, Top = 10, Left = 410, Text = "Kết nối" };
            var btnSendFile = new Button { Parent = panelControl, Top = 10, Left = 500, Text = "Gửi File", Width = 80, Enabled = false };
            lblStatus = new Label { Parent = panelControl, Top = 15, Left = 600, Width = 300, Text = "Sẵn sàng" };

            // PictureBox hiển thị màn hình
            pbScreen = new PictureBox 
            { 
                Parent = this, 
                Dock = DockStyle.Fill, 
                BackColor = Color.Black, 
                SizeMode = PictureBoxSizeMode.Zoom // Tự động co giãn ảnh giữ đúng tỷ lệ
            };
            
            // Đăng ký các sự kiện chuột/phím để gửi lên Server
            pbScreen.MouseMove += PbScreen_MouseMove;
            pbScreen.MouseDown += PbScreen_MouseDown;
            pbScreen.MouseUp += PbScreen_MouseUp;
            this.KeyDown += Client_KeyDown;
            this.KeyUp += Client_KeyUp;

            // Xử lý nút Kết nối
            btnConnect.Click += async (s, e) => {
                btnConnect.Enabled = false;
                lblStatus.Text = "Đang kết nối...";
                bool connected = await _connection.ConnectAsync(txtIp.Text, 8888);
                if (connected)
                {
                    lblStatus.Text = "Đã kết nối. Đang đăng nhập...";
                    SendLogin();
                    btnSendFile.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "Kết nối thất bại";
                    btnConnect.Enabled = true;
                }
            };

            // Xử lý nút Gửi File
            btnSendFile.Click += async (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        lblStatus.Text = $"Đang gửi {Path.GetFileName(ofd.FileName)}...";
                        await _connection.SendFileAsync(ofd.FileName);
                        lblStatus.Text = "Gửi file hoàn tất!";
                    }
                }
            };
        }

        /// <summary>
        /// Chuyển đổi tọa độ chuột từ PictureBox (Client) sang tọa độ thực tế trên màn hình Server.
        /// </summary>
        private void ConvertCoordinates(int clientX, int clientY, out int serverX, out int serverY)
        {
            serverX = 0; serverY = 0;
            if (_serverWidth == 0 || _serverHeight == 0) return;

            // Tính tỷ lệ Zoom của PictureBox
            float ratioX = (float)pbScreen.Width / _serverWidth;
            float ratioY = (float)pbScreen.Height / _serverHeight;
            float ratio = Math.Min(ratioX, ratioY);

            // Kích thước hiển thị thực tế
            int displayedW = (int)(_serverWidth * ratio);
            int displayedH = (int)(_serverHeight * ratio);

            // Offset (khoảng trắng đen do Zoom)
            int offsetX = (pbScreen.Width - displayedW) / 2;
            int offsetY = (pbScreen.Height - displayedH) / 2;

            // Tính toán ngược lại tọa độ gốc
            serverX = (int)((clientX - offsetX) / ratio);
            serverY = (int)((clientY - offsetY) / ratio);
            
            // Giới hạn trong kích thước màn hình Server
            if (serverX < 0) serverX = 0;
            if (serverX >= _serverWidth) serverX = _serverWidth - 1;
            if (serverY < 0) serverY = 0;
            if (serverY >= _serverHeight) serverY = _serverHeight - 1;
        }

        /// <summary>
        /// Gửi sự kiện Input lên Server.
        /// </summary>
        private async void SendInput(InputType type, int x, int y, int keyCode, int button)
        {
            if (!_connection.IsConnected) return;

            var inputDto = new InputEventDto
            {
                Type = type,
                X = x,
                Y = y,
                KeyCode = keyCode,
                Button = button
            };
            byte[] payload = SerializationHelper.Serialize(inputDto);
            await _connection.SendPacketAsync(PacketType.InputEvent, payload);
        }

        private void PbScreen_MouseMove(object sender, MouseEventArgs e)
        {
            // Throttle: Giới hạn gửi tối đa 20 lần/giây để tránh spam packet
            if ((DateTime.Now - _lastMouseMove).TotalMilliseconds < 50) return;
            _lastMouseMove = DateTime.Now;

            ConvertCoordinates(e.X, e.Y, out int sx, out int sy);
            SendInput(InputType.MouseMove, sx, sy, 0, 0);
        }

        private void PbScreen_MouseDown(object sender, MouseEventArgs e)
        {
            ConvertCoordinates(e.X, e.Y, out int sx, out int sy);
            int btn = (e.Button == MouseButtons.Left) ? 0 : (e.Button == MouseButtons.Right) ? 1 : 2;
            SendInput(InputType.MouseDown, sx, sy, 0, btn);
        }

        private void PbScreen_MouseUp(object sender, MouseEventArgs e)
        {
            ConvertCoordinates(e.X, e.Y, out int sx, out int sy);
            int btn = (e.Button == MouseButtons.Left) ? 0 : (e.Button == MouseButtons.Right) ? 1 : 2;
            SendInput(InputType.MouseUp, sx, sy, 0, btn);
        }

        private void Client_KeyDown(object sender, KeyEventArgs e)
        {
            SendInput(InputType.KeyDown, 0, 0, (int)e.KeyCode, 0);
            e.Handled = true; // Ngăn chặn sự kiện mặc định (ví dụ Tab chuyển control)
        }

        private void Client_KeyUp(object sender, KeyEventArgs e)
        {
            SendInput(InputType.KeyUp, 0, 0, (int)e.KeyCode, 0);
             e.Handled = true;
        }

        private async void SendLogin()
        {
            // Băm mật khẩu trước khi gửi
            string hash = ComputeSha256Hash(txtPass.Text);
            var loginDto = new LoginRequestDto { Username = txtUser.Text, PasswordHash = hash };
            var payload = SerializationHelper.Serialize(loginDto);
            await _connection.SendPacketAsync(PacketType.LoginRequest, payload);
        }

        /// <summary>
        /// Xử lý gói tin nhận được từ Server (chạy trên UI Thread).
        /// </summary>
        private void OnPacketReceived(PacketType type, byte[] payload)
        {
            if (type == PacketType.LoginResponse)
            {
                var response = SerializationHelper.Deserialize<LoginResponseDto>(payload);
                Invoke(new Action(() => {
                    lblStatus.Text = response.IsSuccess ? "Đăng nhập thành công! Đang nhận màn hình..." : "Lỗi đăng nhập: " + response.Message;
                    btnConnect.Enabled = !response.IsSuccess;
                    if (response.IsSuccess) pbScreen.Focus(); // Focus để bắt sự kiện phím
                }));
            }
            else if (type == PacketType.ScreenFrame)
            {
                var screenDto = SerializationHelper.Deserialize<ScreenFrameDto>(payload);
                if (screenDto != null && screenDto.ImageData != null)
                {
                    _serverWidth = screenDto.Width;
                    _serverHeight = screenDto.Height;

                    // Hiển thị khung hình mới
                    using (var ms = new MemoryStream(screenDto.ImageData))
                    {
                        var image = Image.FromStream(ms);
                        Invoke(new Action(() => {
                            var oldImage = pbScreen.Image;
                            pbScreen.Image = (Image)image.Clone(); // Clone để tránh lỗi Dispose stream
                            oldImage?.Dispose(); // Giải phóng ảnh cũ
                        }));
                    }
                }
            }
        }
        
        /// <summary>
        /// Hàm băm SHA256
        /// </summary>
        static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
