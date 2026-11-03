using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class LocalDocumentCandidate
    {
        public bool Selected { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string Format { get; set; }
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public bool CanReview { get; set; }
        public string Status { get; set; }
    }

    public sealed class LocalDocumentScanProgress
    {
        public int FoldersVisited { get; set; }
        public int FilesVisited { get; set; }
        public int CandidatesFound { get; set; }
        public int SkippedLocations { get; set; }
    }

    public sealed class LocalDocumentScanResult
    {
        public string RootPath { get; set; }
        public string NameFilter { get; set; }
        public bool SystemFoldersSkipped { get; set; }
        public int FoldersVisited { get; set; }
        public int FilesVisited { get; set; }
        public int LinksSkipped { get; set; }
        public int SystemFoldersSkippedCount { get; set; }
        public int UnavailableLocations { get; set; }
        public bool LimitReached { get; set; }
        public List<LocalDocumentCandidate> Files { get; } = new List<LocalDocumentCandidate>();
        public string Summary => (LimitReached ? "Partial scan (safety limit reached). " : "Scan complete. ")
            + Files.Count + " candidates; " + FilesVisited + " files checked; " + FoldersVisited + " folders; "
            + UnavailableLocations + " inaccessible/unavailable locations; " + LinksSkipped + " links and "
            + SystemFoldersSkippedCount + " system/cache folders skipped." + Environment.NewLine
            + "Contents are classified only when selected for review. No records saved.";
    }
}
