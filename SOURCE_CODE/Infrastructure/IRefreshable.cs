namespace ServoERP.Infrastructure
{
    /// <summary>Contract for pages that can refresh their data when shown again.</summary>
    public interface IRefreshable
    {
        /// <summary>Reloads page data when navigation returns to the page.</summary>
        void RefreshData();
    }
}
