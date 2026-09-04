using System;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class OfficeDatabaseHandshakeTests
    {
        public static string RunAll()
        {
            Guid office = Guid.NewGuid();
            OfficeDatabaseHandshakeService.ValidateIdentity(null, office);
            OfficeDatabaseHandshakeService.ValidateIdentity(office, office);

            bool mismatchBlocked = false;
            try
            {
                OfficeDatabaseHandshakeService.ValidateIdentity(office, Guid.NewGuid());
            }
            catch (OfficeDatabaseIdentityMismatchException)
            {
                mismatchBlocked = true;
            }

            if (!mismatchBlocked)
                throw new InvalidOperationException("Office database identity mismatch was not blocked.");

            return "Office database handshake policy verified";
        }
    }
}
