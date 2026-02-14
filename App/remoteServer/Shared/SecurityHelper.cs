using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Security
{
    public static class SecurityHelper
    {
        // RSA Key Generation
        public static (string PublicKey, string PrivateKey) GenerateRsaKeys()
        {
            using (var rsa = RSA.Create())
            {
                rsa.KeySize = 2048;
                return (rsa.ToXmlString(false), rsa.ToXmlString(true));
            }
        }

        // RSA Encrypt (for AES Key exchange)
        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            }
        }

        // RSA Decrypt
        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKeyXml);
                return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
            }
        }

        // AES Encrypt (for Payload)
        public static byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        // AES Decrypt
        public static byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(data))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var resultMs = new MemoryStream())
                {
                    cs.CopyTo(resultMs);
                    return resultMs.ToArray();
                }
            }
        }
    }
}
