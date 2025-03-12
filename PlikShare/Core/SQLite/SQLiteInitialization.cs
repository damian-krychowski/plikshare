using System.Diagnostics;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using Serilog;

namespace PlikShare.Core.SQLite;

public class SQLiteInitialization(
    IClock clock,
    PlikShareDb plikShareDb,
    PlikShareAiDb plikShareAiDb,
    IEnumerable<ISQLiteMigration> migrations)
{
    public void Initialize()
    {
        ThrowIfMigrationNamesAreNotUnique();

        using (var mainConnection = plikShareDb.OpenInitialConnection())
        {
            InitializeDb(
                migrations: migrations, 
                utcNow: clock.UtcNow, 
                dbType: PlikShareDbType.Main, 
                connection: mainConnection);
        }

        using (var aiConnection = plikShareAiDb.OpenInitialConnection())
        {
            InitializeDb(
                migrations: migrations,
                utcNow: clock.UtcNow,
                dbType: PlikShareDbType.Ai,
                connection: aiConnection);
        }
    }

    private static void InitializeDb(
        IEnumerable<ISQLiteMigration> migrations,
        DateTimeOffset utcNow,
        PlikShareDbType dbType,
        SqliteConnection connection)
    {
        var migrationsInOrder = migrations
            .Where(m => m.Type == dbType)
            .OrderBy(m => m.Date);

        CreateMigrationsTableIfNotExists(dbType, connection);

        foreach (var migration in migrationsInOrder)
        {
            ApplyMigration(dbType, utcNow, migration, connection);
        }
    }

    private void ThrowIfMigrationNamesAreNotUnique()
    {
        var names = new HashSet<string>();

        foreach (var sqLiteMigration in migrations)
        {
            if (!names.Add(sqLiteMigration.Name))
            {
                throw new InvalidOperationException(
                    $"Migration name '{sqLiteMigration.Name}' is not unique");
            }
        }
    }
    
    private static void CreateMigrationsTableIfNotExists(
        PlikShareDbType dbType,
        SqliteConnection connection)
    {
        const string checkTableQuery = @"
            SELECT name 
            FROM sqlite_master 
            WHERE type='table' AND name='m_migrations'
        ";

        var result = connection
            .OneRowCmd(
                sql: checkTableQuery,
                readRowFunc: reader => reader.GetString(0))
            .Execute();
        
        if (!result.IsEmpty)
        {
            Log.Information("[SQLITE INIT] Migrations table creation skipped in '{DbType}'. It was already created in the past.",
                dbType);

            return;
        }

        const string createTableQuery = @"
            CREATE TABLE m_migrations (
                m_id INTEGER PRIMARY KEY AUTOINCREMENT,
                m_name TEXT NOT NULL,
                m_executed_at TEXT NOT NULL
            )
        ";

        var createResult = connection
            .NonQueryCmd(
                sql: createTableQuery)
            .Execute();
        
        Debug.Assert(createResult.AffectedRows == 0);
        
        Log.Information("[SQLITE INIT] Migrations table was created in '{DbType}'.", dbType);
    }

    private static void ApplyMigration(
        PlikShareDbType dbType,
        DateTimeOffset utcNow,
        ISQLiteMigration migration,
        SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            var isMigrationAlreadyApplied = connection
                .OneRowCmd(
                    sql: @"
                        SELECT EXISTS (
                            SELECT 1 
                            FROM m_migrations 
                            WHERE m_name = $name
                        );
                    ",
                    readRowFunc: reader => reader.GetBoolean(0),
                    transaction: transaction)
                .WithParameter("$name", migration.Name)
                .Execute();

            if (!isMigrationAlreadyApplied.Value)
            {
                migration.Run(connection, transaction);

                var migrationMarkedResult = connection
                    .NonQueryCmd(
                        sql: @"
                            INSERT INTO m_migrations (m_name, m_executed_at)
                            VALUES ($name, $executedAt)
                        ",
                        transaction: transaction)
                    .WithParameter("$name", migration.Name)
                    .WithParameter("$executedAt", utcNow)
                    .Execute();

                if (migrationMarkedResult.AffectedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Migration '{migration.Name}' in '{dbType}' cannot be marked as applied");
                }
                
                Log.Information("[SQLITE INIT] Migration '{MigrationName}' in '{DbType}' was applied to the database.", 
                    migration.Name,
                    dbType);
            }
            else
            {
                Log.Information("[SQLITE INIT] Migration '{MigrationName}' in '{DbType}' skipped. It was already applied in the past.", 
                    migration.Name,
                    dbType);
            }
            
            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while applying migration '{Migration}' in '{DbType}'", 
                migration.Name,
                dbType);
            
            throw;
        }
    }
}