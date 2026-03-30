using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using UserTrace.Models;

namespace UserTrace.Services;

public static class ActiveDirectorySearchService
{
    public static Task<List<string>> GetAllOfficesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetAllOfficesCore(cancellationToken), cancellationToken);
    }

    public static Task<List<SearchResultItem>> GetUsersByOfficeAsync(
        string officeName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetUsersByOfficeCore(officeName, cancellationToken), cancellationToken);
    }

    public static Task<List<SearchResultItem>> SearchByNameAsync(
        string term,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SearchByNameCore(term, cancellationToken), cancellationToken);
    }

    private static List<SearchResultItem> SearchByNameCore(string term, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var escaped = EscapeLdapFilterValue(term);
        var ldapPath = GetDomainLdapPath();

        var filter = $"(&(objectClass=user)(objectCategory=person)(|" +
                     $"(displayName=*{escaped}*)" +
                     $"(cn=*{escaped}*)" +
                     $"(givenName=*{escaped}*)" +
                     $"(sn=*{escaped}*)))";

        var results = new Dictionary<string, SearchResultItem>(StringComparer.OrdinalIgnoreCase);

        using var root    = string.IsNullOrEmpty(ldapPath)
                                ? new DirectoryEntry()
                                : new DirectoryEntry(ldapPath);

        using var searcher = new DirectorySearcher(root)
        {
            Filter      = filter,
            SearchScope = SearchScope.Subtree,
            SizeLimit   = 100,
            PageSize    = 100
        };

        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("displayName");

        ct.ThrowIfCancellationRequested();

        using var found = searcher.FindAll();

        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;

            var sam     = GetProp(sr, "sAMAccountName");
            var display = GetProp(sr, "displayName");

            if (string.IsNullOrEmpty(sam)) continue;

            if (!results.ContainsKey(sam))
            {
                results[sam] = new SearchResultItem
                {
                    SamAccountName = sam,
                    DisplayName    = display
                };
            }
        }

        return results.Values
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<string> GetAllOfficesCore(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ldapPath = GetDomainLdapPath();
        // Comparação ordinal para não "normalizar" acentos/cultura e manter setores distintos
        // quando houver variação de escrita (ex.: com/sem acento).
        var offices = new HashSet<string>(StringComparer.Ordinal);

        using var root = string.IsNullOrEmpty(ldapPath)
            ? new DirectoryEntry()
            : new DirectoryEntry(ldapPath);

        using var searcher = new DirectorySearcher(root)
        {
            Filter = "(&(objectCategory=person)(objectClass=user)(physicalDeliveryOfficeName=*)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 5000,
            PageSize = 1000
        };

        searcher.PropertiesToLoad.Add("physicalDeliveryOfficeName");
        ct.ThrowIfCancellationRequested();

        using var found = searcher.FindAll();
        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;

            var office = GetProp(sr, "physicalDeliveryOfficeName").Trim();
            if (string.IsNullOrWhiteSpace(office)) continue;
            offices.Add(office);
        }

        return offices
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    private static List<SearchResultItem> GetUsersByOfficeCore(string officeName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(officeName))
            return [];

        var escapedOffice = EscapeLdapFilterValue(officeName.Trim());
        var ldapPath = GetDomainLdapPath();

        using var root = string.IsNullOrEmpty(ldapPath)
            ? new DirectoryEntry()
            : new DirectoryEntry(ldapPath);

        using var searcher = new DirectorySearcher(root)
        {
            Filter = $"(&(objectCategory=person)(objectClass=user)(physicalDeliveryOfficeName={escapedOffice})(!(userAccountControl:1.2.840.113556.1.4.803:=2)))",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 5000,
            PageSize = 1000
        };

        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("displayName");
        searcher.PropertiesToLoad.Add("physicalDeliveryOfficeName");
        ct.ThrowIfCancellationRequested();

        var list = new List<SearchResultItem>();
        using var found = searcher.FindAll();
        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;

            // O AD pode considerar strings equivalentes (ex.: com/sem acento) no match do filtro.
            // Aqui garantimos match EXATO (incluindo acentos) com base no valor retornado.
            var officeFromAd = GetProp(sr, "physicalDeliveryOfficeName");
            if (!string.Equals(officeFromAd, officeName, StringComparison.Ordinal))
                continue;

            var sam = GetProp(sr, "sAMAccountName");
            if (string.IsNullOrWhiteSpace(sam)) continue;

            list.Add(new SearchResultItem
            {
                SamAccountName = sam,
                DisplayName = GetProp(sr, "displayName")
            });
        }

        return list
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetDomainLdapPath()
    {
        try
        {
            var domainName = Domain.GetComputerDomain().Name;
            return $"LDAP://{domainName}";
        }
        catch { }

        var envDomain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
        if (!string.IsNullOrEmpty(envDomain))
            return $"LDAP://{envDomain}";

        var userDomain = Environment.UserDomainName;
        if (!string.IsNullOrEmpty(userDomain))
            return $"LDAP://{userDomain}";

        return string.Empty;
    }

    private static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*",  "\\2a")
            .Replace("(",  "\\28")
            .Replace(")",  "\\29")
            .Replace("\0", "\\00")
            .Replace("/",  "\\2f");
    }

    private static string GetProp(SearchResult sr, string name)
    {
        var props = sr.Properties[name];
        if (props == null || props.Count == 0) return string.Empty;
        return props[0]?.ToString() ?? string.Empty;
    }

    // --- Senhas expiradas (política 180 dias). Filtro: usuários ativos, sem "senha nunca expira". ---
    private const string FilterSenhasExpiradas =
        "(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(!(userAccountControl:1.2.840.113556.1.4.803:=65536)))";

    private const int PasswordMaxAgeDays = 180;

    public static Task<List<SenhaExpiraItem>> GetPasswordExpiringInRangeAsync(
        DateTime dataInicio,
        DateTime dataFim,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetPasswordExpiringCore(dataInicio, dataFim, cancellationToken), cancellationToken);
    }

    public static Task<List<SenhaExpiraItem>> GetPasswordExpiringOnDateAsync(
        DateTime data,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetPasswordExpiringCore(data, data, cancellationToken), cancellationToken);
    }

    public static Task<List<SenhaExpiraItem>> GetPasswordExpiringTodayAsync(CancellationToken cancellationToken = default)
    {
        var hoje = DateTime.Today;
        return Task.Run(() => GetPasswordExpiringCore(hoje, hoje, cancellationToken), cancellationToken);
    }

    private static List<SenhaExpiraItem> GetPasswordExpiringCore(
        DateTime dataInicio,
        DateTime dataFim,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ldapPath = GetDomainLdapPath();
        using var root = string.IsNullOrEmpty(ldapPath) ? new DirectoryEntry() : new DirectoryEntry(ldapPath);
        using var searcher = new DirectorySearcher(root)
        {
            Filter      = FilterSenhasExpiradas,
            SearchScope = SearchScope.Subtree,
            SizeLimit   = 5000,
            PageSize    = 1000
        };
        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("displayName");
        searcher.PropertiesToLoad.Add("pwdLastSet");
        ct.ThrowIfCancellationRequested();

        var list = new List<SenhaExpiraItem>();
        using var found = searcher.FindAll();
        var ini = dataInicio.Date;
        var fim = dataFim.Date;

        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;
            var pwdLastSet = GetPropLong(sr, "pwdLastSet");
            if (pwdLastSet == 0) continue; // must change at next logon, not "expiring"
            var expira = DateTime.FromFileTime(pwdLastSet).AddDays(PasswordMaxAgeDays);
            if (expira.Date < ini || expira.Date > fim) continue;
            var sam = GetProp(sr, "sAMAccountName");
            if (string.IsNullOrEmpty(sam)) continue;
            list.Add(new SenhaExpiraItem
            {
                SamAccountName = sam,
                DisplayName    = GetProp(sr, "displayName"),
                Expira         = expira
            });
        }

        return list.OrderBy(x => x.Expira).ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Task<List<SearchResultItem>> GetMustChangePasswordAtNextLogonAsync(CancellationToken cancellationToken = default)
    {
        const string filter = "(&(objectCategory=person)(objectClass=user)(pwdLastSet=0)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
        return Task.Run(() => SearchByLdapFilterAsync(filter, cancellationToken), cancellationToken);
    }

    public static Task<List<SearchResultItem>> GetLockedOutAccountsAsync(CancellationToken cancellationToken = default)
    {
        const string filter = "(&(objectCategory=person)(objectClass=user)(lockoutTime>=1)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
        return Task.Run(() => SearchByLdapFilterAsync(filter, cancellationToken), cancellationToken);
    }

    /// <summary>Contas com userAccountControl bit 2 (ADS_UF_ACCOUNTDISABLE) = desativadas.</summary>
    public static Task<List<SearchResultItem>> GetDisabledAccountsAsync(CancellationToken cancellationToken = default)
    {
        const string filter = "(&(objectCategory=person)(objectClass=user)(userAccountControl:1.2.840.113556.1.4.803:=2))";
        return Task.Run(() => SearchByLdapFilterAsync(filter, cancellationToken), cancellationToken);
    }

    private static List<SearchResultItem> SearchByLdapFilterAsync(string filter, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ldapPath = GetDomainLdapPath();
        using var root = string.IsNullOrEmpty(ldapPath) ? new DirectoryEntry() : new DirectoryEntry(ldapPath);
        using var searcher = new DirectorySearcher(root)
        {
            Filter      = filter,
            SearchScope = SearchScope.Subtree,
            SizeLimit   = 5000,
            PageSize    = 1000
        };
        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("displayName");
        ct.ThrowIfCancellationRequested();

        var list = new List<SearchResultItem>();
        using var found = searcher.FindAll();
        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;
            var sam = GetProp(sr, "sAMAccountName");
            if (string.IsNullOrEmpty(sam)) continue;
            list.Add(new SearchResultItem
            {
                SamAccountName = sam,
                DisplayName    = GetProp(sr, "displayName")
            });
        }
        return list.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static long GetPropLong(SearchResult sr, string name)
    {
        var props = sr.Properties[name];
        if (props == null || props.Count == 0) return 0;
        var v = props[0];
        if (v is long l) return l;
        if (v is int i) return i;
        return 0;
    }
}
