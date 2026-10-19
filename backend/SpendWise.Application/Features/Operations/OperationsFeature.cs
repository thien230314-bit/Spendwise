using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Operations;

public sealed record MaintenanceStateDto(bool IsEnabled, DateTime? ChangedAt);
public sealed record ChangeMaintenanceRequest(bool IsEnabled, string SecurityPin, string Reason);
public sealed record EmergencyAnnouncementDto(Guid Id, string Title, string Description, string Status,
    DateTime CreatedAt, DateTime? DeactivatedAt);
public sealed record PublishAnnouncementRequest(string Title, string Description, string SecurityPin, string Reason);
public sealed record DeactivateAnnouncementRequest(string SecurityPin, string Reason);

public sealed record GetMaintenanceStateQuery : IRequest<MaintenanceStateDto>;
public sealed record ChangeMaintenanceStateCommand(Guid AdminId, ChangeMaintenanceRequest Request) : IRequest<MaintenanceStateDto>;
public sealed record GetActiveAnnouncementQuery : IRequest<EmergencyAnnouncementDto?>;
public sealed record PublishAnnouncementCommand(Guid AdminId, PublishAnnouncementRequest Request) : IRequest<EmergencyAnnouncementDto>;
public sealed record DeactivateAnnouncementCommand(Guid AdminId, Guid AnnouncementId, DeactivateAnnouncementRequest Request) : IRequest;

public sealed class OperationsHandler(
    ISpendWiseDbContext db,
    IClock clock,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    INotificationService notifications,
    IRealtimeNotifier realtime) :
    IRequestHandler<GetMaintenanceStateQuery, MaintenanceStateDto>,
    IRequestHandler<ChangeMaintenanceStateCommand, MaintenanceStateDto>,
    IRequestHandler<GetActiveAnnouncementQuery, EmergencyAnnouncementDto?>,
    IRequestHandler<PublishAnnouncementCommand, EmergencyAnnouncementDto>,
    IRequestHandler<DeactivateAnnouncementCommand>
{
    public async Task<MaintenanceStateDto> Handle(GetMaintenanceStateQuery query, CancellationToken cancellationToken)
    {
        // Bản ghi mới nhất là trạng thái vận hành có hiệu lực.
        var state = await db.SystemMaintenanceStates.AsNoTracking()
            .OrderByDescending(item => item.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var planned = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && p.ClosedAt == null && p.StartsAt <= clock.UtcNow)
            .OrderByDescending(p => p.StartsAt).Select(p => (DateTime?)p.StartsAt).FirstOrDefaultAsync(cancellationToken);
        return new MaintenanceStateDto((state?.IsEnabled ?? false) || planned is not null, planned ?? state?.ChangedAt);
    }

    public async Task<MaintenanceStateDto> Handle(ChangeMaintenanceStateCommand command, CancellationToken cancellationToken)
    {
        // PIN quản trị phải được xác minh ngay trong thao tác nhạy cảm này.
        await security.VerifyAsync(command.AdminId, command.Request.SecurityPin, cancellationToken);
        AdminAuditWriter.RequireReason(command.Request.Reason);

        var state = await db.SystemMaintenanceStates
            .OrderByDescending(item => item.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SystemMaintenanceState();
            db.SystemMaintenanceStates.Add(state);
        }

        state.IsEnabled = command.Request.IsEnabled;
        state.ChangedAt = clock.UtcNow;
        state.ChangedByAdminId = command.AdminId;
        audit.Add(command.AdminId, "maintenance.change", nameof(SystemMaintenanceState), state.Id.ToString(),
            command.Request.Reason, new { state.IsEnabled });
        await db.SaveChangesAsync(cancellationToken);

        var result = new MaintenanceStateDto(state.IsEnabled, state.ChangedAt);
        await realtime.SendAllAsync("maintenanceChanged", result, cancellationToken);
        return result;
    }

    public async Task<EmergencyAnnouncementDto?> Handle(GetActiveAnnouncementQuery query, CancellationToken cancellationToken)
    {
        var item = await db.EmergencyAnnouncements.AsNoTracking()
            .Where(candidate => candidate.Status == EmergencyAnnouncementStatus.Active)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<EmergencyAnnouncementDto> Handle(PublishAnnouncementCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.AdminId, command.Request.SecurityPin, cancellationToken);
        var title = InputBoundary.RequiredText(command.Request.Title, 200, "Announcement title");
        var description = InputBoundary.RequiredText(command.Request.Description, 4000, "Announcement description");
        AdminAuditWriter.RequireReason(command.Request.Reason);

        var result = await db.ExecuteInTransactionAsync(async token =>
        {
            // Chỉ một thông báo khẩn cấp được Active tại một thời điểm.
            var activeItems = await db.EmergencyAnnouncements
                .Where(item => item.Status == EmergencyAnnouncementStatus.Active)
                .ToListAsync(token);
            foreach (var active in activeItems)
            {
                active.Status = EmergencyAnnouncementStatus.Inactive;
                active.DeactivatedAt = clock.UtcNow;
            }

            var item = new EmergencyAnnouncement
            {
                Title = title,
                Description = description,
                CreatedByAdminId = command.AdminId,
                Status = EmergencyAnnouncementStatus.Active
            };
            db.EmergencyAnnouncements.Add(item);
            audit.Add(command.AdminId, "announcement.publish", nameof(EmergencyAnnouncement), item.Id.ToString(),
                command.Request.Reason, new { item.Title });
            await db.SaveChangesAsync(token);

            // Notification được lưu cho mọi User; SystemAdmin không nhận thông báo nghiệp vụ User.
            var userIds = await db.Users.AsNoTracking()
                .Where(user => user.SystemRole == SystemRole.User && user.Status == UserStatus.Active)
                .Select(user => user.Id)
                .ToListAsync(token);
            if (userIds.Count > 0)
            {
                await notifications.CreateManyAsync(userIds.Select(userId => new CreateNotificationRequest(
                    userId, "EmergencyAnnouncement", title, description,
                    new { announcementId = item.Id, actionRoute = "/announcements", actionLabelKey = "announcement.view" }))
                    .ToList(), token);
            }

            return Map(item);
        }, cancellationToken);

        await realtime.SendAllAsync("emergencyAnnouncement", result, cancellationToken);
        return result;
    }

    public async Task Handle(DeactivateAnnouncementCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.AdminId, command.Request.SecurityPin, cancellationToken);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        var item = await db.EmergencyAnnouncements.SingleOrDefaultAsync(
            candidate => candidate.Id == command.AnnouncementId, cancellationToken)
            ?? throw new NotFoundException("announcement_not_found", "Announcement not found.");
        if (item.Status != EmergencyAnnouncementStatus.Active)
        {
            throw new ConflictException("announcement_inactive", "Announcement is already inactive.");
        }

        item.Status = EmergencyAnnouncementStatus.Inactive;
        item.DeactivatedAt = clock.UtcNow;
        audit.Add(command.AdminId, "announcement.deactivate", nameof(EmergencyAnnouncement), item.Id.ToString(),
            command.Request.Reason, null);
        await db.SaveChangesAsync(cancellationToken);
        await realtime.SendAllAsync("emergencyAnnouncement", new { active = false, item.Id }, cancellationToken);
    }

    private static EmergencyAnnouncementDto Map(EmergencyAnnouncement item) =>
        new(item.Id, item.Title, item.Description, item.Status.ToString(), item.CreatedAt, item.DeactivatedAt);
}
