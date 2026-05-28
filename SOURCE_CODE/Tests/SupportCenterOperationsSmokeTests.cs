using System;
using System.Collections.Generic;
using System.IO;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class SupportCenterOperationsSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            var service = new SupportCenterService();

            var serverPackage = service.GenerateClientServerSetupPackage();
            RequireSuccessWithPath(serverPackage, "client server setup package");
            passed.Add("client server setup package generated");

            var health = service.CreateOfficeHealthReport();
            RequireSuccessWithPath(health, "office health report");
            passed.Add("office health report generated");

            var operations = service.CreateOperationsCommandCenterReport();
            RequireSuccessWithPath(operations, "operations command center report");
            passed.Add("operations command center report generated");

            var price = service.CreateMaterialPriceIntelligenceReport();
            RequireSuccessWithPath(price, "material price intelligence report");
            passed.Add("material price intelligence report generated");

            var documents = service.CreateDocumentAutomationReport();
            RequireSuccessWithPath(documents, "document automation report");
            passed.Add("document automation report generated");

            var deployment = service.CreateFreshClientDeploymentReport();
            RequireSuccessWithPath(deployment, "fresh client deployment report");
            passed.Add("fresh client deployment report generated");

            string cleanRoomFolder = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS", "data-clean-room-smoke");
            Directory.CreateDirectory(cleanRoomFolder);
            File.WriteAllText(Path.Combine(cleanRoomFolder, "Sales Tax Invoice sample.xlsx"), string.Empty);
            var cleanRoom = service.CreateDataCleanRoomReport(cleanRoomFolder);
            RequireSuccessWithPath(cleanRoom, "data clean room report");
            passed.Add("data clean room report generated");

            return passed;
        }

        private static void RequireSuccessWithPath(SupportToolResult result, string name)
        {
            if (result == null || !result.Success)
                throw new InvalidOperationException("Expected success for " + name + ": " + (result == null ? "null result" : result.Message));
            if (string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
                throw new InvalidOperationException("Expected output file for " + name + ".");
        }
    }
}
