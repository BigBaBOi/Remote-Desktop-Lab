using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace remoteServer.Services
{
    /// <summary>
    /// Service chụp màn hình và nén ảnh để gửi đi.
    /// </summary>
    public class ScreenCaptureService
    {
        private Rectangle _bounds;
        private ImageCodecInfo _jpegCodec;
        private EncoderParameters _encoderParams;

        public ScreenCaptureService()
        {
            // Mặc định chụp màn hình chính
            _bounds = Screen.PrimaryScreen.Bounds;

            // Tìm Codec cho JPEG và cấu hình tham số nén một lần để tối ưu hiệu năng
            _jpegCodec = GetEncoderInfo("image/jpeg");
            _encoderParams = new EncoderParameters(1);
            // Thiết lập chất lượng ảnh là 60% để giảm băng thông mạng
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 60L);
        }

        /// <summary>
        /// Chụp màn hình hiện tại, nén thành JPEG byte array.
        /// </summary>
        /// <param name="width">Trả về chiều rộng ảnh</param>
        /// <param name="height">Trả về chiều cao ảnh</param>
        /// <returns>Mảng byte của ảnh JPEG</returns>
        public byte[] CaptureScreen(out int width, out int height)
        {
            width = _bounds.Width;
            height = _bounds.Height;

            try
            {
                // Tạo Bitmap chứa ảnh chụp màn hình
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    // Tạo đối tượng Graphics từ Bitmap để thực hiện thao tác vẽ/copy
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        // Copy toàn bộ dữ liệu từ màn hình vào Bitmap
                        g.CopyFromScreen(_bounds.Location, Point.Empty, _bounds.Size);
                    }

                    // Lưu ảnh vào MemoryStream để chuyển thành byte array
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (_jpegCodec != null)
                        {
                            // Lưu với định dạng JPEG và chất lượng đã cấu hình (60%)
                            bitmap.Save(ms, _jpegCodec, _encoderParams);
                        }
                        else
                        {
                            // Fallback nếu không tìm thấy codec (hiếm khi xảy ra)
                            bitmap.Save(ms, ImageFormat.Jpeg);
                        }
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi chụp màn hình: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Tìm ImageCodecInfo dựa trên MimeType (ví dụ: "image/jpeg")
        /// </summary>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
