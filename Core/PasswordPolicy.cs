namespace UserTrace.Core;

/// <summary>
/// Idade máxima de senha do domínio. <paramref name="MaxAge"/> zero significa que a senha não expira.
/// O atributo LDAP <c>maxPwdAge</c> vem como intervalo negativo de 100 ns.
/// </summary>
public readonly record struct PasswordPolicy(TimeSpan MaxAge)
{
    public bool PasswordsExpire => MaxAge > TimeSpan.Zero;

    public string Describe()
    {
        if (!PasswordsExpire)
            return "não expira";

        if (MaxAge.Ticks % TimeSpan.TicksPerDay == 0)
        {
            var days = MaxAge.Days;
            return days == 1 ? "1 dia" : $"{days} dias";
        }

        return $"{(int)MaxAge.TotalDays} dias e {MaxAge.Hours} h";
    }

    public static PasswordPolicy FromMaxPwdAge(long maxPwdAge)
    {
        if (maxPwdAge == 0 || maxPwdAge == long.MinValue)
            return new PasswordPolicy(TimeSpan.Zero);

        var ticks = maxPwdAge < 0 ? -maxPwdAge : maxPwdAge;
        return new PasswordPolicy(TimeSpan.FromTicks(ticks));
    }
}
