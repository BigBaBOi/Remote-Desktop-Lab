<div style="font-family: 'Segoe UI', Tahoma, Arial, sans-serif; font-size:14px; line-height:1.5;">

# TODO (Checklist)

Tập hợp các việc cần làm (ưu tiên) dựa trên `README.md` và `HUONG_DAN_CAI_DAT_VA_TEST.md`.

---

## P0 - Khẩn cấp
- [x] Hiển thị cảnh báo khi MySQL không chạy (Đã implement MessageBox trên `server.Load`).
  - [x] Thêm nút `Mở Cấu hình Database` trong hộp cảnh báo để mở `DatabaseConfigForm`.
  - [x] Thêm nút `Thử lại` (Retry) trong hộp cảnh báo để kiểm tra lại kết nối MySQL ngay lập tức.
  - [x] Đảm bảo các hành động không block UI (kêu gọi kiểm tra async và cập nhật trạng thái khi hoàn thành).
- [x] Loại bỏ mọi output ra `Console` trong toàn bộ solution.
  - [x] Tìm và chuyển mọi `Console.WriteLine`/`Console.Write` sang logging file hoặc MessageBox tùy ngữ cảnh.

## P1 - Server / DB
- [x] Cải thiện `DatabaseService`.
  - [x] Tách khởi tạo khỏi constructor UI: tạo phương thức `InitializeAsync()` thay vì `new DatabaseService()` trong UI thread.
  - [x] Triển khai cơ chế retry/backoff (exponential backoff) để tự phục hồi `IsDatabaseAvailable` khi MySQL tái khởi động.
  - [x] Ghi log lỗi kết nối vào file `Logs/server.log` (với rolling file hoặc append đơn giản).
  - [x] Thêm trạng thái trong UI (icon/label) hiển thị `Database: Online/Offline` và thời gian kiểm tra cuối cùng.
- [x] Thêm script cài DB `setup_database.sql` vào thư mục `Data` và cập nhật README nếu cần.
  - [x] Kiểm tra script hiện tại (`setup_database.sql`) tồn tại và copy vào `remoteServer/Data/setup_database.sql`.
  - [x] Cập nhật `HUONG_DAN_CAI_DAT_VA_TEST.md` để tham chiếu đúng đường dẫn script.

## P1 - UI / UX
- [x] Sửa `ListBox` hiển thị client.
  - [x] Override `ClientSession.ToString()` để trả về `"{Username} ({ClientIP}) - {Status}"` hoặc
  - [x] Hoặc sử dụng `ListBox.DisplayMember` với wrapper object có thuộc tính hiển thị.
- [ ] Dọn dẹp các warning nullability trong dự án.
  - [ ] Chạy build, liệt kê warnings, và sửa từng cảnh báo theo ưu tiên (constructor, events, nullable fields).
- [ ] Khi gửi/nhận file hiển thị progress.
  - [x] Thêm ProgressBar trên Server UI khi gửi file cho Client.
  - [ ] Thêm ProgressBar trên Client UI khi nhận file từ Server.
  - [x] Gửi `FileMeta`/`FileChunk` DTO có thông tin `TotalChunks`/`BytesSent` để UI cập nhật.

## P2 - Network / Protocol
- [ ] Viết unit test cho `PacketHeader` packing/unpacking.
  - [ ] Test trường hợp big-endian/little-endian và kích thước header.
- [x] Kiểm tra toàn bộ I/O mạng đảm bảo `async/await` (non-blocking).
  - [x] Audit `Read`/`Write` trong `ClientSession`, `ClientConnection`, `AsyncTcpListener`.
  - [x] Thay mọi blocking API bằng async equivalents.
- [x] Thêm timeout và retry cho handshake/login.
  - [x] Đặt timeout hợp lý (5s-10s) cho handshake;
  - [x] Nếu handshake thất bại, thực hiện tối đa N retry với backoff trước khi ngắt kết nối.

## P2 - Security
- [x] Implement CA nội bộ (Server cấp cert tự ký bằng CA riêng).
  - [x] Tạo helper để khởi tạo CA key/cert và ghi ra `Data/certs`.
  - [x] Tạo và lưu TLS certificate của Server, dùng cho `SslStream` khi AuthenticateAsServer.
- [x] Cập nhật `CertificateHelper.ValidateServerCertificate`.
  - [x] Không bypass validation trong release; chỉ accept certs ký bởi CA nội bộ.
- [ ] Thêm unit tests cho `SecurityHelper` (RSA/AES encrypt/decrypt).
  - [ ] Test encrypt->decrypt roundtrip for AES and RSA.

## P3 - Test cases / QA
- [ ] Tạo bộ test manual tự động hóa theo `HUONG_DAN_CAI_DAT_VA_TEST.md`:
  - [ ] Login: sai mật khẩu / mật khẩu đúng -> xác nhận behavior UI và DB.
  - [ ] Screen: đo latency, kiểm tra chất lượng ảnh, kiểm tra resize behavior.
  - [ ] Input: kiểm tra mapping chuột & phím (key codes, modifiers).
  - [ ] File transfer 2 chiều: kiểm tra integrity file (hash), resume behavior khi gián đoạn.
  - [ ] Register account: tạo user mới và thử đăng nhập.
- [ ] Thiết lập CI (GitHub Actions):
  - [ ] Build solution (.NET 8)
  - [ ] Chạy unit tests
  - [ ] (Tùy chọn) Chạy static code analysis/formatting

## P4 - Nâng cao (tùy chọn)
- [ ] Resume upload/download file khi gián đoạn.
  - [x] Thiết kế `FileMeta` DTO chứa `FileId`, `TotalSize`, `ChunkSize`, `Checksum`.
  - [ ] Hỗ trợ resume bằng `FileAck` với `LastReceivedChunkIndex`.
- [ ] Thống kê lịch sử kết nối, xuất CSV.
  - [ ] Thêm UI export CSV, endpoint lưu trữ log lịch sử.
- [ ] Thêm chế độ debug/verbose logging (chỉ bật khi cần).
  - [ ] Đọc thiết lập logging từ `appsettings` hoặc file config.

---

## Hướng dẫn sử dụng checklist
- Đánh dấu `[x]` khi hoàn thành mỗi mục.
- Với các task lớn, tạo issue trên Git và gắn tag `P0/P1/P2` tương ứng.
- Khi implement code, commit thay đổi kèm mô tả ngắn và tham chiếu issue.

</div>

