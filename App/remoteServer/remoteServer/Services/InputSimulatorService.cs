using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Shared.DTO;

namespace remoteServer.Services
{
    /// <summary>
    /// Service chịu trách nhiệm giả lập thao tác Chuột và Bàn phím lên hệ điều hành Windows.
    /// Sử dụng thư viện user32.dll (Win32 API).
    /// </summary>
    public class InputSimulatorService
    {
        // Import hàm API để điều khiển chuột
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        
        // Import hàm API để điều khiển bàn phím
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // Các cờ (Flags) quy định hành động chuột
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;   // Nhấn chuột trái
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;     // Nhả chuột trái
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;  // Nhấn chuột phải
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;    // Nhả chuột phải
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020; // Nhấn chuột giữa
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;   // Nhả chuột giữa
        
        private const uint MOUSEEVENTF_MOVE = 0x0001;       // Di chuyển chuột
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;   // Tọa độ tuyệt đối (Toàn màn hình)

        private const uint KEYEVENTF_KEYUP = 0x0002;        // Nhả phím

        /// <summary>
        /// Xử lý một sự kiện đầu vào từ Client gửi lên.
        /// </summary>
        public void ProcessInput(InputEventDto input)
        {
            // Chuyển đổi tọa độ chuẩn hóa (0.0 - 1.0) sang tọa độ tuyệt đối của màn hình (0 - 65535)
            // Cách này giúp chuột hiển thị đúng vị trí bất kể độ phân giải của Server là bao nhiêu (DPI Independent)
            uint absX = (uint)(input.X * 65535);
            uint absY = (uint)(input.Y * 65535);

            switch (input.Type)
            {
                case InputType.MouseMove:
                    mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, absX, absY, 0, 0);
                    break;
                    
                case InputType.MouseDown:
                    // Di chuyển chuột đến vị trí trước khi click để đảm bảo chính xác
                    mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, absX, absY, 0, 0);
                    if (input.Button == 0) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN, absX, absY, 0, 0);   // Trái
                    if (input.Button == 1) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN, absX, absY, 0, 0);  // Phải
                    if (input.Button == 2) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MIDDLEDOWN, absX, absY, 0, 0); // Giữa
                    break;

                case InputType.MouseUp:
                    mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, absX, absY, 0, 0);
                    if (input.Button == 0) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP, absX, absY, 0, 0);
                    if (input.Button == 1) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTUP, absX, absY, 0, 0);
                    if (input.Button == 2) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MIDDLEUP, absX, absY, 0, 0);
                    break;

                case InputType.KeyDown:
                    keybd_event((byte)input.KeyCode, 0, 0, 0);
                    break;

                case InputType.KeyUp:
                    // Cờ KEYEVENTF_KEYUP báo hiệu là nhả phím
                    keybd_event((byte)input.KeyCode, 0, KEYEVENTF_KEYUP, 0);
                    break;
            }
        }
    }
}
