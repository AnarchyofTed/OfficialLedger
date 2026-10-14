using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OfficialLedger.Data;
using OfficialLedger.Email;

namespace OfficialLedger.Components.Account;

public sealed class AccountEmailService(
    UserManager<ApplicationUser> users,
    IEmailSender<ApplicationUser> sender,
    IOptions<EmailOptions> options,
    ILogger<AccountEmailService> logger)
{
    public async Task SendConfirmationAsync(ApplicationUser user, string? returnUrl = null)
    {
        var code = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = CreateLink("Account/ConfirmEmail", new()
        {
            ["userId"] = await users.GetUserIdAsync(user),
            ["code"] = Encode(code),
            ["returnUrl"] = SafeReturnUrl(returnUrl)
        });
        await sender.SendConfirmationLinkAsync(user, user.Email!, link);
    }

    public async Task SendEmailChangeAsync(ApplicationUser user, string newEmail)
    {
        var code = await users.GenerateChangeEmailTokenAsync(user, newEmail);
        var link = CreateLink("Account/ConfirmEmailChange", new()
        {
            ["userId"] = await users.GetUserIdAsync(user), ["email"] = newEmail, ["code"] = Encode(code)
        });
        await sender.SendConfirmationLinkAsync(user, newEmail, link);
    }

    public async Task RequestConfirmationAsync(string email)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || await users.IsEmailConfirmedAsync(user)) return;
        try
        {
            await SendConfirmationAsync(user);
        }
        catch (EmailDeliveryException)
        {
            // Keep the public response identical for existing and unknown addresses.
            // The email sender/configuration logs the operational failure without account data.
        }
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !await users.IsEmailConfirmedAsync(user) || !await users.HasPasswordAsync(user)) return;
        try
        {
            var code = await users.GeneratePasswordResetTokenAsync(user);
            var link = CreateLink("Account/ResetPassword", new()
            {
                ["email"] = user.Email, ["code"] = Encode(code)
            });
            await sender.SendPasswordResetLinkAsync(user, user.Email!, link);
        }
        catch (EmailDeliveryException)
        {
            // Avoid revealing account existence through an account-specific delivery error.
        }
    }

    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') &&
        !returnUrl.StartsWith("//") && !returnUrl.Contains('\\') && !returnUrl.Any(char.IsControl)
            ? returnUrl : "/dashboard";

    public static bool TryDecodeToken(string? encoded, out string token)
    {
        token = "";
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 4096) return false;
        try
        {
            token = new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(encoded));
            return token.Length > 0;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return false;
        }
    }

    private static string Encode(string code) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    private string CreateLink(string path, Dictionary<string, string?> query)
    {
        if (!options.Value.IsConfigured || !options.Value.TryGetBaseUri(out var baseUri))
        {
            logger.LogError("Account email configuration is incomplete or invalid. Check the Email settings, including the public HTTPS site URL.");
            throw new EmailDeliveryException();
        }
        return QueryHelpers.AddQueryString(new Uri(baseUri!, path).AbsoluteUri, query);
    }
}
