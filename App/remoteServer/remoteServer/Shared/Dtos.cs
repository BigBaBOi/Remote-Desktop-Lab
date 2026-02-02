using System;

namespace remoteServer.Shared
{
    // DTOs dùng ?? trao ??i d? li?u gi?a Client và Server
    // Thi?t k? "DTO-first": không g?i string t? do, m?i d? li?u ph?i ?óng gói trong DTO    // DTO ??ng nh?p    public record LoginRequestDto(string Username, string PasswordHash);    public record LoginResponseDto(bool Success, string Message);    // DTO s? ki?n input (chu?t/ phím)    public