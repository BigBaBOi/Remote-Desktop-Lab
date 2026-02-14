# Remote Desktop Application (C# WinForms)

Dự án phần mềm điều khiển máy tính từ xa (Remote Desktop) được xây dựng bằng C# .NET 8, WinForms, và SQL Server. Hệ thống hỗ trợ xem màn hình, điều khiển chuột/phím, truyền file và bảo mật cao.

## 🚀 Tính năng đã triển khai

### 1. Core Networking (Lớp Mạng & Giao tiếp)

- **Kiến trúc Client-Server**: Sử dụng `TcpListener` và `TcpClient` với mô hình bất đồng bộ (`async`/`await`) để đảm bảo hiệu năng cao, không gây treo giao diện (Non-blocking I/O).
- **Protocol tùy chỉnh**: Giao thức gói tin (Packet) binary được tối ưu hóa:
  - `Header` (Cố định): Loại gói tin, kích thước payload.
  - `Payload` (Linh hoạt): Dữ liệu được tuần tự hóa (Serialize) từ các DTO (Data Transfer Object).
- **TCP Socket**: Kết nối ổn định, tin cậy.

### 2. Bảo mật (Security Layer)

Hệ thống áp dụng mô hình bảo mật nhiều lớp (Defense in Depth):

- **SSL/TLS Encryption**: Toàn bộ luồng kết nối được bọc trong `SslStream` (sử dụng chứng chỉ X.509 tự ký) để chống nghe lén (Man-in-the-middle).
- **Application Layer Encryption**:
  - **Handshake**: Sử dụng **RSA** để trao đổi khóa phiên (Session Key) an toàn khi vừa kết nối.
  - **Data Encryption**: Toàn bộ dữ liệu payload (Màn hình, Input, File) được mã hóa **AES-256** trước khi gửi đi.

### 3. Remote Desktop (Điều khiển từ xa)

- **Screen Capture (Server)**:
  - Chụp màn hình liên tục (~10-20 FPS).
  - Nén ảnh định dạng **JPEG** (Quality 60%) để giảm băng thông mạng.
  - Gửi dữ liệu qua gói tin `ScreenFrameDto`.
- **Remote Control (Client -> Server)**:
  - Client bắt sự kiện chuột (Move, Click) và bàn phím.
  - Tự động tính toán tỷ lệ (Zoom) khi kích thước cửa sổ Client khác với màn hình Server.
  - Server sử dụng `user32.dll` (Windows API) để giả lập thao tác chuột/phím thực tế.

### 4. Truyền File (File Transfer)

- Hỗ trợ gửi file từ Client lên Server.
- Cơ chế **Chunking**: File được chia nhỏ thành các gói 4KB để gửi, tránh nghẽn mạng và cho phép theo dõi tiến độ.
- File nhận được lưu tại thư mục `ReceivedFiles` trên Server.

### 5. Quản lý & Logging

- **SQL Server**: Lưu trữ thông tin người dùng (`Users`) và lịch sử kết nối (`SessionHistory`).
- **Session Logging**: Ghi nhận thời gian bắt đầu và kết thúc của mỗi phiên điều khiển.

---

## 📂 Cấu trúc Dự án

Solution bao gồm 3 Project chính:

### 1. `Shared` (Class Library)

Chứa các thành phần dùng chung cho cả Client và Server:

- **DTOs**: Các lớp dữ liệu (`LoginRequestDto`, `ScreenFrameDto`, `InputEventDto`, `FileChunkDto`).
- **Protocol**: Định nghĩa `PacketType`, cấu trúc `PacketHeader`.
- **Security**: Các lớp Helper cho RSA, AES, Certificate.
- **Utils**: Serialization Helper.

### 2. `remoteServer` (Windows Forms App)

Ứng dụng chạy trên máy bị điều khiển (Host):

- **Network/AsyncTcpListener**: Lắng nghe kết nối đến.
- **Network/ClientSession**: Quản lý phiên làm việc của từng Client (Handshake, nhận lệnh, gửi màn hình).
- **Services/ScreenCaptureService**: Chụp và nén màn hình.
- **Services/InputSimulatorService**: Giả lập chuột/phím (P/Invoke `user32.dll`).
- **Services/DatabaseService**: Tương tác với SQL Server.

### 3. `remoteClient` (Windows Forms App)

Ứng dụng chạy trên máy điều khiển (Guest):

- **Network/ClientConnection**: Kết nối đến Server, xử lý SSL và giải mã dữ liệu.
- **UI/client.cs**: Giao diện hiển thị màn hình remote, bắt sự kiện Input, nút Connect/Gửi File.

---

## 🛠️ Hướng dẫn Cài đặt & Chạy

### Yêu cầu hệ thống

- Windows 10/11.
- .NET 8 SDK.
- SQL Server (LocalDB hoặc Full).

### Bước 1: Thiết lập Cơ sở dữ liệu

1.  Mở SQL Server Management Studio (SSMS).
2.  Chạy script `setup_database.sql` (nằm trong thư mục `remoteServer`) để tạo DB `RemoteDesktopDB` và bảng `Users`.
3.  Tài khoản mặc định: `admin` / `admin123`.

### Bước 2: Build Ứng dụng

Mở Terminal tại thư mục gốc và chạy:

```powershell
dotnet build
```

### Bước 3: Chạy Server

1.  Vào thư mục: `remoteServer/bin/Debug/net8.0-windows`
2.  Chạy `remoteServer.exe`.
3.  Server sẽ tự động tạo chứng chỉ SSL (`server.pfx`) nếu chưa có và bắt đầu lắng nghe cổng `8888`.

### Bước 4: Chạy Client

1.  Vào thư mục: `remoteClient/bin/Debug/net8.0-windows`
2.  Chạy `remoteClient.exe`.
3.  Nhập IP Server (mặc định `127.0.0.1` nếu chạy local).
4.  Nhập User: `admin`, Pass: `admin123`.
5.  Nhấn **Connect**.

---

## 👨‍💻 Công nghệ sử dụng

- **Language**: C# 12 / .NET 8
- **GUI**: Windows Forms
- **Database**: SQL Server (ADO.NET)
- **Network**: System.Net.Sockets, SslStream
- **Security**: System.Security.Cryptography (RSA, AES, X509Certificates)
- **Win32 API**: user32.dll (cho Input Simulation)
