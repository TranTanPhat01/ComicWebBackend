using System.Globalization;
using System.Text;
using ComicWeb.Application.Common.Interfaces;

namespace ComicWeb.Persistence.Content;

public sealed class VietnameseSlugGenerator : ISlugGenerator
{
    public string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        var pendingDash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && result.Length > 0) result.Append('-');
                result.Append(char.ToLowerInvariant(character));
                pendingDash = false;
            }
            else pendingDash = result.Length > 0;
        }
        return result.ToString();
    }
}
