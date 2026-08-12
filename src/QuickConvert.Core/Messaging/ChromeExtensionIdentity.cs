using System.Security.Cryptography;

namespace QuickConvert.Core.Messaging;

public sealed record ChromeIdentity(string ManifestKey, string ExtensionId);

public static class ChromeExtensionIdentity
{
    private const string Alphabet = "abcdefghijklmnop";

    public static string ComputeId(ReadOnlySpan<byte> publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        Span<char> id = stackalloc char[32];
        for (var index = 0; index < 16; index++)
        {
            id[index * 2] = Alphabet[hash[index] >> 4];
            id[index * 2 + 1] = Alphabet[hash[index] & 0x0F];
        }
        return new string(id);
    }

    public static ChromeIdentity Generate()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        return new ChromeIdentity(
            Convert.ToBase64String(publicKey),
            ComputeId(publicKey));
    }
}
