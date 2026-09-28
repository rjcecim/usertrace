namespace UserTrace.Core;

/// <summary>
/// Extrai CN e o caminho de OUs de um distinguished name.
/// O caminho para na OU <see cref="DefaultRootOu"/> (exclusive) e segue da OU mais alta para a mais próxima do CN.
/// </summary>
public static class DistinguishedNameParser
{
    public const string DefaultRootOu = "Tribunal";

    public static string ExtractCn(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
            return string.Empty;

        foreach (var part in Split(distinguishedName))
        {
            if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return part.Length > 3 ? part[3..] : string.Empty;
        }

        return string.Empty;
    }

    public static string ExtractOuPath(string? distinguishedName, string? stopAtOu = DefaultRootOu)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
            return string.Empty;

        var ous = new List<string>(capacity: 6);
        var seenCn = false;
        foreach (var part in Split(distinguishedName))
        {
            if (!seenCn)
            {
                if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    seenCn = true;
                continue;
            }

            if (part.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            {
                var ou = part.Length > 3 ? part[3..] : string.Empty;
                if (!string.IsNullOrEmpty(stopAtOu) &&
                    string.Equals(ou, stopAtOu, StringComparison.OrdinalIgnoreCase))
                    break;

                if (!string.IsNullOrWhiteSpace(ou))
                    ous.Add(ou);
                continue;
            }

            if (part.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (ous.Count == 0)
            return string.Empty;

        ous.Reverse();
        return string.Join("\\", ous);
    }

    private static List<string> Split(string distinguishedName)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < distinguishedName.Length; i++)
        {
            var c = distinguishedName[i];
            if (c == '\\' && i + 1 < distinguishedName.Length)
            {
                current.Append(c);
                current.Append(distinguishedName[++i]);
                continue;
            }

            if (c == ',')
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString().Trim());

        return parts;
    }
}
