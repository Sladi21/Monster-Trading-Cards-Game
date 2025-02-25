using System.Security.Cryptography;
using System.Text;

namespace Monster_Trading_Cards_Game;

public static class SecurityService
{
    public static (string hash, string salt) CalculatePasswordHashAndSalt(string password)
    {
        var saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }

        var salt = Convert.ToBase64String(saltBytes);

        var saltedPassword = salt + password;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedPassword));
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    public static bool VerifyPassword(string inputPassword, string storedHash, string storedSalt)
    {
        var saltedInput = storedSalt + inputPassword;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedInput));
        var hash = Convert.ToBase64String(bytes);

        return hash.Equals(storedHash);
    }
}