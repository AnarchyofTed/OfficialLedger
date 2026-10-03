Run with .NET 8:

```sh
dotnet build OfficialLedger/OfficialLedger.csproj
dotnet run --project OfficialLedger.SeasonChecks/OfficialLedger.SeasonChecks.csproj
```

The checks cover required season selection, date/name/image validation, legacy nullable game assignments, and the non-cascading season foreign key. No database connection is needed for these model checks.

Manual integration checks against a test SQL Server database:

1. Start the app to apply `20261003120000_AddUserSeasons`; verify existing games and seeded seasons remain.
2. Open the dashboard menu; verify Add Season is its last option.
3. Create a season with a sport image and dates spanning two years; verify it is selected on return.
4. Add a game from that dashboard and verify the season is preselected. Save and verify dashboard totals include that game only in its assigned season.
5. Create another season covering the same dates; verify the first season's game is excluded from its totals.
6. Edit a game and change its season, then cancel another edit to verify the season selection resets.
7. Sign in as another user; verify the first user's new season is absent from dashboard and game dropdowns. Seeded legacy seasons remain shared.
8. Check the form at mobile width and navigate the image choices by keyboard.
