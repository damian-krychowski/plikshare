using System.Reflection;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Official BIP-39 English wordlist (2048 words).
/// Source: https://github.com/bitcoin/bips/blob/master/bip-0039/english.txt
/// </summary>
public static class Bip39Wordlist
{
    public const int Size = 2048;
    private const string ResourceName = "PlikShare.Core.Encryption.bip39-english.txt";

    private static readonly Lazy<(string[] Words, Dictionary<string, int> Index)> Data = new(Load);

    public static IReadOnlyList<string> Words => Data.Value.Words;

    public static bool TryGetIndex(string word, out int index)
    {
        return Data.Value.Index.TryGetValue(word, out index);
    }

    public static string GetWord(int index)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));

        return Data.Value.Words[index];
    }

    private static (string[] Words, Dictionary<string, int> Index) Load()
    {
        using var stream = typeof(Bip39Wordlist).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Check PlikShare.csproj <EmbeddedResource> entries.");

        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();

        var words = raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .ToArray();

        if (words.Length != Size)
            throw new InvalidOperationException(
                $"BIP-39 English wordlist must contain exactly {Size} words but found {words.Length}. " +
                $"The embedded resource is corrupted.");

        var index = new Dictionary<string, int>(Size, StringComparer.Ordinal);
        for (var i = 0; i < words.Length; i++)
            index[words[i]] = i;

        return (words, index);
    }
}
