using System;

namespace HVAC_Pro_Desktop.Models
{
    /// <summary>Represents a validated one-based pagination state for list and grid views.</summary>
    public sealed class PaginationState
    {
        public const int DefaultPageSize = 10;

        public PaginationState()
        {
            CurrentPage = 1;
            PageSize = DefaultPageSize;
        }

        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }

        public int TotalPages
        {
            get
            {
                int size = Math.Max(1, PageSize);
                return TotalRecords <= 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)size);
            }
        }

        public int Skip
        {
            get { return Math.Max(0, (NormalizePage(CurrentPage, TotalRecords, PageSize) - 1) * Math.Max(1, PageSize)); }
        }

        public int DisplayFrom
        {
            get { return TotalRecords <= 0 ? 0 : Skip + 1; }
        }

        public int DisplayTo
        {
            get { return TotalRecords <= 0 ? 0 : Math.Min(TotalRecords, Skip + Math.Max(1, PageSize)); }
        }

        /// <summary>Returns a safe one-based page number for the supplied record count and page size.</summary>
        public static int NormalizePage(int requestedPage, int totalRecords, int pageSize)
        {
            int totalPages = GetTotalPages(totalRecords, pageSize);
            if (totalPages <= 0)
                return 1;

            return Math.Max(1, Math.Min(requestedPage, totalPages));
        }

        /// <summary>Returns a safe total-page count without divide-by-zero.</summary>
        public static int GetTotalPages(int totalRecords, int pageSize)
        {
            if (totalRecords <= 0)
                return 0;

            return (int)Math.Ceiling(totalRecords / (double)Math.Max(1, pageSize));
        }

        /// <summary>Creates a validated copy with page and page size clamped to legal values.</summary>
        public PaginationState Normalize()
        {
            int size = Math.Max(1, PageSize);
            return new PaginationState
            {
                PageSize = size,
                TotalRecords = Math.Max(0, TotalRecords),
                CurrentPage = NormalizePage(CurrentPage, Math.Max(0, TotalRecords), size)
            };
        }
    }
}
