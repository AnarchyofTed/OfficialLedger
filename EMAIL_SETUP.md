# BlueLedger account email

Account confirmation, resend, password reset and profile email changes use Resend's HTTPS email API. There is no no-op sender or browser shortcut to confirm an account. No database migration is needed.

## Production setup

1. Create a [Resend account](https://resend.com/) and add a sending domain you control. Use the DNS records Resend provides in GoDaddy, and wait for the domain to verify. A dedicated sending subdomain is also supported. See [domain verification](https://resend.com/docs/dashboard/domains/introduction).
2. Create a sending API key restricted to that domain. Keep the key out of Git and never send it to the browser.
3. In Azure App Service, open **Settings → Environment variables → App settings** and add:

   | Setting | Value |
   | --- | --- |
   | `Email__ApiKey` | Your Resend sending API key |
   | `Email__FromAddress` | A sender address on the verified domain, such as `noreply@your-domain.com` |
   | `Email__FromName` | `BlueLedger` |
   | `Email__PublicBaseUrl` | Your actual public HTTPS site URL, such as `https://blueledger.azurewebsites.net` or your custom domain |
   | `Email__ReplyToAddress` | Optional monitored support email address |

4. Save the settings and restart the app. Deploy this branch, register a test account, and open the email on another device. Confirm it, log in, then request a password reset and check that the old password stops working. Use Resend's email dashboard to check actual delivery or bounces; API acceptance alone does not prove inbox delivery.

The default `appsettings.json` contains empty placeholders. Missing or invalid settings do not stop the site from starting, but email cannot be delivered until configured. Signup saves an unconfirmed account and shows a recovery message if sending fails, with a link to resend. Public resend/reset forms deliberately show the same generic result for unknown addresses and delivery failures; monitor server logs and the provider dashboard for failures.

## Local development

Store the same values in .NET user secrets:

```bash
dotnet user-secrets set "Email:ApiKey" "YOUR_RESEND_SENDING_KEY" --project OfficialLedger
dotnet user-secrets set "Email:FromAddress" "noreply@your-domain.com" --project OfficialLedger
dotnet user-secrets set "Email:PublicBaseUrl" "https://localhost:7076" --project OfficialLedger
```

Use the actual HTTPS port from your launch profile. HTTP is accepted only for loopback development URLs. Development still uses real delivery; the account checks use a fake HTTP transport and never contact Resend.

## Link and delivery behavior

- Links use `Email:PublicBaseUrl`, never a caller-controlled Host header. Confirmation return URLs must be local paths.
- Confirmation/email change tokens last 24 hours. Password reset tokens last 1 hour, are bound to the user and become invalid after a successful reset. Resetting does not auto-login or mark an unconfirmed email as confirmed.
- Invalid, expired and reused password reset links lead to a page that requests a fresh link. Malformed confirmation tokens show a recovery action instead of an exception.
- HTML and plain-text email versions are sent. Names and links are HTML-encoded once. Reset emails include the account email in the link to prefill the form.
- Temporary provider/network errors are retried up to three total attempts using one idempotency key. Authentication/configuration errors are not retried. Logs contain status codes, without provider response bodies, recipient addresses, tokens or API keys.
- Email-related account POSTs share a limit of 10 requests per client IP per 10 minutes on each app instance, including signup steps. Rejected requests return HTTP 429 and a Retry-After header. Account responses disable caching and referrer forwarding.
- Existing confirmed accounts retain their status. Unconfirmed accounts must use the real resend flow; do not bulk-confirm users as a workaround.

## Data Protection and hosting

Identity email links depend on the app's Data Protection key ring. Azure App Service automatically persists and shares the key ring within a deployment slot. Preserve that key storage across restarts, and do not delete keys. Separate slots have separate key rings; if you adopt slot swaps or another hosting arrangement, configure a shared persistent key ring before relying on outstanding links. See [Microsoft's key management guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-8.0).

## Verification

```bash
dotnet build OfficialLedger.sln
dotnet run --project OfficialLedger.AccountChecks
```

The checks exercise the actual Identity token providers, an isolated SQLite database and a fake Resend HTTP handler. The final live inbox/DNS check still requires your provider settings.
