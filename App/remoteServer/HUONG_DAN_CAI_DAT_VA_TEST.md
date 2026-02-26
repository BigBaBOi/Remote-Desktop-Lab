# HƯỚNG DẪN CÀI ĐẶT VÀ KIỂM THỬ (TESTING) CHI TIẾT

## Remote Desktop Application (C# .NET 8)

Tài liệu này hướng dẫn chi tiết từng bước từ cài đặt môi trường, cấu hình Database, đến cách chạy và kiểm thử các tính năng của ứng dụng.

---

## 1. Yêu Cầu Hệ Thống

Trước khi bắt đầu, đảm bảo máy tính của bạn đã cài đặt:

1.  **Windows 10/11** (64-bit).
2.  **.NET Desktop Runtime 8.0** (Để chạy ứng dụng).
    - Tải tại: [https://dotnet.microsoft.com/en-us/download/dotnet/8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
3.  **MySQL Server** (XAMPP, WAMP, hoặc MySQL Workbench đều được).
4.  **Công cụ quản lý MySQL** (phpMyAdmin, MySQL Workbench, DBeaver, v.v... để tương tác với Database).

---

## 2. Phần 1: Cài Đặt Database (MySQL)

Ứng dụng cần Database để lưu trữ tài khoản người dùng và lịch sử đăng nhập.

**Bước 1:** Mở **công cụ quản lý MySQL** của bạn (ví dụ: phpMyAdmin tại `http://localhost/phpmyadmin/` nếu dùng XAMPP) và kết nối vào MySQL Server của bạn (thường thông qua user `root`, mật khẩu rỗng).

**Bước 2:** Chạy Script tạo Database.

- Tìm đến thư mục source code, copy nội dung của file cấu hình: `Remote-Desktop-Lab\App\remoteServer\Data\setup_database.sql`
- Hoặc copy nội dung dưới đây và dán vào cửa sổ **SQL Query** của công cụ quản lý:

```sql
-- Create Database
CREATE DATABASE IF NOT EXISTS RemoteDesktopDB;
USE RemoteDesktopDB;

-- Create Users Table
CREATE TABLE IF NOT EXISTS Users (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL, -- SHA256
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Insert Default Admin User (Password: admin123)
-- Hash: 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9 (SHA256 of 'admin123')
INSERT IGNORE INTO Users (Username, PasswordHash)
VALUES ('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9');

-- Create SessionHistory Table
CREATE TABLE IF NOT EXISTS SessionHistory (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL,
    ClientIP VARCHAR(50),
    StartTime DATETIME DEFAULT CURRENT_TIMESTAMP,
    EndTime DATETIME,
    FOREIGN KEY (Username) REFERENCES Users(Username)
);
```

**Bước 3:** Nhấn nút **Execute / Go** để chạy script.

- Không có lỗi hiện ra tức là đã thành công. Bảng `Users` và `SessionHistory` đã được tạo trong Database `RemoteDesktopDB`.

---

## 3. Phần 2: Cài Đặt & Chạy Server (Máy Điều Khiển)

Server là nơi nhận kết nối và quản lý dữ liệu.

**Bước 1:** Build và Chạy ứng dụng Server.

- Mở thư mục chứa file `remoteServer.exe` (thường trong `bin\Debug\net8.0-windows`).
- Chạy `remoteServer.exe` (Run as Administrator để đảm bảo quyền mạng).

**Bước 2:** Cấu hình kết nối Database.

- Trên giao diện Server, nhấn nút **"Cấu hình Database"**.
- Nhập thông tin MySQL Server của bạn:
  - **IP Máy SQL**: `127.0.0.1` (hoặc `localhost`).
  - **Tên Database**: `RemoteDesktopDB` (giữ nguyên).
  - **User (root)**: `root` (mặc định của XAMPP/WAMP thường là root).
  - **Mật khẩu**: (Bỏ trống hoặc nhập mật khẩu root của bạn).
- Nhấn **"Lưu & Kết nối"**. Nếu không báo lỗi pop-up nào và tự đóng bảng thì kết nối đã thành công.

**Bước 3:** Đăng ký tài khoản cho Client (Tùy chọn).

- Tài khoản mặc định có sẵn: User: `admin`, Pass: `admin123`.
- Để tạo thêm, nhấn nút **"Đăng ký Tài khoản Mới"** trên Server -> Nhập User/Pass -> Tạo.

**Bước 4:** Mở Port Firewall (Nếu chạy khác máy).

- Nếu Client và Server khác máy, bạn cần mở Port **8888 (TCP)** trên Firewall của máy Server.
- Cách nhanh nhất để test LAN: Tắt tạm thời Firewall (Private Network).

---

## 4. Phần 3: Cài Đặt & Chạy Client (Máy Bị Điều Khiển)

Client là máy sẽ gửi màn hình về cho Server xem.

**Bước 1:** Chạy ứng dụng Client.

- Mở folder chứa `remoteClient.exe`.
- Chạy `remoteClient.exe`.

**Bước 2:** Nhập thông tin kết nối.

- **IP**: Nhập địa chỉ IP của máy Server (Ví dụ: `192.168.1.100`).
  - _Mẹo: Trên máy Server, mở CMD gõ `ipconfig` để xem IPv4 Address._
- **User**: `admin`
- **Pass**: `admin123`

**Bước 3:** Kết nối.

- Nhấn nút **"Kết nối"**.
- Trạng thái sẽ chuyển từ "Sẵn sàng" -> "Đang kết nối..." -> "Đang đăng nhập..." -> **"Đang nhận màn hình..."**.
- Nếu thành công, khung hình đen sẽ hiện ra màn hình của Server (chính là màn hình máy đang chạy Client nếu test trên 1 máy, cẩn thận hiệu ứng gương vô tận!).

---

## 5. Phần 4: Kịch Bản Kiểm Thử (Test Cases)

Hãy thực hiện lần lượt các bài test sau để đảm bảo ứng dụng hoạt động tốt.

### Test Case 1: Đăng Nhập

1.  **Nhập sai Pass**: Nhập User `admin`, Pass `123`. Nhấn Kết nối.
    - _Kết quả mong đợi_: Báo lỗi "Sai tên đăng nhập hoặc mật khẩu".
2.  **Nhập đúng Pass**: Nhập Pass `admin123`.
    - _Kết quả mong đợi_: Đăng nhập thành công, màn hình hiện lên.

### Test Case 2: Hiển Thị Màn Hình (Quan Trọng)

1.  **Độ sắc nét**: Quan sát chữ trên màn hình remote có đọc được không?
2.  **Độ trễ (Lag)**: Di chuột trên máy Client, xem trên Server có chạy theo ngay lập tức không? (Độ trễ chấp nhận được < 200ms trong mạng LAN).
3.  **Thay đổi kích thước**: Kéo giãn cửa sổ Client to/nhỏ.
    - _Kết quả mong đợi_: Hình ảnh tự động co giãn (Zoom) vừa khít cửa sổ, không bị mất góc, không bị đen quá nhiều.

### Test Case 3: Điều Khiển Chuột & Phím

1.  **Chuột Trái/Phải**: Click thử vào một icon hoặc mở menu chuột phải trên màn hình Client.
2.  **Gõ phím**: Mở Notepad trên máy Client. Từ máy Server (đang view Client? _Lưu ý: Ứng dụng này đang thiết kế là Server VIEW Client hay Client VIEW Server? Theo code hiện tại là Client kết nối đến Server và gửi màn hình Client cho Server xem -> Tức là Server theo dõi Client_).
    - _Đính chính_: Ứng dụng này là **Remote Desktop Viewer** chạy ở phía Client để điều khiển Server.
    - **Theo Code**:
      - `ClientSession.cs` (Server) có `SendScreenUpdatesAsync` -> **Server gửi màn hình cho Client**.
      - `client.cs` (Client) có `pbScreen` và gửi `InputEvent` -> **Client xem và điều khiển Server**.
    - **Vậy kịch bản đúng là**:
      - Bạn ngồi máy **Client**, bạn sẽ thấy màn hình của máy **Server**.
      - Test gõ phím từ Client -> Máy Server phải nhận được.

### Test Case 4: Truyền File (2 Chiều)

1.  **Client -> Server**:
    - Trên Client, nhấn nút **"Gửi File"**.
    - Chọn 1 file ảnh hoặc text nhỏ.
    - Trên Server, kiểm tra thư mục `ReceivedFiles` (Nhấn nút "Mở thư mục nhận File").
2.  **Server -> Client**:
    - Trên Server, chọn 1 dòng trong danh sách Client đang kết nối.
    - Nhấn **"Gửi File cho Client"**.
    - Trên Client, chờ nhận xong thông báo và mở folder `ReceivedFiles`.

### Test Case 5: Đăng Ký Tài Khoản (Admin)

1.  Trên Server, nhấn **"Đăng ký Tài khoản Mới"**.
2.  Tạo User: `testuser`, Pass: `123456`.
3.  Dùng Client đăng nhập bằng `testuser` / `123456`.
    - _Kết quả mong đợi_: Đăng nhập thành công.

---

## 6. Xử Lý Sự Cố Thường Gặp (Troubleshooting)

| Vấn đề                                     | Nguyên nhân & Khắc phục                                                                                                                |
| :----------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------- |
| **Không thể kết nối (Connection Timeout)** | - Sai IP Server. Kiểm tra lại `ipconfig`.<br>- Firewall chặn Port 8888. Tắt Firewall hoặc thêm Rule Inbound cho Port 8888 trên Server. |
| **Màn hình đen thui**                      | - Server chưa gửi được dữ liệu ảnh.<br>- Kiểm tra lại log trên Server xem có lỗi "CaptureScreen" không.                                |
| **Đăng nhập cứ báo sai**                   | - Kiểm tra Database đã có User chưa.<br>- Kiểm tra file `db_config.txt` trên Server xem đã trỏ đúng Database chưa.                     |
| **Hình ảnh bị vỡ/mờ**                      | - Đây là tính năng tối ưu. Chất lượng ảnh set ở mức 40-65% để đảm bảo tốc độ.                                                          |
| **Hình ảnh bị mất một góc**                | - Do High DPI. Hãy chắc chắn bạn đã build bản mới nhất có fix lỗi DPI (PerMonitorV2).                                                  |

---

_Tài liệu được tạo tự động bởi Trợ lý AI Antigravity - 19/02/2026_
