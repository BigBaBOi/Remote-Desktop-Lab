using System;

namespace Shared.DTO
{
    // Auth DTOs
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Input DTOs
    public enum InputType
    {
        MouseMove,
        MouseDown,
        MouseUp,
        KeyDown,
        KeyUp
    }

    public class InputEventDto
    {
        public InputType Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int KeyCode { get; set; }
        public int Button { get; set; } // 0: Left, 1: Right, 2: Middle
    }

    // Screen DTOs
    public class ScreenFrameDto
    {
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

    // File Transfer DTOs
    public class FileChunkDto
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int ChunkIndex { get; set; }
        public bool IsLastChunk { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    // Handshake DTOs
    public class PublicKeyDto
    {
        public string PublicKeyXml { get; set; } = string.Empty;
    }

    public class HandshakeDto
    {
        public byte[] EncryptedAesKey { get; set; } = Array.Empty<byte>();
        public byte[] EncryptedAesIV { get; set; } = Array.Empty<byte>();
    }
}
