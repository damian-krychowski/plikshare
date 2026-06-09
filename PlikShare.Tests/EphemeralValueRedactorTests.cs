using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class EphemeralValueRedactorTests
{
    [Fact]
    public void Redact_replaces_ephemeral_value_keeping_prefix()
    {
        var json = """{"encodedKey":"eph:AAAABBBBCCCC=="}""";

        Assert.Equal(
            """{"encodedKey":"eph:[redacted]"}""",
            EphemeralValueRedactor.Redact(json));
    }

    [Fact]
    public void Redact_replaces_all_ephemeral_values_and_keeps_the_rest()
    {
        var json = """{"a":"eph:AAAA","b":"eph:BBBB","c":"plain"}""";

        Assert.Equal(
            """{"a":"eph:[redacted]","b":"eph:[redacted]","c":"plain"}""",
            EphemeralValueRedactor.Redact(json));
    }

    [Fact]
    public void Redact_strips_full_ephemeral_key_in_compact_seed_keeping_salts()
    {
        var json = """{"3050589":"1.0.Cbd2-_salt.mcVq-_salt.eph:AbC-def_GHI"}""";

        Assert.Equal(
            """{"3050589":"1.0.Cbd2-_salt.mcVq-_salt.eph:[redacted]"}""",
            EphemeralValueRedactor.Redact(json));
    }

    [Fact]
    public void Redact_leaves_json_without_ephemeral_untouched()
    {
        var json = """{"name":"photo.jpg","size":123}""";

        Assert.Equal(json, EphemeralValueRedactor.Redact(json));
    }

    [Fact]
    public void Redact_does_not_touch_metadata_prefix()
    {
        var json = """{"name":"pse:AAAABBBBCCCC"}""";

        Assert.Equal(json, EphemeralValueRedactor.Redact(json));
    }

    [Fact]
    public void Redact_is_idempotent()
    {
        var json = """{"k":"eph:AAAABBBBCCCC"}""";

        var once = EphemeralValueRedactor.Redact(json);
        var twice = EphemeralValueRedactor.Redact(once);

        Assert.Equal("""{"k":"eph:[redacted]"}""", once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Redact_null_returns_null()
    {
        Assert.Null(EphemeralValueRedactor.Redact(null));
    }

    [Fact]
    public void Redact_empty_returns_empty()
    {
        Assert.Equal(string.Empty, EphemeralValueRedactor.Redact(string.Empty));
    }
}
