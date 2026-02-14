using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Shared.DTO;

namespace remoteServer.Services
{
    public class InputSimulatorService
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
        private const int MOUSEEVENTF_MIDDLEUP = 0x40;

        private const int KEYEVENTF_KEYUP = 0x0002;

        public void ProcessInput(InputEventDto input)
        {
            switch (input.Type)
            {
                case InputType.MouseMove:
                    SetCursorPos(input.X, input.Y);
                    break;
                    
                case InputType.MouseDown:
                    switch (input.Button)
                    {
                        case 0: mouse_event(MOUSEEVENTF_LEFTDOWN, input.X, input.Y, 0, 0); break;
                        case 1: mouse_event(MOUSEEVENTF_RIGHTDOWN, input.X, input.Y, 0, 0); break;
                        case 2: mouse_event(MOUSEEVENTF_MIDDLEDOWN, input.X, input.Y, 0, 0); break;
                    }
                    break;

                case InputType.MouseUp:
                    switch (input.Button)
                    {
                        case 0: mouse_event(MOUSEEVENTF_LEFTUP, input.X, input.Y, 0, 0); break;
                        case 1: mouse_event(MOUSEEVENTF_RIGHTUP, input.X, input.Y, 0, 0); break;
                        case 2: mouse_event(MOUSEEVENTF_MIDDLEUP, input.X, input.Y, 0, 0); break;
                    }
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
