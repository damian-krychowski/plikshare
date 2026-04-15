using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class WorkspaceDekDerivationTests
{
    [Fact]
    public void Derive_IsDeterministic()
    {
        var storageDek = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(32);

        var a = WorkspaceDekDerivation.Derive(storageDek, salt);
        var b = WorkspaceDekDerivation.Derive(storageDek, salt);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Derive_DifferentSalts_ProduceIndependentDeks()
    {
        // This is the core property that makes two sibling workspaces on the same storage
        // cryptographically isolated — an attacker who only holds one workspace's DEK cannot
        // derive another workspace's DEK without also knowing its salt.
        var storageDek = RandomNumberGenerator.GetBytes(32);
        var saltA = RandomNumberGenerator.GetBytes(32);
        var saltB = RandomNumberGenerator.GetBytes(32);

        var workspaceDekA = WorkspaceDekDerivation.Derive(storageDek, saltA);
        var workspaceDekB = WorkspaceDekDerivation.Derive(storageDek, saltB);

        Assert.NotEqual(workspaceDekA, workspaceDekB);
    }

    [Fact]
    public void Derive_DifferentStorageDeks_ProduceIndependentDeks()
    {
        var storageDekA = RandomNumberGenerator.GetBytes(32);
        var storageDekB = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(32);

        var workspaceDekA = WorkspaceDekDerivation.Derive(storageDekA, salt);
        var workspaceDekB = WorkspaceDekDerivation.Derive(storageDekB, salt);

        Assert.NotEqual(workspaceDekA, workspaceDekB);
    }

    [Fact]
    public void Derive_ReturnsThirtyTwoBytes()
    {
        var storageDek = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(32);

        var dek = WorkspaceDekDerivation.Derive(storageDek, salt);

        Assert.Equal(32, dek.Length);
    }

    [Fact]
    public void Derive_RejectsWrongStorageDekSize()
    {
        var badStorageDek = RandomNumberGenerator.GetBytes(16);
        var salt = RandomNumberGenerator.GetBytes(32);

        Assert.Throws<ArgumentException>(() =>
            WorkspaceDekDerivation.Derive(badStorageDek, salt));
    }

    [Fact]
    public void Derive_RejectsWrongSaltSize()
    {
        var storageDek = RandomNumberGenerator.GetBytes(32);
        var badSalt = RandomNumberGenerator.GetBytes(16);

        Assert.Throws<ArgumentException>(() =>
            WorkspaceDekDerivation.Derive(storageDek, badSalt));
    }

    [Fact]
    public void Derive_EqualsSingleStepOfKeyDerivationChain()
    {
        // Load-bearing invariant: WorkspaceDekDerivation must match exactly one step of the
        // HKDF chain walked by KeyDerivationChain.Derive. Offline recovery reads the V2 file
        // header's ChainStepSalts and walks from the Storage DEK down to the file key using
        // only those salts and an empty info — if this helper ever diverges (e.g. someone
        // reintroduces a non-empty info) the recovery path silently produces a different key.
        var storageDek = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(32);

        var fromHelper = WorkspaceDekDerivation.Derive(storageDek, salt);
        var fromChain = KeyDerivationChain.Derive(storageDek, [salt]);

        Assert.Equal(fromChain, fromHelper);
    }
}
