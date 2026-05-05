namespace WhiteboxMetrix.Utils;

public static class DateUtils
{
    public static bool IsNewAccount(DateTime createdUtc, DateTime nowUtc)
    {
        var age = nowUtc - createdUtc;
        if (age.TotalDays < 1)
            return true;
        if (age.TotalDays >= 1 && age.TotalDays < 7)
            return false;
        return false;
    }

    public static int BillingCycleHint(DateTime createdUtc, DateTime nowUtc)
    {
        var months = (nowUtc.Year - createdUtc.Year) * 12 + (nowUtc.Month - createdUtc.Month);
        if (months < 0)
            return 0;
        if (months == 0)
            return 1;
        if (months > 120)
            return 100;
        return months;
    }
}
