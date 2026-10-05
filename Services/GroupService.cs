using System.DirectoryServices;
using UserTrace.Core;
using UserTrace.Models;

namespace UserTrace.Services;

public static class GroupService
{
    private const int GroupGlobal = 0x00000002;
    private const int GroupLocal = 0x00000004;
    private const int GroupUniversal = 0x00000008;

    public static Task<QueryResult<GroupItem>> GetAllGroupsAsync(
        string? filterName = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetAllGroupsCore(filterName, cancellationToken), cancellationToken);

    public static Task<QueryResult<GroupItem>> SearchGroupsLimitedAsync(
        string? filterName,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var nameFilter = string.IsNullOrWhiteSpace(filterName)
                ? string.Empty
                : $"(cn=*{LdapFilter.Escape(filterName.Trim())}*)";
            var ldapFilter = $"(&(objectClass=group)(objectCategory=group){nameFilter})";
            return LdapDirectory.Query(
                ldapFilter,
                ["cn", "description", "groupType"],
                Math.Max(1, limit),
                MapGroup,
                cancellationToken);
        }, cancellationToken);

    public static Task<QueryResult<SearchResultItem>> GetGroupMembersAsync(
        string groupName,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetGroupMembersCore(groupName, cancellationToken), cancellationToken);

    private static QueryResult<GroupItem> GetAllGroupsCore(string? filter, CancellationToken ct)
    {
        var nameFilter = string.IsNullOrWhiteSpace(filter)
            ? string.Empty
            : $"(cn=*{LdapFilter.Escape(filter.Trim())}*)";

        var ldapFilter = $"(&(objectClass=group)(objectCategory=group){nameFilter})";
        var result = LdapDirectory.Query(
            ldapFilter,
            ["cn", "description", "groupType"],
            SearchLimits.Safety,
            MapGroup,
            ct);

        var items = result.Items
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return result.WithItems(items);
    }

    private static QueryResult<SearchResultItem> GetGroupMembersCore(string groupName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return QueryResult<SearchResultItem>.None(SearchLimits.Safety);

        var groupDn = FindGroupDn(groupName.Trim());
        if (string.IsNullOrEmpty(groupDn))
            return QueryResult<SearchResultItem>.None(SearchLimits.Safety);

        ct.ThrowIfCancellationRequested();

        var filter =
            $"(&(objectClass=user)(objectCategory=person)" +
            $"(memberOf:1.2.840.113556.1.4.1941:={LdapFilter.Escape(groupDn)}))";

        var result = LdapDirectory.Query(
            filter,
            ["sAMAccountName", "displayName"],
            SearchLimits.Safety,
            sr =>
            {
                var sam = LdapDirectory.ReadString(sr, "sAMAccountName");
                if (string.IsNullOrEmpty(sam))
                    return null;

                return new SearchResultItem
                {
                    SamAccountName = sam,
                    DisplayName = LdapDirectory.ReadString(sr, "displayName")
                };
            },
            ct);

        var items = result.Items
            .GroupBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result.WithItems(items);
    }

    private static string FindGroupDn(string groupName)
    {
        using var root = LdapDirectory.OpenRoot();
        using var searcher = new DirectorySearcher(root)
        {
            Filter = $"(&(objectClass=group)(cn={LdapFilter.Escape(groupName)}))",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 1
        };
        searcher.PropertiesToLoad.Add("distinguishedName");

        try
        {
            var result = searcher.FindOne();
            if (result is null)
                return string.Empty;
            return LdapDirectory.ReadString(result, "distinguishedName");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha ao consultar o Active Directory: {ex.Message}", ex);
        }
    }

    private static GroupItem? MapGroup(SearchResult sr)
    {
        var name = LdapDirectory.ReadString(sr, "cn");
        if (string.IsNullOrEmpty(name))
            return null;

        return new GroupItem
        {
            Name = name,
            Description = LdapDirectory.ReadString(sr, "description"),
            GroupType = ResolveGroupType(LdapDirectory.ReadInt32(sr, "groupType"))
        };
    }

    private static string ResolveGroupType(int raw)
    {
        var scope = raw & 0x0000000F;
        return scope switch
        {
            GroupGlobal => "Global",
            GroupLocal => "Local",
            GroupUniversal => "Universal",
            _ => string.Empty
        };
    }
}
