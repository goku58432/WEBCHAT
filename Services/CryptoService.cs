using System.Security.Cryptography;
using System.Text;
namespace ChatAPI.Services
{
    public class CryptoService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        public CryptoService(IConfiguration config)
        {
            var secret = config["Crypto:Key"] ?? "ChatAppSecretKey2024!XYZ123456AB";
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
            _iv  = MD5.HashData(Encoding.UTF8.GetBytes(secret));
        }
        public string Encrypt(string plain)
        {
            using var aes = Aes.Create();
            aes.Key = _key; aes.IV = _iv;
            var enc = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plain);
            return Convert.ToBase64String(enc.TransformFinalBlock(bytes, 0, bytes.Length));
        }
        public string Decrypt(string cipher)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _key; aes.IV = _iv;
                var dec = aes.CreateDecryptor();
                var bytes = Convert.FromBase64String(cipher);
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(bytes, 0, bytes.Length));
            }
            catch { return "[mensaje no disponible]"; }
        }
    }
}
