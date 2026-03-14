namespace SupplierSystem.Api.Services.Email;

public static class SmtpDeliveryMode
{
    public const string PickupDirectoryEnv = "SMTP_PICKUP_DIR";
    public const string DefaultPickupDirectory = @"E:\SMP2\supplier-deploy\SupplierSystem\tests\Email test";

    public static string ResolvePickupDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable(PickupDirectoryEnv);
        return string.IsNullOrWhiteSpace(overridePath) ? DefaultPickupDirectory : overridePath;
    }
}
