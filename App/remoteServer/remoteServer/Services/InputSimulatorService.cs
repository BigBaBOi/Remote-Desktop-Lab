using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Shared.DTO;

namespace remoteServer.Services
{
    public class InputSimulatorService
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        public void ProcessInput(InputEventDto input)
        {
            // Normalize Input (0.0 - 1.0) -> Absolute (0 - 65535)
            // This is DPI/Resolution Independent!
            uint absX = (uint)(input.X * 65535);
            uint absY = (uint)(input.Y * 65535);

            switch (input.Type)
            {
                case InputType.MouseMove:
                    mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, absX, absY, 0, 0);
                    break;
                    
                case InputType.MouseDown:
                    // Move cursor first
                     mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, absX, absY, 0, 0);
                    if (input.Button == 0) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN, absX, absY, 0, 0);
                    if (input.Button == 1) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN, absX, absY, 0, 0);
                    if (input.Button == 2) mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MIDDLEDOWN, absX, absY, 0, 0);
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
                    keybd_event((byte)input.KeyCode, 0, KEYEVENTF_KEYUP, 0);
                    break;
            }
        }
    }
}
