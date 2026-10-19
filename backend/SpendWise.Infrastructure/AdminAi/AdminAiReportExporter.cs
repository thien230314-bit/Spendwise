using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SpendWise.Application.Features.AdminAi;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SpendWise.Infrastructure.AdminAi;

public sealed class AdminAiReportExporter : IAdminAiReportExporter
{
    public const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public AdminAiReportResult Export(AdminAiContext context, AdminAiReportFormat format) => format switch
    {
        AdminAiReportFormat.Word => new AdminAiReportResult(
            CreateWord(context),
            WordContentType,
            FileName(context, "docx")),
        AdminAiReportFormat.Excel => new AdminAiReportResult(
            CreateExcel(context),
            ExcelContentType,
            FileName(context, "xlsx")),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static byte[] CreateWord(AdminAiContext context)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document();
            var body = main.Document.AppendChild(new W.Body());
            body.Append(Paragraph("SpendWise System Overview", true, "32"));
            body.Append(Paragraph($"Generated: {Timestamp(context.GeneratedAtUtc)}"));
            body.Append(Paragraph($"Reporting window: {Timestamp(context.WindowStartUtc)} to {Timestamp(context.GeneratedAtUtc)} ({context.PeriodDays} days)"));

            AppendSection(body, "Users", UserRows(context));
            AppendSection(body, "Transactions and Ledger", TransactionRows(context));
            AppendSection(body, "Cases", CaseRows(context));
            AppendSection(body, "Banking and Reconciliation", BankingRows(context));
            AppendSection(body, "System", SystemRows(context));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] CreateExcel(AdminAiContext context)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new S.Sheets());
            uint sheetId = 1;

            AddWorksheet(workbookPart, sheets, sheetId++, "Overview",
                new[]
                {
                    ("SpendWise System Overview", ""),
                    ("Generated UTC", Timestamp(context.GeneratedAtUtc)),
                    ("Window start UTC", Timestamp(context.WindowStartUtc)),
                    ("Period days", Number(context.PeriodDays))
                }.Concat(UserRows(context)).Concat(SystemRows(context)));
            AddWorksheet(workbookPart, sheets, sheetId++, "Transactions", TransactionRows(context));
            AddWorksheet(workbookPart, sheets, sheetId++, "Cases", CaseRows(context));
            AddWorksheet(workbookPart, sheets, sheetId, "Banking-System", BankingRows(context));
            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static void AppendSection(W.Body body, string title, IEnumerable<(string Label, string Value)> rows)
    {
        body.Append(Paragraph(title, true, "26"));
        var table = new W.Table();
        table.AppendChild(new W.TableProperties(
            new W.TableBorders(
                Border<W.TopBorder>(), Border<W.LeftBorder>(), Border<W.BottomBorder>(),
                Border<W.RightBorder>(), Border<W.InsideHorizontalBorder>(), Border<W.InsideVerticalBorder>())));
        table.AppendChild(new W.TableGrid(
            new W.GridColumn { Width = "6500" },
            new W.GridColumn { Width = "3000" }));
        table.Append(Row("Metric", "Value", true));
        foreach (var row in rows) table.Append(Row(row.Label, row.Value));
        body.Append(table);
    }

    private static T Border<T>() where T : W.BorderType, new() => new()
    {
        Val = W.BorderValues.Single,
        Size = 4U,
        Color = "B8C2CC"
    };

    private static W.TableRow Row(string label, string value, bool bold = false) =>
        new(Cell(label, bold), Cell(value, bold));

    private static W.TableCell Cell(string value, bool bold) =>
        new(new W.Paragraph(new W.Run(
            bold ? new W.RunProperties(new W.Bold()) : new W.RunProperties(),
            new W.Text(value) { Space = SpaceProcessingModeValues.Preserve })));

    private static W.Paragraph Paragraph(string value, bool bold = false, string? fontSize = null)
    {
        var properties = new W.RunProperties();
        if (bold) properties.Append(new W.Bold());
        if (fontSize is not null) properties.Append(new W.FontSize { Val = fontSize });
        return new W.Paragraph(new W.Run(properties,
            new W.Text(value) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static void AddWorksheet(
        WorkbookPart workbookPart,
        S.Sheets sheets,
        uint sheetId,
        string name,
        IEnumerable<(string Label, string Value)> rows)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new S.SheetData();
        sheetData.Append(SpreadsheetRow("Metric", "Value"));
        foreach (var row in rows) sheetData.Append(SpreadsheetRow(row.Label, row.Value));
        worksheetPart.Worksheet = new S.Worksheet(
            new S.Columns(
                new S.Column { Min = 1, Max = 1, Width = 36, CustomWidth = true },
                new S.Column { Min = 2, Max = 2, Width = 24, CustomWidth = true }),
            sheetData);
        worksheetPart.Worksheet.Save();
        sheets.Append(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = name
        });
    }

    private static S.Row SpreadsheetRow(string label, string value) =>
        new(SpreadsheetCell(label), SpreadsheetCell(value));

    private static S.Cell SpreadsheetCell(string value) => new()
    {
        DataType = S.CellValues.InlineString,
        InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static IEnumerable<(string Label, string Value)> UserRows(AdminAiContext context) =>
    [
        ("Users total", Number(context.Users.Total)),
        ("Users active", Number(context.Users.Active)),
        ("Users restricted", Number(context.Users.Restricted)),
        ("Users locked", Number(context.Users.Locked)),
        ("Users suspended", Number(context.Users.Suspended)),
        ("Users new in window", Number(context.Users.NewInWindow))
    ];

    private static IEnumerable<(string Label, string Value)> TransactionRows(AdminAiContext context) =>
    [
        ("Transactions count", Number(context.Transactions.Count)),
        ("Transactions volume VND", Money(context.Transactions.Volume)),
        ("Transactions completed", Number(context.Transactions.Completed)),
        ("Transactions pending", Number(context.Transactions.Pending)),
        ("Transactions failed", Number(context.Transactions.Failed)),
        ("Transaction success rate %", Money(context.Transactions.SuccessRate)),
        ("Ledger transactions count", Number(context.Transactions.Ledger.Count)),
        ("Ledger pending", Number(context.Transactions.Ledger.Pending)),
        ("Ledger completed", Number(context.Transactions.Ledger.Completed)),
        ("Ledger failed", Number(context.Transactions.Ledger.Failed)),
        ("Ledger reversed", Number(context.Transactions.Ledger.Reversed))
    ];

    private static IEnumerable<(string Label, string Value)> CaseRows(AdminAiContext context) =>
    [
        ("Support cases awaiting", Number(context.Cases.Support.Awaiting)),
        ("Support cases new", Number(context.Cases.Support.NewCases)),
        ("Support cases waiting for user", Number(context.Cases.Support.WaitingForUser)),
        ("Disputes open", Number(context.Cases.Disputes.Open)),
        ("Disputes investigating", Number(context.Cases.Disputes.Investigating)),
        ("Risk reviews pending", Number(context.Cases.PendingRiskReviews))
    ];

    private static IEnumerable<(string Label, string Value)> BankingRows(AdminAiContext context) =>
    [
        ("Direct payments pending", Number(context.Banking.DirectPayments.Pending)),
        ("Direct payments awaiting reconciliation", Number(context.Banking.DirectPayments.AwaitingReconciliation)),
        ("Direct payments completed in window", Number(context.Banking.DirectPayments.CompletedInWindow)),
        ("Direct payments failed in window", Number(context.Banking.DirectPayments.FailedInWindow)),
        ("Reconciliation events unmatched in window", Number(context.Banking.Reconciliation.UnmatchedInWindow)),
        ("Reconciliation events failed in window", Number(context.Banking.Reconciliation.FailedInWindow)),
        ("Bank provider enabled", YesNo(context.Banking.Provider.Enabled)),
        ("Bank provider configured", YesNo(context.Banking.Provider.Configured)),
        ("Bank provider environment", context.Banking.Provider.Environment)
    ];

    private static IEnumerable<(string Label, string Value)> SystemRows(AdminAiContext context) =>
    [
        ("Database health", context.System.Database),
        ("AI provider status", context.System.AiProvider),
        ("Maintenance enabled", YesNo(context.System.MaintenanceEnabled)),
        ("Notification outbox pending", Number(context.System.NotificationsOutbox.Pending)),
        ("Notification outbox failed", Number(context.System.NotificationsOutbox.Failed)),
        ("Audit events in window", Number(context.System.AuditEventsInWindow))
    ];

    private static string FileName(AdminAiContext context, string extension) =>
        $"SpendWise-System-Overview-{context.GeneratedAtUtc:yyyyMMdd-HHmm}.{extension}";
    private static string Timestamp(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string YesNo(bool value) => value ? "Yes" : "No";
}
