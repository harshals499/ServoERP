using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class ReleaseNotesSmokeTests
    {
        internal static IEnumerable<string> RunAll()
        {
            string current = UpdateService.GetCurrentAssemblyVersion();
            string notes = UpdateService.GetInstalledReleaseNotesText();
            if (string.IsNullOrWhiteSpace(notes) || !notes.Contains(current) || notes.Length < 100)
                throw new InvalidOperationException("The installed version must bundle its actual release notes.");
            yield return "Embedded installed-version release notes are nonblank and contain actual changes.";
            if (!UpdateService.ShouldShowReleaseNotice(current, "0.0.0", string.Empty))
                throw new InvalidOperationException("Manual updates must show notes without a pending marker.");
            yield return "A version change shows notes without updater staging.";
            if (UpdateService.ShouldShowReleaseNotice(current, current + ".0", string.Empty))
                throw new InvalidOperationException("Acknowledged notes must not repeat for equivalent versions.");
            if (!UpdateService.ShouldShowReleaseNotice(current, current, current + ".0"))
                throw new InvalidOperationException("A current pending notice must be displayed.");
            if (UpdateService.ShouldShowReleaseNotice(current, current, "9.9.999"))
                throw new InvalidOperationException("Future staged notes must not display before installation.");
            yield return "Equivalent versions, pending notices and future-version staging behave correctly.";
            string fallback = UpdateService.BuildEnglishWhatsNewTextForTest(current, string.Empty);
            if (string.IsNullOrWhiteSpace(fallback) || fallback.Contains("Performance, stability, and security improvements"))
                throw new InvalidOperationException("Fallback notes must remain nonblank without inventing changes.");
            yield return "Empty remote-note fallback is explicit and does not invent changes.";
        }
    }
}
