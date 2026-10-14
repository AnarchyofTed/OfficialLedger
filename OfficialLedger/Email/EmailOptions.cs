using System.Net.Mail;

namespace OfficialLedger.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ApiKey { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "BlueLedger";
    public string? ReplyToAddress { get; set; }
    public string PublicBaseUrl { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Any(char.IsWhiteSpace) &&
        IsAddress(FromAddress) && !string.IsNullOrWhiteSpace(FromName) &&
        !FromName.Contains('\r') && !FromName.Contains('\n') &&
        (string.IsNullOrWhiteSpace(ReplyToAddress) || IsAddress(ReplyToAddress)) &&
        TryGetBaseUri(out _);

    public bool TryGetBaseUri(out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var candidate) ||
            !(candidate.Scheme == Uri.UriSchemeHttps ||
              (candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback)) ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static bool IsAddress(string address) =>
        !address.Contains('\r') && !address.Contains('\n') &&
        MailAddress.TryCreate(address, out var parsed) && parsed.Address == address;
}

public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException() : base("Account email could not be sent. Please try again later.") { }
}
