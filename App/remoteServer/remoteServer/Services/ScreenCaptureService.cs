using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Shared.Utils;

namespace remoteServer.Services
{
    /// <summary>
    /// Service chịu trách nhiệm chụp màn hình, phát hiện thay đổi (Dirty Rect), và nén ảnh để gửi đi.
    /// Tối ưu hóa: High Performance, Low Bandwidth, Differential Update.
    /// </summary>
    public class ScreenCaptureService
    {
        #region Fields & Constants

        // Giới hạn độ phân giải (Cân bằng giữa Chất lượng và Tốc độ)
        private const int MAX_WIDTH = 1600;
        private const int MAX_HEIGHT = 900;

        private Rectangle _bounds; // Kích thước màn hình
        private ImageCodecInfo _jpegCodec; // Bộ mã hóa JPEG
        private EncoderParameters _encoderParams; // Tham số nén ảnh
        private Bitmap _prevBitmap; // Lưu frame trước đó để so sánh sự thay đổi

        #endregion

        #region Constructor

        public ScreenCaptureService()
        {
            _bounds = Screen.PrimaryScreen.Bounds;
            _jpegCodec = GetEncoderInfo("image/jpeg");
            _encoderParams = new EncoderParameters(1);

            // Thiết lập chất lượng ảnh JPEG là 65% (Đủ rõ nét cho văn bản, nhưng vẫn nhẹ)
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 65L);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Chụp màn hình, xử lý resize, so sánh thay đổi và trả về dữ liệu ảnh nén.
        /// </summary>
        /// <param name="width">Chiều rộng của ảnh trả về (hoặc của chunk thay đổi).</param>
        /// <param name="height">Chiều cao của ảnh trả về (hoặc của chunk thay đổi).</param>
        /// <param name="left">Tọa độ X của vùng thay đổi.</param>
        /// <param name="top">Tọa độ Y của vùng thay đổi.</param>
        /// <param name="totalW">Chiều rộng tổng thể của màn hình gốc.</param>
        /// <param name="totalH">Chiều cao tổng thể của màn hình gốc.</param>
        /// <returns>Mảng byte ảnh JPEG, hoặc null nếu không có sự thay đổi đáng kể.</returns>
        public byte[] CaptureScreen(out int width, out int height, out int left, out int top, out int totalW, out int totalH)
        {
            // Cập nhật kích thước màn hình thực tế (đề phòng thay đổi độ phân giải lúc chạy)
            _bounds = Screen.PrimaryScreen.Bounds;

            // Khởi tạo giá trị mặc định
            left = 0; top = 0; width = 0; height = 0; totalW = 0; totalH = 0;

            try
            {
                // 1. Chụp màn hình hiện tại
                // Sử dụng Format32bppPArgb để tương thích tốt nhất với JPEG encoder và tốc độ GDI+ nhanh
                Bitmap currentBitmap = new Bitmap(_bounds.Width, _bounds.Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(currentBitmap))
                {
                    g.CopyFromScreen(_bounds.Location, Point.Empty, _bounds.Size);
                }

                // 2. Bỏ logic Resize (Theo yêu cầu User: Giữ nguyên độ phân giải gốc để nét nhất)
                int finalW = _bounds.Width;
                int finalH = _bounds.Height;
                Bitmap processedBitmap = currentBitmap;

                // 4. Phát hiện sự thay đổi (Differential Update - Dirty Rect)
                Rectangle dirtyRect = new Rectangle(0, 0, finalW, finalH);
                bool isFullUpdate = true;

                if (_prevBitmap != null && _prevBitmap.Width == finalW && _prevBitmap.Height == finalH)
                {
                    dirtyRect = GetBoundingBox(processedBitmap, _prevBitmap);

                    // Nếu không có thay đổi gì -> Trả về null để tiết kiệm băng thông tuyệt đối
                    if (dirtyRect.Width == 0 || dirtyRect.Height == 0)
                    {
                        processedBitmap.Dispose();
                        return null;
                    }

                    // Nếu vùng thay đổi nhỏ hơn toàn màn hình -> Chỉ gửi phần thay đổi (Partial Update)
                    if (dirtyRect.Width < finalW || dirtyRect.Height < finalH)
                    {
                        isFullUpdate = false;
                    }
                }

                // Cập nhật _prevBitmap cho lần sau so sánh
                if (_prevBitmap != null) _prevBitmap.Dispose();
                _prevBitmap = (Bitmap)processedBitmap.Clone();

                // 5. Chuẩn bị ảnh để gửi đi
                Bitmap bitmapToSend = processedBitmap;
                if (!isFullUpdate)
                {
                    // Cắt (Crop) lấy vùng thay đổi
                    bitmapToSend = processedBitmap.Clone(dirtyRect, processedBitmap.PixelFormat);
                    left = dirtyRect.X;
                    top = dirtyRect.Y;
                }
                else
                {
                    // Gửi toàn bộ
                    left = 0; top = 0;
                }

                // Thiết lập các tham số đầu ra
                width = dirtyRect.Width;
                height = dirtyRect.Height;
                totalW = finalW;
                totalH = finalH;

                // 6. Nén ảnh thành JPEG và trả về byte[]
                using (MemoryStream ms = new MemoryStream())
                {
                    if (_jpegCodec != null)
                        bitmapToSend.Save(ms, _jpegCodec, _encoderParams);
                    else
                        bitmapToSend.Save(ms, ImageFormat.Jpeg);

                    // Giải phóng tài nguyên
                    if (bitmapToSend != processedBitmap) bitmapToSend.Dispose();
                    processedBitmap.Dispose();

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[ScreenCapture] Lỗi: {ex.Message}");
                width = 0; height = 0;
                return Array.Empty<byte>();
            }
        }

        // Tạm giữ overload cũ để tương thích ngược (nếu còn chỗ nào gọi)
        public void SetTargetResolution(int width, int height) { }
        public byte[] CaptureScreen(out int width, out int height)
        {
            return CaptureScreen(out width, out height, out int l, out int t, out int tw, out int th);
        }

        /// <summary>
        /// Chụp toàn bộ màn hình (bỏ qua dirty rect detection) để dùng cho keepalive frame.
        /// Cập nhật _prevBitmap để dirty rect lần sau vẫn hoạt động đúng.
        /// </summary>
        public byte[] CaptureFullFrame(out int width, out int height, out int totalW, out int totalH)
        {
            _bounds = Screen.PrimaryScreen?.Bounds ?? _bounds;
            totalW = width = _bounds.Width;
            totalH = height = _bounds.Height;

            try
            {
                Bitmap bmp = new Bitmap(_bounds.Width, _bounds.Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(_bounds.Location, Point.Empty, _bounds.Size);

                // Cập nhật _prevBitmap để dirty rect tiếp theo so sánh đúng với frame này
                _prevBitmap?.Dispose();
                _prevBitmap = (Bitmap)bmp.Clone();

                using (var ms = new MemoryStream())
                {
                    if (_jpegCodec != null) bmp.Save(ms, _jpegCodec, _encoderParams);
                    else bmp.Save(ms, ImageFormat.Jpeg);
                    bmp.Dispose();
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[ScreenCapture] CaptureFullFrame lỗi: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// So sánh hai Bitmap bằng con trỏ (Unsafe) để tìm vùng chữ nhật thay đổi nhỏ nhất.
        /// </summary>
        private unsafe Rectangle GetBoundingBox(Bitmap current, Bitmap prev)
        {
            if (current.Size != prev.Size) return new Rectangle(0, 0, current.Width, current.Height);

            int w = current.Width;
            int h = current.Height;
            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool changed = false;

            // LockBits để truy cập trực tiếp vào bộ nhớ pixel (nhanh hơn GetPixel rất nhiều)
            BitmapData dataCur = current.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, current.PixelFormat);
            BitmapData dataPrev = prev.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, prev.PixelFormat);

            int bytesPerPixel = 4; // 32bpp = 4 bytes/pixel
            int stride = dataCur.Stride;

            // Con trỏ tới dòng đầu tiên
            byte* scan0Cur = (byte*)dataCur.Scan0.ToPointer();
            byte* scan0Prev = (byte*)dataPrev.Scan0.ToPointer();

            // Duyệt qua các dòng (nhảy cóc 4 dòng mỗi lần để tăng tốc độ quét)
            for (int y = 0; y < h; y += 4)
            {
                byte* rowCur = scan0Cur + (y * stride);
                byte* rowPrev = scan0Prev + (y * stride);

                // Duyệt qua các cột (nhảy cóc 4 pixel mỗi lần)
                for (int x = 0; x < w; x += 4)
                {
                    // So sánh giá trị pixel (uint vì là 32-bit color)
                    uint p1 = *((uint*)(rowCur + x * bytesPerPixel));
                    uint p2 = *((uint*)(rowPrev + x * bytesPerPixel));

                    if (p1 != p2)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        changed = true;
                    }
                }
            }

            current.UnlockBits(dataCur);
            prev.UnlockBits(dataPrev);

            if (!changed) return Rectangle.Empty;

            // Mở rộng vùng phát hiện ra một chút (Padding) để đảm bảo bao phủ cả các pixel bị nhảy cóc
            minX = Math.Max(0, minX - 4);
            minY = Math.Max(0, minY - 4);
            maxX = Math.Min(w, maxX + 4);
            maxY = Math.Min(h, maxY + 4);

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType) return codec;
            }
            return null;
        }

        #endregion
    }
}
