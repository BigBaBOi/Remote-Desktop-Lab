using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Shared.Utils
{
    public static class CertificateHelper
    {
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null || chain == null) return false;

            // Trong môi trường development, file cer có thể đặt cùng thư mục chạy
            string caPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteDesktopRootCA.cer");
            if (!System.IO.File.Exists(caPath))
            {
                caPath = "RemoteDesktopRootCA.cer";
                if (!System.IO.File.Exists(caPath))
                {
                    Logger.Log("[SSL] Không tìm thấy RemoteDesktopRootCA.cer để xác thực Server. Từ chối kết nối.");
                    return false;
                }
            }

            try
            {
                var caCert = new X509Certificate2(caPath);

                // Chỉ định Chain tin tưởng Root CA cụ thể
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                // Dùng CustomRootTrust (Hỗ trợ từ .NET 5+)
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Clear();
                chain.ChainPolicy.CustomTrustStore.Add(caCert);

                // Ép kiểu về X509Certificate2 để build chain
                var serverCert2 = new X509Certificate2(certificate);
                bool isValid = chain.Build(serverCert2);

                if (!isValid)
                {
                    Logger.Log("[SSL] Lỗi xác thực chứng chỉ Server:");
                    foreach (var status in chain.ChainStatus)
                    {
                        Logger.Log($"   -> {status.StatusInformation}");
                    }
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Logger.Log($"[SSL] Lỗi Exception trong quá trình xác thực: {ex.Message}");
                return false;
            }
        }

        public static X509Certificate2? LoadCertificate(string path, string password)
        {
            try
            {
                return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                Logger.Log($"[CertificateHelper] Error loading certificate: {ex.Message}");
                return null;
            }
        }
    }
}
