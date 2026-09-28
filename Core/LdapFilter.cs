using System.Globalization;
using System.Text;

namespace UserTrace.Core;

/// <summary>Escape de valores para filtros LDAP (RFC 4515).</summary>
public static class LdapFilter
{
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal)
            .Replace("/", "\\2f", StringComparison.Ordinal);
    }

    public static string EscapeBinary(ReadOnlySpan<byte> value)
    {
        var builder = new StringBuilder(value.Length * 3);
        foreach (var b in value)
            builder.Append('\\').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
