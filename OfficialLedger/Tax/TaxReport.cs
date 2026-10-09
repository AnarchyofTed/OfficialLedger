using System.Globalization;
using OfficialLedger.Data;
using OfficialLedger.Models;

namespace OfficialLedger.Tax;

public sealed record TaxReceipt(int Id, int ExpenseId, string FileName, string ContentType, long SizeBytes);
public sealed record TaxReviewItem(string Area, string Record, string Reason, string Href);
public sealed record TaxPayerSummary(string Payer, string IncomeKind, int Count, decimal Fees, decimal Reimbursements)
{
    public decimal Total => Fees + Reimbursements;
}
public sealed record TaxExpenseSummary(string Category, int Count, decimal Amount, decimal BusinessPortion, int UnclassifiedCount, decimal Reimbursements);

/// <summary>Cash-date worksheet of recorded activity; no automatic deduction or tax calculation.</summary>
public sealed class TaxReport
{
    public required int Year { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required ApplicationUser Owner { get; init; }
    public required TaxYearProfile Profile { get; init; }
    public required List<Game> ReceivedGames { get; init; }
    public required List<Game> WorkGames { get; init; }
    public required List<Game> UndatedPaidGames { get; init; }
    public required List<Game> MileageGames { get; init; }
    public required List<Expense> PaidExpenses { get; init; }
    public required List<Expense> UndatedExpenses { get; init; }
    public required List<TaxReceipt> Receipts { get; init; }
    public required List<TaxReviewItem> ReviewItems { get; init; }
    public required List<TaxPayerSummary> Payers { get; init; }
    public required List<TaxExpenseSummary> Categories { get; init; }
    public string OwnerName => string.Join(" ", new[] { Owner.FirstName, Owner.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))) is var name && name.Length > 0 ? name : Owner.UserName ?? "Official";
    public decimal FeesReceived => ReceivedGames.Sum(x => x.FeeAmount);
    public decimal TravelAllowancesReceived => ReceivedGames.Sum(x => x.TravelReimbursement);
    public decimal TotalReceived => FeesReceived + TravelAllowancesReceived;
    public decimal ContractorReceipts => ReceivedGames.Where(x => x.IncomeKind == "Contractor").Sum(ReceivedAmount);
    public decimal EmployeeReceipts => ReceivedGames.Where(x => x.IncomeKind == "Employee").Sum(ReceivedAmount);
    public decimal UnclassifiedReceipts => TotalReceived - ContractorReceipts - EmployeeReceipts;
    public decimal PaidExpenseTotal => PaidExpenses.Sum(x => x.Amount);
    public decimal RecordedBusinessPortion => PaidExpenses.Sum(x => BusinessPortion(x) ?? 0m);
    public decimal ExpenseReimbursements => PaidExpenses.Sum(x => x.ReimbursedAmount);
    public decimal UndatedIncomeTotal => UndatedPaidGames.Sum(ReceivedAmount);
    public decimal UndatedExpenseTotal => UndatedExpenses.Sum(x => x.Amount);
    public decimal OutstandingFees => WorkGames.Where(x => !x.IsPaid).Sum(x => x.FeeAmount);
    public decimal TotalMiles => MileageGames.Sum(x => x.MilesDriven);
    public decimal BusinessMiles => MileageGames.Where(x => x.MileageKind == "Business").Sum(x => x.MilesDriven);
    public decimal PersonalMiles => MileageGames.Where(x => x.MileageKind == "Personal").Sum(x => x.MilesDriven);
    public decimal UnclassifiedMiles => TotalMiles - BusinessMiles - PersonalMiles;
    public IEnumerable<Expense> AllReportExpenses => PaidExpenses.Concat(UndatedExpenses);
    public bool HasRecords => ReceivedGames.Count + WorkGames.Count + UndatedPaidGames.Count + MileageGames.Count + PaidExpenses.Count + UndatedExpenses.Count > 0;
    public int ReceiptCount(int expenseId) => Receipts.Count(x => x.ExpenseId == expenseId);
    public static decimal ReceivedAmount(Game game) => game.FeeAmount + game.TravelReimbursement;
    public static DateTime MileageDate(Game game) => game.TravelDate ?? game.GameDate;
    public static decimal? BusinessPortion(Expense expense) => expense.BusinessUsePercent is >= 0m and <= 100m
        ? decimal.Round(expense.Amount * expense.BusinessUsePercent.Value / 100m, 2, MidpointRounding.AwayFromZero) : null;
    public static string Payer(Game game) => string.IsNullOrWhiteSpace(game.PayerName) ? "Payer not recorded" : game.PayerName.Trim();

    public static TaxReport Build(int year, ApplicationUser owner, IEnumerable<Game> games, IEnumerable<Expense> expenses,
        IEnumerable<TaxReceipt> receipts, TaxYearProfile? profile = null, DateTime? generatedAtUtc = null)
    {
        if (year is < 1900 or > 9998) throw new ArgumentOutOfRangeException(nameof(year));
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        // Owner filtering remains here as well as in the database reader so exports cannot mix users.
        var ownGames = games.Where(x => x.UserId == owner.Id).ToList();
        var ownExpenses = expenses.Where(x => x.UserId == owner.Id).ToList();
        var received = ownGames.Where(x => x.IsPaid && x.PaidDate >= start && x.PaidDate < end)
            .OrderBy(x => x.PaidDate).ThenBy(x => x.Id).ToList();
        var undatedGames = ownGames.Where(x => x.IsPaid && x.PaidDate == null).OrderBy(x => x.GameDate).ThenBy(x => x.Id).ToList();
        var workGames = ownGames.Where(x => x.GameDate >= start && x.GameDate < end).OrderBy(x => x.GameDate).ThenBy(x => x.Id).ToList();
        var mileage = ownGames.Where(x => x.MilesDriven > 0 && MileageDate(x) >= start && MileageDate(x) < end)
            .OrderBy(MileageDate).ThenBy(x => x.Id).ToList();
        var paidExpenses = ownExpenses.Where(x => x.PaidDate >= start && x.PaidDate < end).OrderBy(x => x.PaidDate).ThenBy(x => x.Id).ToList();
        var undatedExpenses = ownExpenses.Where(x => x.PaidDate == null).OrderBy(x => x.ExpenseDate).ThenBy(x => x.Id).ToList();
        var expenseIds = paidExpenses.Concat(undatedExpenses).Select(x => x.Id).ToHashSet();
        var reportReceipts = receipts.Where(x => expenseIds.Contains(x.ExpenseId)).OrderBy(x => x.ExpenseId).ThenBy(x => x.Id).ToList();
        var issues = new List<TaxReviewItem>();
        void GameIssue(Game game, string area, string reason) => issues.Add(new(area, $"Game #{game.Id} · {game.GameDate:MMM d, yyyy}", reason, $"/games/edit/{game.Id}"));
        void ExpenseIssue(Expense expense, string reason) => issues.Add(new("Expenses", $"Expense #{expense.Id} · {expense.ExpenseDate:MMM d, yyyy}", reason, $"/expenses/details/{expense.Id}"));
        foreach (var game in received.Concat(undatedGames))
        {
            if (game.PaidDate == null) GameIssue(game, "Income", "Paid status has no receipt date. Excluded from annual received totals; tax year is unknown.");
            if (string.IsNullOrWhiteSpace(game.PayerName)) GameIssue(game, "Income", "Record the payer to reconcile payments with 1099s, W-2s, and other statements.");
            if (game.IncomeKind != "Contractor" && game.IncomeKind != "Employee") GameIssue(game, "Income", "Confirm whether this payment is contractor income or employee compensation.");
            if (game.TravelReimbursement > 0) GameIssue(game, "Income", "Travel allowance is shown separately and included in received totals. Confirm its tax treatment and avoid counting it twice.");
        }
        if (received.Any(x => x.IncomeKind == "Employee"))
            issues.Add(new("Income", "Employee payments", "Reconcile with W-2 gross wages and withholding. Recorded game payments may represent take-home pay.", "/games"));
        foreach (var expense in paidExpenses.Concat(undatedExpenses))
        {
            if (expense.PaidDate == null) ExpenseIssue(expense, "Payment date is unknown. Excluded from annual paid-expense totals until its tax year can be confirmed.");
            if (BusinessPortion(expense) == null) ExpenseIssue(expense, "Confirm business-use percentage. No business portion has been assumed.");
            if (string.IsNullOrWhiteSpace(expense.Vendor)) ExpenseIssue(expense, "Payee is missing.");
            if (string.IsNullOrWhiteSpace(expense.BusinessPurpose)) ExpenseIssue(expense, "Add a description supporting the officiating purpose.");
            if (!reportReceipts.Any(x => x.ExpenseId == expense.Id)) ExpenseIssue(expense, "No receipt is attached. Provide any other supporting records to your preparer.");
            if (expense.ReimbursedAmount > 0) ExpenseIssue(expense, "Reconcile expense reimbursement with payment records. It has not automatically reduced spending or increased income.");
            if (expense.Category is "Meals" or "Clothing" or "Equipment" or "Travel")
                ExpenseIssue(expense, "Review eligibility and any limits, vehicle-method overlap, or capitalization requirements for this category.");
        }
        foreach (var game in mileage)
        {
            if (game.TravelDate == null) GameIssue(game, "Mileage", "Trip date is unconfirmed. The game date is used only to organize this mileage log.");
            if (game.MileageKind != "Business" && game.MileageKind != "Personal") GameIssue(game, "Mileage", "Classify business versus commuting/personal travel with your preparer.");
            if (string.IsNullOrWhiteSpace(game.LocationName) || string.IsNullOrWhiteSpace(game.TravelOrigin) || string.IsNullOrWhiteSpace(game.TravelPurpose))
                GameIssue(game, "Mileage", "Complete the destination, route/origin, and reason for travel.");
        }
        foreach (var group in mileage.GroupBy(x => MileageDate(x).Date).Where(x => x.Count() > 1))
            issues.Add(new("Mileage", group.Key.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), "Several games have miles on this date. Verify each trip is counted once.", $"/games/edit/{group.First().Id}"));
        return new TaxReport
        {
            Year = year, Owner = owner, GeneratedAtUtc = generatedAtUtc ?? DateTime.UtcNow,
            Profile = profile?.UserId == owner.Id && profile.TaxYear == year ? profile : new() { UserId = owner.Id, TaxYear = year },
            ReceivedGames = received, WorkGames = workGames, UndatedPaidGames = undatedGames,
            MileageGames = mileage, PaidExpenses = paidExpenses, UndatedExpenses = undatedExpenses, Receipts = reportReceipts,
            ReviewItems = issues,
            Payers = received.GroupBy(x => (Name: Payer(x).ToUpperInvariant(), Kind: x.IncomeKind))
                .Select(x => new TaxPayerSummary(Payer(x.First()), x.Key.Kind, x.Count(), x.Sum(g => g.FeeAmount), x.Sum(g => g.TravelReimbursement)))
                .OrderBy(x => x.Payer, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.IncomeKind).ToList(),
            Categories = paidExpenses.GroupBy(x => x.Category).OrderBy(x => x.Key)
                .Select(x => new TaxExpenseSummary(x.Key, x.Count(), x.Sum(e => e.Amount), x.Sum(e => BusinessPortion(e) ?? 0m), x.Count(e => BusinessPortion(e) == null), x.Sum(e => e.ReimbursedAmount))).ToList()
        };
    }
}
