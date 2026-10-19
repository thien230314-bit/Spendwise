using Microsoft.Data.SqlClient;
using Xunit;

namespace SpendWise.Tests;

public sealed class SqlTestFixtureLifecycleTests
{
    [Fact]
    public async Task Setup_and_cleanup_failures_preserve_both_instances_in_original_order_and_stacks()
    {
        var setupError = new InvalidOperationException("Controlled setup failure");
        var cleanupError = new IOException("Controlled cleanup failure");

        var actual = await Assert.ThrowsAsync<AggregateException>(() =>
            SqlTestFixtureLifecycle.CreateAsync(() => FailSetupAsync(setupError),
                () => FailCleanupAsync(cleanupError)));

        Assert.Collection(actual.InnerExceptions,
            error => Assert.Same(setupError, error),
            error => Assert.Same(cleanupError, error));
        Assert.Contains(nameof(FailSetupAsync), setupError.StackTrace);
        Assert.Contains(nameof(FailCleanupAsync), cleanupError.StackTrace);
    }

    [Fact]
    public async Task Setup_cancellation_remains_visible_when_cleanup_also_fails()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var setupError = new OperationCanceledException(cancellation.Token);
        var cleanupError = new IOException("Controlled cleanup failure");

        var actual = await Assert.ThrowsAsync<AggregateException>(() =>
            SqlTestFixtureLifecycle.CreateAsync(() => FailSetupAsync(setupError),
                () => FailCleanupAsync(cleanupError)));

        Assert.Collection(actual.InnerExceptions,
            error => Assert.Same(setupError, error),
            error => Assert.Same(cleanupError, error));
        Assert.Equal(cancellation.Token, setupError.CancellationToken);
        Assert.Contains(nameof(FailSetupAsync), setupError.StackTrace);
    }

    [Fact]
    public async Task Successful_failure_cleanup_rethrows_original_setup_error_with_stack()
    {
        var setupError = new InvalidOperationException("Controlled setup failure");
        var cleaned = false;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqlTestFixtureLifecycle.CreateAsync(() => FailSetupAsync(setupError), () =>
            {
                cleaned = true;
                return ValueTask.CompletedTask;
            }));

        Assert.True(cleaned);
        Assert.Same(setupError, actual);
        Assert.Contains(nameof(FailSetupAsync), actual.StackTrace);
    }

    [Fact]
    public async Task Successful_initialization_returns_result_without_failure_cleanup()
    {
        var expected = new object();
        var cleaned = false;

        var actual = await SqlTestFixtureLifecycle.CreateAsync(() => Task.FromResult(expected), () =>
        {
            cleaned = true;
            return ValueTask.CompletedTask;
        });

        Assert.Same(expected, actual);
        Assert.False(cleaned);
    }

    [Fact]
    public async Task Failure_is_not_returned_until_asynchronous_cleanup_finishes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var setupError = new InvalidOperationException("Controlled setup failure");
        var operation = SqlTestFixtureLifecycle.CreateAsync(() => FailSetupAsync(setupError), async () =>
        {
            entered.TrySetResult();
            await release.Task;
        });

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(operation.IsCompleted);
        }
        finally { release.TrySetResult(); }

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Same(setupError, actual);
    }

    [Fact]
    public async Task Failed_creation_is_not_registered_or_cleaned_up_twice()
    {
        var scope = new TestScope();
        var cleanupCalls = 0;
        var setupError = new InvalidOperationException("Controlled setup failure");
        var creation = SqlTestFixtureLifecycle.CreateAsync<ControlledFixture>(
            () => Task.FromException<ControlledFixture>(setupError), () =>
            {
                cleanupCalls++;
                return ValueTask.CompletedTask;
            });

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Own(creation));
        await scope.DisposeAsync();

        Assert.Same(setupError, actual);
        Assert.Equal(1, cleanupCalls);
    }

    [Fact]
    public async Task Owned_fixture_remains_alive_until_lifecycle_cleanup_and_cleanup_is_awaited()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new ControlledFixture(async () =>
        {
            entered.TrySetResult();
            await release.Task;
        });
        var scope = new TestScope();
        Assert.Same(fixture, await scope.Own(Task.FromResult(fixture)));
        Assert.Equal(0, fixture.DisposeCalls);

        var cleanup = scope.DisposeAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(cleanup.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await cleanup;
        Assert.Equal(1, fixture.DisposeCalls);
    }

    [Fact]
    public async Task Lifecycle_cleanup_failure_is_propagated_without_swallowing()
    {
        var cleanupError = new IOException("Controlled lifecycle cleanup failure");
        var fixture = new ControlledFixture(() => FailCleanupAsync(cleanupError));
        var scope = new TestScope();
        await scope.Own(Task.FromResult(fixture));

        var actual = await Assert.ThrowsAsync<IOException>(() => scope.DisposeAsync());

        Assert.Same(cleanupError, actual);
        Assert.Contains(nameof(FailCleanupAsync), actual.StackTrace);
        Assert.Equal(1, fixture.DisposeCalls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("master")]
    [InlineData("application-database")]
    [InlineData("foreign-prefix")]
    [InlineData("invalid-guid")]
    [InlineData("different-server")]
    [InlineData("sql-authentication")]
    [InlineData("explicit-user")]
    [InlineData("explicit-password")]
    public async Task Catalog_configuration_rejects_non_owned_connections_before_opening_sql(string invalidCase)
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"DESKTOP-B5KA3LT\SQLEXPRESS02",
            InitialCatalog = "SpendWise_GroupDeletionS052_Test_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };
        switch (invalidCase)
        {
            case "empty": connection.InitialCatalog = ""; break;
            case "master": connection.InitialCatalog = "master"; break;
            case "application-database": connection.InitialCatalog = "SpendWiseDb"; break;
            case "foreign-prefix": connection.InitialCatalog = "Other_Test_" + Guid.NewGuid().ToString("N"); break;
            case "invalid-guid": connection.InitialCatalog = "SpendWise_GroupDeletionS052_Test_not-a-guid"; break;
            case "different-server": connection.DataSource = @"(localdb)\OtherInstance"; break;
            case "sql-authentication": connection.IntegratedSecurity = false; break;
            case "explicit-user": connection.UserID = "test-only-dummy-user"; break;
            case "explicit-password": connection.Password = "test-only-dummy-password"; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(connection.ConnectionString));
    }

    [Fact]
    public async Task Gate_wait_propagates_early_operation_failure_without_waiting_for_gate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new IOException("Controlled operation failure before gate");
        var operation = Task.FromException(expected);

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            SqlTestFixtureLifecycle.WaitForGateAsync(gate.Task, operation, TimeSpan.FromSeconds(5)));

        Assert.Same(expected, actual);
        Assert.False(gate.Task.IsCompleted);
    }

    [Fact]
    public async Task Gate_wait_propagates_early_operation_cancellation_without_waiting_for_gate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var operation = Task.FromCanceled(cancellation.Token);

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqlTestFixtureLifecycle.WaitForGateAsync(gate.Task, operation, TimeSpan.FromSeconds(5)));

        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.False(gate.Task.IsCompleted);
    }

    [Fact]
    public async Task Gate_wait_rejects_successful_operation_that_never_reached_gate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqlTestFixtureLifecycle.WaitForGateAsync(gate.Task, Task.CompletedTask, TimeSpan.FromSeconds(5)));

        Assert.False(gate.Task.IsCompleted);
    }

    [Fact]
    public async Task Gate_wait_returns_on_gate_while_pending_operation_remains_pending()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = SqlTestFixtureLifecycle.WaitForGateAsync(gate.Task, operation.Task, TimeSpan.FromSeconds(5));

        Assert.False(wait.IsCompleted);
        Assert.False(operation.Task.IsCompleted);
        gate.SetResult();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(operation.Task.IsCompleted);
    }

    [Theory]
    [InlineData("OtpS04")]
    [InlineData("GroupFundS052")]
    [InlineData("GroupDeletionS052")]
    [InlineData("GoalS053")]
    [InlineData("FinancialS05")]
    public void Dedicated_factory_accepts_only_owned_catalogs_with_safe_connection_options(string family)
    {
        var catalog = $"SpendWise_{family}_Test_0123456789abcdef0123456789abcdef";
        var actual = new SqlConnectionStringBuilder(SqlTestFixtureLifecycle.CreateOwnedConnectionString(
            catalog, @"DESKTOP-B5KA3LT\SQLEXPRESS02"));
        Assert.Equal(@"DESKTOP-B5KA3LT\SQLEXPRESS02", actual.DataSource);
        Assert.Equal(catalog, actual.InitialCatalog);
        Assert.True(actual.IntegratedSecurity);
        Assert.Empty(actual.UserID);
        Assert.Empty(actual.Password);
        Assert.False(actual.Pooling);
        Assert.Equal(15, actual.ConnectTimeout);
        Assert.True(actual.TrustServerCertificate);
        SqlTestFixtureLifecycle.ValidateOwnedConnection(actual.ConnectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(@"DESKTOP-B5KA3LT\SQLEXPRESS01")]
    [InlineData(@"DESKTOP-B5KA3LT\SQLEXPRESS")]
    [InlineData(@"(localdb)\MSSQLLocalDB")]
    [InlineData(@"OtherHost\Test")]
    [InlineData(@"tcp:DESKTOP-B5KA3LT\SQLEXPRESS02")]
    public void Dedicated_factory_rejects_unapproved_or_missing_instance(string? instance)
    {
        Assert.Throws<InvalidOperationException>(() => SqlTestFixtureLifecycle.CreateOwnedConnectionString(
            "SpendWise_GoalS053_Test_0123456789abcdef0123456789abcdef", instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("master")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    [InlineData("SpendWiseDb")]
    [InlineData("Other_Test_0123456789abcdef0123456789abcdef")]
    [InlineData("SpendWise_GoalS053_Test_0123456789abcdef0123456789abcdeg")]
    [InlineData("SpendWise_GoalS053_Test_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("SpendWise_GoalS053_Test_0123456789abcdef")]
    public void Dedicated_factory_rejects_non_owned_catalog(string catalog)
    {
        Assert.Throws<InvalidOperationException>(() => SqlTestFixtureLifecycle.CreateOwnedConnectionString(
            catalog, @"DESKTOP-B5KA3LT\SQLEXPRESS02"));
    }

    [Theory]
    [InlineData("attach-file")]
    [InlineData("user-instance")]
    [InlineData("failover-partner")]
    [InlineData("sql-authentication")]
    [InlineData("explicit-user")]
    [InlineData("explicit-password")]
    [InlineData("production-instance")]
    [InlineData("application-database")]
    public void Dedicated_validation_rejects_redirects_credentials_and_production_targets_before_sql(string invalidCase)
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"DESKTOP-B5KA3LT\SQLEXPRESS02",
            InitialCatalog = "SpendWise_FinancialS05_Test_0123456789abcdef0123456789abcdef",
            IntegratedSecurity = true, Pooling = false, ConnectTimeout = 15, TrustServerCertificate = true
        };
        switch (invalidCase)
        {
            case "attach-file": connection.AttachDBFilename = @"C:\test-only-dummy.mdf"; break;
            case "user-instance": connection.UserInstance = true; break;
            case "failover-partner": connection.FailoverPartner = @"DESKTOP-B5KA3LT\SQLEXPRESS01"; break;
            case "sql-authentication": connection.IntegratedSecurity = false; break;
            case "explicit-user": connection.UserID = "test-only-dummy-user"; break;
            case "explicit-password": connection.Password = "test-only-dummy-password"; break;
            case "production-instance": connection.DataSource = @"DESKTOP-B5KA3LT\SQLEXPRESS01"; break;
            case "application-database": connection.InitialCatalog = "SpendWiseDb"; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase));
        }
        Assert.Throws<InvalidOperationException>(() =>
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection.ConnectionString));
    }

    private sealed class TestScope : SqlTestFixtureScope
    {
        public Task<ControlledFixture> Own(Task<ControlledFixture> creation) => OwnAsync(creation);
    }

    private sealed class ControlledFixture(Func<ValueTask> cleanup) : IAsyncDisposable
    {
        public int DisposeCalls { get; private set; }
        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return cleanup();
        }
    }

    private static async Task<object> FailSetupAsync(Exception error)
    {
        await Task.Yield();
        throw error;
    }

    private static async ValueTask FailCleanupAsync(Exception error)
    {
        await Task.Yield();
        throw error;
    }
}
