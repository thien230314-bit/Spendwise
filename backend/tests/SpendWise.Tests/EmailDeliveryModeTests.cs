using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Khóa hành vi Pickup/SMTP để Development có thể thử email thật một cách chủ động.
public sealed class EmailDeliveryModeTests
{
    private static readonly EmailMessage Message = new(
        "recipient@example.test",
        "SpendWise test",
        "<p>secret-message-body</p>",
        "secret-message-body");

    [Theory]
    [InlineData(null)]
    [InlineData("Pickup")]
    public async Task Development_defaults_to_or_accepts_pickup(string? deliveryMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"spendwise-email-{Guid.NewGuid():N}");
        try
        {
            var values = new Dictionary<string, string?>
            {
                ["PasswordReset:PickupDirectory"] = directory,
                ["Email:DeliveryMode"] = deliveryMode
            };
            var sender = CreateSender(values, Environments.Development);

            await sender.SendAsync(Message, default);

            var pickup = Assert.Single(Directory.GetFiles(directory, "email-*.txt"));
            Assert.Contains("recipient@example.test", await File.ReadAllTextAsync(pickup));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Development_smtp_uses_existing_smtp_path_without_pickup()
    {
        await using var server = new LocalSmtpServer();
        var logger = new CollectingLogger<ConfiguredEmailSender>();
        var sender = CreateSender(SmtpValues(server.Port), Environments.Development, logger);

        await sender.SendAsync(Message, default);
        var received = await server.MessageReceived.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("secret-message-body", received);
        Assert.DoesNotContain("secret-message-body", logger.Text, StringComparison.Ordinal);
        Assert.Contains("Transactional email delivered", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_delivery_mode_fails_with_controlled_configuration_error()
    {
        var sender = CreateSender(new Dictionary<string, string?>
        {
            ["Email:DeliveryMode"] = "Automatic"
        }, Environments.Development);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(Message, default));

        Assert.Equal("Email:DeliveryMode must be either Pickup or Smtp.", error.Message);
    }

    [Fact]
    public async Task Non_development_rejects_pickup_instead_of_falling_back()
    {
        var sender = CreateSender(new Dictionary<string, string?>
        {
            ["Email:DeliveryMode"] = "Pickup"
        }, Environments.Production);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(Message, default));

        Assert.Equal("Email:DeliveryMode=Pickup is allowed only in Development.", error.Message);
    }

    [Fact]
    public async Task Non_development_without_mode_requires_smtp_configuration()
    {
        var sender = CreateSender([], Environments.Production);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(Message, default));

        Assert.Equal(
            "Email SMTP configuration requires Email:Smtp:Host and Email:Smtp:From.",
            error.Message);
    }

    [Theory]
    [InlineData("Email:Smtp:Host", "not a valid host", "Email:Smtp:Host must be a valid DNS name or IP address.")]
    [InlineData("Email:Smtp:Port", "0", "Email:Smtp:Port must be an integer between 1 and 65535.")]
    [InlineData("Email:Smtp:Port", "65536", "Email:Smtp:Port must be an integer between 1 and 65535.")]
    [InlineData("Email:Smtp:Port", "abc", "Email:Smtp:Port must be an integer between 1 and 65535.")]
    [InlineData("Email:Smtp:EnableSsl", "sometimes", "Email:Smtp:EnableSsl must be true or false.")]
    [InlineData("Email:Smtp:From", "invalid", "Email:Smtp:From must be a valid email address.")]
    public async Task Smtp_mode_validates_required_value_formats(
        string key,
        string value,
        string expectedMessage)
    {
        var values = SmtpValues(2525);
        values[key] = value;
        var sender = CreateSender(values, Environments.Development);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(Message, default));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData("smtp-user", null)]
    [InlineData(null, "smtp-password")]
    public async Task Smtp_username_and_password_must_be_configured_together(
        string? username,
        string? password)
    {
        var values = SmtpValues(2525);
        values["Email:Smtp:Username"] = username;
        values["Email:Smtp:Password"] = password;
        var logger = new CollectingLogger<ConfiguredEmailSender>();
        var sender = CreateSender(values, Environments.Development, logger);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(Message, default));

        Assert.Equal(
            "Email:Smtp:Username and Email:Smtp:Password must be configured together.",
            error.Message);
        Assert.DoesNotContain("smtp-password", logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-message-body", logger.Text, StringComparison.Ordinal);
    }

    private static ConfiguredEmailSender CreateSender(
        IEnumerable<KeyValuePair<string, string?>> values,
        string environmentName,
        ILogger<ConfiguredEmailSender>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ConfiguredEmailSender(
            configuration,
            new TestEnvironment(environmentName),
            logger ?? new CollectingLogger<ConfiguredEmailSender>(),
            new EmailTemplateRenderer());
    }

    private static Dictionary<string, string?> SmtpValues(int port) => new()
    {
        ["Email:DeliveryMode"] = "Smtp",
        ["Email:Smtp:Host"] = IPAddress.Loopback.ToString(),
        ["Email:Smtp:From"] = "sender@example.test",
        ["Email:Smtp:Port"] = port.ToString(),
        ["Email:Smtp:EnableSsl"] = "false"
    };

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SpendWise.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        private readonly List<string> entries = [];
        public string Text => string.Join(Environment.NewLine, entries);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(formatter(state, exception));
    }

    private sealed class LocalSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new();
        private readonly Task serverTask;
        private readonly TaskCompletionSource<string> messageReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LocalSmtpServer()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverTask = RunAsync();
        }

        public int Port { get; }
        public Task<string> MessageReceived => messageReceived.Task;

        private async Task RunAsync()
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(stopping.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };
                await writer.WriteLineAsync("220 localhost SpendWise test SMTP");

                var data = new StringBuilder();
                var readingData = false;
                while (await reader.ReadLineAsync(stopping.Token) is { } line)
                {
                    if (readingData)
                    {
                        if (line == ".")
                        {
                            readingData = false;
                            messageReceived.TrySetResult(data.ToString());
                            await writer.WriteLineAsync("250 accepted");
                        }
                        else
                        {
                            data.AppendLine(line);
                        }
                        continue;
                    }

                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 SIZE 10485760");
                    }
                    else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)
                             || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250 accepted");
                    }
                    else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        readingData = true;
                        await writer.WriteLineAsync("354 end with dot");
                    }
                    else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 bye");
                        break;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 accepted");
                    }
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                // Test kết thúc trước khi có kết nối SMTP.
            }
            catch (Exception exception)
            {
                messageReceived.TrySetException(exception);
            }
        }

        public async ValueTask DisposeAsync()
        {
            stopping.Cancel();
            listener.Stop();
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
                // Hủy listener là hành vi dọn dẹp bình thường của test.
            }
            stopping.Dispose();
        }
    }
}
