# Remote Desktop Application (Tài Liệu Kỹ Thuật & Hướng Dẫn Code)

Đây là tài liệu chi tiết về kiến trúc, logic hoạt động và hướng dẫn đọc mã nguồn cho dự án **Remote Desktop** (Điều khiển máy tính từ xa).

---

## 1. Tổng Quan Kiến Trúc

Dự án được xây dựng theo mô hình **Client-Server** sử dụng giao thức **TCP/IP** với các kỹ thuật tối ưu hóa hiệu năng và bảo mật cao.

```mermaid
graph LR
    Client[Remote Client] <-->|TCP (Port 8888)| Server[Remote Server]
    Server -->|SQL| DB[(SQL Server)]
```

- **Server (Máy Bị Điều Khiển)**:
  - Chụp màn hình liên tục -> Nén ảnh -> Gửi cho Client.
  - Nhận lệnh chuột/phím từ Client -> Giả lập thao tác lên Windows (User32.dll).
  - Quản lý Database (Đăng ký, Đăng nhập, Log).
- **Client (Máy Điều Khiển)**:
  - Hiển thị hình ảnh từ Server.
  - Bắt sự kiện chuột/phím của người dùng -> Gửi lên Server.
  - Gửi file lên Server.

---

## 2. Cấu Trúc Thư Mục & Giải Thích Code

Solution bao gồm 3 Project chính. Dưới đây là giải thích chi tiết từng file quan trọng:

### 2.1 `Shared` (Thư viện dùng chung)

Chứa các class mà cả Client và Server đều cần biết để hiểu nhau.

- **Protocol/Packet.cs**: Định nghĩa gói tin giao tiếp.
  - `PacketHeader`: Cấu trúc cố định (Type, Length) giúp tách luồng byte thành các gói tin riêng biệt.
  - `PacketType`: Enum liệt kê các loại lệnh (ScreenFrame, MouseEvent, Login...).
- **DTOs/**: Các Data Transfer Object (dữ liệu được đóng gói).
  - `ScreenFrameDto`: Chứa dữ liệu ảnh, tọa độ cắt (Dirty Rect) để tối ưu băng thông.
  - `LoginRequestDto`: Chứa Username và Hash Password.

### 2.2 `remoteServer` (Ứng dụng Server)

Đây là trái tim của hệ thống, chứa các logic xử lý phức tạp nhất.

- **server.cs**: Giao diện chính (Form UI).
  - Khởi tạo `AsyncTcpListener`.
  - Nút bấm: Cấu hình DB, Đăng ký (Admin), Gửi File.
- **Network/ClientSession.cs**: _Quan trọng nhất_.
  - Đại diện cho một kết nối từ Client.
  - Quản lý vòng đời: Handshake (RSA/AES) -> Login -> Loop nhận lệnh -> Disconnect.
  - Chứa luồng `SendScreenUpdatesAsync` để gửi màn hình liên tục.
- **Services/ScreenCaptureService.cs**:
  - Dùng `GDI+` để chụp màn hình.
  - **Dirty Rect Algorithm**: So sánh ảnh hiện tại với ảnh trước đó, chỉ gửi phần hình chữ nhật thay đổi -> Giảm băng thông từ 5MB/s xuống 50KB/s khi ít chuyển động.
  - Nén JPEG (Quality 40-65%) để cân bằng tốc độ/chất lượng.
- **Services/DatabaseService.cs**:
  - Kết nối SQL Server (ADO.NET).
  - Xử lý Login (Validate User), Register, Log Session Time.
- **Services/InputSimulatorService.cs**:
  - Dùng `mouse_event` và `keybd_event` (Win32 API) để điều khiển máy tính thật dựa trên lệnh nhận được.

### 2.3 `remoteClient` (Ứng dụng Client)

Giao diện phía người điều khiển.

- **client.cs**:
  - `PictureBox`: Hiển thị màn hình Server (dùng `Double Buffering` để tránh nhấp nháy).
  - `GetImageDisplayRectangle`: Tính toán tọa độ chuột chính xác ngay cả khi cửa sổ bị Resize (Zoom).
  - Bắt sự kiện Mouse/Key -> Gửi `InputEventDto` lên Server.
- **Network/ClientConnection.cs**:
  - Quản lý socket TCP kết nối đến Server.
  - Xử lý xác thực SSL và mã hóa AES.

---

## 3. Hướng Dẫn Đọc Code (Code Reading Path)

Để hiểu rõ dự án, bạn nên đọc code theo trình tự logic sau:

### Bước 1: Hiểu cách dữ liệu di chuyển (Protocol)

1.  Xem `Shared/Protocol/Packet.cs`: Để biết cấu trúc gói tin (Header + Payload).
2.  Xem `Shared/Security/SecurityHelper.cs`: Để hiểu cách mã hóa RSA/AES hoạt động.

### Bước 2: Luồng kết nối & Xác thực (Handshake)

1.  Mở `remoteServer/Network/ClientSession.cs` -> Hàm `ProcessAsync`.
    - Đọc đoạn `SendPublicKeyAsync` (Bước 1).
    - Đọc đoạn nhận `HandshakeDto` (Bước 2 - Trao đổi khóa AES).
2.  Mở `remoteClient/Network/ClientConnection.cs` -> Hàm `ConnectAsync` và `HandshakeAsync`.

### Bước 3: Luồng Chụp & Gửi Màn Hình (Core Feature)

1.  Server: `ClientSession.cs` -> `SendScreenUpdatesAsync`.
    - Gọi `_screenCapture.CaptureScreen`.
    - Đóng gói `ScreenFrameDto` -> Gửi đi.
2.  Server: `ScreenCaptureService.cs`.
    - Xem cách so sánh pixel (`LockBits`, `unsafe`) để tìm vùng thay đổi.
3.  Client: `client.cs` -> `HandleScreenFrame`.
    - Xem cách vẽ đè bản vá (Chunk) lên `_backBuffer` để tái tạo màn hình.

### Bước 4: Luồng Điều Khiển (Input)

1.  Client: `client.cs` -> `PbScreen_MouseMove/MouseDown`.
    - Chuyển đổi tọa độ chuột sang tỉ lệ % (0.0 - 1.0).
2.  Server: `InputSimulatorService.cs`.
    - Nhân tỉ lệ % với độ phân giải màn hình thật -> Gọi hàm API Windows.

---

## 4. Các Vấn Đề Kỹ Thuật Đã Giải Quyết

Trong quá trình phát triển, các vấn đề sau đã được xử lý:

1.  **Lag/Delay**:
    - _Giải pháp_: Dùng thuật toán **Dirty Rect** (chỉ gửi phần thay đổi) thay vì gửi toàn bộ màn hình.
    - _Giải pháp_: Giảm chất lượng JPEG xuống mức hợp lý.
    - _Giải pháp_: Dùng `UDP` (cân nhắc) hoặc tối ưu TCP `NoDelay`.

2.  **High DPI / Màn hình bị cắt**:
    - _Vấn đề_: Windows Scale 125% làm tọa độ chuột bị sai lệch.
    - _Giải pháp_: Bật chế độ `PerMonitorV2` trong `app.manifest` (.csproj) để ứng dụng nhận diện đúng pixel vật lý.

3.  **Treo giao diện (UI Freeze)**:
    - _Giải pháp_: Toàn bộ thao tác Mạng và Database đều chạy `async/await` hoặc `Task.Run` trên luồng nền, không chặn Main UI Thread.

---

## 5. Hướng Dẫn Mở Rộng (Future Work)

Nếu muốn phát triển tiếp, bạn có thể:

- [ ] **Audio Streaming**: Gửi âm thanh từ Server về Client (dùng NAudio).
- [ ] **Clipboard Sharing**: Copy text ở máy này paste sang máy kia.
- [ ] **Web Client**: Viết thêm Client chạy trên trình duyệt (dùng WebSocket).

---

_Created with ❤️ by Antigravity AI_
