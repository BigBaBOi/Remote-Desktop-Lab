using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Shared.Utils
{
    public static class CertificateHelper
    {
        // For testing self-signed certificates, we often bypass validation or check specific thumbprints
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            // Allow all for development/lab environment
            // In production, check certificate.Subject/Thumbprint/Chain
            return true; 
        }

        public static X509Certificate2? LoadCertificate(string path, string password)
        {
            try
            {
                return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
            }
            catch
            {
                return null;
            }
        }
    }
}
