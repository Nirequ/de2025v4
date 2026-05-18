using System;
using System.Security.Cryptography;

namespace Module3.Domain.Services;

/// <summary>
/// Хеширование паролей по схеме PBKDF2-HMAC-SHA256.
/// Формат хранения: <c>pbkdf2_sha256$&lt;iter&gt;$&lt;salt_hex&gt;$&lt;hash_hex&gt;</c>.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 200_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Algorithm = "pbkdf2_sha256";

    /// <summary>
    /// Сгенерировать хеш пароля для сохранения в БД.
    /// </summary>
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return string.Join('$',
            Algorithm,
            Iterations.ToString(),
            Convert.ToHexString(salt),
            Convert.ToHexString(hash));
    }

    /// <summary>
    /// Проверить, что пароль соответствует ранее сохранённому хешу.
    /// </summary>
    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm)
            return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromHexString(parts[2]);
            expected = Convert.FromHexString(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
