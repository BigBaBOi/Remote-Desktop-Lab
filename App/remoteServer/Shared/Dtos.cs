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

    public class RegisterRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }

    public class RegisterResponseDto
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
        public float X { get; set; } // Normalized 0.0 - 1.0
        public float Y { get; set; } // Normalized 0.0 - 1.0
        public int KeyCode { get; set; }
        public int Button { get; set; } // 0: Left, 1: Right, 2: Middle
    }

    // Screen DTOs
    public class ScreenFrameDto
    {
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }  // Chunk Width
        public int Height { get; set; } // Chunk Height
        public int Left { get; set; }   // Chunk X
        public int Top { get; set; }    // Chunk Y
        public int TotalWidth { get; set; }  // Full Screen Width
        public int TotalHeight { get; set; } // Full Screen Height
        public long Timestamp { get; set; }
    }

    // File Transfer DTOs
    public class FileMetaDto
    {
        public string FileId { get; set; } = string.Empty; // GUID để theo dõi phiên truyền
        public string FileName { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public string Checksum { get; set; } = string.Empty;
    }

    public class FileAckDto
    {
        public string FileId { get; set; } = string.Empty;
        public int LastReceivedChunkIndex { get; set; } // Server báo cho Client biết đã nhận tới chunk nào
        public bool IsComplete { get; set; }
    }

    public class FileChunkDto
    {
        public string FileId { get; set; } = string.Empty;
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

    public class ResolutionRequestDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
