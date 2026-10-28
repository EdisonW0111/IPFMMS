using System.Security.Cryptography;
using System.Text;

namespace IPFMMS.Services
{
    public class PasswordResetTokenService
    {
        public string GenerateToken()
        {
            // Generate 32 cryptographically secure random bytes.
            byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

            // Encode as a URL-safe string.
            return Convert.ToBase64String(randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public string HashToken(string token)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] hashBytes = SHA256.HashData(tokenBytes);

            return Convert.ToHexString(hashBytes);
        }

        public bool VerifyToken(string token, string storedHash)
        {
            // Compare hashes without an ordinary string comparison.
            string suppliedHash = HashToken(token);

            if (storedHash.Length != suppliedHash.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(suppliedHash),
                Convert.FromHexString(storedHash));
        }
    }
}