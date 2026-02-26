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
        #region Fields

        private ClientConnection _connection;

        // UI Controls (được tạo dynamically)
        private TextBox txtIp;
        private TextBox txtUser;
        private TextBox txtPass;
        private Button btnConnect;
        private Label lblStatus;
        private PictureBox pbScreen; // Hiển thị màn hình remote
        private ProgressBar pbTransfer; // Hiển thị tiến độ file

        // Screen & Rendering State
        private int _serverWidth;
        private int _serverHeight;
        private Bitmap _backBuffer; // Buffer off-screen để vẽ mượt mà (Double Buffering)
        private DateTime _lastMouseMove = DateTime.MinValue; // Dùng để giới hạn tần suất gửi chuột (Throttling)

        #endregion

        #region Constructor & UI Setup

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

            // Panel chứa các control kết nối (Top Bar)
            var panelControl = new Panel { Parent = this, Dock = DockStyle.Top, Height = 60, Padding = new Padding(5) };

            // Dùng FlowLayoutPanel để tự động sắp xếp control (Fix lỗi giao diện trên màn hình High DPI)
            var flowLayout = new FlowLayoutPanel
            {
                Parent = panelControl,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0) // Cắn giữa theo chiều dọc
            };

            // Tạo các Label và TextBox
            var lblIp = new Label { Parent = flowLayout, Text = "IP:", AutoSize = true, Margin = new Padding(5, 5, 0, 5) };
            txtIp = new TextBox { Parent = flowLayout, Text = "192.168.1.135", Width = 120, Margin = new Padding(0, 3, 10, 3) };

            var lblUser = new Label { Parent = flowLayout, Text = "User:", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            txtUser = new TextBox { Parent = flowLayout, Text = "admin", Width = 100, Margin = new Padding(0, 3, 10, 3) };

            var lblPass = new Label { Parent = flowLayout, Text = "Pass:", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            txtPass = new TextBox { Parent = flowLayout, Text = "admin123", Width = 100, PasswordChar = '*', Margin = new Padding(0, 3, 10, 3) };

            // Tạo các Button chức năng
            btnConnect = new Button { Parent = flowLayout, Text = "Kết nối", AutoSize = true, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0) };
            var btnSendFile = new Button { Parent = flowLayout, Text = "Gửi File", AutoSize = true, Enabled = false, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0) };
            lblStatus = new Label { Parent = flowLayout, Text = "Sẵn sàng", AutoSize = true, ForeColor = Color.Blue, Margin = new Padding(0, 5, 0, 5) };

            // Thêm ProgressBar
            pbTransfer = new ProgressBar { Parent = flowLayout, Width = 150, Height = 20, Margin = new Padding(10, 3, 0, 3), Visible = false };

            // PictureBox hiển thị màn hình Remote
            pbScreen = new PictureBox
            {
                Parent = this,
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom // Zoom để hiển thị toàn bộ màn hình Server trong Client
            };

            // QUAN TRỌNG: Đưa Panel kết nối lên lớp trên cùng để không bị PictureBox che mất
            panelControl.BringToFront();
            pbScreen.SendToBack(); // Đảm bảo PictureBox nằm dưới cùng để Dock.Fill hoạt động đúng với Panel Dock.Top 

            // Đăng ký các sự kiện chuột/phím để gửi thao tác lên Server
            pbScreen.MouseMove += PbScreen_MouseMove;
            pbScreen.MouseDown += PbScreen_MouseDown;
            pbScreen.MouseUp += PbScreen_MouseUp;
            this.KeyDown += Client_KeyDown;
            this.KeyUp += Client_KeyUp;

            // Xử lý sự kiện click nút
            btnConnect.Click += BtnConnect_Click;
            btnSendFile.Click += BtnSendFile_Click;
        }

        #endregion

        #region User Interaction Handlers

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = false;
            lblStatus.Text = "Đang kết nối...";

            // Nếu chưa kết nối -> Thực hiện kết nối mới
            if (!_connection.IsConnected)
            {
                int maxRetries = 3;
                bool connected = false;

                for (int i = 0; i < maxRetries; i++)
                {
                    lblStatus.Text = $"Đang kết nối... (Thử lại {i + 1}/{maxRetries})";
                    connected = await _connection.ConnectAsync(txtIp.Text, 8888);

                    if (connected)
                    {
                        lblStatus.Text = "Đã kết nối TCP. Đang thực hiện Handshake...";

                        // Chờ Handshake xong với timeout 10 giây
                        var handshakeTask = _connection.HandshakeComplete;
                        var timeoutTask = Task.Delay(10000);

                        var completedTask = await Task.WhenAny(handshakeTask, timeoutTask);
                        if (completedTask == handshakeTask && handshakeTask.Status == TaskStatus.RanToCompletion)
                        {
                            break; // Handshake thành công
                        }

                        // Handshake timeout hoặc lỗi -> Ngắt kết nối và thử lại
                        _connection.Disconnect();
                        connected = false;
                    }

                    if (!connected && i < maxRetries - 1)
                    {
                        lblStatus.Text = $"Kết nối/Handshake lỗi. Chờ 2s để thử lại...";
                        await Task.Delay(2000); // Backoff 2s
                    }
                }

                if (!connected)
                {
                    lblStatus.Text = "Lỗi kết nối Server sau nhiều lần thử. Vui lòng kiểm tra IP/Firewall.";
                    btnConnect.Enabled = true;
                    return;
                }
            }

            // Khi đã kết nối an toàn -> Gửi yêu cầu đăng nhập
            lblStatus.Text = "Đang đăng nhập...";
            SendLogin();

            // Mở khóa các tính năng khác
            // Lưu ý: btnSendFile chỉ nên enable khi đăng nhập thành công, 
            // nhưng tạm thời enable ở đây để user thấy phản hồi.
            // Logic chuẩn sẽ nằm trong OnPacketReceived (LoginResponse)
        }

        private async void BtnSendFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    lblStatus.Text = $"Đang gửi {Path.GetFileName(ofd.FileName)}...";
                    pbTransfer.Visible = true;
                    pbTransfer.Value = 0;

                    // Vì SendFileAsync ở ClientConnection không báo progress (chỉ Server mới có event progress)
                    // Ta chỉ có thể chờ hoàn tất, nếu không thì phải sửa ClientConnection.SendFileAsync
                    await _connection.SendFileAsync(ofd.FileName);

                    lblStatus.Text = "Gửi file hoàn tất!";
                    pbTransfer.Value = 100;
                    await Task.Delay(2000);
                    pbTransfer.Visible = false;
                }
            }
        }

        // --- Xử lý Input (Chuột & Phím) ---

        private void PbScreen_MouseMove(object sender, MouseEventArgs e)
        {
            // Throttle: Giới hạn gửi ~60 lần/giây (15ms) để tránh quá tải mạng
            if ((DateTime.Now - _lastMouseMove).TotalMilliseconds < 15) return;
            _lastMouseMove = DateTime.Now;

            Rectangle rect = GetImageDisplayRectangle(pbScreen);
            if (rect.Contains(e.Location))
            {
                // Chuẩn hóa tọa độ về [0.0 - 1.0] để Server tự nội suy theo độ phân giải của nó
                float nx = (float)(e.X - rect.X) / rect.Width;
                float ny = (float)(e.Y - rect.Y) / rect.Height;
                SendInput(InputType.MouseMove, nx, ny, 0, 0);
            }
        }

        private void PbScreen_MouseDown(object sender, MouseEventArgs e)
        {
            Rectangle rect = GetImageDisplayRectangle(pbScreen);
            if (rect.Contains(e.Location))
            {
                float nx = (float)(e.X - rect.X) / rect.Width;
                float ny = (float)(e.Y - rect.Y) / rect.Height;
                int btn = (e.Button == MouseButtons.Left) ? 0 : (e.Button == MouseButtons.Right) ? 1 : 2;
                SendInput(InputType.MouseDown, nx, ny, 0, btn);
            }
        }

        private void PbScreen_MouseUp(object sender, MouseEventArgs e)
        {
            Rectangle rect = GetImageDisplayRectangle(pbScreen);
            if (rect.Contains(e.Location))
            {
                float nx = (float)(e.X - rect.X) / rect.Width;
                float ny = (float)(e.Y - rect.Y) / rect.Height;
                int btn = (e.Button == MouseButtons.Left) ? 0 : (e.Button == MouseButtons.Right) ? 1 : 2;
                SendInput(InputType.MouseUp, nx, ny, 0, btn);
            }
        }

        private void Client_KeyDown(object sender, KeyEventArgs e)
        {
            SendInput(InputType.KeyDown, 0, 0, (int)e.KeyCode, 0);
            e.Handled = true; // Ngăn chặn hành vi mặc định (ví dụ: Tab chuyển control)
        }

        private void Client_KeyUp(object sender, KeyEventArgs e)
        {
            SendInput(InputType.KeyUp, 0, 0, (int)e.KeyCode, 0);
            e.Handled = true;
        }

        #endregion

        #region Network Logic (Send/Receive)

        /// <summary>
        /// Gửi gói tin Login.
        /// </summary>
        private async void SendLogin()
        {
            // Hash mật khẩu trước khi gửi qua mạng (ngay cả khi đã có AES, thêm lớp bảo vệ)
            string hash = ComputeSha256Hash(txtPass.Text);
            var loginDto = new LoginRequestDto { Username = txtUser.Text, PasswordHash = hash };
            var payload = SerializationHelper.Serialize(loginDto);
            await _connection.SendPacketAsync(PacketType.LoginRequest, payload);
        }

        /// <summary>
        /// Gửi gói tin Input.
        /// </summary>
        private async void SendInput(InputType type, float x, float y, int keyCode, int button)
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

        /// <summary>
        /// Xử lý các gói tin nhận được từ Server (Chạy trên Main UI Thread thông qua Invoke).
        /// </summary>
        private void OnPacketReceived(PacketType type, byte[] payload)
        {
            // Vì OnPacketReceived được gọi từ luồng Network, cần Invoke để cập nhật UI
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnPacketReceived(type, payload)));
                return;
            }

            switch (type)
            {
                case PacketType.LoginResponse:
                    HandleLoginResponse(payload);
                    break;
                case PacketType.RegisterResponse:
                    HandleRegisterResponse(payload);
                    break;
                case PacketType.ScreenFrame:
                    HandleScreenFrame(payload);
                    break;
                case PacketType.FileChunk:
                    HandleFileChunk(payload);
                    break;
            }
        }

        private void HandleLoginResponse(byte[] payload)
        {
            var response = SerializationHelper.Deserialize<LoginResponseDto>(payload);
            lblStatus.Text = response.IsSuccess ? "Đăng nhập thành công! Bắt đầu nhận màn hình..." : "Lỗi đăng nhập: " + response.Message;
            btnConnect.Enabled = !response.IsSuccess; // Nếu thất bại thì cho phép thử lại

            if (response.IsSuccess)
            {
                // Focus vào PictureBox để bắt đầu nhận sự kiện bàn phím ngay
                pbScreen.Focus();
                // Kích hoạt nút gửi file
                foreach (Control c in pbScreen.Parent.Controls) // Tìm nút SendFile trong Panel (cách này hơi hack)
                {
                    if (c is Panel p)
                    {
                        foreach (Control pc in p.Controls)
                        {
                            if (pc is FlowLayoutPanel flp)
                            {
                                foreach (Control fc in flp.Controls)
                                {
                                    if (fc is Button b && b.Text == "Gửi File") b.Enabled = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void HandleRegisterResponse(byte[] payload)
        {
            var response = SerializationHelper.Deserialize<RegisterResponseDto>(payload);
            if (response.IsSuccess)
            {
                MessageBox.Show("Đăng ký thành công! Hãy nhấn 'Kết nối' để đăng nhập.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Đăng ký thành công.";
            }
            else
            {
                MessageBox.Show("Đăng ký thất bại: " + response.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Lỗi đăng ký: " + response.Message;
            }
        }

        private void HandleScreenFrame(byte[] payload)
        {
            var screenDto = SerializationHelper.Deserialize<ScreenFrameDto>(payload);
            if (screenDto != null && screenDto.ImageData != null)
            {
                using (var ms = new MemoryStream(screenDto.ImageData))
                {
                    var chunk = Image.FromStream(ms);

                    // 1. Xác định kích thước thực (Server gửi TotalW/H)
                    int totalW = screenDto.TotalWidth > 0 ? screenDto.TotalWidth : screenDto.Width;
                    int totalH = screenDto.TotalHeight > 0 ? screenDto.TotalHeight : screenDto.Height;

                    // 2. Tạo hoặc Resize BackBuffer nếu kích thước thay đổi
                    if (_backBuffer == null || _backBuffer.Width != totalW || _backBuffer.Height != totalH)
                    {
                        _backBuffer?.Dispose();
                        _backBuffer = new Bitmap(totalW, totalH);
                    }

                    // 3. Vẽ phần thay đổi (Dirty Rect Chunk) lên BackBuffer
                    using (var g = Graphics.FromImage(_backBuffer))
                    {
                        g.DrawImage(chunk, screenDto.Left, screenDto.Top);
                    }

                    // 4. Gán BackBuffer vào PictureBox (chỉ khi reference thay đổi)
                    if (pbScreen.Image != _backBuffer)
                    {
                        pbScreen.Image = _backBuffer;
                    }

                    // Yêu cầu vẽ lại UI (Async)
                    pbScreen.Invalidate();

                    // Cập nhật thông tin kích thước để dùng cho tính toán Input
                    _serverWidth = totalW;
                    _serverHeight = totalH;
                }
            }
        }

        private async void HandleFileChunk(byte[] payload)
        {
            var chunkDto = SerializationHelper.Deserialize<FileChunkDto>(payload);
            if (chunkDto != null)
            {
                // Chạy IO trên Thread khác để không block UI
                await Task.Run(async () =>
                {
                    string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");
                    Directory.CreateDirectory(saveDir);
                    string filePath = Path.Combine(saveDir, chunkDto.FileName);

                    using (var fs = new FileStream(filePath, chunkDto.ChunkIndex == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write))
                    {
                        await fs.WriteAsync(chunkDto.Data, 0, chunkDto.Data.Length);
                    }

                    // Cập nhật ProgressBar trên UI thread
                    long currentSize = new FileInfo(filePath).Length;
                    int percent = chunkDto.FileSize > 0 ? (int)(currentSize * 100 / chunkDto.FileSize) : 0;

                    Invoke(new Action(() =>
                    {
                        if (!pbTransfer.Visible) pbTransfer.Visible = true;
                        pbTransfer.Value = Math.Min(100, Math.Max(0, percent));
                        lblStatus.Text = $"Đang nhận: {chunkDto.FileName} ({percent}%)";
                    }));

                    if (chunkDto.IsLastChunk)
                    {
                        Invoke(new Action(() =>
                        {
                            pbTransfer.Visible = false;
                            MessageBox.Show($"Đã nhận file từ Server:\n{filePath}", "Nhận File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            lblStatus.Text = $"Đã nhận xong: {chunkDto.FileName}";
                            // Mở thư mục chứa file
                            System.Diagnostics.Process.Start("explorer.exe", saveDir);
                        }));
                    }
                });
            }
        }

        #endregion

        #region Helpers & Dialogs

        /// <summary>
        /// Tính toán vùng hiển thị thực tế của ảnh trong PictureBox (khi dùng ZoomMode).
        /// Giúp loại bỏ phần viền đen khi tính tọa độ chuột.
        /// </summary>

        private Rectangle GetImageDisplayRectangle(PictureBox pb)
        {
            if (pb.Image == null) return new Rectangle(0, 0, pb.Width, pb.Height);

            Size imgSize = pb.Image.Size;
            Size pbSize = pb.ClientSize;

            float ratioImg = (float)imgSize.Width / imgSize.Height;
            float ratioPb = (float)pbSize.Width / pbSize.Height;

            int w, h, x, y;

            if (ratioImg > ratioPb) // Ảnh rộng hơn so với khung -> Fit Width, có viền trên/dưới
            {
                w = pbSize.Width;
                h = (int)(w / ratioImg);
                x = 0;
                y = (pbSize.Height - h) / 2;
            }
            else // Ảnh cao hơn so với khung -> Fit Height, có viền trái/phải
            {
                h = pbSize.Height;
                w = (int)(h * ratioImg);
                y = 0;
                x = (pbSize.Width - w) / 2;
            }

            return new Rectangle(x, y, w, h);
        }

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

        #endregion
    }
}
