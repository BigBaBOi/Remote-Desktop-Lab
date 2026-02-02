using System;

namespace remoteServer.Shared
{
    // PacketType - lo?i packet trong giao th?c
    public enum PacketType
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

    // PacketHeader - c?u trúc header nh? n?m tr??c payload
    public struct PacketHeader
    {
        public PacketType PacketType { get; set; }
        public int PayloadLength { get; set; }
        public Guid SessionId { get; set; }

        public const int HeaderSize = 4 + 4 + 16; // PacketType (int) + PayloadLength (int) + Guid (16)

        // Chuy?n header thành m?ng byte ?? g?i qua m?ng
        public byte[] ToBytes()
        {
            var buf = new byte[HeaderSize];
            BitConverter.TryWriteBytes(buf.AsSpan(0,4), (int)PacketType);
            BitConverter.TryWriteBytes(buf.AsSpan(4,4), PayloadLength);
            var guidBytes = SessionId.ToByteArray();
            Buffer.BlockCopy(guidBytes, 0, buf, 8, 16);
            return buf;
        }

        // Parse header t? m?ng byte nh?n ???c
        public static PacketHeader FromBytes(ReadOnlySpan<byte> span)
        {
            if (span.Length < HeaderSize) throw new ArgumentException("Span too small");
            var type = BitConverter.ToInt32(span.Slice(0,4));
            var len = BitConverter.ToInt32(span.Slice(4,4));
            var guidSpan = span.Slice(8,16).ToArray();
            return new PacketHeader
            {
                PacketType = (PacketType)type,
                PayloadLength = len,
                SessionId = new Guid(guidSpan)
            };
        }
    }
}
