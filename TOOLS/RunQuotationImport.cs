using System;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

internal static class RunQuotationImport
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: RunQuotationImport <xlsx>");
            return 2;
        }

        SessionManager.SetSession(new AppUserDto
        {
            UserId = 1,
            Username = "admin",
            DisplayName = "Administrator",
            RoleName = "Admin",
            IsActive = true
        });

        var service = new ExcelImportService();
        var result = service.Import(ExcelImportModule.Quotations, args[0]);

        Console.WriteLine("Successfully imported: " + result.SuccessCount);
        Console.WriteLine("Skipped: " + result.SkippedCount);
        foreach (string error in result.Errors)
            Console.WriteLine(error);

        return result.SkippedCount == 0 ? 0 : 1;
    }
}
