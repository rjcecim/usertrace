using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using UserTrace.Core;
using UserTrace.Models;

namespace UserTrace.Services;

/// <summary>
/// Acesso LDAP compartilhado: domínio em cache, política de senha e consultas paginadas.
/// </summary>
public static class LdapDirectory
{
    public const string Person = "(objectCategory=person)(objectClass=user)";
    public const string ActiveAccount = "(!(userAccountControl:1.2.840.113556.1.4.803:=2))";
    public const string PasswordCanExpire = "(!(userAccountControl:1.2.840.113556.1.4.803:=65536))";

    private static readonly object Gate = new();
    private static string? _ldapPath;
    private static PasswordPolicy? _policy;

    public static string GetLdapPath()
    {
        var cached = Volatile.Read(ref _ldapPath);
        if (cached is not null)
            return cached;

        var resolved = ResolveLdapPath();
        lock (Gate)
        {
            _ldapPath ??= resolved;
            return _ldapPath;
        }
    }

    public static string GetDomainDnsName()
    {
        const string prefix = "LDAP://";
        var path = GetLdapPath();
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return path[prefix.Length..];

        return Environment.UserDomainName;
    }

    public static DirectoryEntry OpenRoot()
    {
        var path = GetLdapPath();
        return string.IsNullOrEmpty(path) ? new DirectoryEntry() : new DirectoryEntry(path);
    }

    public static PasswordPolicy GetPasswordPolicy(bool refresh = false)
    {
        if (!refresh)
        {
            lock (Gate)
            {
                if (_policy is { } cached)
                    return cached;
            }
        }

        var policy = ReadPasswordPolicy();
        lock (Gate)
            _policy = policy;
        return policy;
    }

    public static Task<PasswordPolicy> RefreshPasswordPolicyAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => GetPasswordPolicy(refresh: true), cancellationToken);

    public static QueryResult<T> Query<T>(
        string filter,
        string[] properties,
        int limit,
        Func<SearchResult, T?> map,
        CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var root = OpenRoot();
        using var searcher = new DirectorySearcher(root)
        {
            Filter = filter,
            SearchScope = SearchScope.Subtree,
            PageSize = 1000,
            SizeLimit = 0
        };

        foreach (var property in properties)
            searcher.PropertiesToLoad.Add(property);

        var items = new List<T>(capacity: Math.Min(limit, 256));
        var truncated = false;

        try
        {
            using var found = searcher.FindAll();
            foreach (SearchResult sr in found)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mapped = map(sr);
                if (mapped is null)
                    continue;

                if (items.Count >= limit)
                {
                    truncated = true;
                    break;
                }

                items.Add(mapped);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha ao consultar o Active Directory: {ex.Message}", ex);
        }

        return new QueryResult<T>
        {
            Items = items,
            Truncated = truncated,
            Limit = limit
        };
    }

    public static string ReadString(SearchResult result, string name)
    {
        var props = result.Properties[name];
        if (props is null || props.Count == 0)
            return string.Empty;
        return props[0]?.ToString() ?? string.Empty;
    }

    public static long ReadInt64(SearchResult result, string name)
    {
        var props = result.Properties[name];
        if (props is null || props.Count == 0)
            return 0;
        return AdFileTime.Read(props[0]);
    }

    public static int ReadInt32(SearchResult result, string name)
    {
        var value = ReadInt64(result, name);
        return value is > int.MaxValue or < int.MinValue ? 0 : (int)value;
    }

    public static byte[]? ReadBytes(SearchResult result, string name)
    {
        var props = result.Properties[name];
        if (props is null || props.Count == 0)
            return null;
        return props[0] as byte[];
    }

    private static string ResolveLdapPath()
    {
        Exception? failure = null;
        try
        {
            var name = Domain.GetComputerDomain().Name;
            if (!string.IsNullOrWhiteSpace(name))
                return "LDAP://" + name;
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        var dns = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
        if (!string.IsNullOrWhiteSpace(dns))
            return "LDAP://" + dns;

        var netbios = Environment.UserDomainName;
        if (!string.IsNullOrWhiteSpace(netbios) &&
            !string.Equals(netbios, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            return "LDAP://" + netbios;

        throw new InvalidOperationException(
            "Não foi possível localizar o domínio do Active Directory. A máquina precisa estar ingressada no domínio.",
            failure);
    }

    private static PasswordPolicy ReadPasswordPolicy()
    {
        using var root = OpenRoot();
        using var searcher = new DirectorySearcher(root)
        {
            Filter = "(objectClass=domainDNS)",
            SearchScope = SearchScope.Base
        };
        searcher.PropertiesToLoad.Add("maxPwdAge");

        SearchResult? result;
        try
        {
            result = searcher.FindOne();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível ler a política de senha do domínio: {ex.Message}", ex);
        }

        if (result is null || result.Properties["maxPwdAge"] is null || result.Properties["maxPwdAge"].Count == 0)
            throw new InvalidOperationException("O domínio não publicou o atributo maxPwdAge.");

        return PasswordPolicy.FromMaxPwdAge(AdFileTime.Read(result.Properties["maxPwdAge"][0]));
    }
}
