using System;
using System.Runtime.InteropServices;
using remoteServer.Shared;

namespace remoteServer.Utils
{
    // Ti?n ích mô ph?ng input trên máy local (máy b? ?i?u khi?n)
    // L?u ý: s? d?ng P/Invoke ?? t??ng tác v?i Win32 API.
    public static class InputSimulator
    {
        // Mouse flags
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        // Keyboard flags
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Th?c hi?n mô ph?ng Input d?a vào InputEventDto
        public static void SimulateInput(InputEventDto evt)
        {
            if (evt == null) return;

            try
            {
                if (evt.Type == InputType.Mouse)
                {
                    // Di chuy?n con tr?
                    SetCursorPos(evt.X, evt.Y);

                    // N?u KeyCode dùng ?? ch? lo?i click: 1 = left, 2 = right
                    if (evt.KeyCode == 1)
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)evt.X, (uint)evt.Y, 0, UIntPtr.Zero);
                        mouse_event(MOUSEEVENTF_LEFTUP, (uint)evt.X, (uint)evt.Y, 0, UIntPtr.Zero);
                    }
                    else if (evt.KeyCode == 2)
                    {
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)evt.X, (uint)evt.Y, 0, UIntPtr.Zero);
                        mouse_event(MOUSEEVENTF_RIGHTUP, (uint)evt.X, (uint)evt.Y, 0, UIntPtr.Zero);
                    }
                }
                else if (evt.Type == InputType.Keyboard)
                {
                    // KeyCode ???c dùng nh? mã virtual key (VK)
                    byte vk = (byte)evt.KeyCode;
                    // key down
                    keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    // key up
                    keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SimulateInput error: " + ex);
            }
        }
    }
}
