using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using OfficialLedger.Components.Account;
using OfficialLedger.Data;

namespace OfficialLedger.Email;

public static class AccountEmailSetup
{
    private static readonly HashSet<string> EmailRequestPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Register", "/Account/ExternalLogin", "/Account/ForgotPassword",
        "/Account/ResendEmailConfirmation", "/Account/Manage/Email"
    };

    public static IServiceCollection AddAccountEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate on send so missing email settings don't take down the rest of the app.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
        services.AddScoped<AccountEmailService>();
        services.AddHttpClient<IEmailSender<ApplicationUser>, IdentityEmailSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).RedactLoggedHeaders(new[] { "Authorization" })
          .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                HttpMethods.IsPost(context.Request.Method) && EmailRequestPaths.Contains(context.Request.Path.Value?.TrimEnd('/') ?? "")
                    ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    {
                        PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true
                    })
                    : RateLimitPartition.GetNoLimiter("other"));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsync("Too many email requests. Please wait a few minutes and try again.", cancellationToken);
            };
        });
        return services;
    }

    public static IApplicationBuilder UseAccountEmailPrivacy(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
        }
        await next(context);
    });
}
