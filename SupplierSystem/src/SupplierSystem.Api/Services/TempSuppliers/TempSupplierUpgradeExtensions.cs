namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed class TempSupplierUpgradeException : Exception
{
    public int StatusCode { get; }

    public TempSupplierUpgradeException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public TempSupplierUpgradeException(int statusCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

public static class DateTimeExtensions
{
    public static DateTimeOffset AddWorkingDays(this DateTimeOffset date, int days)
    {
        var result = date;
        var added = 0;

        while (added < days)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday)
            {
                added++;
            }
        }

        return result;
    }
}

public static class DecisionExtensions
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static string? NormalizeDecision(string decision)
    {
        var normalized = decision.Trim().ToLowerInvariant();

        return normalized switch
        {
            "approved" or "approve" or "yes" or "y" => Approved,
            "rejected" or "reject" or "no" or "n" => Rejected,
            _ => null
        };
    }
}
