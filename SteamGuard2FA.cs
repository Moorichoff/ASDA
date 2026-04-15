using System.Security.Cryptography;
using System.Text;

namespace SteamGuard;

public static class SteamGuard2FA
{
    private static readonly char[] SteamGuardChars = "23456789BCDFGHJKMNPQRTVWXY".ToCharArray();
    
    /// <summary>
    /// Генерация 2FA кода Steam на основе shared_secret
    /// </summary>
    public static string GenerateCode(string sharedSecret)
    {
        if (string.IsNullOrEmpty(sharedSecret))
            return "ERR: empty secret";

        try
        {
            byte[] secret = Convert.FromBase64String(sharedSecret);
            
            // Получаем текущий временной интервал (30 секунд)
            // Используем синхронизированное время Steam если доступно
            long currentTime = TimeAligner.GetSteamTime();
            long timeStep = currentTime / 30L;
            
            // Преобразуем в big-endian байты
            byte[] timeBytes = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(timeBytes);
            
            // HMAC-SHA1
            using var hmac = new HMACSHA1(secret);
            byte[] hash = hmac.ComputeHash(timeBytes);
            
            // Dynamic truncation
            int offset = hash[hash.Length - 1] & 0x0F;
            int codeInt = ((hash[offset] & 0x7F) << 24) |
                         ((hash[offset + 1] & 0xFF) << 16) |
                         ((hash[offset + 2] & 0xFF) << 8) |
                         (hash[offset + 3] & 0xFF);
            
            // Преобразуем в 5-значный код Steam
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                sb.Append(SteamGuardChars[codeInt % SteamGuardChars.Length]);
                codeInt /= SteamGuardChars.Length;
            }
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERR: {ex.Message}";
        }
    }
}
