using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OfficialLedger.Components.Account;
using OfficialLedger.Data;
using OfficialLedger.Email;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
    Console.WriteLine($"PASS {description}");
}

const string originalPassword = "Original_2026!";
const string newPassword = "Replacement_2026!";
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Email:ApiKey"] = "re_fake_account_checks",
    ["Email:FromAddress"] = "noreply@blueledger.test",
    ["Email:ReplyToAddress"] = "support@blueledger.test",
    ["Email:PublicBaseUrl"] = "https://blueledger.test"
}).Build();
var transport = new FakeEmailHandler();
var logs = new CapturedLogs();
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddProvider(logs));
services.AddDataProtection().UseEphemeralDataProtectionProvider();
services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
services.AddAuthentication().AddIdentityCookies();
services.AddAuthorization();
services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);
services.AddAccountEmail(configuration);
services.AddHttpClient<IEmailSender<ApplicationUser>, IdentityEmailSender>()
    .ConfigurePrimaryHttpMessageHandler(() => transport);
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.EnsureCreatedAsync();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
var email = scope.ServiceProvider.GetRequiredService<AccountEmailService>();
var settings = scope.ServiceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
var user = new ApplicationUser { Email = "official@blueledger.test", UserName = "official@blueledger.test", FirstName = "<script>Bad</script> & Blake" };
Check((await users.CreateAsync(user, originalPassword)).Succeeded, "Create an unconfirmed account");
Check(!await signIn.CanSignInAsync(user), "An unconfirmed account cannot sign in");

await email.SendConfirmationAsync(user, "https://evil.example/redirect");
var confirmation = transport.Messages.Last();
var confirmationLink = confirmation.Link("Confirm email");
var confirmationQuery = QueryHelpers.ParseQuery(new Uri(confirmationLink).Query);
Check(new Uri(confirmationLink).Host == "blueledger.test", "Confirmation uses the configured public site URL");
Check(confirmationQuery["returnUrl"] == "/dashboard", "Confirmation discards external return URLs");
Check(confirmation.Html.Contains("&amp;") && !confirmation.Html.Contains("&amp;amp;"), "Email URLs are HTML-encoded exactly once");
Check(!confirmation.Html.Contains("<script>") && confirmation.Html.Contains("&lt;script&gt;"), "Email greeting escapes user profile text");
Check(confirmation.Payload.GetProperty("reply_to").GetString() == "support@blueledger.test", "Configured reply-to address is included");
Check(confirmation.Authorization == "Bearer re_fake_account_checks", "Email API request authenticates on the server");
Check(AccountEmailService.TryDecodeToken(confirmationQuery["code"], out var confirmationToken), "Confirmation email contains a decodable Identity token");
Check((await users.ConfirmEmailAsync(user, confirmationToken)).Succeeded && await signIn.CanSignInAsync(user), "An emailed link confirms the account and enables sign-in");

var other = new ApplicationUser { Email = "other@blueledger.test", UserName = "other@blueledger.test" };
await users.CreateAsync(other, originalPassword);
var count = transport.Messages.Count;
await email.RequestConfirmationAsync("missing@blueledger.test");
await email.RequestConfirmationAsync(user.Email!);
Check(transport.Messages.Count == count, "Unknown and already-confirmed accounts do not receive resend emails");
await email.RequestConfirmationAsync("  other@blueledger.test  ");
Check(transport.Messages.Count == count + 1, "Resend delivers a real confirmation email and trims input");

var external = new ApplicationUser { Email = "external@blueledger.test", UserName = "external@blueledger.test", EmailConfirmed = true };
await users.CreateAsync(external);
count = transport.Messages.Count;
await email.RequestPasswordResetAsync("missing@blueledger.test");
await email.RequestPasswordResetAsync(other.Email!);
await email.RequestPasswordResetAsync(external.Email!);
Check(transport.Messages.Count == count, "Reset does not send for unknown, unconfirmed or passwordless accounts");

await email.RequestPasswordResetAsync("  official@blueledger.test  ");
var reset = transport.Messages.Last();
var resetLink = reset.Link("Reset password");
var resetQuery = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
Check(resetQuery["email"] == user.Email, "Reset link includes the account email for form prefilling");
Check(AccountEmailService.TryDecodeToken(resetQuery["code"], out var resetToken), "Reset email contains a decodable Identity token");
Check(await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", resetToken), "Reset email contains a valid token");
Check(!(await users.ResetPasswordAsync(other, resetToken, newPassword)).Succeeded, "Reset tokens cannot change another user's password");
Check(!await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", confirmationToken), "Confirmation tokens cannot reset passwords");
Check(!(await users.ResetPasswordAsync(user, resetToken, "weak")).Succeeded, "Reset enforces the Identity password rules");
Check((await users.ResetPasswordAsync(user, resetToken, newPassword)).Succeeded, "Valid reset changes the password");
Check(!await users.CheckPasswordAsync(user, originalPassword) && await users.CheckPasswordAsync(user, newPassword), "Old password stops working and the new password works");
Check(!(await users.ResetPasswordAsync(user, resetToken, originalPassword)).Succeeded, "A successful password reset invalidates the used link");
Check(!await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", resetToken), "A used reset token fails link validation");

var protections = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
var tokenLogger = scope.ServiceProvider.GetRequiredService<ILogger<DataProtectorTokenProvider<ApplicationUser>>>();
var expiredReset = new PasswordResetTokenProvider(protections,
    Options.Create(new PasswordResetTokenProviderOptions { TokenLifespan = TimeSpan.FromMinutes(-1) }), tokenLogger);
var expiredResetToken = await expiredReset.GenerateAsync("ResetPassword", users, user);
Check(!await expiredReset.ValidateAsync("ResetPassword", expiredResetToken, users, user), "Expired reset tokens are rejected");
var expiredConfirmation = new DataProtectorTokenProvider<ApplicationUser>(protections,
    Options.Create(new DataProtectionTokenProviderOptions { TokenLifespan = TimeSpan.FromMinutes(-1) }), tokenLogger);
var expiredConfirmationToken = await expiredConfirmation.GenerateAsync("EmailConfirmation", users, other);
Check(!await expiredConfirmation.ValidateAsync("EmailConfirmation", expiredConfirmationToken, users, other), "Expired confirmation tokens are rejected");
Check(scope.ServiceProvider.GetRequiredService<IOptions<PasswordResetTokenProviderOptions>>().Value.TokenLifespan == TimeSpan.FromHours(1), "Production password reset lifetime is one hour");
Check(scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan == TimeSpan.FromHours(24), "Production confirmation lifetime is 24 hours");

await email.SendEmailChangeAsync(user, "changed@blueledger.test");
var changeLink = transport.Messages.Last().Link("Confirm email");
var changeQuery = QueryHelpers.ParseQuery(new Uri(changeLink).Query);
AccountEmailService.TryDecodeToken(changeQuery["code"], out var changeToken);
Check((await users.ChangeEmailAsync(user, "changed@blueledger.test", changeToken)).Succeeded, "Profile email changes use real delivered confirmation tokens");
Check((await users.SetUserNameAsync(user, user.Email)).Succeeded, "Confirmed email can also become the login name");

Check(new string?[] { null, "", "%%%", "a", "_w", new string('a', 4097) }.All(code => !AccountEmailService.TryDecodeToken(code, out _)), "Missing, malformed and oversized token input is handled safely");
Check(AccountEmailService.SafeReturnUrl("/games?year=2026") == "/games?year=2026", "Local return paths are preserved");
Check(new[] { "https://evil.example", "//evil.example", "/\\evil.example", "/\nheader" }.All(path => AccountEmailService.SafeReturnUrl(path) == "/dashboard"), "Unsafe return paths are rejected");
Check(new EmailOptions { ApiKey = "key", FromAddress = "test@blueledger.test", PublicBaseUrl = "https://blueledger.test" }.IsConfigured, "Valid email configuration is accepted");
Check(new EmailOptions { ApiKey = "key", FromAddress = "test@blueledger.test", PublicBaseUrl = "http://localhost:5000" }.IsConfigured, "Loopback HTTP is supported for local development");
Check(new[] { "", "http://evil.example", "https://user:password@blueledger.test", "https://blueledger.test?query=1", "https://blueledger.test/#fragment" }
    .All(url => !new EmailOptions { ApiKey = "key", FromAddress = "test@blueledger.test", PublicBaseUrl = url }.IsConfigured), "Untrusted or malformed public URL configuration is rejected");

var sender = scope.ServiceProvider.GetRequiredService<IEmailSender<ApplicationUser>>();
count = transport.Messages.Count;
transport.Statuses.Enqueue(HttpStatusCode.ServiceUnavailable);
transport.Statuses.Enqueue(HttpStatusCode.OK);
await sender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
Check(transport.Messages.Count == count + 2 && transport.Messages[^1].IdempotencyKey == transport.Messages[^2].IdempotencyKey, "Temporary provider failures retry with one idempotency key");
count = transport.Messages.Count;
transport.Statuses.Enqueue(HttpStatusCode.TooManyRequests);
transport.Statuses.Enqueue(HttpStatusCode.OK);
await sender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
Check(transport.Messages.Count == count + 2, "Provider rate limits are retried");
count = transport.Messages.Count;
transport.Statuses.Enqueue(HttpStatusCode.Unauthorized);
var rejected = false;
try { await sender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink); }
catch (EmailDeliveryException) { rejected = true; }
Check(rejected && transport.Messages.Count == count + 1, "Permanent provider failures surface without retries");
foreach (var body in new[] { "{}", "null", "[]", "not-json" })
{
    transport.Bodies.Enqueue(body);
    rejected = false;
    try { await sender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink); }
    catch (EmailDeliveryException) { rejected = true; }
    Check(rejected, "Invalid provider success payload is handled safely: " + body);
}
transport.Statuses.Enqueue(HttpStatusCode.Unauthorized);
await email.RequestPasswordResetAsync(user.Email!);
Check(true, "Public reset requests keep a generic response on delivery failure");
transport.Statuses.Enqueue(HttpStatusCode.Unauthorized);
await email.RequestConfirmationAsync(other.Email!);
Check(true, "Public resend requests keep a generic response on delivery failure");

var unconfiguredTransport = new FakeEmailHandler();
var unconfiguredSender = new IdentityEmailSender(new HttpClient(unconfiguredTransport), Options.Create(new EmailOptions()),
    scope.ServiceProvider.GetRequiredService<ILogger<IdentityEmailSender>>());
rejected = false;
try { await unconfiguredSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink); }
catch (EmailDeliveryException) { rejected = true; }
Check(rejected && unconfiguredTransport.Messages.Count == 0, "Missing settings fail safely without pretending to send");
Check(logs.Messages.All(message => !message.Contains("re_fake_account_checks") && !message.Contains(resetToken) &&
    !message.Contains(confirmationToken) && !message.Contains("official@blueledger.test")), "Delivery logs do not disclose credentials, tokens or recipient addresses");

var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
var request = new DefaultHttpContext();
request.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
request.Request.Method = "POST";
request.Request.Path = "/Account/ForgotPassword";
for (var i = 0; i < 10; i++)
{
    using var lease = limiter.AttemptAcquire(request);
    if (!lease.IsAcquired) throw new InvalidOperationException("Email limit rejected a request before its threshold.");
}
using (var lease = limiter.AttemptAcquire(request)) Check(!lease.IsAcquired, "Email requests are limited after ten attempts");
request.Request.Path = "/Account/ResendEmailConfirmation/";
using (var lease = limiter.AttemptAcquire(request)) Check(!lease.IsAcquired, "Reset and resend share the same IP limit, including trailing slashes");
request.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
using (var lease = limiter.AttemptAcquire(request)) Check(lease.IsAcquired, "Other client IPs keep their own allowance");
request.Request.Method = "GET";
using (var lease = limiter.AttemptAcquire(request)) Check(lease.IsAcquired, "Account GETs are not limited by the email POST limit");

var pipeline = new ApplicationBuilder(provider);
pipeline.UseAccountEmailPrivacy();
pipeline.Run(_ => Task.CompletedTask);
request.Request.Path = "/Account/ResetPassword";
await pipeline.Build()(request);
Check(request.Response.Headers.CacheControl.ToString().Contains("no-store") && request.Response.Headers["Referrer-Policy"] == "no-referrer", "Account pages prevent caching and token referrer leakage");
Console.WriteLine($"{checks} account email checks passed. No real emails were sent.");

sealed class EmailMessage(JsonElement payload, string? authorization, string idempotencyKey)
{
    public JsonElement Payload { get; } = payload;
    public string? Authorization { get; } = authorization;
    public string IdempotencyKey { get; } = idempotencyKey;
    public string Html => Payload.GetProperty("html").GetString()!;
    public string Link(string label) => Regex.Match(Payload.GetProperty("text").GetString()!, Regex.Escape(label) + @": (https?://[^\r\n]+)").Groups[1].Value;
}

sealed class FakeEmailHandler : HttpMessageHandler
{
    public List<EmailMessage> Messages { get; } = [];
    public Queue<HttpStatusCode> Statuses { get; } = new();
    public Queue<string> Bodies { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsoluteUri != "https://api.resend.com/emails") throw new InvalidOperationException("Unexpected email API URL");
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        Messages.Add(new(body.RootElement.Clone(), request.Headers.Authorization?.ToString(), request.Headers.GetValues("Idempotency-Key").Single()));
        var status = Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK;
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(Bodies.Count > 0 ? Bodies.Dequeue() :
                status == HttpStatusCode.OK ? "{\"id\":\"fake-email-id\"}" : "{\"message\":\"re_fake_account_checks official@blueledger.test\"}")
        };
    }
}

sealed class CapturedLogs : ILoggerProvider
{
    public List<string> Messages { get; } = [];
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
    public void Dispose() { }
    private sealed class CaptureLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }
}
