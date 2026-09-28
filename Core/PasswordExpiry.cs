namespace UserTrace.Core;

public readonly record struct PasswordExpiryText(string ExpiresOn, string DaysToExpire, bool Expired);

public static class PasswordExpiry
{
    public static PasswordExpiryText Describe(
        PasswordPolicy policy,
        bool passwordNeverExpires,
        long pwdLastSetFileTime,
        DateTime today)
    {
        if (passwordNeverExpires || !policy.PasswordsExpire)
            return new PasswordExpiryText("Nunca", "—", false);

        if (pwdLastSetFileTime <= 0)
            return new PasswordExpiryText("No próximo logon", "—", false);

        try
        {
            var lastSet = DateTime.FromFileTimeUtc(pwdLastSetFileTime).ToLocalTime();
            var expires = lastSet + policy.MaxAge;
            var days = (expires.Date - today.Date).Days;
            return new PasswordExpiryText(
                expires.ToString("dd/MM/yyyy HH:mm"),
                days.ToString(),
                days < 0);
        }
        catch (ArgumentException)
        {
            return new PasswordExpiryText("Conforme política", "—", false);
        }
    }

    /// <summary>
    /// Intervalo inclusivo de <c>pwdLastSet</c> (FILETIME) cuja expiração cai entre as datas locais informadas.
    /// </summary>
    public static (long StartInclusive, long EndInclusive) PwdLastSetWindow(
        PasswordPolicy policy,
        DateTime startDate,
        DateTime endDate)
    {
        if (!policy.PasswordsExpire)
            throw new InvalidOperationException("A política do domínio não expira senha.");

        var start = startDate.Date - policy.MaxAge;
        var endExclusive = endDate.Date.AddDays(1) - policy.MaxAge;
        return (start.ToFileTime(), endExclusive.ToFileTime() - 1);
    }
}
