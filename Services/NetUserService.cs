using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using UserTrace.Models;

namespace UserTrace.Services;

public static class NetUserService
{
    #region P/Invoke declarations

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int NetUserGetInfo(
        string? servername,
        string  username,
        int     level,
        out IntPtr bufptr);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int NetUserGetLocalGroups(
        string? servername,
        string  username,
        int     level,
        int     flags,
        out IntPtr bufptr,
        int     prefmaxlen,
        out int entriesread,
        out int totalentries);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct USER_INFO_3
    {
        public string?  usri3_name;
        public string?  usri3_password;
        public uint     usri3_password_age;
        public uint     usri3_priv;
        public string?  usri3_home_dir;
        public string?  usri3_comment;
        public uint     usri3_flags;
        public string?  usri3_script_path;
        public uint     usri3_auth_flags;
        public string?  usri3_full_name;
        public string?  usri3_usr_comment;
        public string?  usri3_parms;
        public string?  usri3_workstations;
        public uint     usri3_last_logon;
        public uint     usri3_last_logoff;
        public uint     usri3_acct_expires;
        public uint     usri3_max_storage;
        public uint     usri3_units_per_week;
        public IntPtr   usri3_logon_hours;
        public uint     usri3_bad_pw_count;
        public uint     usri3_num_logons;
        public string?  usri3_logon_server;
        public uint     usri3_country_code;
        public uint     usri3_code_page;
        public uint     usri3_user_id;
        public uint     usri3_primary_group_id;
        public string?  usri3_profile;
        public string?  usri3_home_dir_drive;
        public uint     usri3_password_expired;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOCALGROUP_USERS_INFO_0
    {
        public string? lgrui0_name;
    }

    private const int  NERR_Success         = 0;
    private const int  NERR_UserNotFound     = 2221;
    private const int  ERROR_ACCESS_DENIED   = 5;
    private const int  RPC_S_SERVER_UNAVAIL  = 1722;
    private const int  MAX_PREFERRED_LENGTH  = -1;
    private const int  LG_INCLUDE_INDIRECT   = 0x0001;

    private const uint UF_ACCOUNTDISABLE     = 0x0002;
    private const uint UF_PASSWD_NOTREQD     = 0x0020;
    private const uint UF_PASSWD_CANT_CHANGE = 0x0040;
    private const uint UF_DONT_EXPIRE_PASSWD = 0x10000;
    private const uint UF_SMARTCARD_REQUIRED = 0x40000;
    private const uint UF_PASSWORD_EXPIRED   = 0x800000;

    #endregion

    public static Task<CommandResult> GetUserDetailsAsync(
        string samAccountName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetUserDetailsCore(samAccountName, cancellationToken), cancellationToken);
    }

    private static CommandResult GetUserDetailsCore(string sam, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string? dc = GetDomainController();

        IntPtr buf = IntPtr.Zero;
        try
        {
            int result = NetUserGetInfo(dc, sam, 3, out buf);

            if (result != NERR_Success)
            {
                string msg = result switch
                {
                    NERR_UserNotFound    => $"O nome de usuário '{sam}' não foi encontrado.",
                    ERROR_ACCESS_DENIED  => "Acesso negado. Verifique suas permissões no domínio.",
                    RPC_S_SERVER_UNAVAIL => "O servidor RPC não está disponível. Verifique a conectividade com o DC.",
                    _                    => $"Erro da API Win32: código {result}."
                };
                return new CommandResult { Error = msg, ExitCode = result };
            }

            ct.ThrowIfCancellationRequested();

            var u = Marshal.PtrToStructure<USER_INFO_3>(buf);

            ct.ThrowIfCancellationRequested();

            var localGroups  = GetLocalGroups(dc, sam);
            var globalGroups = GetGlobalGroupsViaLdap(sam, dc);

            var userInfo = BuildUserInfo(sam, u, localGroups, globalGroups, dc);
            return new CommandResult { UserInfo = userInfo, ExitCode = 0 };
        }
        finally
        {
            if (buf != IntPtr.Zero)
                NetApiBufferFree(buf);
        }
    }

    private static UserInfo BuildUserInfo(
        string sam, USER_INFO_3 u,
        List<string> localGroups, List<string> globalGroups,
        string? dc)
    {
        var ad = GetAdIdentityProps(sam, dc);
        return new UserInfo
        {
            SamAccountName    = u.usri3_name    ?? sam,
            FullName          = u.usri3_full_name ?? string.Empty,
            Email             = ad.Email,
            PhoneNumber       = ad.PhoneNumber,
            Office            = ad.Office,
            Comment           = u.usri3_comment  ?? string.Empty,
            UserComment       = u.usri3_usr_comment ?? string.Empty,

            AccountActive     = !HasFlag(u.usri3_flags, UF_ACCOUNTDISABLE),
            AccountExpires    = FormatTimestamp(u.usri3_acct_expires),

            PasswordLastSet      = FormatPasswordAge(u.usri3_password_age),
            BadPasswordCount     = u.usri3_bad_pw_count.ToString(),
            BadPasswordTime      = ad.BadPasswordTime,
            PasswordNeverExpires = HasFlag(u.usri3_flags, UF_DONT_EXPIRE_PASSWD),
            PasswordExpired      = HasFlag(u.usri3_flags, UF_PASSWORD_EXPIRED),
            PasswordRequired     = !HasFlag(u.usri3_flags, UF_PASSWD_NOTREQD),
            PasswordChangeable   = !HasFlag(u.usri3_flags, UF_PASSWD_CANT_CHANGE),
            SmartcardRequired    = HasFlag(u.usri3_flags, UF_SMARTCARD_REQUIRED),

            LastLogon      = FormatTimestamp(u.usri3_last_logon),
            LastLogoff     = FormatTimestamp(u.usri3_last_logoff),
            Workstations   = string.IsNullOrEmpty(u.usri3_workstations) ? "Todas" : u.usri3_workstations,
            LogonScript    = u.usri3_script_path  ?? string.Empty,
            ProfilePath    = u.usri3_profile      ?? string.Empty,
            HomeDirectory  = u.usri3_home_dir     ?? string.Empty,

            LocalGroups    = localGroups.AsReadOnly(),
            GlobalGroups   = globalGroups.AsReadOnly(),

            Domain = dc ?? Environment.UserDomainName
        };
    }

    private readonly record struct AdIdentityProps(string Email, string PhoneNumber, string Office, string BadPasswordTime);

    private static AdIdentityProps GetAdIdentityProps(string sam, string? dc)
    {
        try
        {
            string ldapPath = string.IsNullOrEmpty(dc) ? "LDAP://" : $"LDAP://{dc}";
            using var root = new DirectoryEntry(ldapPath);
            using var searcher = new DirectorySearcher(root)
            {
                Filter      = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))",
                SearchScope = SearchScope.Subtree,
                SizeLimit   = 1
            };

            searcher.PropertiesToLoad.Add("mail");
            searcher.PropertiesToLoad.Add("telephoneNumber");
            searcher.PropertiesToLoad.Add("physicalDeliveryOfficeName");
            searcher.PropertiesToLoad.Add("badPasswordTime");

            var result = searcher.FindOne();
            if (result == null) return default;

            string GetProp(string name)
            {
                var props = result.Properties[name];
                if (props == null || props.Count == 0) return string.Empty;
                return props[0]?.ToString() ?? string.Empty;
            }

            return new AdIdentityProps(
                Email:       GetProp("mail"),
                PhoneNumber: GetProp("telephoneNumber"),
                Office:      GetProp("physicalDeliveryOfficeName"),
                BadPasswordTime: FormatAdFileTime(GetAdProp(result, "badPasswordTime")));
        }
        catch
        {
            return default;
        }
    }

    private static object? GetAdProp(SearchResult result, string name)
    {
        var props = result.Properties[name];
        if (props == null || props.Count == 0) return null;
        return props[0];
    }

    private static string FormatAdFileTime(object? value)
    {
        long fileTime = TryReadAdFileTime(value);
        if (fileTime <= 0) return "Nunca";

        try
        {
            // FILETIME: 100ns intervals since 1601-01-01 (UTC).
            return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        }
        catch
        {
            return "Nunca";
        }
    }

    private static long TryReadAdFileTime(object? value)
    {
        if (value == null) return 0;

        // DirectoryServices pode retornar Int64 direto.
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is IConvertible c)
        {
            try { return c.ToInt64(null); } catch { }
        }

        // Ou um COM (IADsLargeInteger) com HighPart/LowPart.
        try
        {
            var t = value.GetType();
            var highProp = t.GetProperty("HighPart");
            var lowProp  = t.GetProperty("LowPart");
            if (highProp != null && lowProp != null)
            {
                int high = Convert.ToInt32(highProp.GetValue(value, null));
                int low  = Convert.ToInt32(lowProp.GetValue(value, null));
                return ((long)high << 32) | (uint)low;
            }
        }
        catch { }

        return 0;
    }

    private static string? GetDomainController()
    {
        try { return Domain.GetComputerDomain().FindDomainController().Name; }
        catch { return null; }
    }

    private static List<string> GetLocalGroups(string? dc, string sam)
    {
        var groups = new List<string>();
        IntPtr buf = IntPtr.Zero;
        try
        {
            int result = NetUserGetLocalGroups(
                dc, sam, 0, LG_INCLUDE_INDIRECT,
                out buf, MAX_PREFERRED_LENGTH,
                out int read, out _);

            if (result != NERR_Success || buf == IntPtr.Zero)
                return groups;

            int size = Marshal.SizeOf<LOCALGROUP_USERS_INFO_0>();
            for (int i = 0; i < read; i++)
            {
                var entry = Marshal.PtrToStructure<LOCALGROUP_USERS_INFO_0>(buf + i * size);
                if (!string.IsNullOrEmpty(entry.lgrui0_name))
                    groups.Add(entry.lgrui0_name);
            }
            groups.Sort(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (buf != IntPtr.Zero) NetApiBufferFree(buf);
        }
        return groups;
    }

    private static List<string> GetGlobalGroupsViaLdap(string sam, string? dc)
    {
        var groups = new List<string>();
        try
        {
            string ldapPath = string.IsNullOrEmpty(dc) ? "LDAP://" : $"LDAP://{dc}";
            using var root    = new DirectoryEntry(ldapPath);
            using var searcher = new DirectorySearcher(root)
            {
                Filter      = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))",
                SearchScope = SearchScope.Subtree,
                SizeLimit   = 1
            };
            searcher.PropertiesToLoad.Add("memberOf");

            var result = searcher.FindOne();
            if (result == null) return groups;

            foreach (object? dn in result.Properties["memberOf"])
            {
                if (dn is string dnStr)
                {
                    var cn = ExtractCn(dnStr);
                    if (!string.IsNullOrEmpty(cn)) groups.Add(cn);
                }
            }
            groups.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch { }
        return groups;
    }

    private static string ExtractCn(string dn)
    {
        foreach (var part in dn.Split(','))
        {
            var t = part.Trim();
            if (t.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return t[3..];
        }
        return string.Empty;
    }

    private static string EscapeLdap(string value) =>
        value.Replace("\\", "\\5c").Replace("*", "\\2a")
             .Replace("(", "\\28").Replace(")", "\\29")
             .Replace("\0", "\\00").Replace("/", "\\2f");

    private static bool HasFlag(uint flags, uint flag) => (flags & flag) != 0;

    private static string FormatTimestamp(uint ts)
    {
        if (ts == 0 || ts == uint.MaxValue) return "Nunca";
        try { return DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime.ToString("dd/MM/yyyy HH:mm"); }
        catch { return "Nunca"; }
    }

    private static string FormatPasswordAge(uint ageSeconds)
    {
        if (ageSeconds == 0) return "Nunca";
        return DateTime.Now.AddSeconds(-(double)ageSeconds).ToString("dd/MM/yyyy HH:mm");
    }
}
