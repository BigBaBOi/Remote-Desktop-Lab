using System.Runtime.InteropServices;

namespace Shared.Protocol
{
    public enum PacketType : int
    {
        Handshake = 0,
        LoginRequest = 1,
        LoginResponse = 2,
        ScreenFrame = 3,
        InputEvent = 4,
        FileMeta = 5,
        FileChunk = 6,
        FileAck = 7,
        Heartbeat = 8,
        Disconnect = 9
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public PacketType Type;
        public int PayloadLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SessionId;

        public static int Size => Marshal.SizeOf<PacketHeader>();
    }
}
