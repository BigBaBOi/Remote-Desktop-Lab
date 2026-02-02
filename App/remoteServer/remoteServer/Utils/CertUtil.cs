using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace remoteServer.Utils
{
    // CertUtil: ti?n ích t?o ch?ng ch? t? ký (demo CA n?i b?) và ký ch?ng ch? (server/client) b?i CA
    public static class CertUtil
    {
        // T?o m?t CA t? ký
        public static X509Certificate2 CreateCertificateAuthority(string subjectName)
        {
            using var rsa = RSA.Create(4096);
            var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            // ?ây là CA root
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
            // Export và import l?i ?? private key ???c ?ánh d?u exportable và có th? l?u d??i PFX
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));
        }

        // T?o ch?ng ch? (server ho?c client) do CA ký
        public static X509Certificate2 CreateCertificateSignedByCA(X509Certificate2 caCert, string subjectName, bool isServer)
        {
            if (caCert == null) throw new ArgumentNullException(nameof(caCert));

            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            if (isServer)
            {
                // Dùng cho TLS server
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // serverAuth

                // Thêm SAN (localhost) ?? tránh c?nh báo tên CN không kh?p
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName("localhost");
                sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
                req.CertificateExtensions.Add(sanBuilder.Build());
            }
            else
            {
                // Dùng cho TLS client
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false)); // clientAuth
            }

            var serial = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddYears(5);

            // T?o cert do CA ký. Create s? dùng private key c?a caCert n?u ???c n?p cùng PFX.
            using var signed = req.Create(caCert, notBefore, notAfter, serial);

            // Export PFX ?? bao g?m private key r?i import l?i
            return new X509Certificate2(signed.Export(X509ContentType.Pfx));
        }

        // Hàm c? gi? nguyên cho t??ng thích (t?o self-signed server cert không ph?i ký b?i CA)
        public static X509Certificate2 CreateSelfSignedCert(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));
        }
    }
}
