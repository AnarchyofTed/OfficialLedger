using System.Text;
using OfficialLedger.Models;

namespace OfficialLedger.Data;

public sealed record ReceiptInfo(int Id, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);

// Uploads stay in the form until the expense and all of its receipts are saved together.
public sealed record ReceiptUpload(string FileName, string ContentType, byte[] Content, string? PreviewUrl = null);

public static class ReceiptFiles
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxReceipts = 5;
    public const string Accept = ".jpg,.jpeg,.png,.webp,.heic,.heif,.pdf,image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf";

    public static bool CanPreview(string contentType) => contentType is "image/jpeg" or "image/png" or "image/webp";
    public static string ViewUrl(int id) => $"/expenses/receipts/{id}";
    public static string DownloadUrl(int id) => $"{ViewUrl(id)}?download=true";
    public static void ValidateCount(int savedCount, int pendingCount)
    {
        if (savedCount < 0 || pendingCount < 0 || (long)savedCount + pendingCount > MaxReceipts)
            throw new InvalidDataException($"Attach no more than {MaxReceipts} receipts.");
    }

    public static string DetectContentType(byte[] content)
    {
        if (content.Length == 0 || content.LongLength > MaxFileBytes)
            throw new InvalidDataException("Receipts must be between 1 byte and 10 MB.");
        var bytes = content.AsSpan();
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith("%PDF-"u8)) return "application/pdf";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (bytes.Length >= 16 && bytes[4..8].SequenceEqual("ftyp"u8))
        {
            var boxSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]);
            if (boxSize >= 16 && boxSize <= bytes.Length && boxSize <= 4096)
            {
                for (var offset = 8; offset + 4 <= boxSize; offset += 4)
                {
                    if (offset == 12) continue; // Minor version, not a brand.
                    var brand = Encoding.ASCII.GetString(bytes.Slice(offset, 4));
                    if (brand is "heic" or "heix" or "hevc" or "hevx") return "image/heic";
                }
                var majorBrand = Encoding.ASCII.GetString(bytes[8..12]);
                if (majorBrand is "mif1" or "msf1") return "image/heif";
            }
        }
        throw new InvalidDataException("Choose a JPG, PNG, WebP, HEIC, HEIF, or PDF receipt.");
    }

    public static string SafeFileName(string fileName)
    {
        var name = fileName.Replace('\\', '/').Split('/').Last();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "receipt";
        return name.Length > 200 ? name[..200] : name;
    }

    public static ExpenseReceipt CreateReceipt(ReceiptUpload upload) => new()
    {
        FileName = SafeFileName(upload.FileName),
        ContentType = DetectContentType(upload.Content), // Never trust the browser's MIME type.
        Content = upload.Content,
        SizeBytes = upload.Content.LongLength,
        UploadedAtUtc = DateTime.UtcNow
    };
}
