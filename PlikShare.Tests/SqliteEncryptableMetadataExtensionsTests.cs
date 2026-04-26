using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class SqliteEncryptableMetadataExtensionsTests
{
    private const int WorkspaceId = 42;
    private const byte StorageDekVersion = 0;

    private static (SqliteConnection Connection, WorkspaceEncryptionSession Session, DecryptMetadataFunctionScope Scope)
        OpenConnectionWithRegisteredSession()
    {
        var dekBytes = new byte[32];
        RandomNumberGenerator.Fill(dekBytes);

        var session = new WorkspaceEncryptionSession(
            workspaceId: WorkspaceId,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = StorageDekVersion,
                    Dek = SecureBytes.CopyFrom(dekBytes)
                }
            ]);

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var scope = connection.RegisterDecryptMetadataFunction(
            new Dictionary<int, WorkspaceEncryptionSession>
            {
                [WorkspaceId] = session
            });

        return (connection, session, scope);
    }

    private static object? Decrypt(SqliteConnection connection, object? value, long workspaceId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT app_decrypt_metadata($v, $w)";
        cmd.Parameters.AddWithValue("$v", value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$w", workspaceId);
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }

    [Fact]
    public void Null_input_returns_null()
    {
        var (connection, _, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        using (scope)
        {
            Assert.Null(Decrypt(connection, value: null, WorkspaceId));
        }
    }

    [Fact]
    public void Plain_string_without_pse_prefix_passes_through()
    {
        var (connection, _, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        using (scope)
        {
            Assert.Equal("hello world", Decrypt(connection, "hello world", WorkspaceId));
            Assert.Equal("", Decrypt(connection, "", WorkspaceId));
        }
    }

    [Fact]
    public void Pse_envelope_with_matching_session_is_decoded()
    {
        var (connection, session, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        using (scope)
        {
            var encoded = ((WorkspaceEncryptionSession?)session).Encode("secret-name").Encoded;

            Assert.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, encoded);

            Assert.Equal("secret-name", Decrypt(connection, encoded, WorkspaceId));
        }
    }

    [Fact]
    public void Pse_envelope_for_unknown_workspace_returns_null()
    {
        var (connection, session, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        using (scope)
        {
            var encoded = ((WorkspaceEncryptionSession?)session).Encode("secret-name").Encoded;

            Assert.Null(Decrypt(connection, encoded, workspaceId: 9999));
        }
    }

    [Fact]
    public void Pse_envelope_with_unknown_key_version_returns_null()
    {
        var dekBytes = new byte[32];
        RandomNumberGenerator.Fill(dekBytes);

        // Session only has DEK v0, but we'll feed it an envelope tagged with v7.
        var session = new WorkspaceEncryptionSession(
            workspaceId: WorkspaceId,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = 0,
                    Dek = SecureBytes.CopyFrom(dekBytes)
                }
            ]);

        var fakeSession = new WorkspaceEncryptionSession(
            workspaceId: WorkspaceId,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = 7,
                    Dek = SecureBytes.CopyFrom(dekBytes)
                }
            ]);

        var encodedV7 = ((WorkspaceEncryptionSession?)fakeSession).Encode("secret-name").Encoded;

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var scope = connection.RegisterDecryptMetadataFunction(
            new Dictionary<int, WorkspaceEncryptionSession> { [WorkspaceId] = session });

        Assert.Null(Decrypt(connection, encodedV7, WorkspaceId));
    }

    [Fact]
    public void Garbled_pse_envelope_returns_null_without_throwing()
    {
        var (connection, _, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        using (scope)
        {
            Assert.Null(Decrypt(connection, "pse:not-base64!!!", WorkspaceId));
            Assert.Null(Decrypt(connection, "pse:AAAA", WorkspaceId));
        }
    }

    [Fact]
    public void Empty_session_map_returns_null_for_pse_envelope()
    {
        var dekBytes = new byte[32];
        RandomNumberGenerator.Fill(dekBytes);
        var session = new WorkspaceEncryptionSession(
            workspaceId: WorkspaceId,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = 0,
                    Dek = SecureBytes.CopyFrom(dekBytes)
                }
            ]);
        var encoded = ((WorkspaceEncryptionSession?)session).Encode("secret-name").Encoded;

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var scope = connection.RegisterDecryptMetadataFunction(
            new Dictionary<int, WorkspaceEncryptionSession>());

        Assert.Null(Decrypt(connection, encoded, WorkspaceId));
    }

    [Fact]
    public void After_scope_dispose_pse_envelope_call_throws()
    {
        var (connection, session, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        {
            var encoded = ((WorkspaceEncryptionSession?)session).Encode("x").Encoded;

            scope.Dispose();

            Assert.ThrowsAny<Exception>(() => Decrypt(connection, encoded, WorkspaceId));
        }
    }

    [Fact]
    public void After_scope_dispose_passthrough_call_also_throws()
    {
        var (connection, _, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        {
            scope.Dispose();

            Assert.ThrowsAny<Exception>(() => Decrypt(connection, "plain text", WorkspaceId));
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var (connection, _, scope) = OpenConnectionWithRegisteredSession();
        using (connection)
        {
            scope.Dispose();
            scope.Dispose();
        }
    }
}
