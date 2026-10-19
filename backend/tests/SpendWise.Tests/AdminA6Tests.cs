using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.MobilePush;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA6Tests
{
    private static AdminA6Service CreateService(TargetedCorrectnessFixture f) =>
        new(
            f.Db,
            new(f.Db),
            new(f.Db),
            f.Clock
        );

    [Theory]
    [InlineData(SystemRole.Admin)]
    [InlineData(SystemRole.SuperAdmin)]
    [InlineData(SystemRole.User)]
    public async Task A6_HTTP_routes_enforce_canonical_roles_and_scope(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        Guid caseIdForTest;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var key = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "RoleTestUser",
                Email = key + "@test.local",
                NormalizedEmail = key + "@TEST.LOCAL",
                SpendId = key[..12],
                NormalizedSpendId = key[..12],
                PasswordHash = "hash",
                SystemRole = role
            };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
            db.Users.Add(user);
            db.UserSessions.Add(session);
            var targetUser = new User
            {
                FullName = "TargetCustomer",
                Email = "customer_" + key + "@test.local",
                NormalizedEmail = "CUSTOMER_" + key + "@TEST.LOCAL",
                SpendId = "c" + key[..11],
                NormalizedSpendId = "C" + key[..11],
                PasswordHash = "hash",
                SystemRole = SystemRole.User
            };
            db.Users.Add(targetUser);

            var supportCase = new SupportCase
            {
                LinkedUserId = targetUser.Id,
                Subject = "Case for A6 route test",
                Description = "Verification case",
                AssignedAdminId = user.Id,
                Status = CaseStatus.Investigating
            };
            db.SupportCases.Add(supportCase);
            await db.SaveChangesAsync();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);

            caseIdForTest = supportCase.Id;
        }

        // Search notifications
        var searchResp = await client.PostAsJsonAsync("/api/v1/admin/notifications/search",
            new { reason = "Audit notifications search", caseId = caseIdForTest });

        // Delivery history
        var deliveryResp = await client.PostAsJsonAsync("/api/v1/admin/notifications/delivery-history",
            new { reason = "Audit delivery history", caseId = caseIdForTest });

        // Delivery failures
        var failuresResp = await client.PostAsJsonAsync("/api/v1/admin/notifications/failures",
            new { reason = "Audit delivery failures" });

        // Templates catalog
        var templatesResp = await client.PostAsJsonAsync("/api/v1/admin/notifications/templates",
            new { reason = "Catalog review" });

        // Maintenance overview
        var maintenanceResp = await client.PostAsJsonAsync("/api/v1/admin/notifications/maintenance-overview",
            new { reason = "Maintenance check" });

        var denied = role == SystemRole.SuperAdmin ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        if (role == SystemRole.Admin)
        {
            Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
            Assert.Equal(HttpStatusCode.OK, deliveryResp.StatusCode);
            Assert.Equal(HttpStatusCode.OK, failuresResp.StatusCode);
            Assert.Equal(HttpStatusCode.OK, templatesResp.StatusCode);
            Assert.Equal(HttpStatusCode.OK, maintenanceResp.StatusCode);
        }
        else
        {
            Assert.Equal(denied, searchResp.StatusCode);
            Assert.Equal(denied, deliveryResp.StatusCode);
            Assert.Equal(denied, failuresResp.StatusCode);
            Assert.Equal(denied, templatesResp.StatusCode);
            Assert.Equal(denied, maintenanceResp.StatusCode);
        }
    }

    [Fact]
    public async Task A6_1_Notifications_Search_Redacts_OTPs_And_Auth_Tokens()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var n1 = new Notification
        {
            UserId = subject.Id,
            Type = NotificationType.SecurityAlert,
            Title = "Mã xác thực",
            Message = "Mã xác thực của bạn là 847291. Đừng chia sẻ cho ai.",
            DataJson = "{\"otp\":\"847291\",\"token\":\"Bearer abc123secretTokenValue\"}",
            CreatedAt = f.Clock.UtcNow
        };

        var n2 = new Notification
        {
            UserId = subject.Id,
            Type = NotificationType.MoneyReceived,
            Title = "Nhận tiền",
            Message = "Bạn vừa nhận được 500,000 VND từ Nguyễn Văn B.",
            DataJson = "{\"amount\":500000}",
            ReadAt = f.Clock.UtcNow,
            CreatedAt = f.Clock.UtcNow.AddMinutes(5)
        };

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "A6.1 Test Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };

        f.Db.AddRange(n1, n2, ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var paged = await service.NotificationsAsync(principal,
            new A6NotificationQuery("Search notifications", CaseId: ticket.Id, UserId: subject.Id), default);

        Assert.Equal(2, paged.Total);
        var item1 = Assert.Single(paged.Items, i => i.Id == n1.Id);
        Assert.DoesNotContain("847291", item1.Message);
        Assert.Contains("[REDACTED_CODE]", item1.Message);
        Assert.DoesNotContain("847291", item1.DataJson);
        Assert.DoesNotContain("abc123secretTokenValue", item1.DataJson);
        Assert.False(item1.IsRead);

        var item2 = Assert.Single(paged.Items, i => i.Id == n2.Id);
        Assert.True(item2.IsRead);
        Assert.NotNull(item2.ReadAt);
    }

    [Fact]
    public async Task A6_2_Delivery_History_Unifies_Channels_And_Distinguishes_Statuses()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        // 1. In-App Notification (Unread)
        var notif = new Notification
        {
            UserId = subject.Id,
            Type = NotificationType.GroupFundRequest,
            Title = "Yêu cầu đóng quỹ",
            Message = "Quỹ lớp đang yêu cầu đóng tiền",
            CreatedAt = f.Clock.UtcNow.AddMinutes(1)
        };

        // 2. Email Outbox Message
        var email = new EmailOutboxMessage
        {
            RecipientEmail = subject.Email,
            Subject = "Mã xác thực đăng nhập 123456",
            Status = EmailDeliveryStatus.Sent,
            SentAt = f.Clock.UtcNow.AddMinutes(2),
            CreatedAt = f.Clock.UtcNow.AddMinutes(2)
        };

        // 3. Mobile Push Delivery
        var installation = new MobilePushInstallation
        {
            UserId = subject.Id,
            Platform = MobilePlatform.Android,
            PushToken = "ExponentPushToken[super_secret_device_token_xyz]"
        };

        var pushDelivery = new MobilePushDelivery
        {
            Notification = notif,
            Installation = installation,
            Status = MobilePushDeliveryStatus.Completed,
            SubmittedAt = f.Clock.UtcNow.AddMinutes(3),
            CreatedAt = f.Clock.UtcNow.AddMinutes(3)
        };

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "A6.2 Test Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };

        f.Db.AddRange(notif, email, installation, pushDelivery, ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var paged = await service.DeliveryHistoryAsync(principal,
            new A6DeliveryHistoryQuery("Audit history", CaseId: ticket.Id, UserId: subject.Id), default);

        Assert.Equal(3, paged.Total);

        var inAppItem = Assert.Single(paged.Items, i => i.Channel == "InApp");
        Assert.Equal("Sent", inAppItem.Status); // Unread in-app is "Sent", never fake "UserRead"

        var emailItem = Assert.Single(paged.Items, i => i.Channel == "Email");
        Assert.Equal("Sent", emailItem.Status);
        Assert.DoesNotContain("123456", emailItem.TypeOrSubject);
        Assert.Contains("[CODE]", emailItem.TypeOrSubject);
        Assert.DoesNotContain(subject.Email, emailItem.MaskedRecipient); // Email is masked

        var pushItem = Assert.Single(paged.Items, i => i.Channel == "Push");
        Assert.Equal("ProviderAccepted", pushItem.Status);
        Assert.DoesNotContain("super_secret_device_token_xyz", pushItem.MaskedRecipient); // Push token is masked
    }

    [Fact]
    public async Task A6_3_Delivery_Failures_Returns_Sanitized_Errors()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);

        var failedEmail = new EmailOutboxMessage
        {
            RecipientEmail = "victim@example.com",
            Subject = "Test Email",
            Status = EmailDeliveryStatus.Failed,
            AttemptCount = 3,
            NextAttemptAt = f.Clock.UtcNow.AddHours(1),
            LastError = "SMTP server timeout connecting with token=secretBearerToken12345",
            CreatedAt = f.Clock.UtcNow
        };

        f.Db.Add(failedEmail);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var failures = await service.DeliveryFailuresAsync(principal,
            new A6DeliveryFailureQuery("Inspect failures"), default);

        Assert.NotEmpty(failures.Items);
        var failure = Assert.Single(failures.Items, i => i.Id == failedEmail.Id);
        Assert.Equal("Email", failure.Channel);
        Assert.DoesNotContain("victim@example.com", failure.RecipientMasked);
        Assert.DoesNotContain("secretBearerToken12345", failure.SanitizedError);
        Assert.Contains("[REDACTED_TOKEN]", failure.SanitizedError);
    }

    [Fact]
    public async Task A6_4_Maintenance_Overview_Is_ReadOnly_And_Accurate()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);

        var activePlan = new V1MaintenancePlan
        {
            Scope = "Global",
            StartsAt = f.Clock.UtcNow.AddMinutes(-30),
            ExpectedEndsAt = f.Clock.UtcNow.AddMinutes(60),
            Reason = "Nâng cấp cơ sở dữ liệu định kỳ",
            CreatedByUserId = admin.Id
        };

        var scheduledPlan = new V1MaintenancePlan
        {
            Scope = "Money",
            StartsAt = f.Clock.UtcNow.AddHours(2),
            ExpectedEndsAt = f.Clock.UtcNow.AddHours(4),
            Reason = "Bảo trì module thanh toán",
            CreatedByUserId = admin.Id
        };

        var announcement = new EmergencyAnnouncement
        {
            Title = "Bảo trì khẩn",
            Description = "Cập nhật hệ thống gateway",
            Status = EmergencyAnnouncementStatus.Active,
            CreatedByAdminId = admin.Id,
            CreatedAt = f.Clock.UtcNow
        };

        var sysState = new SystemMaintenanceState
        {
            IsEnabled = true,
            ChangedAt = f.Clock.UtcNow,
            ChangedByAdminId = admin.Id
        };

        f.Db.AddRange(activePlan, scheduledPlan, announcement, sysState);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var overview = await service.GetMaintenanceOverviewAsync(principal, "Inspect maintenance state", default);

        Assert.True(overview.SystemMaintenanceEnabled);
        Assert.Single(overview.ActivePlans, p => p.PlanId == activePlan.Id);
        Assert.Single(overview.ScheduledPlans, p => p.PlanId == scheduledPlan.Id);
        Assert.Single(overview.ActiveAnnouncements, a => a.Id == announcement.Id);
    }

    [Fact]
    public async Task A6_Redaction_Sanitizes_8_Digit_OTPs_And_Secrets()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var n = new Notification
        {
            UserId = subject.Id,
            Type = NotificationType.SecurityAlert,
            Title = "OTP 8 chữ số 98765432",
            Message = "Mã xác thực của bạn là 12345678 và mật khẩu là password='SuperSecretPassword123'",
            DataJson = "{\"secret\":\"topSecretValue\",\"apiKey\":\"myApiKey999\",\"otp\":\"87654321\"}",
            CreatedAt = f.Clock.UtcNow
        };

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "A6 Redaction Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };

        f.Db.AddRange(n, ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var paged = await service.NotificationsAsync(principal,
            new A6NotificationQuery("Check 8-digit redaction", CaseId: ticket.Id, UserId: subject.Id), default);

        var item = Assert.Single(paged.Items);
        Assert.DoesNotContain("12345678", item.Message);
        Assert.DoesNotContain("SuperSecretPassword123", item.Message);
        Assert.Contains("[REDACTED_CODE]", item.Message);
        Assert.Contains("[REDACTED_PASSWORD]", item.Message);
        Assert.DoesNotContain("topSecretValue", item.DataJson);
        Assert.DoesNotContain("myApiKey999", item.DataJson);
        Assert.DoesNotContain("87654321", item.DataJson);
    }

    [Fact]
    public async Task Notifications_Fails_When_CaseId_Is_Missing()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.NotificationsAsync(principal,
                new A6NotificationQuery("Attempt without case", CaseId: null, UserId: subject.Id), default));

        Assert.Equal("case_access_denied", ex.Code);
    }

    [Fact]
    public async Task DeliveryHistory_Fails_When_CaseId_Is_Missing()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeliveryHistoryAsync(principal,
                new A6DeliveryHistoryQuery("Attempt without case", CaseId: null, UserId: subject.Id), default));

        Assert.Equal("case_access_denied", ex.Code);
    }

    [Fact]
    public async Task Notifications_Denies_IDOR_When_Case_Belongs_To_Different_Admin()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin1 = await f.AddUserAsync("Admin1", role: SystemRole.Admin);
        var admin2 = await f.AddUserAsync("Admin2", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session2 = await f.AddSessionAsync(admin2.Id);

        // Case assigned to Admin1, but Admin2 attempts access
        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "Admin1 Case",
            Description = "Support ticket",
            AssignedAdminId = admin1.Id,
            Status = CaseStatus.Investigating
        };
        f.Db.SupportCases.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal2 = V1PermissionTests.Principal(admin2.Id, session2.Id, "Admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.NotificationsAsync(principal2,
                new A6NotificationQuery("IDOR probe", CaseId: ticket.Id, UserId: subject.Id), default));

        Assert.Equal("case_access_denied", ex.Code);
    }

    [Fact]
    public async Task Notifications_Denies_When_UserId_Mismatches_Case_Subject()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var victim = await f.AddUserAsync("Victim");
        var otherUser = await f.AddUserAsync("OtherUser");
        var session = await f.AddSessionAsync(admin.Id);

        var ticket = new SupportCase
        {
            LinkedUserId = victim.Id,
            Subject = "Victim Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };
        f.Db.SupportCases.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Admin has case for victim, but asks for otherUser
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.NotificationsAsync(principal,
                new A6NotificationQuery("Cross-user access", CaseId: ticket.Id, UserId: otherUser.Id), default));

        Assert.Equal("case_access_denied", ex.Code);
    }

    [Fact]
    public async Task DeliveryHistory_Denies_When_Case_Is_Closed_Or_Resolved()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var resolvedTicket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "Resolved Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Resolved
        };
        f.Db.SupportCases.Add(resolvedTicket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeliveryHistoryAsync(principal,
                new A6DeliveryHistoryQuery("Closed case check", CaseId: resolvedTicket.Id, UserId: subject.Id), default));

        Assert.Equal("case_access_denied", ex.Code);
    }

    [Fact]
    public async Task Notifications_Denies_When_Operator_Session_Is_Revoked()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id, revokedAt: f.Clock.UtcNow);

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "Revoked Session Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };
        f.Db.SupportCases.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.NotificationsAsync(principal,
                new A6NotificationQuery("Revoked session attempt", CaseId: ticket.Id, UserId: subject.Id), default));

        Assert.Equal("permission_denied", ex.Code);
    }

    [Fact]
    public async Task Notifications_Title_And_Message_Sanitize_PIN_And_Secrets()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var n = new Notification
        {
            UserId = subject.Id,
            Type = NotificationType.SecurityAlert,
            Title = "Mã OTP bí mật 847291 và PIN pin=123456",
            Message = "Token đăng nhập bearer abc123secretTokenValue",
            DataJson = "{\"pin\":\"654321\",\"apiKey\":\"superSecretApiKey\"}",
            CreatedAt = f.Clock.UtcNow
        };

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "Title Sanitization Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };
        f.Db.AddRange(n, ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var paged = await service.NotificationsAsync(principal,
            new A6NotificationQuery("Check title sanitization", CaseId: ticket.Id, UserId: subject.Id), default);

        var item = Assert.Single(paged.Items);
        Assert.DoesNotContain("847291", item.Title);
        Assert.DoesNotContain("123456", item.Title);
        Assert.Contains("[CODE]", item.Title);
        Assert.Contains("[REDACTED_PIN]", item.Title);
        Assert.DoesNotContain("abc123secretTokenValue", item.Message);
        Assert.Contains("[REDACTED_TOKEN]", item.Message);
        Assert.DoesNotContain("654321", item.DataJson);
        Assert.DoesNotContain("superSecretApiKey", item.DataJson);
    }

    [Fact]
    public async Task DeliveryHistory_Pagination_Accurate_Across_Over_200_Records()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOperator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var ticket = new SupportCase
        {
            LinkedUserId = subject.Id,
            Subject = "250 Items Pagination Case",
            Description = "Support ticket",
            AssignedAdminId = admin.Id,
            Status = CaseStatus.Investigating
        };
        f.Db.SupportCases.Add(ticket);

        // Add 150 Notifications (InApp)
        for (int i = 0; i < 150; i++)
        {
            f.Db.Notifications.Add(new Notification
            {
                UserId = subject.Id,
                Type = NotificationType.SystemAnnouncement,
                Title = $"Notif {i}",
                Message = $"Message {i}",
                CreatedAt = f.Clock.UtcNow.AddMinutes(-i)
            });
        }

        // Add 100 Emails
        for (int i = 0; i < 100; i++)
        {
            f.Db.EmailOutboxMessages.Add(new EmailOutboxMessage
            {
                RecipientEmail = subject.Email,
                Subject = $"Email Subject {i}",
                DeduplicationKey = $"test-dedup-a6-{Guid.NewGuid():N}-{i}",
                Status = EmailDeliveryStatus.Sent,
                CreatedAt = f.Clock.UtcNow.AddMinutes(-150 - i)
            });
        }

        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Page 1 (pageSize = 50)
        var p1 = await service.DeliveryHistoryAsync(principal,
            new A6DeliveryHistoryQuery("Page 1", CaseId: ticket.Id, UserId: subject.Id, Page: 1, PageSize: 50), default);

        Assert.Equal(250, p1.Total);
        Assert.Equal(50, p1.Items.Count);

        // Page 5 (items 201 to 250) -> deeper than 200 items
        var p5 = await service.DeliveryHistoryAsync(principal,
            new A6DeliveryHistoryQuery("Page 5", CaseId: ticket.Id, UserId: subject.Id, Page: 5, PageSize: 50), default);

        Assert.Equal(250, p5.Total);
        Assert.Equal(50, p5.Items.Count);
        // Ensure that page 1 and page 5 items are completely disjoint sets
        var p1Ids = p1.Items.Select(x => x.Id).ToHashSet();
        Assert.DoesNotContain(p5.Items, x => p1Ids.Contains(x.Id));
    }
}
