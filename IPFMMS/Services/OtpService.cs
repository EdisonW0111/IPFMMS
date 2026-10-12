using System.Security.Cryptography;
using System.Text;

namespace IPFMMS.Services
{
    public class OtpService
    {
        public string GenerateOtp()
        {
            int number = RandomNumberGenerator.GetInt32(
                0, 1_000_000);

            return number.ToString("D6");
        }

        public string HashOtp(string userId, string otp)
        {
            byte[] key = GetSecretKey();

            using var hmac = new HMACSHA256(key);

            byte[] data = Encoding.UTF8.GetBytes(
                $"{userId}:{otp}");

            byte[] hash = hmac.ComputeHash(data);

            return Convert.ToHexString(hash);
        }

        public bool VerifyOtp(
            string userId,
            string enteredOtp,
            string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            string calculatedHash = HashOtp(
                userId, enteredOtp);

            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(calculatedHash),
                Convert.FromHexString(storedHash));
        }

        private byte[] GetSecretKey()
        {
            string secret = _configuration["OtpSettings:SecretKey"]
                ?? throw new InvalidOperationException(
                    "OTP secret key is missing.");

            return Convert.FromBase64String(secret);
        }

        private readonly IConfiguration _configuration;

        public OtpService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
    }
}