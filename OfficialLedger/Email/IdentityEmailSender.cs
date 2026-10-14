using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OfficialLedger.Data;

namespace OfficialLedger.Email;

public sealed class IdentityEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<IdentityEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendLinkAsync(user, email, "Confirm your BlueLedger email", "Confirm your email",
            "Confirm this email address for your BlueLedger account.",
            "Confirm email", confirmationLink, "This link expires in 24 hours.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendLinkAsync(user, email, "Reset your BlueLedger password", "Reset your password",
            "We received a request to reset your BlueLedger password.",
            "Reset password", resetLink, "This link expires in 1 hour and can only be used once.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var encodedCode = HtmlEncoder.Default.Encode(resetCode);
        return SendAsync(email, "Your BlueLedger password reset code",
            $"<h1>Reset your password</h1><p>Your reset code is:</p><p><strong>{encodedCode}</strong></p><p>If you did not request this, you can ignore this email.</p>",
            $"Your BlueLedger password reset code is: {resetCode}\n\nIf you did not request this, you can ignore this email.");
    }

    private Task SendLinkAsync(ApplicationUser user, string email, string subject, string heading,
        string introduction, string button, string link, string expiry)
    {
        // Callers supply a raw URL. Encode it exactly once for HTML, preserving the plain-text URL.
        var encodedLink = HtmlEncoder.Default.Encode(link);
        var greeting = string.IsNullOrWhiteSpace(user.FirstName) ? "Hello" : $"Hi {user.FirstName.Trim()}";
        var encodedGreeting = HtmlEncoder.Default.Encode(greeting);
        var html = $$"""
            <!doctype html><html lang="en"><body style="margin:0;background:#f4f2ec;font-family:Arial,sans-serif;color:#172d48">
            <div style="max-width:560px;margin:32px auto;padding:32px;background:#fff;border-radius:16px">
                <p style="font-size:20px;font-weight:bold;color:#2472c8">BlueLedger</p>
                <h1 style="font-size:28px">{{heading}}</h1>
                <p>{{encodedGreeting}},</p><p>{{introduction}}</p>
                <p style="margin:28px 0"><a href="{{encodedLink}}" style="display:inline-block;padding:14px 22px;background:#2472c8;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold">{{button}}</a></p>
                <p>{{expiry}}</p>
                <p style="font-size:14px;color:#52677f">If the button does not work, copy this link into your browser:<br><a href="{{encodedLink}}" style="color:#2472c8;word-break:break-all">{{encodedLink}}</a></p>
                <p style="font-size:14px;color:#52677f">If you did not request this, you can ignore this email.</p>
            </div></body></html>
            """;
        var text = $"{greeting},\n\n{introduction}\n\n{button}: {link}\n\n{expiry}\n\nIf you did not request this, you can ignore this email.";
        return SendAsync(email, subject, html, text);
    }

    private async Task SendAsync(string email, string subject, string html, string text)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogError("Account email is not configured. Set Email:ApiKey, Email:FromAddress and Email:PublicBaseUrl, and check the optional sender settings.");
            throw new EmailDeliveryException();
        }

        var payload = new Dictionary<string, object>
        {
            ["from"] = new MailAddress(settings.FromAddress, settings.FromName).ToString(),
            ["to"] = new[] { email },
            ["subject"] = subject,
            ["html"] = html,
            ["text"] = text
        };
        if (!string.IsNullOrWhiteSpace(settings.ReplyToAddress))
            payload["reply_to"] = settings.ReplyToAddress;

        var idempotencyKey = $"account-email/{Guid.NewGuid():N}";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = JsonContent.Create(payload);
                using var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (result.RootElement.ValueKind == JsonValueKind.Object &&
                        result.RootElement.TryGetProperty("id", out var id) &&
                        id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        logger.LogInformation("Account email accepted by the email provider.");
                        return;
                    }
                    logger.LogError("Email provider returned a success response without a message ID.");
                    throw new EmailDeliveryException();
                }

                if (attempt < 2 && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)));
                    continue;
                }

                // Never log provider response bodies: they can echo addresses, links or credentials.
                logger.LogError("Email provider rejected an account email with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw new EmailDeliveryException();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)));
                    continue;
                }
                logger.LogError("Account email delivery failed due to a network error or timeout.");
                throw new EmailDeliveryException();
            }
            catch (JsonException)
            {
                logger.LogError("Email provider returned an invalid account email response.");
                throw new EmailDeliveryException();
            }
        }
    }
}
