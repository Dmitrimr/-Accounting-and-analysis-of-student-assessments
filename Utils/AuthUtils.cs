using System.Security.Cryptography;
using System.Text;

namespace WebAPI.Utils
{
    public static class AuthUtils
    {
        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var hashedPasswordBytes = Convert.FromBase64String(hashedPassword);
            using (var sha256 = SHA256.Create())
            {
                var computedHashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return computedHashBytes.SequenceEqual(hashedPasswordBytes);
            }
        }
    }
}