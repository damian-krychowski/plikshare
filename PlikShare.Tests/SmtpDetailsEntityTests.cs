using FluentAssertions;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.ExternalProviders.Smtp;

namespace PlikShare.Tests;

// These tests pin down the backward-compatibility contract for SmtpDetailsEntity:
// rows persisted before RequiresAuthentication existed must continue to behave as
// if authentication was required. Breaking this would silently disable AUTH for
// every existing SMTP provider on upgrade.
public class SmtpDetailsEntityTests
{
    [Fact]
    public void deserializing_legacy_json_without_requires_authentication_field_defaults_to_true()
    {
        const string legacyJson = """
            {
                "hostname": "smtp.example.com",
                "port": 587,
                "sslMode": "startTls",
                "username": "user@example.com",
                "password": "secret"
            }
            """;

        var entity = Json.Deserialize<SmtpDetailsEntity>(legacyJson);

        entity.Should().NotBeNull();
        entity!.RequiresAuthentication.Should().BeTrue();
        entity.Hostname.Should().Be("smtp.example.com");
        entity.Port.Should().Be(587);
        entity.SslMode.Should().Be(SslMode.StartTls);
        entity.Username.Should().Be("user@example.com");
        entity.Password.Should().Be("secret");
    }

    [Fact]
    public void deserializing_new_json_with_explicit_false_keeps_value()
    {
        const string newJson = """
            {
                "hostname": "localhost",
                "port": 25,
                "sslMode": "none",
                "username": "",
                "password": "",
                "requiresAuthentication": false
            }
            """;

        var entity = Json.Deserialize<SmtpDetailsEntity>(newJson);

        entity.Should().NotBeNull();
        entity!.RequiresAuthentication.Should().BeFalse();
        entity.Username.Should().BeEmpty();
        entity.Password.Should().BeEmpty();
    }

    [Fact]
    public void deserializing_new_json_with_explicit_true_keeps_value()
    {
        const string newJson = """
            {
                "hostname": "smtp.example.com",
                "port": 587,
                "sslMode": "startTls",
                "username": "user@example.com",
                "password": "secret",
                "requiresAuthentication": true
            }
            """;

        var entity = Json.Deserialize<SmtpDetailsEntity>(newJson);

        entity.Should().NotBeNull();
        entity!.RequiresAuthentication.Should().BeTrue();
    }
}
