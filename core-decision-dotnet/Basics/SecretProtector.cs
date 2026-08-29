using System.Security.Cryptography;
using System.Text;

namespace Photon.JobSeeker;

static class SecretProtector
{
    private const string Prefix = "enc:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static byte[]? key;

    public static bool IsReady => key != null;

    public static void SetKey(string base64_key)
    {
        var bytes = Convert.FromBase64String(base64_key);

        if (bytes.Length != 32)
            throw new Exception("Auth:CredentialKey must be a 32-byte base64 key (openssl rand -base64 32).");

        key = bytes;
    }

    public static bool LooksEncrypted(string? value)
    {
        return value?.StartsWith(Prefix, StringComparison.Ordinal) == true;
    }

    public static string Encrypt(string plain)
    {
        if (key == null) throw new InvalidOperationException("SecretProtector key is not set.");

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain_bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[plain_bytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain_bytes, cipher, tag);

        var output = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, output, NonceSize + cipher.Length, TagSize);

        return Prefix + Convert.ToBase64String(output);
    }

    public static string Decrypt(string value)
    {
        if (key == null) throw new InvalidOperationException("SecretProtector key is not set.");
        if (!TryGetPayload(value, out var payload)) throw new FormatException("Invalid encrypted value.");

        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);

        var cipher = new byte[payload.Length - NonceSize - TagSize];
        Buffer.BlockCopy(payload, NonceSize, cipher, 0, cipher.Length);

        var tag = new byte[TagSize];
        Buffer.BlockCopy(payload, NonceSize + cipher.Length, tag, 0, TagSize);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, null);

        return Encoding.UTF8.GetString(plain);
    }

    private static bool TryGetPayload(string value, out byte[] payload)
    {
        payload = [];
        if (!LooksEncrypted(value)) return false;
        try
        {
            payload = Convert.FromBase64String(value[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }
        return payload.Length > NonceSize + TagSize;
    }
}
