using System.DirectoryServices;
using UserTrace.Core;
using UserTrace.Models;

namespace UserTrace.Services;

public static class NetUserService
{
    private const int UfAccountDisable = 0x0002;
    private const int UfPasswdNotReqd = 0x0020;
    private const int UfPasswdCantChange = 0x0040;
    private const int UfDontExpirePasswd = 0x10000;
    private const int UfSmartcardRequired = 0x40000;
    private const int UfPasswordExpired = 0x800000;

    private const int GroupDomainLocal = 0x00000004;

    private static readonly string[] UserProperties =
    [
        "sAMAccountName",
        "displayName",
        "mail",
        "telephoneNumber",
        "physicalDeliveryOfficeName",
        "distinguishedName",
        "description",
        "comment",
        "userAccountControl",
        "accountExpires",
        "pwdLastSet",
        "badPwdCount",
        "badPasswordTime",
        "lockoutTime",
        "lastLogonTimestamp",
        "userWorkstations",
        "scriptPath",
        "profilePath",
        "homeDirectory",
        "objectSid",
        "primaryGroupID"
    ];

    public static Task<CommandResult> GetUserDetailsAsync(
        string samAccountName,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetUserDetailsCore(samAccountName, cancellationToken), cancellationToken);

    private static CommandResult GetUserDetailsCore(string sam, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var login = sam.Trim();
        if (string.IsNullOrEmpty(login))
            return new CommandResult { Error = "Informe o login.", ExitCode = 1 };

        try
        {
            using var root = LdapDirectory.OpenRoot();
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={LdapFilter.Escape(login)}))",
                SearchScope = SearchScope.Subtree,
                SizeLimit = 1
            };

            foreach (var property in UserProperties)
                searcher.PropertiesToLoad.Add(property);

            var result = searcher.FindOne();
            if (result is null)
            {
                return new CommandResult
                {
                    Error = $"O nome de usuário '{login}' não foi encontrado.",
                    ExitCode = 2221
                };
            }

            ct.ThrowIfCancellationRequested();
            var user = BuildUserInfo(login, root, result, ct);
            return new CommandResult { UserInfo = user, ExitCode = 0 };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Error = $"Falha ao consultar o Active Directory: {ex.Message}",
                ExitCode = 1
            };
        }
    }

    private static UserInfo BuildUserInfo(string sam, DirectoryEntry root, SearchResult result, CancellationToken ct)
    {
        var flags = LdapDirectory.ReadInt32(result, "userAccountControl");
        var passwordNeverExpires = HasFlag(flags, UfDontExpirePasswd);
        var pwdLastSet = LdapDirectory.ReadInt64(result, "pwdLastSet");
        var policy = LdapDirectory.GetPasswordPolicy();
        var expiry = PasswordExpiry.Describe(policy, passwordNeverExpires, pwdLastSet, DateTime.Today);

        var (localGroups, globalGroups) = LoadGroups(root, result, ct);
        var workstations = LdapDirectory.ReadString(result, "userWorkstations");
        var fullName = LdapDirectory.ReadString(result, "displayName");

        return new UserInfo
        {
            SamAccountName = FirstNonEmpty(LdapDirectory.ReadString(result, "sAMAccountName"), sam),
            FullName = fullName,
            Email = LdapDirectory.ReadString(result, "mail"),
            PhoneNumber = LdapDirectory.ReadString(result, "telephoneNumber"),
            Office = LdapDirectory.ReadString(result, "physicalDeliveryOfficeName"),
            OrganizationalUnit = DistinguishedNameParser.ExtractOuPath(
                LdapDirectory.ReadString(result, "distinguishedName")),
            Comment = LdapDirectory.ReadString(result, "description"),
            UserComment = LdapDirectory.ReadString(result, "comment"),

            AccountActive = !HasFlag(flags, UfAccountDisable),
            AccountExpires = AdFileTime.FormatAccountExpires(LdapDirectory.ReadInt64(result, "accountExpires")),

            PasswordLastSet = AdFileTime.FormatLocal(pwdLastSet, "No próximo logon"),
            PasswordExpiresOn = expiry.ExpiresOn,
            PasswordDaysToExpire = expiry.DaysToExpire,
            BadPasswordCount = FirstNonEmpty(LdapDirectory.ReadString(result, "badPwdCount"), "0"),
            BadPasswordTime = AdFileTime.FormatLocal(LdapDirectory.ReadInt64(result, "badPasswordTime"), "Nunca"),
            LockoutTime = AdFileTime.FormatLocal(LdapDirectory.ReadInt64(result, "lockoutTime"), "Não bloqueada"),
            PasswordNeverExpires = passwordNeverExpires,
            PasswordExpired = HasFlag(flags, UfPasswordExpired) || expiry.Expired,
            PasswordRequired = !HasFlag(flags, UfPasswdNotReqd),
            PasswordChangeable = !HasFlag(flags, UfPasswdCantChange),
            SmartcardRequired = HasFlag(flags, UfSmartcardRequired),

            LastLogon = AdFileTime.FormatLocal(LdapDirectory.ReadInt64(result, "lastLogonTimestamp"), "Nunca"),
            LastLogoff = "Não disponível",
            Workstations = string.IsNullOrEmpty(workstations) ? "Todas" : workstations,
            LogonScript = LdapDirectory.ReadString(result, "scriptPath"),
            ProfilePath = LdapDirectory.ReadString(result, "profilePath"),
            HomeDirectory = LdapDirectory.ReadString(result, "homeDirectory"),

            LocalGroups = localGroups,
            GlobalGroups = globalGroups,
            Domain = LdapDirectory.GetDomainDnsName()
        };
    }

    private static (IReadOnlyList<string> Local, IReadOnlyList<string> Global) LoadGroups(
        DirectoryEntry root,
        SearchResult user,
        CancellationToken ct)
    {
        var local = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var global = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var dn = LdapDirectory.ReadString(user, "distinguishedName");
        if (!string.IsNullOrEmpty(dn))
        {
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectCategory=group)(member:1.2.840.113556.1.4.1941:={LdapFilter.Escape(dn)}))",
                SearchScope = SearchScope.Subtree,
                PageSize = 500,
                SizeLimit = 0
            };
            searcher.PropertiesToLoad.Add("cn");
            searcher.PropertiesToLoad.Add("groupType");

            using var found = searcher.FindAll();
            foreach (SearchResult sr in found)
            {
                ct.ThrowIfCancellationRequested();
                var name = LdapDirectory.ReadString(sr, "cn");
                if (string.IsNullOrEmpty(name))
                    continue;

                if (HasFlag(LdapDirectory.ReadInt32(sr, "groupType"), GroupDomainLocal))
                    local.Add(name);
                else
                    global.Add(name);

                if (local.Count + global.Count >= SearchLimits.Safety)
                    break;
            }
        }

        AddPrimaryGroup(root, user, local, global);
        return (local.ToList(), global.ToList());
    }

    private static void AddPrimaryGroup(
        DirectoryEntry root,
        SearchResult user,
        SortedSet<string> local,
        SortedSet<string> global)
    {
        var sid = LdapDirectory.ReadBytes(user, "objectSid");
        if (sid is null || sid.Length < 8)
            return;

        var primaryRid = LdapDirectory.ReadInt32(user, "primaryGroupID");
        if (primaryRid <= 0)
            return;

        var groupSid = (byte[])sid.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            groupSid.AsSpan(groupSid.Length - 4),
            (uint)primaryRid);

        using var searcher = new DirectorySearcher(root)
        {
            Filter = $"(objectSid={LdapFilter.EscapeBinary(groupSid)})",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 1
        };
        searcher.PropertiesToLoad.Add("cn");
        searcher.PropertiesToLoad.Add("groupType");

        var result = searcher.FindOne();
        if (result is null)
            return;

        var name = LdapDirectory.ReadString(result, "cn");
        if (string.IsNullOrEmpty(name))
            return;

        if (HasFlag(LdapDirectory.ReadInt32(result, "groupType"), GroupDomainLocal))
            local.Add(name);
        else
            global.Add(name);
    }

    private static bool HasFlag(int flags, int flag) => (flags & flag) != 0;

    private static string FirstNonEmpty(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
