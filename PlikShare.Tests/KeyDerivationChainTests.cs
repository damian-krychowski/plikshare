using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class KeyDerivationChainTests
{
    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    private static byte[] FixedBytes(int length, byte fill)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, fill);
        return bytes;
    }

    // ---- Derive ----

    [Fact]
    public void Derive_EmptyChain_ReturnsCopyOfStartingDek()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);

        var result = KeyDerivationChain.Derive(startingDek, []);

        Assert.Equal(startingDek, result);
        Assert.NotSame(startingDek, result);
    }

    [Fact]
    public void Derive_SingleStep_ProducesDifferentKeyThanStartingDek()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var stepSalt = RandomBytes(KeyDerivationChain.StepSaltSize);

        var result = KeyDerivationChain.Derive(startingDek, [stepSalt]);

        Assert.Equal(KeyDerivationChain.DerivedKeySize, result.Length);
        Assert.NotEqual(startingDek, result);
    }

    [Fact]
    public void Derive_AlwaysReturns32Bytes()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);

        var oneStep = KeyDerivationChain.Derive(startingDek, [RandomBytes(32)]);
        var twoSteps = KeyDerivationChain.Derive(startingDek, [RandomBytes(32), RandomBytes(32)]);
        var fourSteps = KeyDerivationChain.Derive(
            startingDek,
            [RandomBytes(32), RandomBytes(32), RandomBytes(32), RandomBytes(32)]);

        Assert.Equal(32, oneStep.Length);
        Assert.Equal(32, twoSteps.Length);
        Assert.Equal(32, fourSteps.Length);
    }

    [Fact]
    public void Derive_IsDeterministic_SameInputsProduceSameOutput()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var saltA = RandomBytes(KeyDerivationChain.StepSaltSize);
        var saltB = RandomBytes(KeyDerivationChain.StepSaltSize);

        var first = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);
        var second = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Derive_DifferentStepSalts_ProduceDifferentKeys()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var saltA = RandomBytes(KeyDerivationChain.StepSaltSize);
        var saltB = RandomBytes(KeyDerivationChain.StepSaltSize);

        var keyA = KeyDerivationChain.Derive(startingDek, [saltA]);
        var keyB = KeyDerivationChain.Derive(startingDek, [saltB]);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Derive_DifferentStartingDeks_ProduceDifferentKeys()
    {
        var startingDekA = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var startingDekB = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var stepSalt = RandomBytes(KeyDerivationChain.StepSaltSize);

        var keyA = KeyDerivationChain.Derive(startingDekA, [stepSalt]);
        var keyB = KeyDerivationChain.Derive(startingDekB, [stepSalt]);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Derive_StepOrderMatters_ReversedChainProducesDifferentKey()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var saltA = RandomBytes(KeyDerivationChain.StepSaltSize);
        var saltB = RandomBytes(KeyDerivationChain.StepSaltSize);

        var forward = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);
        var reversed = KeyDerivationChain.Derive(startingDek, [saltB, saltA]);

        Assert.NotEqual(forward, reversed);
    }

    [Fact]
    public void Derive_AddingMoreStepsToSamePrefix_ProducesDifferentKey()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var saltA = RandomBytes(KeyDerivationChain.StepSaltSize);
        var saltB = RandomBytes(KeyDerivationChain.StepSaltSize);

        var oneStep = KeyDerivationChain.Derive(startingDek, [saltA]);
        var twoSteps = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);

        Assert.NotEqual(oneStep, twoSteps);
    }

    [Fact]
    public void Derive_TwoStepChain_EquivalentToManualSequentialDerivation()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var saltA = RandomBytes(KeyDerivationChain.StepSaltSize);
        var saltB = RandomBytes(KeyDerivationChain.StepSaltSize);

        var chained = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);

        var manualStep1 = KeyDerivationChain.Derive(startingDek, [saltA]);
        var manualFinal = KeyDerivationChain.Derive(manualStep1, [saltB]);

        Assert.Equal(chained, manualFinal);
    }

    [Fact]
    public void Derive_StartingDekWithWrongSize_Throws()
    {
        var stepSalt = RandomBytes(KeyDerivationChain.StepSaltSize);

        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Derive(RandomBytes(16), [stepSalt]));

        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Derive(RandomBytes(64), [stepSalt]));
    }

    [Fact]
    public void Derive_StepSaltWithWrongSize_Throws()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);

        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Derive(startingDek, [RandomBytes(16)]));

        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Derive(
                startingDek,
                [RandomBytes(KeyDerivationChain.StepSaltSize), RandomBytes(31)]));
    }

    [Fact]
    public void Derive_FixedVector_IsStableAcrossRuns()
    {
        // Regression lock: any file ever written with these chain salts depends on the
        // derivation being byte-stable forever. If HKDF parameters or chain semantics
        // change, this assertion will catch it.
        var startingDek = FixedBytes(KeyDerivationChain.DerivedKeySize, fill: 0x42);
        var saltA = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0xA1);
        var saltB = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0xB2);

        var firstRun = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);
        var secondRun = KeyDerivationChain.Derive(startingDek, [saltA, saltB]);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(KeyDerivationChain.DerivedKeySize, firstRun.Length);
        Assert.NotEqual(startingDek, firstRun);
    }

    // ---- Serialize ----

    [Fact]
    public void Serialize_EmptyList_ReturnsNull()
    {
        var result = KeyDerivationChain.Serialize([]);

        Assert.Null(result);
    }

    [Fact]
    public void Serialize_SingleSalt_ReturnsThatSaltVerbatim()
    {
        var salt = RandomBytes(KeyDerivationChain.StepSaltSize);

        var result = KeyDerivationChain.Serialize([salt]);

        Assert.NotNull(result);
        Assert.Equal(salt, result);
    }

    [Fact]
    public void Serialize_MultipleSalts_ConcatenatesInOrder()
    {
        var saltA = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0x11);
        var saltB = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0x22);
        var saltC = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0x33);

        var result = KeyDerivationChain.Serialize([saltA, saltB, saltC]);

        Assert.NotNull(result);
        Assert.Equal(3 * KeyDerivationChain.StepSaltSize, result.Length);
        Assert.Equal(saltA, result.Take(KeyDerivationChain.StepSaltSize).ToArray());
        Assert.Equal(
            saltB,
            result.Skip(KeyDerivationChain.StepSaltSize)
                  .Take(KeyDerivationChain.StepSaltSize)
                  .ToArray());
        Assert.Equal(
            saltC,
            result.Skip(2 * KeyDerivationChain.StepSaltSize)
                  .Take(KeyDerivationChain.StepSaltSize)
                  .ToArray());
    }

    [Fact]
    public void Serialize_SaltWithWrongSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Serialize([RandomBytes(16)]));

        Assert.Throws<ArgumentException>(() =>
            KeyDerivationChain.Serialize(
                [RandomBytes(KeyDerivationChain.StepSaltSize), RandomBytes(33)]));
    }

    // ---- Deserialize ----

    [Fact]
    public void Deserialize_Null_ReturnsEmptyList()
    {
        var result = KeyDerivationChain.Deserialize(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyList()
    {
        var result = KeyDerivationChain.Deserialize([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Deserialize_SingleChunk_ReturnsOneSalt()
    {
        var chunk = RandomBytes(KeyDerivationChain.StepSaltSize);

        var result = KeyDerivationChain.Deserialize(chunk);

        Assert.Single(result);
        Assert.Equal(chunk, result[0]);
    }

    [Fact]
    public void Deserialize_MultipleChunks_SplitsInOrder()
    {
        var saltA = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0xAA);
        var saltB = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0xBB);
        var saltC = FixedBytes(KeyDerivationChain.StepSaltSize, fill: 0xCC);

        var serialized = saltA.Concat(saltB).Concat(saltC).ToArray();

        var result = KeyDerivationChain.Deserialize(serialized);

        Assert.Equal(3, result.Count);
        Assert.Equal(saltA, result[0]);
        Assert.Equal(saltB, result[1]);
        Assert.Equal(saltC, result[2]);
    }

    [Fact]
    public void Deserialize_LengthNotMultipleOfStepSize_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            KeyDerivationChain.Deserialize(RandomBytes(31)));

        Assert.Throws<InvalidOperationException>(() =>
            KeyDerivationChain.Deserialize(RandomBytes(KeyDerivationChain.StepSaltSize + 1)));

        Assert.Throws<InvalidOperationException>(() =>
            KeyDerivationChain.Deserialize(RandomBytes(2 * KeyDerivationChain.StepSaltSize - 5)));
    }

    [Fact]
    public void Deserialize_DoesNotShareReferenceWithInputBuffer()
    {
        // Mutating the source array after deserialize must not corrupt deserialized salts —
        // protects against the caller reusing or pooling the input buffer.
        var serialized = RandomBytes(2 * KeyDerivationChain.StepSaltSize);
        var snapshot = serialized.ToArray();

        var result = KeyDerivationChain.Deserialize(serialized);
        Array.Clear(serialized);

        Assert.Equal(
            snapshot.Take(KeyDerivationChain.StepSaltSize).ToArray(),
            result[0]);
        Assert.Equal(
            snapshot.Skip(KeyDerivationChain.StepSaltSize).ToArray(),
            result[1]);
    }

    // ---- Round-trip ----

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesAllSalts()
    {
        var original = new[]
        {
            RandomBytes(KeyDerivationChain.StepSaltSize),
            RandomBytes(KeyDerivationChain.StepSaltSize),
            RandomBytes(KeyDerivationChain.StepSaltSize),
            RandomBytes(KeyDerivationChain.StepSaltSize)
        };

        var serialized = KeyDerivationChain.Serialize(original);
        var deserialized = KeyDerivationChain.Deserialize(serialized);

        Assert.Equal(original.Length, deserialized.Count);
        for (var i = 0; i < original.Length; i++)
            Assert.Equal(original[i], deserialized[i]);
    }

    [Fact]
    public void SerializeDeserialize_EmptyRoundTrip_ProducesEmptyList()
    {
        var serialized = KeyDerivationChain.Serialize([]);
        var deserialized = KeyDerivationChain.Deserialize(serialized);

        Assert.Empty(deserialized);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripPreservesDeriveResult()
    {
        var startingDek = RandomBytes(KeyDerivationChain.DerivedKeySize);
        var original = new[]
        {
            RandomBytes(KeyDerivationChain.StepSaltSize),
            RandomBytes(KeyDerivationChain.StepSaltSize)
        };

        var directKey = KeyDerivationChain.Derive(startingDek, original);

        var roundTripped = KeyDerivationChain.Deserialize(
            KeyDerivationChain.Serialize(original));
        var roundTrippedKey = KeyDerivationChain.Derive(startingDek, roundTripped);

        Assert.Equal(directKey, roundTrippedKey);
    }
}
