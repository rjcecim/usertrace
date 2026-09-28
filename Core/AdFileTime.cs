using System.Globalization;

namespace UserTrace.Core;

/// <summary>Leitura e formatação de FILETIME do Active Directory (intervalos de 100 ns desde 1601-01-01 UTC).</summary>
public static class AdFileTime
{
    public static long Read(object? value)
    {
        if (value is null)
            return 0;

        switch (value)
        {
            case long number:
                return number;
            case int number:
                return number;
            case uint number:
                return number;
            case IConvertible convertible when value is not string:
                try
                {
                    return convertible.ToInt64(CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    break;
                }
        }

        try
        {
            var type = value.GetType();
            var highProp = type.GetProperty("HighPart");
            var lowProp = type.GetProperty("LowPart");
            if (highProp is null || lowProp is null)
                return 0;

            var high = Convert.ToInt32(highProp.GetValue(value), CultureInfo.InvariantCulture);
            var low = Convert.ToInt32(lowProp.GetValue(value), CultureInfo.InvariantCulture);
            return ((long)high << 32) | (uint)low;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static string FormatLocal(long fileTime, string zeroText)
    {
        if (fileTime <= 0)
            return zeroText;

        try
        {
            return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        }
        catch (ArgumentException)
        {
            return zeroText;
        }
    }

    public static string FormatAccountExpires(long fileTime)
    {
        if (fileTime == 0 || fileTime == long.MaxValue)
            return "Nunca";

        return FormatLocal(fileTime, "Nunca");
    }
}
