# Receipt checks

Run `dotnet run --project OfficialLedger.ReceiptChecks` from the repository root.

Checks cover allowed file signatures, renamed videos, rejected document formats,
the exact 10 MB boundary, receipt count limits, SQL Server migration discovery and
DDL, durable relational storage, authenticated HTTP retrieval, cross-user access,
download headers, no-cache responses, and deletion cascading. The HTTP checks use
an isolated in-memory SQLite database and test-only authentication; no live data
or production configuration is used.

# Manual phone check

1. On iOS Safari and Android Chrome, sign in and select Expenses → Add Expense.
2. Use Take Photo, capture a receipt, and confirm the preview and Ready to save label.
3. Use Choose Receipts for an existing photo or PDF. Verify selections accumulate.
4. Save the expense. Reopen it and verify View and Download return the receipt.
5. Edit: remove a receipt, add another, and Cancel. Reopen to verify no changes.
6. Repeat and Save Changes. Verify the replacement is retained after refreshing.
7. Select an expense year or season in Reports. Open Saved receipts and follow a link.
8. Try an unsupported video/Word document and a file larger than 10 MB. Verify rejection.
9. Try attaching a sixth receipt, including existing receipts. Verify rejection.
10. Sign out and open a previously copied receipt URL. Sign in as another user and
    try the same URL. Both must fail to return the receipt.

# Storage and deployment

Migration `20261009060000_AddExpenseReceipts` adds `ExpenseReceipt`. Existing
expenses do not need backfilling. The app's existing startup migration process
applies it. Receipt bytes, original filename, validated MIME type, actual size,
and UTC upload time are stored in SQL Server. No local upload directory or Azure
storage account is needed. Database backups therefore include receipts. Ordinary
expense and report queries retrieve metadata/counts only, not the image bytes.

Only JPEG, PNG, WebP, HEIC/HEIF, and PDF signatures are accepted. Maximum 10 MiB
per receipt and five receipts per expense are checked during reading and saving.
Original images are kept; the small preview does not replace the source. HEIC/HEIF
originals can be downloaded when the browser does not support inline viewing.
Removal is deferred until Save Changes. Saving an expense and its receipts is
atomic, and deleting an expense cascades to its attachments. Existing-expense
saves use a serializable transaction to keep simultaneous edits within the limit.

Receipts are available through owner-authorized endpoints and the Reports year/
season filters. A future Tax Center can query the same expense relationships and
metadata without changing receipt storage. OCR and tax package generation are
outside this change.
