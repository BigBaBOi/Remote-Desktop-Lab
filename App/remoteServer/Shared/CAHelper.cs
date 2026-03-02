using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shared.Utils;

namespace Shared.Security
{
    public static class CAHelper
    {
        private const string CaCertName = "RemoteDesktopRootCA";
        private const string ServerCertName = "RemoteDesktopServer";

        /// <summary>
        /// Tạo và lưu Root CA tự ký (file .cer).
        /// Client cần cài đặt/tin cậy file .cer này để xác thực Server.
        /// </summary>
        public static X509Certificate2 GenerateCACertificate(string savePathCer)
        {
            using (var rsa = RSA.Create(4096))
            {
                var request = new CertificateRequest(
                    $"CN={CaCertName}",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Basic Constraints: is a CA
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, false, 0, true));

                // Key Usage
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                        true));

                // Create the cert valid for 10 years
                var caCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

                // Export .cer (Public key only)
                File.WriteAllBytes(savePathCer, caCert.Export(X509ContentType.Cert));

                Logger.Log($"[CAHelper] Đã tạo Root CA tại: {savePathCer}");
                return caCert;
            }
        }

        /// <summary>
        /// Tạo chứng chỉ Server (file .pfx) được ký bởi Root CA.
        /// </summary>
        public static void GenerateServerCertificate(X509Certificate2 caCert, string savePathPfx, string password)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    $"CN={ServerCertName}",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Enhanced Key Usage: Server Authentication
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                // Subject Alternative Name: localhost, loopback + tất cả IP thật và Hostname của máy chủ
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName("localhost");
                sanBuilder.AddIpAddress(IPAddress.Loopback);
                sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

                // Tự động thêm Hostname và tất cả IP thật của máy chủ vào SAN
                string hostName = Dns.GetHostName();
                sanBuilder.AddDnsName(hostName);
                Logger.Log($"[CAHelper] Thêm hostname vào SAN: {hostName}");

                try
                {
                    IPAddress[] hostAddresses = Dns.GetHostAddresses(hostName);
                    foreach (var ip in hostAddresses)
                    {
                        // Chỉ thêm IPv4 và IPv6 unicast (bỏ qua loopback đã thêm ở trên)
                        if (!IPAddress.IsLoopback(ip))
                        {
                            sanBuilder.AddIpAddress(ip);
                            Logger.Log($"[CAHelper] Thêm IP vào SAN: {ip}");
                        }
                    }
                }
                catch (Exception dnsEx)
                {
                    Logger.Log($"[CAHelper] Không thể lấy địa chỉ IP của host: {dnsEx.Message}");
                }

                request.CertificateExtensions.Add(sanBuilder.Build());

                // Create the cert signed by CA, valid for 1 year
                byte[] serialNumber = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(serialNumber);
                }

                var serverCert = request.Create(
                    caCert,
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(1),
                    serialNumber);

                // Combine with private key for exportation
                var serverCertWithKey = serverCert.CopyWithPrivateKey(rsa);

                // Export .pfx (includes private key)
                File.WriteAllBytes(savePathPfx, serverCertWithKey.Export(X509ContentType.Pfx, password));

                Logger.Log($"[CAHelper] Đã tạo Server Certificate tại: {savePathPfx}");
            }
        }
    }
}
