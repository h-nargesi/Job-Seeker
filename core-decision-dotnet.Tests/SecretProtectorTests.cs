using System.Security.Cryptography;

namespace Photon.JobSeeker.Tests;

public class SecretProtectorTests
{
    private static string Key32(byte fill)
    {
        var bytes = new byte[32];
        Array.Fill(bytes, fill);
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public void Round_trip_returns_the_plain_secret_with_the_enc_prefix()
    {
        SecretProtector.SetKey(Key32(1));
        Assert.True(SecretProtector.IsReady);

        var encrypted = SecretProtector.Encrypt("p@ssw0rd!");

        Assert.StartsWith("enc:", encrypted);
        Assert.True(SecretProtector.LooksEncrypted(encrypted));
        Assert.False(SecretProtector.LooksEncrypted("plaintext"));
        Assert.False(SecretProtector.LooksEncrypted(null));
        Assert.Equal("p@ssw0rd!", SecretProtector.Decrypt(encrypted));
    }

    [Fact]
    public void Each_encryption_uses_a_fresh_nonce()
    {
        SecretProtector.SetKey(Key32(2));

        Assert.NotEqual(SecretProtector.Encrypt("same"), SecretProtector.Encrypt("same"));
    }

    [Fact]
    public void The_key_must_be_32_bytes_of_base64()
    {
        Assert.Throws<Exception>(() => SecretProtector.SetKey(Convert.ToBase64String(new byte[16])));
        Assert.Throws<FormatException>(() => SecretProtector.SetKey("not base64!"));
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication()
    {
        SecretProtector.SetKey(Key32(3));
        var encrypted = SecretProtector.Encrypt("secret");
        var payload = Convert.FromBase64String(encrypted["enc:".Length..]);
        payload[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(
            () => SecretProtector.Decrypt("enc:" + Convert.ToBase64String(payload)));
    }

    [Fact]
    public void A_different_key_cannot_decrypt()
    {
        SecretProtector.SetKey(Key32(4));
        var encrypted = SecretProtector.Encrypt("secret");

        SecretProtector.SetKey(Key32(5));

        Assert.ThrowsAny<CryptographicException>(() => SecretProtector.Decrypt(encrypted));
    }
}
