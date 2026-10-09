using System.Globalization;
using System.Net;
using System.Text;
using OfficialLedger.Models;

namespace OfficialLedger.Tax;

public static class TaxReportExporter
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public const string PrintScript = "document.querySelector('[data-print]').addEventListener('click', () => window.print());";
    public const string IncomeBasis = "Received totals use the date full payment was received or made available, including payments for games worked in other years. Game fees and travel allowances are separate. Undated paid games are excluded and listed for review. Employee payments are separated for W-2 reconciliation.";
    public const string ExpenseBasis = "Paid-expense totals use recorded payment dates, including expenses incurred in other years. Business portions use user-entered percentages, rounded per expense; unknown percentages remain unallocated. Reimbursements are separate and are not automatically netted or added to income. Spending totals are not deductible amounts.";
    public const string MileageBasis = "Miles are recorded trip distances. User classifications do not establish deductibility. Unconfirmed trip dates use game dates for organization. The preparer should confirm commuting rules, duplicate trips, vehicle records, and standard-mileage versus actual-expense treatment. No mileage deduction is calculated.";
    public const string ScopeNote = "This worksheet contains BlueLedger records only. Reconcile with all 1099s, W-2s, bank/payment-platform records, split payments, refunds, other income, and expenses recorded elsewhere. It is a preparer worksheet, not an IRS filing form or a calculation of tax liability.";

    public static Dictionary<string, byte[]> CsvFiles(TaxReport report) => new()
    {
        ["summary.csv"] = Csv(["Item", "Amount or count", "Basis"], SummaryRows(report)),
        ["income.csv"] = Csv(["Game ID", "Game date", "Received date", "Payer", "Income type", "Game fee", "Travel allowance", "Total received", "Season", "Sport", "Location", "Notes"],
            report.ReceivedGames.Select(g => Row(g.Id, g.GameDate, g.PaidDate, TaxReport.Payer(g), TaxRecordLabels.Income(g.IncomeKind), g.FeeAmount, g.TravelReimbursement, TaxReport.ReceivedAmount(g), g.Season?.Name, g.SportType?.Name, g.LocationName, g.Notes))),
        ["payer-summary.csv"] = Csv(["Payer", "Income type", "Payments", "Game fees", "Travel allowances", "Total received"],
            report.Payers.Select(p => Row(p.Payer, TaxRecordLabels.Income(p.IncomeKind), p.Count, p.Fees, p.Reimbursements, p.Total))),
        ["expenses.csv"] = Csv(["Expense ID", "Expense date", "Paid date", "Category", "Payee", "Description", "Recorded amount", "Business use percent", "Recorded business portion", "Reimbursement", "Receipt count", "Notes"],
            report.PaidExpenses.Select(e => ExpenseRow(report, e))),
        ["expense-summary.csv"] = Csv(["Category", "Records", "Recorded spending", "Recorded business portion", "Unallocated records", "Reimbursements"],
            report.Categories.Select(c => Row(c.Category, c.Count, c.Amount, c.BusinessPortion, c.UnclassifiedCount, c.Reimbursements))),
        ["mileage.csv"] = Csv(["Game ID", "Trip date used", "Date confirmed", "Game date", "Origin or route", "Destination", "Purpose", "Miles", "Classification", "Notes"],
            report.MileageGames.Select(g => Row(g.Id, TaxReport.MileageDate(g), g.TravelDate.HasValue, g.GameDate, g.TravelOrigin, g.LocationName, g.TravelPurpose, g.MilesDriven, TaxRecordLabels.Mileage(g.MileageKind), g.Notes))),
        ["unassigned-income.csv"] = Csv(["Game ID", "Game date", "Payer", "Income type", "Game fee", "Travel allowance", "Recorded paid amount", "Notes", "Treatment"],
            report.UndatedPaidGames.Select(g => Row(g.Id, g.GameDate, TaxReport.Payer(g), TaxRecordLabels.Income(g.IncomeKind), g.FeeAmount, g.TravelReimbursement, TaxReport.ReceivedAmount(g), g.Notes, "Payment year unknown; excluded from annual totals"))),
        ["unassigned-expenses.csv"] = Csv(["Expense ID", "Expense date", "Paid date", "Category", "Payee", "Description", "Recorded amount", "Business use percent", "Recorded business portion", "Reimbursement", "Receipt count", "Notes"],
            report.UndatedExpenses.Select(e => ExpenseRow(report, e))),
        ["games-worked.csv"] = Csv(["Game ID", "Game date", "Location", "Fee", "Marked paid", "Received date", "Travel allowance", "Notes", "Basis"],
            report.WorkGames.Select(g => Row(g.Id, g.GameDate, g.LocationName, g.FeeAmount, g.IsPaid, g.PaidDate, g.TravelReimbursement, g.Notes, "Informational work-year appendix; not added to received totals"))),
        ["review-items.csv"] = Csv(["Area", "Record", "Review item", "BlueLedger record path"],
            report.ReviewItems.Select(i => Row(i.Area, i.Record, i.Reason, i.Href)))
    };

    private static IEnumerable<object?[]> SummaryRows(TaxReport r) =>
    [
        Row("Tax year", r.Year, "Calendar year"), Row("Official", r.OwnerName, "Profile"),
        Row("Business name", r.Profile.BusinessName, "User-entered"),
        Row("Generated UTC", r.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture), "Export snapshot"),
        Row("Fees received", r.FeesReceived, "Known receipt dates"), Row("Travel allowances received", r.TravelAllowancesReceived, "Included in received total; tax treatment requires review"),
        Row("Total received", r.TotalReceived, "Known receipt dates; all classifications"),
        Row("Contractor receipts", r.ContractorReceipts, "User-classified; includes travel allowances"),
        Row("Employee receipts", r.EmployeeReceipts, "Reconcile with W-2 gross wages and withholding"),
        Row("Unclassified receipts", r.UnclassifiedReceipts, "Confirm income classification"),
        Row("Paid expenses", r.PaidExpenseTotal, "Known payment dates; before tax adjustments"),
        Row("Recorded business portion", r.RecordedBusinessPortion, "User percentages; not a deduction calculation"),
        Row("Expense reimbursements", r.ExpenseReimbursements, "Separate; not automatically netted"),
        Row("Miles logged", r.TotalMiles, "Recorded trips; not a deduction"),
        Row("User-classified business miles", r.BusinessMiles, "Confirm eligibility and vehicle records"),
        Row("Commuting or personal miles", r.PersonalMiles, "User-classified"),
        Row("Unclassified miles", r.UnclassifiedMiles, "Needs review"),
        Row("Paid amounts with unknown tax year", r.UndatedIncomeTotal, "All recorded years; excluded from annual totals"),
        Row("Expenses with unknown payment year", r.UndatedExpenseTotal, "All recorded years; excluded from annual totals"),
        Row("Outstanding fees for games worked this year", r.OutstandingFees, "Current unpaid status at export; not historical year-end receivables"),
        Row("Review items", r.ReviewItems.Count, "Reminders for preparer review"),
        Row("Preparer notes", r.Profile.PreparerNotes, "User-entered"),
        Row("Scope", ScopeNote, "Worksheet limitations")
    ];

    private static object?[] ExpenseRow(TaxReport r, Expense e) => Row(e.Id, e.ExpenseDate, e.PaidDate, e.Category, e.Vendor,
        e.BusinessPurpose, e.Amount, e.BusinessUsePercent, TaxReport.BusinessPortion(e), e.ReimbursedAmount, r.ReceiptCount(e.Id), e.Notes);

    public static byte[] ReceiptManifest(TaxReport r, IReadOnlyDictionary<int, string>? paths = null, bool included = false) =>
        Csv(["Receipt ID", "Expense ID", "Original filename", "MIME type", "Bytes", "Packet file or status"],
            r.Receipts.Select(x => Row(x.Id, x.ExpenseId, x.FileName, x.ContentType, x.SizeBytes,
                included ? paths?.GetValueOrDefault(x.Id) ?? "Unavailable at export" : "Attachments not included")));

    public static string ReceiptPath(TaxReceipt receipt)
    {
        // Keep path separators, Unicode tricks, and filesystem punctuation out of the archive path.
        var safe = new string(receipt.FileName.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        safe = safe.Trim('.');
        if (safe.Length == 0) safe = "receipt";
        return $"receipts/expense-{receipt.ExpenseId}/receipt-{receipt.Id}-{safe}";
    }

    public static string Html(TaxReport r, bool packet = false, bool receiptsIncluded = false, IReadOnlyDictionary<int, string>? receiptPaths = null)
    {
        var b = new StringBuilder();
        b.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(H($"BlueLedger · {r.Year} CPA worksheet"))
            .Append("</title><style>").Append(PrintCss).Append("</style></head><body>");
        b.Append("<div class=\"toolbar\"><button type=\"button\" data-print>Print / save PDF</button><span>Choose Save as PDF in your browser's print dialog.</span></div><main>");
        b.Append("<header><div><p class=\"eyebrow\">BLUELEDGER / PREPARER WORKSHEET</p><h1>Officiating tax records</h1><p class=\"sub\">Annual income, expenses &amp; travel</p></div><strong class=\"year\">").Append(r.Year).Append("</strong></header>");
        b.Append("<section class=\"identity\"><div><b>Official</b><span>").Append(H(r.OwnerName)).Append("</span><span>").Append(H(r.Owner.Email)).Append("</span><span>").Append(H(r.Owner.Address)).Append("</span></div><div><b>Business / activity</b><span>")
            .Append(H(string.IsNullOrWhiteSpace(r.Profile.BusinessName) ? "Sports officiating" : r.Profile.BusinessName))
            .Append("</span><span>January 1 - December 31, ").Append(r.Year).Append("</span><span>Generated ").Append(H(r.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))).Append("</span></div></section>");
        b.Append("<div class=\"note\">").Append(H(ScopeNote)).Append("</div>");
        b.Append("<section class=\"totals\">");
        Metric(b, "Recorded receipts", Money(r.TotalReceived), "Known receipt dates");
        Metric(b, "Recorded spending", Money(r.PaidExpenseTotal), "Known payment dates · before adjustments");
        Metric(b, "Miles logged", r.TotalMiles.ToString("N2", Us), "Trip classifications below");
        Metric(b, "Review reminders", r.ReviewItems.Count.ToString(Us), "See review appendix");
        b.Append("</section>");

        Section(b, "1. Income by payer", IncomeBasis);
        Table(b, ["Payer", "Income type", "Payments", "Game fees", "Travel allowances", "Received"],
            r.Payers.Select(p => Row(p.Payer, TaxRecordLabels.Income(p.IncomeKind), p.Count, Money(p.Fees), Money(p.Reimbursements), Money(p.Total))));
        Table(b, ["Receipt classification", "Amount"], [Row("Independent contractor (user classified)", Money(r.ContractorReceipts)), Row("Employee / W-2 reconciliation", Money(r.EmployeeReceipts)), Row("Needs classification", Money(r.UnclassifiedReceipts)), Row("Total received", Money(r.TotalReceived))]);

        Section(b, "2. Expenses by category", ExpenseBasis, true);
        Table(b, ["Category", "Records", "Spending", "Business portion*", "Unallocated records", "Reimbursements"],
            r.Categories.Select(c => Row(c.Category, c.Count, Money(c.Amount), Money(c.BusinessPortion), c.UnclassifiedCount, Money(c.Reimbursements))));
        b.Append("<p class=\"caption\">*Business portions are recorded amounts before eligibility, meal limits, capitalization, reimbursement, or other tax adjustments. Employee-related spending and actual vehicle costs also require review.</p>");

        Section(b, "3. Mileage summary", MileageBasis);
        Table(b, ["Trip classification", "Recorded miles"], [Row("Business (user classified)", r.BusinessMiles), Row("Commuting / personal", r.PersonalMiles), Row("Needs classification", r.UnclassifiedMiles), Row("Total recorded", r.TotalMiles)]);

        Section(b, "4. Notes for the preparer", "Saved notes for this calendar year.");
        b.Append("<p class=\"preparer-notes\">").Append(H(string.IsNullOrWhiteSpace(r.Profile.PreparerNotes) ? "No additional notes recorded." : r.Profile.PreparerNotes)).Append("</p>");
        b.Append("<div class=\"checklist\"><b>Preparer completion checklist</b><ul><li>Reconcile income with 1099s, W-2s, bank and payment-platform statements. Add income, split payments, or refunds not represented here.</li><li>Confirm business-use allocations, deductible categories, meal limitations, equipment capitalization, and reimbursement treatment.</li><li>For each vehicle, obtain its description, business in-service date, annual total/business/commuting/personal miles, and supporting records; select the appropriate vehicle-expense method.</li><li>Review unknown dates and obtain any missing receipts or other supporting evidence.</li></ul></div>");

        Section(b, "5. Payments received - detail", "Only known receipt dates within the selected year. Full game fees marked paid are used; partial or split payments require separate reconciliation.", true);
        Table(b, ["Game / worked", "Received", "Payer / type", "Fee", "Allowance", "Total"], r.ReceivedGames.Select(g => Row($"#{g.Id} · {g.GameDate:yyyy-MM-dd}\n{g.LocationName}", g.PaidDate, $"{TaxReport.Payer(g)}\n{TaxRecordLabels.Income(g.IncomeKind)}", Money(g.FeeAmount), Money(g.TravelReimbursement), Money(TaxReport.ReceivedAmount(g)))));
        RecordNotes(b, r.ReceivedGames.Where(g => !string.IsNullOrWhiteSpace(g.Notes)).Select(g => Row($"Game #{g.Id}", g.Notes)));

        Section(b, "6. Expenses paid - detail", "Each line retains the original recorded amount and the preparer's supporting information.");
        Table(b, ["Expense / paid", "Payee / category / description", "Amount", "Business use", "Business portion", "Reimbursed", "Receipts"], r.PaidExpenses.Select(e => Row($"#{e.Id}\nPaid: {e.PaidDate:yyyy-MM-dd}\nExpense: {e.ExpenseDate:yyyy-MM-dd}", $"{e.Vendor}\n{e.Category}\n{e.BusinessPurpose}", Money(e.Amount), Percent(e.BusinessUsePercent), BusinessMoney(e), Money(e.ReimbursedAmount), r.ReceiptCount(e.Id))));
        RecordNotes(b, r.PaidExpenses.Where(e => !string.IsNullOrWhiteSpace(e.Notes)).Select(e => Row($"Expense #{e.Id}", e.Notes)));

        Section(b, "7. Mileage log", "Trip date is marked unconfirmed when the game date is used. Miles are not automatically converted into a deduction.");
        Table(b, ["Game / trip date", "Origin / destination", "Purpose", "Miles", "Classification"], r.MileageGames.Select(g => Row($"#{g.Id} · {TaxReport.MileageDate(g):yyyy-MM-dd}{(g.TravelDate == null ? "\nDate unconfirmed" : "")}", $"{g.TravelOrigin}\n→ {g.LocationName}", g.TravelPurpose, g.MilesDriven, TaxRecordLabels.Mileage(g.MileageKind))));

        Section(b, "8. Records with an unknown payment year", "These records come from ALL recorded years. They are excluded from annual received/spending totals. Work or expense dates do not establish the receipt or payment year.", true);
        b.Append("<p><b>Undated paid games: ").Append(Money(r.UndatedIncomeTotal)).Append(" · Undated expenses: ").Append(Money(r.UndatedExpenseTotal)).Append("</b></p>");
        Table(b, ["Paid game", "Work date", "Payer", "Fee", "Travel allowance", "Recorded paid amount"], r.UndatedPaidGames.Select(g => Row($"#{g.Id}", g.GameDate, TaxReport.Payer(g), Money(g.FeeAmount), Money(g.TravelReimbursement), Money(TaxReport.ReceivedAmount(g)))));
        Table(b, ["Expense", "Expense date", "Payee / description", "Category", "Amount", "Business use", "Reimbursed"], r.UndatedExpenses.Select(e => Row($"#{e.Id}", e.ExpenseDate, $"{e.Vendor}\n{e.BusinessPurpose}", e.Category, Money(e.Amount), Percent(e.BusinessUsePercent), Money(e.ReimbursedAmount))));
        RecordNotes(b, r.UndatedPaidGames.Where(g => !string.IsNullOrWhiteSpace(g.Notes)).Select(g => Row($"Game #{g.Id}", g.Notes)).Concat(r.UndatedExpenses.Where(e => !string.IsNullOrWhiteSpace(e.Notes)).Select(e => Row($"Expense #{e.Id}", e.Notes))));

        Section(b, "9. Games worked - reconciliation appendix", "Informational work-year listing. These fees are not added to received totals. Unpaid status is current at export and does not reconstruct a historical year-end balance.");
        Table(b, ["Game", "Worked", "Location", "Fee", "Payment status", "Received date"], r.WorkGames.Select(g => Row($"#{g.Id}", g.GameDate, g.LocationName, Money(g.FeeAmount), g.IsPaid ? "Marked paid" : "Unpaid", g.PaidDate)));

        Section(b, "10. Receipt index", packet ? (receiptsIncluded ? "Original attachments are included in the receipts folder. Unavailable entries are noted in the manifest." : "Attachments were not included in this export. Request them from the official as needed.") : "Receipt links require the official's BlueLedger sign-in. Download the CPA packet with receipts to share the original files.");
        b.Append("<table><thead><tr><th>Expense</th><th>Receipt</th><th>Original filename</th><th>File / location</th></tr></thead><tbody>");
        foreach (var receipt in r.Receipts)
        {
            var path = packet ? receiptPaths?.GetValueOrDefault(receipt.Id) : $"/expenses/receipts/{receipt.Id}?download=true";
            b.Append("<tr><td>#").Append(receipt.ExpenseId).Append("</td><td>#").Append(receipt.Id).Append("</td><td>").Append(H(receipt.FileName)).Append("</td><td>");
            if (path != null) b.Append("<a href=\"").Append(H(path)).Append("\">").Append(H(packet ? path : "Download receipt")).Append("</a>");
            else b.Append(receiptsIncluded ? "Unavailable at export" : "Not included");
            b.Append("</td></tr>");
        }
        if (r.Receipts.Count == 0) b.Append("<tr><td colspan=\"4\">No receipts attached.</td></tr>");
        b.Append("</tbody></table>");
        Section(b, "11. Review reminders", "Resolve missing data in BlueLedger or supply explanations and supporting records to the preparer.", true);
        Table(b, ["Area", "Record", "Review item"], r.ReviewItems.Select(i => Row(i.Area, i.Record, i.Reason)));
        b.Append("<footer>BlueLedger · User-entered records · ").Append(r.Year).Append("<p>Reference guidance: <a href=\"https://www.irs.gov/publications/p334\">IRS Publication 334</a> · <a href=\"https://www.irs.gov/publications/p463\">IRS Publication 463</a> · <a href=\"https://www.irs.gov/instructions/i1040sc\">Schedule C instructions</a>. The preparer should apply the rules for the selected tax year.</p></footer></main><script src=\"")
            .Append(packet ? "worksheet.js" : "/tax-center/print.js").Append("\" defer></script></body></html>");
        return b.ToString();
    }

    private static string H(object? value) => WebUtility.HtmlEncode(value switch
    {
        DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal d => d.ToString("N2", Us),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—"
    });
    private static string Money(decimal amount) => amount.ToString("C2", Us);
    private static string Percent(decimal? percent) => percent?.ToString("0.##", Us) is string value ? value + "%" : "Unallocated";
    private static string BusinessMoney(Expense e) => TaxReport.BusinessPortion(e) is decimal value ? Money(value) : "Unallocated";
    private static void Metric(StringBuilder b, string label, string value, string caption) => b.Append("<div><span>").Append(H(label)).Append("</span><strong>").Append(H(value)).Append("</strong><small>").Append(H(caption)).Append("</small></div>");
    private static void Section(StringBuilder b, string title, string note, bool pageBreak = false) => b.Append(pageBreak ? "<h2 class=\"page-break\">" : "<h2>").Append(H(title)).Append("</h2><p class=\"caption\">").Append(H(note)).Append("</p>");
    private static void RecordNotes(StringBuilder b, IEnumerable<object?[]> notes)
    {
        var rows = notes.ToList();
        if (rows.Count > 0) Table(b, ["Record", "Notes"], rows);
    }
    private static void Table(StringBuilder b, string[] headers, IEnumerable<object?[]> rows)
    {
        b.Append("<table><thead><tr>");
        foreach (var header in headers) b.Append("<th>").Append(H(header)).Append("</th>");
        b.Append("</tr></thead><tbody>");
        var any = false;
        foreach (var row in rows)
        {
            any = true;
            b.Append("<tr>");
            foreach (var value in row) b.Append("<td>").Append(H(value)).Append("</td>");
            b.Append("</tr>");
        }
        if (!any) b.Append("<tr><td colspan=\"").Append(headers.Length).Append("\">No records in this section.</td></tr>");
        b.Append("</tbody></table>");
    }
    private static object?[] Row(params object?[] values) => values;
    private static byte[] Csv(string[] headers, IEnumerable<object?[]> rows)
    {
        var b = new StringBuilder();
        b.AppendLine(string.Join(",", headers.Select(CsvCell)));
        foreach (var row in rows) b.AppendLine(string.Join(",", row.Select(CsvCell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }
    private static string CsvCell(object? value)
    {
        var s = value switch
        {
            null => "",
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "Yes" : "No",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        // User text must never become a spreadsheet formula, including leading whitespace controls.
        if (value is string && s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private const string PrintCss = """
        *{box-sizing:border-box}body{margin:0;background:#e8e2d8;color:#12243b;font:14px/1.5 Arial,Helvetica,sans-serif}.toolbar{max-width:1080px;margin:18px auto;display:flex;gap:16px;align-items:center;padding:0 22px}.toolbar button{background:#153f73;border:0;border-radius:6px;color:white;font:700 14px Arial;padding:13px 18px;cursor:pointer}.toolbar span{color:#495566;font-size:12px}main{max-width:1080px;margin:0 auto 40px;padding:44px;background:white;box-shadow:0 12px 40px #12243b14}header{display:flex;justify-content:space-between;align-items:center;border-bottom:3px solid #3069a5;padding-bottom:22px;gap:16px}.eyebrow{font-size:11px;font-weight:700;letter-spacing:1.7px;color:#3069a5;margin:0}h1{font-size:32px;line-height:1.2;margin:10px 0}h2{font-size:18px;line-height:1.3;margin:28px 0 8px;break-after:avoid}.sub{margin:0;color:#526075}.year{font-size:44px;color:#3069a5}.identity{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin:24px 0}.identity span,.identity b{display:block;white-space:pre-wrap;overflow-wrap:anywhere}.identity b{font-size:11px;text-transform:uppercase;color:#526075;margin-bottom:6px}.note,.checklist{padding:16px;background:#edf3f9;border-left:3px solid #3069a5;font-size:12px}.totals{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-top:22px}.totals div{border:1px solid #c8d2df;border-radius:5px;padding:14px}.totals span,.totals strong,.totals small{display:block}.totals span{font-size:11px;text-transform:uppercase;color:#526075}.totals strong{font-size:23px;margin:7px 0;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}.totals small{font-size:10px;color:#526075}.caption{font-size:11px;color:#526075;margin:7px 0 12px;break-after:avoid}.preparer-notes{white-space:pre-wrap;overflow-wrap:anywhere;padding:12px;border:1px solid #c8d2df;min-height:56px}table{width:100%;border-collapse:collapse;font-size:11px;table-layout:fixed;margin:12px 0 20px}th{text-align:left;background:#edf3f9;color:#234362;font-size:10px;padding:9px 7px;border-bottom:1px solid #a8bad0;vertical-align:top}td{padding:9px 7px;border-bottom:1px solid #dbe1e8;vertical-align:top;white-space:pre-wrap;overflow-wrap:anywhere}tr{break-inside:avoid}thead{display:table-header-group}.checklist ul{margin:10px 0 0;padding-left:20px}.checklist li{margin:6px 0}a{color:#245c99}footer{border-top:2px solid #3069a5;margin-top:30px;padding-top:14px;font-size:11px;color:#526075}footer p{font-size:10px}@page{size:letter portrait;margin:.55in;@bottom-left{content:"BlueLedger / Preparer worksheet";font:8px Arial;color:#526075}@bottom-right{content:"Page " counter(page);font:8px Arial;color:#526075}}@media print{body{background:white;font-size:11px}.toolbar{display:none}main{max-width:none;margin:0;padding:0;box-shadow:none}h1{font-size:26px}.year{font-size:36px}.totals strong{font-size:19px}.identity{margin:16px 0}.totals div{padding:10px}.page-break{break-before:page}table{font-size:9px}th{font-size:8px;padding:6px 5px}td{padding:7px 5px}.note,.checklist{font-size:10px}a{color:inherit;text-decoration:none}}@media screen and (max-width:680px){main{padding:24px 16px}.toolbar{flex-wrap:wrap}.totals{grid-template-columns:repeat(2,1fr)}.identity{grid-template-columns:1fr}h1{font-size:24px}.year{font-size:32px}table{font-size:10px}th,td{padding:7px 4px}}
        """;
}
