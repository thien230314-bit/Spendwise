using MediatR;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Application.Features.AdminAi;

public enum AdminAiReportFormat { Word, Excel }
public enum AdminAiReportType { SystemOverview }

public sealed record AdminAiReportRequest(
    string Format,
    string ReportType = "SystemOverview",
    int PeriodDays = 30);

public sealed record AdminAiReportResult(byte[] Content, string ContentType, string FileName);

public interface IAdminAiReportExporter
{
    AdminAiReportResult Export(AdminAiContext context, AdminAiReportFormat format);
}

public sealed record ExportAdminAiReportCommand(AdminAiReportRequest Request) : IRequest<AdminAiReportResult>;

public sealed class ExportAdminAiReportCommandHandler(
    AdminAiContextBuilder contextBuilder,
    IAdminAiReportExporter exporter)
    : IRequestHandler<ExportAdminAiReportCommand, AdminAiReportResult>
{
    public async Task<AdminAiReportResult> Handle(ExportAdminAiReportCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<AdminAiReportFormat>(command.Request.Format, true, out var format)
            || !Enum.IsDefined(format))
            throw new AppException("invalid_admin_ai_report_format", "Report format must be Word or Excel.");
        if (!Enum.TryParse<AdminAiReportType>(command.Request.ReportType, true, out var reportType)
            || !Enum.IsDefined(reportType) || reportType != AdminAiReportType.SystemOverview)
            throw new AppException("invalid_admin_ai_report_type", "Report type must be SystemOverview.");
        if (command.Request.PeriodDays is < 1 or > 365)
            throw new AppException("invalid_admin_ai_report_period", "Report period must be between 1 and 365 days.");

        var context = await contextBuilder.BuildAsync(command.Request.PeriodDays, ct);
        return exporter.Export(context, format);
    }
}
