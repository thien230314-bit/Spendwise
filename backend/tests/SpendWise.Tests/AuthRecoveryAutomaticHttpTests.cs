using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AuthRecoveryAutomaticHttpTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Real_HTTP_forgot_request_is_automatically_delivered_without_dispatch_call_or_backlog(bool pin)
    {
        const string email="admin@automatic.fixture.invalid";
        var sender=new Sender();
        using var factory=new SpendWiseApiFactory(new Dictionary<string,string?>{["Email:WebsiteUrl"]="http://localhost:3000"},useIndependentDatabaseConnections:true);
        using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureTestServices(services=>
        {
            services.RemoveAll<ITransactionalEmailSender>();services.AddSingleton<ITransactionalEmailSender>(sender);
            services.AddSingleton(new AuthRecoveryDeliveryScope(DateTime.UtcNow.AddSeconds(-1),[email]));
            services.AddScoped<AuthRecoveryEmailDispatcher>();services.AddHostedService<AuthRecoveryEmailBackgroundService>();
        }));
        using var client=host.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        using(var scope=host.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();await db.Database.EnsureCreatedAsync();
            // This file-backed fixture has an HTTP writer, a worker and an observer on independent connections.
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            db.Users.Add(new User{FullName="Synthetic Admin",Email=email,NormalizedEmail=email.ToUpperInvariant(),
                SpendId=Guid.NewGuid().ToString("N"),NormalizedSpendId=Guid.NewGuid().ToString("N"),
                SystemRole=SystemRole.SystemAdmin,EmailVerifiedAt=DateTime.UtcNow});
            db.EmailOutboxMessages.Add(new EmailOutboxMessage{RecipientEmail=email,DeduplicationKey="old-backlog",
                HtmlBody="synthetic-backlog",TextBody="synthetic-backlog",Subject="old"});
            await db.SaveChangesAsync();
        }
        var response=await client.PostAsJsonAsync(pin?"/api/auth/pin/forgot":"/api/auth/forgot-password",new {identifier=email});
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
        var message=await sender.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(email,message.RecipientEmail);
        if(pin)Assert.Contains("http://localhost:3000/reset-pin?token=",message.TextBody);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while(true)
        {
            using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            if(await db.EmailOutboxMessages.AsNoTracking().AnyAsync(x=>x.Status==EmailDeliveryStatus.Sent))
            {
                Assert.Equal(1,await db.EmailOutboxMessages.CountAsync(x=>x.Status==EmailDeliveryStatus.Sent));
                Assert.Equal(EmailDeliveryStatus.Pending,(await db.EmailOutboxMessages.SingleAsync(x=>x.DeduplicationKey=="old-backlog")).Status);
                break;
            }
            if(deadline.IsCancellationRequested)
            {
                var metadata=await db.EmailOutboxMessages.AsNoTracking().Where(x=>x.DeduplicationKey!="old-backlog")
                    .Select(x=>new{x.Status,x.AttemptCount,x.LastError}).SingleAsync();
                Assert.Fail($"Automatic dispatch did not persist Sent: {metadata.Status}; attempts={metadata.AttemptCount}; safe error={metadata.LastError ?? "none"}; sender calls={sender.Count}");
            }
            await Task.Delay(10);
        }
        Assert.Equal(1,sender.Count);
    }
    private sealed class Sender:ITransactionalEmailSender
    {
        public int Count;
        public TaskCompletionSource<EmailMessage> Delivered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendAsync(EmailMessage message,CancellationToken ct){Interlocked.Increment(ref Count);Delivered.TrySetResult(message);return Task.CompletedTask;}
    }
}
