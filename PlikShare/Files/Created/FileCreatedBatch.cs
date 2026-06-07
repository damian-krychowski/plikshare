using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Created;

public sealed record FileCreatedBatch(
    WorkspaceContext Workspace,
    WorkspaceEncryptionSession? Session,
    Guid CorrelationId,
    SqliteWriteContext DbWriteContext,
    SqliteTransaction Transaction,
    IReadOnlyList<CreatedFile> Files);