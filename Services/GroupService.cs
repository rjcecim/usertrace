using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using UserTrace.Models;

namespace UserTrace.Services;

public static class GroupService
{
    // Constantes de groupType (bitmask AD)
    private const int GROUP_GLOBAL    = 0x00000002;
    private const int GROUP_LOCAL     = 0x00000004;
    private const int GROUP_UNIVERSAL = 0x00000008;

    /// <summary>
    /// Lista todos os grupos do domínio, equivalente a "net group /domain".
    /// Retorna lista ordenada por nome, limitada a 2000 grupos.
    /// </summary>
    public static Task<List<GroupItem>> GetAllGroupsAsync(
        string? filterName = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetAllGroupsCore(filterName, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Retorna todos os membros (sAMAccountName + displayName) de um grupo.
    /// Resolve membros diretos e, quando possível, membros de subgrupos (1 nível).
    /// </summary>
    public static Task<List<SearchResultItem>> GetGroupMembersAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetGroupMembersCore(groupName, cancellationToken), cancellationToken);
    }

    // ── implementações privadas ──────────────────────────────────────────────

    private static List<GroupItem> GetAllGroupsCore(string? filter, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ldapPath = GetDomainLdapPath();
        var results  = new List<GroupItem>();

        // Filtro: apenas grupos (objectClass=group), opcionalmente por nome
        string nameFilter = string.IsNullOrWhiteSpace(filter)
            ? string.Empty
            : $"(cn=*{EscapeLdap(filter.Trim())}*)";

        string ldapFilter = $"(&(objectClass=group)(objectCategory=group){nameFilter})";

        using var root    = string.IsNullOrEmpty(ldapPath)
                                ? new DirectoryEntry()
                                : new DirectoryEntry(ldapPath);

        using var searcher = new DirectorySearcher(root)
        {
            Filter      = ldapFilter,
            SearchScope = SearchScope.Subtree,
            SizeLimit   = 2000,
            PageSize    = 500
        };

        searcher.PropertiesToLoad.Add("cn");
        searcher.PropertiesToLoad.Add("description");
        searcher.PropertiesToLoad.Add("groupType");

        ct.ThrowIfCancellationRequested();

        using var found = searcher.FindAll();

        foreach (SearchResult? sr in found)
        {
            if (sr == null) continue;

            var name = GetProp(sr, "cn");
            if (string.IsNullOrEmpty(name)) continue;

            var desc      = GetProp(sr, "description");
            var groupType = ResolveGroupType(sr);

            results.Add(new GroupItem
            {
                Name        = name,
                Description = desc,
                GroupType   = groupType
            });
        }

        results.Sort((a, b) =>
            string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        return results;
    }

    private static List<SearchResultItem> GetGroupMembersCore(string groupName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ldapPath = GetDomainLdapPath();
        var members  = new Dictionary<string, SearchResultItem>(StringComparer.OrdinalIgnoreCase);

        // Passo 1: encontrar o DN do grupo pelo nome
        string groupDn = FindGroupDn(groupName, ldapPath);

        if (string.IsNullOrEmpty(groupDn))
            return [];

        ct.ThrowIfCancellationRequested();

        // Passo 2: buscar todos os usuários cujo memberOf contém este grupo
        // Usa filtro LDAP_MATCHING_RULE_IN_CHAIN (1.2.840.113556.1.4.1941)
        // para resolver membros de subgrupos recursivamente no AD
        string filter =
            $"(&(objectClass=user)(objectCategory=person)" +
            $"(memberOf:1.2.840.113556.1.4.1941:={EscapeLdap(groupDn)}))";

        using var root = string.IsNullOrEmpty(ldapPath)
                             ? new DirectoryEntry()
                             : new DirectoryEntry(ldapPath);

        using var searcher = new DirectorySearcher(root)
        {
            Filter      = filter,
            SearchScope = SearchScope.Subtree,
            SizeLimit   = 1000,
            PageSize    = 500
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

            if (!members.ContainsKey(sam))
            {
                members[sam] = new SearchResultItem
                {
                    SamAccountName = sam,
                    DisplayName    = display
                };
            }
        }

        return members.Values
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string FindGroupDn(string groupName, string ldapPath)
    {
        try
        {
            using var root = string.IsNullOrEmpty(ldapPath)
                                 ? new DirectoryEntry()
                                 : new DirectoryEntry(ldapPath);

            using var searcher = new DirectorySearcher(root)
            {
                Filter      = $"(&(objectClass=group)(cn={EscapeLdap(groupName)}))",
                SearchScope = SearchScope.Subtree,
                SizeLimit   = 1
            };
            searcher.PropertiesToLoad.Add("distinguishedName");

            var result = searcher.FindOne();
            if (result == null) return string.Empty;

            return GetProp(result, "distinguishedName");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveGroupType(SearchResult sr)
    {
        var props = sr.Properties["groupType"];
        if (props == null || props.Count == 0) return string.Empty;

        if (props[0] is not int raw) return string.Empty;

        // O bit de sinal (0x80000000) indica grupo de segurança vs. distribuição
        int scope = raw & 0x0000000F;

        return scope switch
        {
            GROUP_GLOBAL    => "Global",
            GROUP_LOCAL     => "Local",
            GROUP_UNIVERSAL => "Universal",
            _               => string.Empty
        };
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

    private static string GetProp(SearchResult sr, string name)
    {
        var props = sr.Properties[name];
        if (props == null || props.Count == 0) return string.Empty;
        return props[0]?.ToString() ?? string.Empty;
    }

    private static string EscapeLdap(string value) =>
        value.Replace("\\", "\\5c").Replace("*", "\\2a")
             .Replace("(", "\\28").Replace(")", "\\29")
             .Replace("\0", "\\00").Replace("/", "\\2f");
}
