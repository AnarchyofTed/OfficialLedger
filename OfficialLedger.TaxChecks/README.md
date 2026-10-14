# Tax Center checks

Run `dotnet run --project OfficialLedger.TaxChecks` from the repository root.

Checks use synthetic records, an isolated SQLite database, and test-only
authentication. They do not run the production app startup or connect to a live
database. Coverage includes cross-year cash dates, unknown dates across recorded
years, employee/contractor separation, reimbursement reconciliation, business-use
rounding, mileage classification, owner isolation, SQL Server migration
discovery/model consistency, HTML escaping, CSV formula protection, original
receipt preservation, duplicate archive filenames, download headers, and cleanup
of temporary packet files.

# Tax Center behavior

`/tax-center` selects a calendar year. The CPA worksheet opens in a separate tab
and can be printed or saved as PDF from the browser print dialog. The downloadable
ZIP contains a standalone HTML worksheet and print script, CSV summaries and
itemized records, an explicit review list, and optionally original receipts.
Extract the ZIP before opening its worksheet so relative receipt links work.

Game income uses `IsPaid` and `PaidDate`, representing the full game fee received
or made available. Reimbursements paid with the game are separately itemized.
Expense totals use `PaidDate`. Work and expense dates do not substitute for
unknown payment dates. All undated paid games and undated expenses remain visible
in an unassigned-records appendix across every report year. Trip dates use
`TravelDate`, with a clearly flagged fallback to the game date for organization.

The module organizes source records for a preparer. It separates employee
payments, user classifications, gross spending, user-entered business portions,
and reimbursements. The preparer determines deductible amounts and the vehicle
method. There is no automatic mileage-rate multiplication or tax-liability
calculation. The app's existing full-game-payment model is retained; split
payments, refunds, additional income, vehicle annual totals, and activity recorded
elsewhere can be described in the saved year notes and reconciled by the preparer.

# Migration and access

The `AddTaxCenter` migration adds nullable tax dates, payer/trip details, review
defaults, expense allocations/reimbursements, and `TaxYearProfile`. Existing
records are not assigned invented dates or business-use percentages. The app's
existing startup migration process applies the change. Each year's business name
and preparer notes are saved independently per user.

All worksheet, CSV, and packet endpoints require authentication and query the
current user's records. Tax Center responses have private no-store caching.
Receipt bytes are loaded one at a time during export; packet files are spooled to
temporary files and deleted when the download stream is disposed. A receipt
deleted during export is explicitly marked unavailable in the manifest.

# Manual verification

1. Add a paid game with its received date and payer; verify its payment-year total.
2. Add an expense with its paid date, business-use percentage, and receipt.
3. In Tax Center, switch years and review reminders and source-record links.
4. Save a business name and notes; refresh and verify each year keeps its own data.
5. Open the CPA worksheet, print/save as PDF, and verify pagination and totals.
6. Download a packet with receipts, extract it, and open the standalone worksheet.
7. Download without attachments; verify the manifest clearly identifies omission.
8. Verify the Tax Center and its download actions remain usable on a phone.
