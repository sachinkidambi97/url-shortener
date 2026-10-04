using System.Security.Cryptography;

namespace UrlShortener.Api.Services;

public static class ShortCodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int DefaultLength = 7;

    public static string Generate(int length = DefaultLength)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            int index = RandomNumberGenerator.GetInt32(Alphabet.Length);
            chars[i] = Alphabet[index];
        }
        return new string(chars);
    }
}
