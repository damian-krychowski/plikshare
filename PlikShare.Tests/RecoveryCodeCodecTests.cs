using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class RecoveryCodeCodecTests
{
    // Official BIP-39 test vectors (Trezor reference implementation, 256-bit entropy).
    // Source: https://github.com/trezor/python-mnemonic/blob/master/vectors.json
    // All 256-bit ("english") vectors from the upstream file are listed below.
    public static IEnumerable<object[]> OfficialVectors()
    {
        yield return
        [
            "0000000000000000000000000000000000000000000000000000000000000000",
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art"
        ];
        yield return
        [
            "7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f",
            "legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth title"
        ];
        yield return
        [
            "8080808080808080808080808080808080808080808080808080808080808080",
            "letter advice cage absurd amount doctor acoustic avoid letter advice cage absurd amount doctor acoustic avoid letter advice cage absurd amount doctor acoustic bless"
        ];
        yield return
        [
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo vote"
        ];
        yield return
        [
            "68a79eaca2324873eacc50cb9c6eca8cc68ea5d936f98787c60c7ebc74e6ce7c",
            "hamster diagram private dutch cause delay private meat slide toddler razor book happy fancy gospel tennis maple dilemma loan word shrug inflict delay length"
        ];
        yield return
        [
            "9f6a2878b2520799a44ef18bc7df394e7061a224d2c33cd015b157d746869863",
            "panda eyebrow bullet gorilla call smoke muffin taste mesh discover soft ostrich alcohol speed nation flash devote level hobby quick inner drive ghost inside"
        ];
        yield return
        [
            "066dca1a2bb7e8a1db2832148ce9933eea0f3ac9548d793112d9a95c9407efad",
            "all hour make first leader extend hole alien behind guard gospel lava path output census museum junior mass reopen famous sing advance salt reform"
        ];
        yield return
        [
            "f585c11aec520db57dd353c69554b21a89b20fb0650966fa0a9d6f74fd989d8f",
            "void come effort suffer camp survey warrior heavy shoot primary clutch crush open amazing screen patrol group space point ten exist slush involve unfold"
        ];
    }

    [Theory]
    [MemberData(nameof(OfficialVectors))]
    public void Encode_OfficialBip39Vector_MatchesExpectedMnemonic(string entropyHex, string expectedMnemonic)
    {
        var entropy = Convert.FromHexString(entropyHex);

        var mnemonic = RecoveryCodeCodec.Encode(entropy);

        Assert.Equal(expectedMnemonic, mnemonic);
    }

    [Theory]
    [MemberData(nameof(OfficialVectors))]
    public void TryDecode_OfficialBip39Vector_RecoversEntropy(string entropyHex, string mnemonic)
    {
        var expected = Convert.FromHexString(entropyHex);

        var result = RecoveryCodeCodec.TryDecode(mnemonic, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(expected, entropy);
    }

    [Fact]
    public void EncodeDecode_RandomEntropy_RoundTripsExactly()
    {
        for (var i = 0; i < 100; i++)
        {
            var entropy = new byte[RecoveryCodeCodec.EntropyBytes];
            RandomNumberGenerator.Fill(entropy);

            var mnemonic = RecoveryCodeCodec.Encode(entropy);

            var result = RecoveryCodeCodec.TryDecode(mnemonic, out var recovered);

            Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
            Assert.Equal(entropy, recovered);
        }
    }

    [Fact]
    public void Encode_WrongEntropyLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecoveryCodeCodec.Encode(new byte[16]));
        Assert.Throws<ArgumentException>(() => RecoveryCodeCodec.Encode(new byte[31]));
        Assert.Throws<ArgumentException>(() => RecoveryCodeCodec.Encode(new byte[33]));
    }

    [Fact]
    public void TryDecode_WrongWordCount_ReturnsWrongWordCount()
    {
        var twentyThree = string.Join(' ', Enumerable.Repeat("abandon", 23));
        var twentyFive = string.Join(' ', Enumerable.Repeat("abandon", 25));

        Assert.Equal(
            RecoveryCodeCodec.DecodeResult.WrongWordCount,
            RecoveryCodeCodec.TryDecode(twentyThree, out _));

        Assert.Equal(
            RecoveryCodeCodec.DecodeResult.WrongWordCount,
            RecoveryCodeCodec.TryDecode(twentyFive, out _));
    }

    [Fact]
    public void TryDecode_UnknownWord_ReturnsUnknownWord()
    {
        // replace one word with something outside the BIP-39 list
        var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon notaword art";

        var result = RecoveryCodeCodec.TryDecode(mnemonic, out _);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.UnknownWord, result);
    }

    [Fact]
    public void TryDecode_InvalidChecksum_ReturnsInvalidChecksum()
    {
        // Valid vocabulary but last word replaced — checksum won't match.
        // "ability" (index 1) is in the wordlist but is the wrong last word for all-zeros entropy.
        var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon ability";

        var result = RecoveryCodeCodec.TryDecode(mnemonic, out _);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.InvalidChecksum, result);
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsWrongWordCount()
    {
        var result = RecoveryCodeCodec.TryDecode("", out _);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.WrongWordCount, result);
    }

    // Canonical mnemonic for all-zero entropy is 23 × "abandon" + "art". We build
    // messy variants of that from a known-correct list so we never miscount inline literals.
    private static string BuildCanonicalMessyVariant(Func<IReadOnlyList<string>, string> combiner)
    {
        var words = Enumerable.Repeat("abandon", 23).Append("art").ToArray();
        return combiner(words);
    }

    [Fact]
    public void TryDecode_NormalizesExcessSpaces()
    {
        var messy = BuildCanonicalMessyVariant(ws => "   " + string.Join("   ", ws) + "   ");

        var result = RecoveryCodeCodec.TryDecode(messy, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(new byte[32], entropy);
    }

    [Fact]
    public void TryDecode_NormalizesTabsAndNewlines()
    {
        var messy = BuildCanonicalMessyVariant(ws => string.Join("\t\n", ws));

        var result = RecoveryCodeCodec.TryDecode(messy, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(new byte[32], entropy);
    }

    [Fact]
    public void TryDecode_NormalizesCase()
    {
        var messy = BuildCanonicalMessyVariant(ws =>
            string.Join(' ', ws.Select((w, i) => i % 2 == 0 ? w.ToUpperInvariant() : w)));

        var result = RecoveryCodeCodec.TryDecode(messy, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(new byte[32], entropy);
    }

    [Fact]
    public void TryDecode_StripsNumberingPrefix()
    {
        var messy = BuildCanonicalMessyVariant(ws =>
            string.Join(' ', ws.Select((w, i) => $"{i + 1}. {w}")));

        var result = RecoveryCodeCodec.TryDecode(messy, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(new byte[32], entropy);
    }

    [Fact]
    public void TryDecode_StripsCommas()
    {
        var messy = BuildCanonicalMessyVariant(ws => string.Join(", ", ws));

        var result = RecoveryCodeCodec.TryDecode(messy, out var entropy);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, result);
        Assert.Equal(new byte[32], entropy);
    }

    [Fact]
    public void Normalize_SplitsOnWhitespaceAndStripsNonLetters()
    {
        var words = RecoveryCodeCodec.Normalize("  1. Abandon, 2. ability!!  3. ABLE\t4.\nabout  ");

        Assert.Equal(new[] { "abandon", "ability", "able", "about" }, words);
    }

    [Fact]
    public void Wordlist_ContainsExactly2048Words()
    {
        Assert.Equal(2048, Bip39Wordlist.Words.Count);
    }

    [Fact]
    public void Wordlist_FirstAndLastWordsMatchSpec()
    {
        Assert.Equal("abandon", Bip39Wordlist.GetWord(0));
        Assert.Equal("zoo", Bip39Wordlist.GetWord(2047));
    }

    [Fact]
    public void Wordlist_AllWordsHaveUniqueFirstFourLetters()
    {
        // BIP-39 guarantee: each word is unambiguously identified by its first 4 letters.
        var prefixes = Bip39Wordlist.Words
            .Select(w => w.Length >= 4 ? w[..4] : w)
            .ToHashSet();

        Assert.Equal(Bip39Wordlist.Words.Count, prefixes.Count);
    }
}
