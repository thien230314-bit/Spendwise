using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SpendWise.Tests;

internal static class SqlTestFixtureLifecycle
{
    private static readonly object DiagnosticWriteLock = new();
    private static int activeCreates;

    public const string InstanceEnvironmentVariable = "SPENDWISE_SQL_TEST_INSTANCE";
    private const string ApprovedInstance = @"DESKTOP-B5KA3LT\SQLEXPRESS02";

    public static string CreateOwnedConnectionString(string catalog, string? instance)
    {
        var connection = new SqlConnectionStringBuilder { DataSource = instance ?? "", InitialCatalog = catalog,
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false, ConnectTimeout = 15 };
        ValidateOwnedConnection(connection.ConnectionString);
        return connection.ConnectionString;
    }

    public static void ValidateOwnedConnection(string connectionString)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        if (target.DataSource != ApprovedInstance || !target.IntegratedSecurity
            || target.UserID.Length != 0 || target.Password.Length != 0 || target.UserInstance
            || target.AttachDBFilename.Length != 0 || target.FailoverPartner.Length != 0
            || !Regex.IsMatch(target.InitialCatalog,
                @"\ASpendWise_(OtpS04|GroupFundS052|GroupDeletionS052|GoalS053|FinancialS05)_Test_[a-f0-9]{32}\z")
            && target.InitialCatalog != "SpendWise_E2E_G4")
            throw new InvalidOperationException("Refusing an unapproved SQL test target.");
    }

    public static async Task CreateOwnedCatalogAsync(string connectionString)
    {
        ValidateOwnedConnection(connectionString);
        var target = new SqlConnectionStringBuilder(connectionString);
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using var database = new SqlConnection(master.ConnectionString);
        await database.OpenAsync();
        await using var command = database.CreateCommand();
        // CREATE must succeed; an existing catalog is never adopted or cleaned by this fixture.
        // https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created
        command.CommandText = """
            IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>@server
                THROW 51000,'Unexpected test server',1;
            IF DB_ID(@catalog) IS NOT NULL THROW 51000,'Test catalog already exists',1;
            DECLARE @ddl nvarchar(256)=N'CREATE DATABASE '+QUOTENAME(@catalog);
            EXEC(@ddl);
            -- Preserve EF SQL Server's prior fresh-catalog RCSI behavior.
            SET @ddl=N'ALTER DATABASE '+QUOTENAME(@catalog)+N' SET READ_COMMITTED_SNAPSHOT ON';
            EXEC(@ddl);
            """;
        command.Parameters.AddWithValue("@server", ApprovedInstance);
        command.Parameters.AddWithValue("@catalog", target.InitialCatalog);
        await MeasureAsync(target.InitialCatalog, "reserve", () => command.ExecuteNonQueryAsync());
        await using var owned = new SqlConnection(connectionString);
        await owned.OpenAsync();
        await using var marker = owned.CreateCommand();
        marker.CommandText = "EXEC sys.sp_addextendedproperty @name=N'SpendWise.TestFixture', @value=@owner";
        marker.Parameters.AddWithValue("@owner", target.InitialCatalog);
        await marker.ExecuteNonQueryAsync();
    }

    public static async Task VerifyCatalogOwnerAsync(string connectionString)
    {
        ValidateOwnedConnection(connectionString);
        var target = new SqlConnectionStringBuilder(connectionString);
        await using var database = new SqlConnection(connectionString);
        await database.OpenAsync();
        await using var command = database.CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'SpendWise.TestFixture' AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server";
        command.Parameters.AddWithValue("@server", ApprovedInstance);
        if (!string.Equals(await command.ExecuteScalarAsync() as string, target.InitialCatalog, StringComparison.Ordinal))
            throw new InvalidOperationException("SQL test catalog ownership could not be verified.");
    }

    public static async Task WaitForGateAsync(Task gate, Task operation, TimeSpan timeout)
    {
        await Task.WhenAny(gate, operation).WaitAsync(timeout);
        if (!gate.IsCompleted)
        {
            await operation;
            throw new InvalidOperationException("SQL operation completed without reaching its concurrency gate.");
        }
        await gate;
    }

    public static async Task ConfigureOwnedCatalogAsync(string connectionString)
    {
        ValidateOwnedConnection(connectionString);
        await VerifyCatalogOwnerAsync(connectionString);
        var target = new SqlConnectionStringBuilder(connectionString);

        await MeasureAsync(target.InitialCatalog, "configure", async () =>
        {
            await using var database = new SqlConnection(connectionString);
            await database.OpenAsync();
            await using var command = database.CreateCommand();
            // Owned test catalogs otherwise repeatedly reopen between Pooling=false contexts.
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-set-options#auto_option
            command.CommandText = "ALTER DATABASE CURRENT SET AUTO_CLOSE OFF";
            await command.ExecuteNonQueryAsync();
        });
    }

    public static async Task<T> CreateAsync<T>(Func<Task<T>> initialize, Func<ValueTask> cleanup)
    {
        try { return await initialize(); }
        catch (Exception initializationError)
        {
            try { await cleanup(); }
            catch (Exception cleanupError)
            {
                throw new AggregateException("SQL fixture initialization and cleanup failed.", initializationError, cleanupError);
            }
            throw;
        }
    }

    public static async Task MeasureAsync(string catalog, string phase, Func<Task> operation)
    {
        var started = DateTime.UtcNow;
        var watch = Stopwatch.StartNew();
        if (phase == "create") Interlocked.Increment(ref activeCreates);
        try
        {
            WriteDiagnostic(catalog, phase, "start", started, 0, null);
            try { await operation(); }
            catch (Exception operationError)
            {
                try { WriteDiagnostic(catalog, phase, "failed", started, watch.Elapsed.TotalMilliseconds, operationError); }
                catch (Exception diagnosticError)
                {
                    throw new AggregateException("SQL fixture operation and diagnostics failed.", operationError, diagnosticError);
                }
                throw;
            }
            WriteDiagnostic(catalog, phase, "completed", started, watch.Elapsed.TotalMilliseconds, null);
        }
        finally
        {
            if (phase == "create") Interlocked.Decrement(ref activeCreates);
        }
    }

    private static void WriteDiagnostic(string catalog, string phase, string outcome,
        DateTime started, double elapsedMs, Exception? error)
    {
        if (Environment.GetEnvironmentVariable("SPENDWISE_SQL_TEST_DIAGNOSTICS") != "1") return;
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults");
        var entry = JsonSerializer.Serialize(new
        {
            Utc = DateTime.UtcNow, StartedUtc = started, Catalog = catalog, Phase = phase,
            Outcome = outcome, ElapsedMs = elapsedMs, ActiveCreates = Volatile.Read(ref activeCreates),
            ErrorType = error?.GetType().FullName, SqlNumber = FindSqlNumber(error)
        });
        // Test-only opt-in metadata: never connection strings, SQL parameters, or exception messages.
        lock (DiagnosticWriteLock)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, $"sql-fixture-phases-{Environment.ProcessId}.jsonl"), entry + Environment.NewLine);
        }
    }

    private static int? FindSqlNumber(Exception? error)
    {
        while (error is not null)
        {
            if (error is SqlException sql) return sql.Number;
            error = error.InnerException;
        }
        return null;
    }
}

public abstract class SqlTestFixtureScope : Xunit.IAsyncLifetime
{
    private IAsyncDisposable? owned;

    protected async Task<T> OwnAsync<T>(Task<T> creation) where T : IAsyncDisposable
    {
        var fixture = await creation;
        owned = fixture;
        return fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    // xUnit records test-body and lifecycle failures separately; all method-local contexts are already disposed.
    public async Task DisposeAsync()
    {
        if (owned is not null) await owned.DisposeAsync();
    }
}
