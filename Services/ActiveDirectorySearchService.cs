using System.DirectoryServices;
using UserTrace.Core;
using UserTrace.Models;

namespace UserTrace.Services;

public static class ActiveDirectorySearchService
{
    public static Task<QueryResult<string>> GetAllOfficesAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => GetAllOfficesCore(cancellationToken), cancellationToken);

    public static Task<QueryResult<SearchResultItem>> GetUsersByOfficeAsync(
        string officeName,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetUsersByOfficeCore(officeName, cancellationToken), cancellationToken);

    public static Task<QueryResult<SearchResultItem>> GetUsersWithoutOfficeAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetUsersWithoutOfficeCore(cancellationToken), cancellationToken);

    public static Task<QueryResult<SearchResultItem>> SearchByNameAsync(
        string term,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => SearchByNameCore(term, cancellationToken), cancellationToken);

    public static Task<QueryResult<SenhaExpiraItem>> GetPasswordExpiringInRangeAsync(
        DateTime dataInicio,
        DateTime dataFim,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetPasswordExpiringCore(dataInicio, dataFim, cancellationToken), cancellationToken);

    public static Task<QueryResult<SenhaExpiraItem>> GetPasswordExpiringOnDateAsync(
        DateTime data,
        CancellationToken cancellationToken = default) =>
        GetPasswordExpiringInRangeAsync(data, data, cancellationToken);

    public static Task<QueryResult<SenhaExpiraItem>> GetPasswordExpiringTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var hoje = DateTime.Today;
        return GetPasswordExpiringInRangeAsync(hoje, hoje, cancellationToken);
    }

    public static Task<QueryResult<SearchResultItem>> GetMustChangePasswordAtNextLogonAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(pwdLastSet=0))";
        return Task.Run(() => SearchUsers(filter, cancellationToken), cancellationToken);
    }

    public static Task<QueryResult<SearchResultItem>> GetLockedOutAccountsAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetLockedOutAccountsCore(cancellationToken), cancellationToken);

    public static Task<QueryResult<SearchResultItem>> GetDisabledAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = $"(&{LdapDirectory.Person}(userAccountControl:1.2.840.113556.1.4.803:=2))";
        return Task.Run(() => SearchUsers(filter, cancellationToken), cancellationToken);
    }

    private static QueryResult<SearchResultItem> SearchByNameCore(string term, CancellationToken ct)
    {
        var escaped = LdapFilter.Escape(term);
        var filter =
            $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(|" +
            $"(displayName=*{escaped}*)" +
            $"(cn=*{escaped}*)" +
            $"(givenName=*{escaped}*)" +
            $"(sn=*{escaped}*)" +
            $"(sAMAccountName=*{escaped}*)))";

        return SearchUsers(filter, ct, SearchLimits.Safety);
    }

    private static QueryResult<string> GetAllOfficesCore(CancellationToken ct)
    {
        var filter = $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(physicalDeliveryOfficeName=*))";
        var result = LdapDirectory.Query(
            filter,
            ["physicalDeliveryOfficeName"],
            SearchLimits.Safety,
            sr =>
            {
                var office = LdapDirectory.ReadString(sr, "physicalDeliveryOfficeName").Trim();
                return string.IsNullOrWhiteSpace(office) ? null : office;
            },
            ct);

        var offices = result.Items
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return result.WithItems(offices);
    }

    private static QueryResult<SearchResultItem> GetUsersByOfficeCore(string officeName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(officeName))
            return QueryResult<SearchResultItem>.None(SearchLimits.Safety);

        var expected = officeName.Trim();
        var escapedOffice = LdapFilter.Escape(expected);
        var filter =
            $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(physicalDeliveryOfficeName={escapedOffice}))";

        var result = LdapDirectory.Query(
            filter,
            ["sAMAccountName", "displayName", "physicalDeliveryOfficeName"],
            SearchLimits.Safety,
            sr =>
            {
                var officeFromAd = LdapDirectory.ReadString(sr, "physicalDeliveryOfficeName");
                if (!string.Equals(officeFromAd, expected, StringComparison.Ordinal))
                    return null;
                return MapUser(sr);
            },
            ct);

        return OrderUsers(result);
    }

    private static QueryResult<SearchResultItem> GetUsersWithoutOfficeCore(CancellationToken ct)
    {
        var filter = $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(!(physicalDeliveryOfficeName=*)))";
        return SearchUsers(filter, ct);
    }

    private static QueryResult<SenhaExpiraItem> GetPasswordExpiringCore(
        DateTime dataInicio,
        DateTime dataFim,
        CancellationToken ct)
    {
        var ini = dataInicio.Date;
        var fim = dataFim.Date;
        if (fim < ini)
            return QueryResult<SenhaExpiraItem>.None(SearchLimits.Safety);

        var policy = LdapDirectory.GetPasswordPolicy();
        if (!policy.PasswordsExpire)
            return QueryResult<SenhaExpiraItem>.None(SearchLimits.Safety);

        var (start, end) = PasswordExpiry.PwdLastSetWindow(policy, ini, fim);
        var filter =
            $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}{LdapDirectory.PasswordCanExpire}" +
            $"(pwdLastSet>={start})(pwdLastSet<={end}))";

        var result = LdapDirectory.Query(
            filter,
            ["sAMAccountName", "displayName", "pwdLastSet"],
            SearchLimits.Safety,
            sr => MapExpiry(sr, policy),
            ct);

        var items = result.Items
            .OrderBy(x => x.Expira)
            .ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result.WithItems(items);
    }

    private static QueryResult<SearchResultItem> GetLockedOutAccountsCore(CancellationToken ct)
    {
        var filter = $"(&{LdapDirectory.Person}{LdapDirectory.ActiveAccount}(lockoutTime>=1))";
        var result = LdapDirectory.Query(
            filter,
            ["sAMAccountName", "displayName", "lockoutTime"],
            SearchLimits.Safety,
            sr =>
            {
                var sam = LdapDirectory.ReadString(sr, "sAMAccountName");
                if (string.IsNullOrWhiteSpace(sam))
                    return null;

                var lockout = LdapDirectory.ReadInt64(sr, "lockoutTime");
                return new LockedHit(
                    new SearchResultItem
                    {
                        SamAccountName = sam,
                        DisplayName = LdapDirectory.ReadString(sr, "displayName"),
                        LockoutTime = AdFileTime.FormatLocal(lockout, string.Empty)
                    },
                    lockout);
            },
            ct);

        var items = result.Items
            .OrderByDescending(x => x.LockoutFileTime)
            .ThenBy(x => x.Item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Item.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item)
            .ToList();

        return new QueryResult<SearchResultItem>
        {
            Items = items,
            Truncated = result.Truncated,
            Limit = result.Limit
        };
    }

    private static QueryResult<SearchResultItem> SearchUsers(string filter, CancellationToken ct, int limit = SearchLimits.Safety)
    {
        var result = LdapDirectory.Query(
            filter,
            ["sAMAccountName", "displayName"],
            limit,
            MapUser,
            ct);
        return OrderUsers(result);
    }

    private static QueryResult<SearchResultItem> OrderUsers(QueryResult<SearchResultItem> result)
    {
        var items = result.Items
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result.WithItems(items);
    }

    private static SearchResultItem? MapUser(SearchResult sr)
    {
        var sam = LdapDirectory.ReadString(sr, "sAMAccountName");
        if (string.IsNullOrWhiteSpace(sam))
            return null;

        return new SearchResultItem
        {
            SamAccountName = sam,
            DisplayName = LdapDirectory.ReadString(sr, "displayName")
        };
    }

    private static SenhaExpiraItem? MapExpiry(SearchResult sr, PasswordPolicy policy)
    {
        var sam = LdapDirectory.ReadString(sr, "sAMAccountName");
        if (string.IsNullOrEmpty(sam))
            return null;

        var pwdLastSet = LdapDirectory.ReadInt64(sr, "pwdLastSet");
        if (pwdLastSet <= 0)
            return null;

        try
        {
            var expira = DateTime.FromFileTimeUtc(pwdLastSet).ToLocalTime() + policy.MaxAge;
            return new SenhaExpiraItem
            {
                SamAccountName = sam,
                DisplayName = LdapDirectory.ReadString(sr, "displayName"),
                Expira = expira
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record LockedHit(SearchResultItem Item, long LockoutFileTime);
}
